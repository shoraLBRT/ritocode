#!/usr/bin/env bash
# One command to release from a clean checkout (#136):
#
#   RITOCODE_SERVER=deploy@server ./scripts/release.sh [commit]
#
# The commit defaults to origin/main; its images must have been built (the release-images workflow
# builds them on every push to main). The server's deploy files are brought up to that commit's, then
# deploy/release.sh runs there. production.env stays on the server and is never copied.
set -euo pipefail

cd "$(dirname "$0")/.."

server="${RITOCODE_SERVER:?set RITOCODE_SERVER, e.g. deploy@203.0.113.10}"
directory="${RITOCODE_DIRECTORY:-ritocode}"

git fetch --quiet origin
commit="$(git rev-parse "${1:-origin/main}")"

if ! git diff --quiet HEAD -- deploy; then
	echo "deploy/ has uncommitted changes; release from a clean checkout." >&2
	exit 1
fi

echo "Releasing $commit to $server:$directory"

# The deploy files of the commit being released, not of the working tree.
staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT
git archive "$commit" deploy | tar -x -C "$staging"

ssh "$server" "mkdir -p '$directory'"
scp -q -r "$staging/deploy/." "$server:$directory/"
ssh "$server" "cd '$directory' && chmod +x release.sh backup.sh restore.sh postgres-init/*.sh && ./release.sh '$commit'"
