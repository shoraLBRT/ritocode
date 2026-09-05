# 0006 — Sandbox execution model

- Status: Accepted
- Date: 2026-09-05
- Relates to: [`spikes/sandbox-execution/`](../../spikes/sandbox-execution/README.md), [`docs/SLICE_PLAN.md`](../SLICE_PLAN.md), [#21](https://github.com/shoraLBRT/ritocode/issues/21), [#22](https://github.com/shoraLBRT/ritocode/issues/22), [#38](https://github.com/shoraLBRT/ritocode/issues/38)
- Builds on: [0005](0005-vertical-slice-before-breadth.md)

## Context

[ADR 0005](0005-vertical-slice-before-breadth.md) fixed the slice's runner as `docker run` from the
worker host with `--network none`, cpu, memory and pid limits, a read-only root filesystem, a
non-root user and a hard timeout. It said nothing about whether that works, what it costs, or what
the orchestrator on the other side of `ISandboxRunner` may assume.

The spike in [`spikes/sandbox-execution/`](../../spikes/sandbox-execution/README.md) ran the
reference package's two real validators — `dotnet build --warnaserror` and `dotnet test --no-build`
— under exactly that flag set, over the known-good and known-bad fixtures. Every flag held. Both
fixtures were separated correctly, at ~500 ms of container overhead and ~8.5 s per submission.

It also surfaced four things ADR 0005 does not answer and that everything downstream assumes an
answer to. Left open, each gets decided by whoever writes
[#21](https://github.com/shoraLBRT/ritocode/issues/21) or
[#20](https://github.com/shoraLBRT/ritocode/issues/20), in code, without the reasoning surviving the
session:

1. `docker run` has no timeout of its own, and a container's exit code cannot say why it died. Exit
   137 is both "the kernel's OOM killer" and "we killed it on the deadline"; a managed
   `OutOfMemoryException` exits **134** with `OOMKilled=false`, so memory exhaustion routinely looks
   like an ordinary crash.
2. Anything the runner imposes through environment variables or discovered configuration files is
   advice that the submitted tree outranks. Two files dropped into a workspace — a `NuGet.Config`
   and a `Directory.Build.props` — turned a clean starter red. Containment was never at risk; the
   network was off, which is precisely why the injected audit failed.
3. `--network none` and on-demand package restore are directly opposed. The image has to carry a
   warmed package cache, which makes the set of packages a problem may depend on a property of the
   image rather than a free choice of the problem author.
4. Determinism holds for a normalised projection of a run's artifacts and never for the artifacts
   themselves — and only while the resource limits are held fixed, because the runtime reads its own
   cgroup (`Environment.ProcessorCount` reports 2 under `--cpus 2`, not the host's 6).

This ADR settles those four. It does not revisit ADR 0005's deferral of the production host.

## Decision

### 1. Containment lives in the container flags, and nowhere else

The flag set from ADR 0005, as measured, is the guarantee:

```
--network none
--cpus 2 --memory 2g --memory-swap 2g --pids-limit 256
--read-only
--cap-drop ALL --security-opt no-new-privileges
--tmpfs /tmp:rw,noexec,nosuid,size=512m
--user 10001:10001
```

Nothing inside the container may be trusted to configure the container. A security property that
depends on an environment variable, a configuration file on disk, or the good behaviour of the
submitted tree is not a security property. Any later addition — a seccomp profile, a user
namespace, a different runtime — is added here, not in the image and not in a validator.

### 2. Precedence-winning arguments are injected, and they belong to the image

Where a toolchain lets discovered configuration override the runner's intent, the runner **appends
arguments that win by precedence** to the command the manifest declared. For the .NET image that is
`--configfile` pointing at the image's source-less NuGet config, `-p:NuGetAudit=false`, and
`--artifacts-path /out`.

Those arguments are **a property of the image, not of the runner**. `ISandboxRunner` stays
language-agnostic, as ADR 0005's reduction table requires: the runner registry entry for an image
carries the image reference, its offline package cache, and the argument list to append. Adding a
second language adds a registry entry, not a branch in the runner.

The consequence is stated rather than hidden: **the runner does not execute the manifest's command
verbatim.** The effective command is the declared command plus the image's injected arguments, and
[PROBLEM_PACKAGE_SPEC.md](../PROBLEM_PACKAGE_SPEC.md) has to say so where it documents `validators`.

The alternative — growing [#8](https://github.com/shoraLBRT/ritocode/issues/8)'s allowed-paths rules
into a denylist that rejects those files at ingest — was considered and is rejected below.

### 3. Selecting an image is selecting a dependency set

The image carries the language toolchain **and a package cache warmed while the network was still
up**. Restore then completes offline in ~190 ms. A problem whose dependencies are not in that cache
cannot be evaluated, ever, by any submission against it.

That failure belongs at **ingest**, not at submission time: the code that turns a package into a
`ProblemVersion` ([#9](https://github.com/shoraLBRT/ritocode/issues/9),
[#42](https://github.com/shoraLBRT/ritocode/issues/42)) resolves the package's declared dependencies
against the cache of the image its `language` selects, and rejects the package when any are missing.
Until that check exists, a missing package surfaces as a failed compile validator — visible, but
attributed to the submitter rather than to the content.

Warming the cache is therefore part of building a runner image
([#22](https://github.com/shoraLBRT/ritocode/issues/22)), along with the three smaller things the
spike paid a failed run each to find: seed `$HOME/.nuget/NuGet/NuGet.Config` with `<clear />` so
NuGet does not write itself a default naming nuget.org, set `NuGetAudit=false` so an offline audit
does not trip the manifest's own `--warnaserror`, and warm the .NET CLI's first-run state as the
runner user at build time, because a read-only root cannot create it later.

### 4. One container per validator, read-only workspace, shared writable output mount

```
-v <workspace>:/work:ro   -v <output>:/out   -w /work
```

The submitted tree is mounted read-only and is byte-identical after every validator has run. All
writes go to the output mount, which is **shared across the validators of one submission** — that is
how `dotnet test --no-build` sees what `dotnet build` produced, and it is why the validators can be
separate containers rather than one.

Read-only is the default now rather than hardening deferred to stage two. It costs one extra mount
and one injected argument, and it makes "user code did not modify the thing we evaluated" a property
of the mount instead of a claim.

### 5. The runner reports what it observed, and is allowed not to know why

`ISandboxRunner` returns, for one validator run:

| Field | Meaning |
| --- | --- |
| `Outcome` | `Completed`, `TimedOut`, `ResourceExhausted`, `Crashed` |
| `ExitCode` | the container's exit code; meaningful only when `Outcome` is `Completed` |
| `OomKilled` | the daemon's `.State.OOMKilled` |
| `Duration` | wall clock, runner-measured |
| `Stdout`, `Stderr` | captured, truncated at a fixed cap, with truncation flagged |
| `OutputPath` | the writable mount, for [#23](https://github.com/shoraLBRT/ritocode/issues/23) to collect |

Three rules govern `Outcome`:

- **`Completed` does not mean the validator passed.** It means the container ran to completion and
  `ExitCode` is the validator's own answer. Pass and fail are the validator's to decide
  ([#18](https://github.com/shoraLBRT/ritocode/issues/18)), never the runner's.
- **`TimedOut` is recorded by whoever issued the kill.** `docker run` has no deadline; the
  orchestrator starts the container, waits with one, and `docker kill`s on expiry. Exit 137 alone
  cannot distinguish that from the kernel, and the container is gone either way, so the fact is
  known only to the caller and has to be carried rather than inferred.
- **`ResourceExhausted` is a reliable positive and an unreliable negative.** `OomKilled` true means
  it; false means nothing, because a managed runtime that reads its own cgroup limit throws and
  aborts at 134 before the kernel is involved. `Crashed` therefore means *the runner could not
  attribute this*, not *this was not a resource problem*. Refining a `Crashed` into something a
  person can read is the validator's job, from its own output — the runner never guesses.

A run ending in `TimedOut`, `ResourceExhausted` or `Crashed` is not retried during the slice, in
line with [#17](https://github.com/shoraLBRT/ritocode/issues/17)'s reduction. It becomes a `Failed`
submission carrying the outcome.

### 6. Determinism is a property of the normalised result, and the limits are part of the contract

The verdict is computed from a **normalised projection** of a run's artifacts — for the test
validator, the sorted `testName` to `outcome` pairs, which were byte-identical across three
independent runs whose raw TRX files differed every time. The raw artifact is what a person reads,
stored under `submission_reports.logs_reference`; it is never what a score is derived from
([#20](https://github.com/shoraLBRT/ritocode/issues/20)), and the determinism test in
[#38](https://github.com/shoraLBRT/ritocode/issues/38) asserts on the projection. Asserting on the
file gives a test that fails constantly, gets deleted, and takes the project's central claim with it.

The resource limits are **part of that contract, not an operational setting**. The runtime reads its
own cgroup: `--cpus 2` makes `Environment.ProcessorCount` report 2, and `--memory 2g` makes
`TotalAvailableMemoryBytes` report 1536 MiB. Change either between two evaluations and a
parallelism- or memory-sensitive test may legitimately change its answer. Limits are therefore
declared **beside the image in the runner registry and versioned with it**; changing an image or its
limits is a change to the evaluation environment, and results from before the change are not
comparable to results from after it.

### 7. Deferred, unchanged from ADR 0005

Warm pools, Docker-in-Docker, a dedicated runner VM, image distribution, and a worker host that is
not the Docker host. Concurrency limiting is real and needed, and it is
[#35](https://github.com/shoraLBRT/ritocode/issues/35)'s cap on simultaneous evaluations rather than
a runner concern. Nothing in the spike brings any of these forward; queue depth decides when they
matter, and that gets its own ADR.

The measurements come from one Windows/WSL2 machine on cgroups v1. A Linux host on cgroups v2 is
worth re-measuring, which is one run of `spikes/sandbox-execution/run-spike.sh`.

## Alternatives considered

**Reject `NuGet.Config` and `Directory.Build.props` at ingest instead of injecting flags.** Grow
[#8](https://github.com/shoraLBRT/ritocode/issues/8)'s allowed-paths rules into a denylist so the
manifest's command runs exactly as declared. Rejected on coverage: it guards packages, and the
problem is not only packages. A user writes files into their workspace before submitting, so the
same two files arrive by a route ingest never sees. Injected flags win wherever the file came from.
It also reopens a format that just landed, and it needs a new rule per toolchain quirk in a document
problem authors read, rather than one argument list in a registry entry operators read.

**Both — a denylist at ingest and injected flags.** Defence in depth, and not wrong. Rejected for
the slice because the second mechanism buys nothing the first does not already cover: with the flags
winning by precedence, a file that slips past ingest is inert. Two mechanisms that have to be kept
in step, only one of which is load-bearing, is worse than one that is.

**One container for all validators of a submission.** Removes ~500 ms per extra validator, and the
shared-mount question with it. Rejected: it turns the per-validator report into a parsing problem
instead of a structural one, lets a runaway first validator starve the rest, and undoes the
reduction in ADR 0005 that makes lint and patch-scope an addition rather than a rewrite. The
overhead measured at roughly 6% of a submission does not buy that back.

**Let the runner classify the failure cause.** Have it read stderr and decide that 134 plus an
`OutOfMemoryException` is a resource problem. Rejected: it puts language-specific string matching in
the one component ADR 0005 requires to stay language-agnostic. The validator already parses that
output for its report; the classification belongs beside it.

**Derive determinism from the raw artifact, normalising the artifact instead.** Post-process the TRX
into a canonical form and store only that. Rejected: it discards the timings, ordering and run
identity that make an artifact useful to a person debugging their own submission, in exchange for a
property the projection already provides.

## Consequences

- `ISandboxRunner` has a shape before [#21](https://github.com/shoraLBRT/ritocode/issues/21) starts:
  a request naming image, command, workspace, output mount, limits and deadline, and the result
  above. The orchestrator owns the deadline; the runner owns the container.
- A **runner registry** exists as a concept from now on — image reference, offline package cache,
  injected arguments and resource limits, versioned together. It arrives with
  [#22](https://github.com/shoraLBRT/ritocode/issues/22), and a second language adds a row to it.
- [PROBLEM_PACKAGE_SPEC.md](../PROBLEM_PACKAGE_SPEC.md) has to state that a declared validator
  command is executed with the image's injected arguments appended. That is a documentation change
  to a shipped format, not a format change: no existing manifest becomes invalid.
- Ingest ([#9](https://github.com/shoraLBRT/ritocode/issues/9),
  [#42](https://github.com/shoraLBRT/ritocode/issues/42)) grows a dependency check against the
  image's cache. Until it exists, a problem depending on an uncached package fails at submission
  time and looks like the submitter's fault.
- [#20](https://github.com/shoraLBRT/ritocode/issues/20) scores from the projection and
  [#38](https://github.com/shoraLBRT/ritocode/issues/38) asserts on it. Neither may assert on a raw
  artifact.
- Evaluations are comparable only within one image-and-limits version. When either changes, older
  results stay readable and stop being comparable; nothing recomputes them.
- Submission reports need somewhere to carry `TimedOut` and `ResourceExhausted` distinctly from an
  ordinary validator failure, or the runner's honesty about what it does not know is discarded by
  the layer above it. That lands with [#14](https://github.com/shoraLBRT/ritocode/issues/14) and
  [#17](https://github.com/shoraLBRT/ritocode/issues/17).
- This ADR is superseded, not edited, when the production host stops being deferred.
