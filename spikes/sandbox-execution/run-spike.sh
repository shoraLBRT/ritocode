#!/usr/bin/env bash
# Reproduces the sandbox execution spike. README.md says what each probe is for and what it found
# on the development machine. Needs a Docker daemon, and a network for the image build only.
#
#   ./spikes/sandbox-execution/run-spike.sh
#
# None of this is production code. It exists so ADR 0006 can be re-checked rather than believed.
set -uo pipefail

export MSYS_NO_PATHCONV=1              # Git Bash rewrites /work into a Windows path otherwise.
HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$(cd "$HERE/../.." && pwd)
WORK=$(mktemp -d)
IMAGE=ritocode-spike-runner:csharp
PACKAGE=$ROOT/content/problems/example-order-total

trap 'rm -rf "$WORK"' EXIT

# A Windows daemon wants a Windows path on the left of -v; a Linux one wants the path unchanged.
hostpath() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi; }

# The flag set under test. Every probe below runs with exactly this one.
FLAGS=(
  --network none
  --cpus 2 --memory 2g --memory-swap 2g --pids-limit 256
  --read-only
  --cap-drop ALL --security-opt no-new-privileges
  --tmpfs /tmp:rw,noexec,nosuid,size=512m
  --user 10001:10001
)

section() { printf '\n== %s\n' "$*"; }

# Lays down starter plus one fixture, the way the orchestrator will materialise a workspace.
workspace() {
  rm -rf "$1" && mkdir -p "$1"
  cp -r "$PACKAGE/starter/." "$1/"
  [ -n "${2:-}" ] && cp -r "$PACKAGE/fixtures/$2/." "$1/"
  return 0
}

# Runs a container to completion under a name, then reports how the failure looks from outside.
verdict() {
  local name=$1; shift
  docker rm -f "spike-$name" >/dev/null 2>&1
  docker run --name "spike-$name" "$@" >/dev/null 2>&1
  printf '%-18s %s\n' "$name" \
    "$(docker inspect "spike-$name" --format 'exit={{.State.ExitCode}} OOMKilled={{.State.OOMKilled}}')"
  docker rm -f "spike-$name" >/dev/null 2>&1
}

section "0. runner image"
docker build -t "$IMAGE" "$(hostpath "$HERE/image")" >/dev/null || exit 1
echo "built $IMAGE"

section "1. the flags hold"
docker run --rm "${FLAGS[@]}" "$IMAGE" sh -c '
  echo "uid           = $(id -u)"
  echo "root fs       = $(touch /probe 2>/dev/null && echo WRITABLE || echo read-only)"
  echo "dns           = $(getent hosts nuget.org >/dev/null 2>&1 && echo RESOLVES || echo dead)"
  echo "capabilities  = $(grep CapEff /proc/self/status | cut -f2)"
  echo "no-new-privs  = $(grep NoNewPrivs /proc/self/status | cut -f2)"
  echo "nproc says    = $(nproc)"
  echo "cpu quota     = $(cat /sys/fs/cgroup/cpu/cpu.cfs_quota_us 2>/dev/null || cat /sys/fs/cgroup/cpu.max 2>/dev/null)"
  echo "memory limit  = $(cat /sys/fs/cgroup/memory/memory.limit_in_bytes 2>/dev/null || cat /sys/fs/cgroup/memory.max 2>/dev/null)"
  echo "pids max      = $(cat /sys/fs/cgroup/pids/pids.max 2>/dev/null)"
  echo "docker socket = $(test -S /var/run/docker.sock && echo VISIBLE || echo absent)"'

section "2. both validators, both fixtures"
for fixture in passing failing; do
  workspace "$WORK/$fixture" "$fixture"
  ws=$(hostpath "$WORK/$fixture")
  docker run --rm "${FLAGS[@]}" -v "$ws":/work -w /work "$IMAGE" dotnet build --warnaserror >/dev/null 2>&1
  compile=$?
  docker run --rm "${FLAGS[@]}" -v "$ws":/work -w /work "$IMAGE" dotnet test --no-build >/dev/null 2>&1
  echo "$fixture: compile=$compile test=$?"
done

section "3. what the runtime sees through the limits"
mkdir -p "$WORK/probe"
cat > "$WORK/probe/Probe.csproj" <<'CSPROJ'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
CSPROJ
cat > "$WORK/probe/Program.cs" <<'CSHARP'
Console.WriteLine($"Environment.ProcessorCount = {Environment.ProcessorCount}");
Console.WriteLine($"GC available memory        = {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024)} MiB");
Console.WriteLine($"Environment.MachineName    = {Environment.MachineName}");
try
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    _ = await http.GetStringAsync("https://api.nuget.org/v3/index.json");
    Console.WriteLine("egress                     = SUCCEEDED, containment is broken");
}
catch (Exception ex)
{
    Console.WriteLine($"egress                     = blocked ({ex.InnerException?.Message ?? ex.Message})");
}
CSHARP
docker run --rm "${FLAGS[@]}" -v "$(hostpath "$WORK/probe")":/work -w /work "$IMAGE" dotnet run 2>&1 | tail -4

