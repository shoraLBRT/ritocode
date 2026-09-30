# Cloud environment trial — 2026-09-30

One-off diagnostic of the Claude Code cloud environment for this repository. No source changed.

## Environment

Ubuntu 24.04.4, x86_64, 4 vCPU, 15 GiB RAM, ~30 GB free disk. Node 22.22.2, npm 10.9.7, psql client 16.13.
Docker CLI 29.3.1 preinstalled, **daemon not running at start** (no `/var/run/docker.sock`). No dotnet preinstalled.
`CLAUDE_CODE_REMOTE=true`; `CLAUDE_CODE_REMOTE_SESSION_ID` set; `GH_TOKEN` set and equals `proxy-injected`. `claude` 2.1.285.
`check-tools` exists (Python, Node, Java 21; no dotnet listed).

## Network (curl through the agent proxy)

| URL | Result |
|---|---|
| dot.net/v1/dotnet-install.sh | 301 (redirect ok) |
| builds.dotnet.microsoft.com/.../releases.json | **403** CONNECT rejected |
| dotnetcli.azureedge.net | **403** CONNECT rejected |
| dotnet.microsoft.com | 302 |
| api.nuget.org/v3/index.json | 200 |
| registry.npmjs.org | 200 |
| registry-1.docker.io/v2/ | 401 (reachable, auth expected) |
| ppa.launchpadcontent.net | **403** CONNECT rejected |
| packages.microsoft.com | 200 |
| api.telegram.org | **403** CONNECT rejected |
| api.anthropic.com | 404 (reachable) |
| example.com | **403** CONNECT rejected |

No `x-deny-reason` header was returned; the proxy answers the CONNECT itself with 403 (organization policy).

## Steps

| # | Command | Result | Duration | Key output |
|---|---|---|---|---|
| 3a | `dotnet-install.sh --jsonfile global.json --install-dir ~/.dotnet` | **fail** | 1 s | `curl: (56) CONNECT tunnel failed, response 403` (script downloads from builds.dotnet.microsoft.com, denied) |
| 3b | `apt-get update && apt-get install -y dotnet-sdk-10.0` | **pass** | 29 s | SDK 10.0.112 from noble-updates/security; `dotnet --list-sdks` → `10.0.112`. PPA index fetches 403 (ignored) |
| 3c | `add-apt-repository ppa:dotnet/backports` | not needed (run by mistake after 3b) | 0 s | `ModuleNotFoundError: apt_pkg`; PPA host is denied anyway |
| 4 | `dockerd > /tmp/dockerd.log 2>&1 &` | **pass** | ~2 s to ready | Daemon runs as root, overlayfs, cgroup v1 warning only |
| 4 | `docker pull postgres:17-alpine` | **pass** | 8.6 s | Pulled; `postgres --version` → 17.11 |
| 5 | `dotnet build Ritocode.slnx --warnaserror` | **pass** | 29 s | 0 warnings, 0 errors |
| 5 | `dotnet test Ritocode.slnx` (Testcontainers) | **pass** | 41 s | Shared 48/48, Architecture 13/13, Content 57/57, Api 39/39; 0 failed |
| 5 | `dotnet format Ritocode.slnx --verify-no-changes` | **fail** (exit 2) | 22 s | `MigratorCommand.cs(15,5): error WHITESPACE: Fix whitespace formatting. Delete 4 characters.` |
| 5 | `dotnet run --project src/Ritocode.ContentTool -- validate content` | **pass** | 3 s | 3 cards, 0 materials, 0 tasks — 0 errors, 0 warnings |
| 5 | `npm ci` (frontend) | **pass** | 5.5 s | |
| 5 | `npm run lint` | **pass** | 4.8 s | |
| 5 | `npm run build` | **pass** | 3.3 s | dist built |
| 5 | `npx vitest run` | **pass** | 5.1 s | 44/44 passed |
| 6 | Postgres fallback | skipped | — | Docker/Testcontainers worked |

## Blockers and observations

1. **Docker daemon is not started by the environment.** It must be started manually (`dockerd &`, ready in ~2 s) before `dotnet test`. A setup script or SessionStart hook should do this.
2. **The official dotnet installer does not work**: `builds.dotnet.microsoft.com` and `dotnetcli.azureedge.net` are denied. Use `apt-get install dotnet-sdk-10.0` (Ubuntu archive, 10.0.112, satisfies `global.json` 10.0.100 + latestFeature). The dotnet/backports PPA is unreachable.
3. **`dotnet format --verify-no-changes` fails** on `src/Ritocode.DbMigrator/MigratorCommand.cs:15` (switch-expression indentation, whitespace). Possibly a formatter difference between the Ubuntu-packaged SDK 10.0.112 and the SDK the maintainer uses, or a real formatting drift on the main branch; not investigated further and not fixed (no code changes allowed).
4. Denied hosts that may matter to sessions: api.telegram.org, example.com, ppa.launchpadcontent.net.
5. Docker Hub, NuGet and npm are reachable, so restores and image pulls work.
