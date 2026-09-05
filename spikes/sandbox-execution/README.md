# Spike — sandbox execution

- **Date:** 2026-09-05
- **Slice item:** stage 1, *Spike — sandbox execution* in [`docs/SLICE_PLAN.md`](../../docs/SLICE_PLAN.md)
- **Closes no issue.** Its output is this page and [ADR 0006](../../docs/adr/), which is written from it.
- **Reproduce:** `./spikes/sandbox-execution/run-spike.sh` — needs a Docker daemon, and a network
  for the image build only.

## The question

[ADR 0005](../../docs/adr/0005-vertical-slice-before-breadth.md) fixes the slice's runner as
`docker run` from the worker host with `--network none`, cpu / memory / pid limits, a read-only
root filesystem, a non-root user and a hard timeout. It does not say whether that actually works,
what it costs, or what the orchestrator may assume. This spike ran the reference package's real
validators under exactly those flags and wrote down what happened.

The work being run is not a toy: `content/problems/example-order-total` declares
`dotnet build --warnaserror` and `dotnet test --no-build` in its `validators` list, and ships a
known-good and a known-bad fixture. Everything below uses those.

## The flag set under test

```
--network none
--cpus 2 --memory 2g --memory-swap 2g --pids-limit 256
--read-only
--cap-drop ALL --security-opt no-new-privileges
--tmpfs /tmp:rw,noexec,nosuid,size=512m
--user 10001:10001
```

Measured on the development machine: Docker 29.4.0, Docker Desktop on Windows 10 with the WSL2
backend, **cgroups v1**, `cgroupfs` driver, runc, 6 host CPUs.

## What works

Every flag in ADR 0005's list holds, and both validators run to a correct verdict underneath them.

| Property | Result |
| --- | --- |
| Non-root user | `uid=10001`, no group membership |
| Read-only root | writes to `/`, `/usr/local`, `$HOME` all refused |
| Network | no DNS, no egress; `HttpClient` to `api.nuget.org` fails in ~0s |
| Capabilities | `CapEff=0000000000000000`, `NoNewPrivs=1`, seccomp filtered |
| Docker socket | not present in the container |
| `/tmp` `noexec` | a `chmod +x` script in `/tmp` is refused; neither validator needs to exec from there |
| Limits | memory, pid and cpu quota all visible in the container's cgroup |

Verdicts, running the two validators as separate containers over one workspace:

| Fixture | `dotnet build --warnaserror` | `dotnet test --no-build` |
| --- | --- | --- |
| `fixtures/passing` | exit 0 | exit 0 — 6 passed |
| `fixtures/failing` | exit 0 | exit 1 — 5 passed, 1 failed |

Cost: **~500–570 ms** of container start and stop per run, and **~8.5 s** wall clock for a whole
submission (compile then test, two containers, cold each time). Container overhead is not the
thing to optimise; the SDK is.

## What does not work without deciding something

### 1. `--network none` and package restore are directly opposed

The first honest run failed, and it is the finding the runner image exists to answer:

```
error NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json.
```

The starter project references `Microsoft.NET.Test.Sdk`, `xunit.v3` and
`xunit.runner.visualstudio`. Restore needs them and cannot fetch them. There is no way to have both
a disabled network and an on-demand restore.

The fix, in [`image/Dockerfile`](image/Dockerfile), is to warm the package cache into the image
while the network is still up and point `NUGET_PACKAGES` at it. It works — restore then completes
offline in ~190 ms. It also has a consequence worth stating plainly in the ADR: **the set of
packages a problem may depend on becomes part of the runner image contract**, not something a
problem author chooses freely. A package whose dependencies are not in the image cannot be
evaluated, and that has to be a validation failure at ingest, not a mystery at submission time.

Three smaller things fall out of the same corner, each of which cost a failed run to find:

- NuGet **writes a default config naming nuget.org** into `$HOME/.nuget/NuGet/NuGet.Config` the
  first time it runs. With `HOME` on an empty tmpfs it does this every time, so the source list is
  never empty by default. The image seeds that file with `<clear />` instead.
- `NuGetAudit` fetches vulnerability data **over the network**. Offline it emits `NU1900`, and the
  manifest's own `--warnaserror` turns that into a failed build — a green submission failing for
  reasons that have nothing to do with the submission. The image sets `NuGetAudit=false`.
- `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` no longer suppresses the CLI's first-run setup in .NET 10.
  On a read-only root the CLI crashes before the validator starts:
  `System.IO.IOException: Read-only file system : '/home/runner/.dotnet'`. The image warms that
  state as the runner user at build time.

### 2. `docker run` has no timeout, and the exit code does not say why a container died

The hard timeout ADR 0005 requires has to be implemented by the orchestrator: start the container,
wait with a deadline, `docker kill` on expiry. What comes back is ambiguous:

| Cause | Exit code | `.State.OOMKilled` |
| --- | --- | --- |
| Validator failed normally | the validator's own code (1, 3, …) | false |
| pid limit reached | 2 — whatever the shell reports on a failed fork | false |
| tmpfs full | 1 — an ordinary write error | false |
| Kernel OOM killer | 137 | **true** |
| .NET threw `OutOfMemoryException` | **134** | **false** |
| Orchestrator killed it on timeout | **137** | **false** |

