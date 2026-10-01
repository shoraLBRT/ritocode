#!/usr/bin/env bash
# Tests of the uptime check's alert (#34), run by .github/workflows/monitor.yml on a pull request that
# touches it: scripts/monitor-alert.sh against a fake gh that keeps one issue in a directory, through
# down, still down, something else down, and back up. Then scripts/monitor-check.sh against an address
# nothing answers. Needs bash, curl and openssl; no network beyond localhost.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

mkdir "$work/bin" "$work/state"
cat >"$work/bin/gh" <<'FAKE'
#!/usr/bin/env bash
# A fake gh: one issue, its number in state/open while it is open, its body in state/body, and every
# call that changes something appended to state/log.
set -euo pipefail
state="$FAKE_GH_STATE"
body() {
	while [ "$#" -gt 0 ]; do
		if [ "$1" = --body ]; then printf '%s' "$2"; return; fi
		shift
	done
}
case "$1 $2" in
"issue list") cat "$state/open" 2>/dev/null || true ;;
"label create") ;;
"issue create")
	body "$@" >"$state/body"
	echo 7 >"$state/open"
	echo "create" >>"$state/log"
	echo "https://github.com/owner/repo/issues/7"
	;;
"issue view") cat "$state/body" ;;
"issue edit")
	body "$@" >"$state/body"
	echo "edit $3" >>"$state/log"
	;;
"issue comment") echo "comment $3: $(body "$@" | head -n 1)" >>"$state/log" ;;
"issue close")
	rm "$state/open"
	echo "close $3" >>"$state/log"
	;;
*)
	echo "fake gh: unexpected call: $*" >&2
	exit 1
	;;
esac
FAKE
chmod +x "$work/bin/gh"

export PATH="$work/bin:$PATH" FAKE_GH_STATE="$work/state" GITHUB_REPOSITORY_OWNER=owner
touch "$work/state/log"

failures=0
alert() {
	printf '%s\n' "$@" >"$work/report"
	"$here/monitor-alert.sh" "$work/report" >/dev/null
}
expect_log() {
	local name="$1" expected="$2" actual
	actual="$(cat "$work/state/log")"
	if [ "$actual" = "$expected" ]; then
		echo "ok   $name"
	else
		echo "FAIL $name"
		printf '     expected:\n%s\n     actual:\n%s\n' "$expected" "$actual"
		failures=$((failures + 1))
	fi
	: >"$work/state/log"
}

alert "ok   health https://site.example/health/ready"
expect_log "a passing report with no alert open does nothing" ""

alert "FAIL health https://site.example/health/ready" "     curl: (7) Failed to connect"
expect_log "a failing report opens an alert" "create"
if grep -q '^@owner ' "$work/state/body"; then
	echo "ok   the alert mentions the owner"
else
	echo "FAIL the alert mentions the owner"
	failures=$((failures + 1))
fi

alert "FAIL health https://site.example/health/ready" "     curl: (28) Operation timed out"
expect_log "the same failure again, said differently, adds nothing" ""

alert "FAIL health https://site.example/health/ready" "     curl: (7) Failed to connect" \
	"FAIL certificate site.example" "     expires within 14 days: Oct 10 00:00:00 2026 GMT"
expect_log "another failure updates the alert and mentions the owner" \
	"edit 7
comment 7: @owner what fails has changed:"

alert "ok   certificate site.example" "FAIL health https://site.example/health/ready" "     HTTP 503"
expect_log "one failure gone, one left, updates it again" \
	"edit 7
comment 7: @owner what fails has changed:"

alert "ok   health https://site.example/health/ready" "ok   certificate site.example"
expect_log "a passing report closes the alert" \
	"comment 7: Every check passes again.
close 7"

alert "ok   health https://site.example/health/ready"
expect_log "a passing report after it is closed does nothing" ""

# Nothing listens on port 9 of localhost: the health check fails, once, with no pause.
if output="$(MONITOR_ATTEMPTS=1 "$here/monitor-check.sh" http://127.0.0.1:9)"; then
	echo "FAIL the check fails when the site does not answer"
	failures=$((failures + 1))
elif [ "$(grep '^FAIL ' <<<"$output")" = "FAIL health http://127.0.0.1:9/health/ready" ]; then
	echo "ok   the check fails when the site does not answer"
else
	echo "FAIL the check names the failing health address"
	printf '%s\n' "$output"
	failures=$((failures + 1))
fi

exit "$((failures > 0))"