section "4. how each failure mode looks from outside"
verdict ordinary-failure "${FLAGS[@]}" "$IMAGE" sh -c 'exit 3'
verdict pid-exhaustion   "${FLAGS[@]}" "$IMAGE" sh -c 'i=0; while [ $i -lt 500 ]; do sleep 30 & i=$((i+1)); done'
verdict tmpfs-exhaustion "${FLAGS[@]}" "$IMAGE" sh -c 'head -c 900000000 /dev/zero > /tmp/big'
# tmpfs pages count against the memory cgroup, so a tmpfs wider than the memory limit is the one
# way to make the kernel OOM killer, rather than the runtime, do the killing.
verdict kernel-oom       --network none --memory 128m --memory-swap 128m --read-only \
                         --tmpfs /tmp:rw,size=512m --user 10001:10001 --cap-drop ALL \
                         "$IMAGE" sh -c 'head -c 400000000 /dev/zero > /tmp/big'

mkdir -p "$WORK/hog"
sed 's/Probe/Hog/' "$WORK/probe/Probe.csproj" > "$WORK/hog/Hog.csproj"
cat > "$WORK/hog/Program.cs" <<'CSHARP'
var blocks = new List<byte[]>();
while (true)
{
    var block = new byte[64 * 1024 * 1024];
    for (var i = 0; i < block.Length; i += 4096) { block[i] = 1; }   // touch every page
    blocks.Add(block);
}
CSHARP
verdict managed-oom --network none --memory 512m --memory-swap 512m --read-only \
                    --tmpfs /tmp:rw,noexec,nosuid,size=256m --user 10001:10001 --cap-drop ALL \
                    -v "$(hostpath "$WORK/hog")":/work -w /work "$IMAGE" dotnet run

# docker run has no timeout of its own. This is the shape the orchestrator has to implement.
docker rm -f spike-hang >/dev/null 2>&1
docker run -d --name spike-hang "${FLAGS[@]}" "$IMAGE" sh -c 'while true; do :; done' >/dev/null
( sleep 5; docker kill --signal=KILL spike-hang >/dev/null 2>&1 ) &
docker wait spike-hang >/dev/null
printf '%-18s %s\n' "timeout-kill" \
  "$(docker inspect spike-hang --format 'exit={{.State.ExitCode}} OOMKilled={{.State.OOMKilled}}')"
docker rm -f spike-hang >/dev/null 2>&1

section "5. determinism across three independent runs"
for run in 1 2 3; do
  workspace "$WORK/det" failing
  ws=$(hostpath "$WORK/det")
  docker run --rm "${FLAGS[@]}" -v "$ws":/work -w /work "$IMAGE" dotnet build --warnaserror >/dev/null 2>&1
  docker run --rm "${FLAGS[@]}" -v "$ws":/work -w /work "$IMAGE" \
    dotnet test --no-build --logger "trx;LogFileName=result.trx" >/dev/null 2>&1
  cp "$WORK/det/TestResults/result.trx" "$WORK/run-$run.trx"
  # The artifact carries run ids, timestamps, the container hostname, per-test durations and
  # whatever order the test host finished in. The projection a score is built from carries none.
  tr '<' '\n' < "$WORK/run-$run.trx" \
    | sed -n 's/.*testName="\([^"]*\)".*outcome="\([^"]*\)".*/\1=\2/p' | sort > "$WORK/run-$run.outcomes"
  printf 'run %s: raw trx %s   name->outcome %s\n' "$run" \
    "$(md5sum < "$WORK/run-$run.trx" | cut -c1-8)" \
    "$(md5sum < "$WORK/run-$run.outcomes" | cut -c1-8)"
done

section "6. read-only workspace, build output on a separate mount"
workspace "$WORK/ro" failing
mkdir -p "$WORK/out"
before=$(find "$WORK/ro" -type f | sort | xargs md5sum | md5sum)
ws=$(hostpath "$WORK/ro"); out=$(hostpath "$WORK/out")
docker run --rm "${FLAGS[@]}" -v "$ws":/work:ro -v "$out":/out -w /work "$IMAGE" \
  dotnet build --warnaserror --artifacts-path /out >/dev/null 2>&1
echo "compile exit=$?"
docker run --rm "${FLAGS[@]}" -v "$ws":/work:ro -v "$out":/out -w /work "$IMAGE" \
  dotnet test --no-build --artifacts-path /out \
  --logger "trx;LogFileName=result.trx" --results-directory /out/testresults >/dev/null 2>&1
echo "test    exit=$?"
after=$(find "$WORK/ro" -type f | sort | xargs md5sum | md5sum)
echo "submitted source unchanged: $([ "$before" = "$after" ] && echo yes || echo NO)"
echo "artifacts on the host:      $(find "$WORK/out" -name '*.trx' | wc -l) trx"

section "7. the workspace overriding the runner's own settings"
workspace "$WORK/hostile"
cat > "$WORK/hostile/NuGet.Config" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
XML
cat > "$WORK/hostile/Directory.Build.props" <<'XML'
<Project>
  <PropertyGroup><NuGetAudit>true</NuGetAudit></PropertyGroup>
</Project>
XML
docker run --rm "${FLAGS[@]}" -v "$(hostpath "$WORK/hostile")":/work -w /work "$IMAGE" \
  dotnet build --warnaserror 2>&1 | grep -E 'error|Build (succeeded|FAILED)' | head -3
echo "the same tree without those two files builds clean; containment held either way"

section "8. container start and stop overhead"
start=$(date +%s%N)
for _ in 1 2 3 4 5 6 7 8 9 10; do docker run --rm "${FLAGS[@]}" "$IMAGE" true; done
echo "$(( ($(date +%s%N) - start) / 10000000 )) ms per container"