Two conclusions for ADR 0006:

- **A timeout must be recorded by whoever issued the kill.** Exit 137 alone cannot distinguish
  "we killed it" from "the kernel killed it", and the container is gone either way.
- **`OOMKilled` is a reliable positive and an unreliable negative.** A managed runtime that reads
  its own cgroup limit throws first and aborts (134) before the kernel gets involved, so memory
  exhaustion frequently looks like an ordinary crash. The result contract needs a
  *resource-exhausted* outcome the runner is allowed to leave unset, and the classification has to
  come from the validator's output as well as from the container's exit state.

### 3. Determinism holds for the verdict, not for the artifact

Three independent runs of the same submission, fresh workspace each time:

```
run 1: raw trx 7c8e84e7   name->outcome b05acdfd
run 2: raw trx 32c2a549   name->outcome b05acdfd
run 3: raw trx eeda47c7   name->outcome b05acdfd
```

The TRX differs on every run. It carries run and execution GUIDs, timestamps, per-test durations,
`computerName` — which is the container id — and the test results in whatever order the test host
happened to finish them, because xunit runs them in parallel. The sorted set of
`testName` → `outcome` pairs is byte-identical every time, and so is the validator's stdout once
timings are stripped.

This is the project's central claim, so it is worth being exact about it: **the score must be
computed from a normalised projection of the artifact, and the determinism test in
[#38](https://github.com/shoraLBRT/ritocode/issues/38) must assert equality of that projection, not
of the raw file.** The raw artifact is what a person reads, stored under
`submission_reports.logs_reference`; it is not what the verdict is derived from. Asserting on the
file would produce a test that fails constantly and gets deleted, and the claim would quietly stop
being checked.

`Environment.ProcessorCount` reports **2**, not the host's 6 — the runtime reads the cgroup quota,
even though `nproc` still says 6. `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` reports
1536 MiB, three quarters of the 2 GiB limit. Both follow from `--cpus` and `--memory`, which makes
those flags **part of the determinism contract rather than an operational knob**: change them
between two evaluations of the same submission and a parallelism- or memory-sensitive test can
legitimately change its answer. `Environment.MachineName` is the container id and is never stable.

### 4. The submitted tree can override the runner's own settings

Dropping two files into the workspace — a `NuGet.Config` re-adding nuget.org and a
`Directory.Build.props` setting `NuGetAudit=true` — makes a clean starter fail to build:

```
error NU1900: Warning As Error: Error occurred while getting package vulnerability data:
Resource temporarily unavailable (api.nuget.org:443)
```

Containment was never at risk: the network stayed off and that is exactly why the audit failed.
The lesson is about where guarantees are allowed to live. Everything the runner imposes through
environment variables and discovered config files is **advice that the submitted tree outranks**;
only the container flags are guarantees. Two things follow, and ADR 0006 should pick between them
rather than leave it to whoever writes [#21](https://github.com/shoraLBRT/ritocode/issues/21):

- the runner injects flags that win by precedence (`--configfile`, `-p:NuGetAudit=false`), which
  means it edits the command the manifest declared; or
- the package format forbids these files, which means
  [#8](https://github.com/shoraLBRT/ritocode/issues/8)'s allowed-paths rules grow a denylist and
  reject them at ingest.

Either way, **security properties stay in the flags**, where nothing inside the container can
reach them.

### 5. The workspace can be mounted read-only

The strongest configuration found also works, and is worth having as the default rather than as
hardening deferred to stage two:

```
-v <workspace>:/work:ro  -v <output>:/out  -w /work
dotnet build --warnaserror --artifacts-path /out
dotnet test  --no-build   --artifacts-path /out --results-directory /out/testresults
```

The submitted source is byte-identical after both validators run, build output and artifacts land
on a separate writable mount, and the TRX is recoverable from the host after the container exits.
It costs one extra mount and `--artifacts-path` on the command.

It also settles how two validators share state. `dotnet test --no-build` depends on what
`dotnet build` produced, so either they share a writable mount or they run in one container. The
separate output mount is the one that keeps the submitted tree provably untouched.

## What the orchestrator may assume

Carried into ADR 0006 as the shape of the runner contract:

1. One container per validator, sharing a read-only workspace mount and a writable output mount.
   ~500 ms overhead per container.
2. The image carries the language toolchain **and its offline package cache**. Selecting an image
   is selecting a dependency set.
3. Containment comes from the flags. Nothing inside the container may be trusted to configure it.
4. The runner reports: exit code, `OOMKilled`, whether *it* timed out, stdout, stderr, and the
   files left in the output mount. It cannot always say why a container died.
5. Determinism is a property of the normalised result, not of the artifact, and it holds only if
   the resource limits are held fixed too.

## Not answered here, deliberately

Warm pools, Docker-in-Docker, a dedicated runner VM, concurrency limits, image distribution and
what happens when the worker host is not the Docker host. ADR 0005 defers all of these until queue
depth makes one of them matter, and nothing found here brings that forward.

The measurements are from one Windows/WSL2 development machine on cgroups v1. A Linux host on
cgroups v2 should be re-measured — `run-spike.sh` reads both cgroup layouts, so re-running it is
the whole of that work.
