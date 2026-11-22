#!/bin/sh
# Tests of the server's checks and their alert (#296): deploy/check.sh and deploy/alert.sh, copied into
# a scratch directory and run against a fake docker, df and /proc, then a fake curl standing in for
# Telegram. Run by scripts/monitor-test.sh (the Monitor workflow's job on a pull request touching it)
# and by the backend's tests (DeployScriptTests), so every pull request runs it. Needs a POSIX sh,
# awk and GNU date (Linux, or Git for Windows); no network, no Docker.
set -eu

here="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

mkdir "$work/deploy" "$work/bin" "$work/fake" "$work/proc" "$work/backups"
cp "$here/check.sh" "$here/alert.sh" "$work/deploy/"
touch "$work/deploy/compose.production.yml" "$work/deploy/production.env"
mkdir "$work/deploy/maintenance"

cat >"$work/bin/docker" <<'FAKE'
#!/bin/sh
# A fake docker: what it answers is in $FAKE/<file>; a file holding "FAIL" makes the call fail.
answer() {
	if [ "$(cat "$FAKE/$1")" = FAIL ]; then echo "fake docker: $1 failed" >&2; exit 1; fi
	cat "$FAKE/$1"
}
case "$1" in
compose)
	shift
	while [ "$1" = -f ] || [ "$1" = --env-file ]; do shift 2; done
	case "$1" in
	config) answer services ;;
	ps) answer ps ;;
	exec) answer psql ;;
	*) echo "fake docker: unexpected compose call: $*" >&2; exit 1 ;;
	esac
	;;
inspect) cat "$FAKE/inspect-$4" 2>/dev/null || echo "0 2020-01-01T00:00:00Z" ;;
logs) answer logs ;;
*) echo "fake docker: unexpected call: $*" >&2; exit 1 ;;
esac
FAKE
cat >"$work/bin/df" <<'FAKE'
#!/bin/sh
echo "Filesystem 1024-blocks Used Available Capacity Mounted on"
echo "/dev/vda1 100000000 $(cat "$FAKE/df") 50% $2"
FAKE
cat >"$work/bin/curl" <<'FAKE'
#!/bin/sh
# A fake curl for Telegram: the config on standard input names the address; each call is appended to
# $FAKE/telegram as "<address> | <arguments>", the text's first line after it.
config="$(cat)"
text=""
for argument in "$@"; do
	case "$argument" in text=*) text="${argument#text=}" ;; esac
done
echo "$config | $* " | head -n 1 | sed 's/ text=.*//' >>"$FAKE/curl-calls"
echo "$text" | head -n 1 >>"$FAKE/telegram"
[ "$(cat "$FAKE/curl-answer" 2>/dev/null)" != FAIL ]
FAKE
chmod +x "$work/bin/docker" "$work/bin/df" "$work/bin/curl"

now=1800000000
started_long_ago="$(date -u -d "@$((now - 86400))" +%Y-%m-%dT%H:%M:%S.123456789Z)"
started_just_now="$(date -u -d "@$((now - 60))" +%Y-%m-%dT%H:%M:%S.123456789Z)"

export PATH="$work/bin:$PATH" FAKE="$work/fake" MONITOR_PROC="$work/proc" MONITOR_CPUS=2 \
	MONITOR_NOW="$now" BACKUP_DIR="$work/backups" SITE_DOMAIN=site.example

healthy() {
	printf 'MemTotal:        4000000 kB\nMemAvailable:    2000000 kB\n' >"$work/proc/meminfo"
	echo "0.50 0.80 0.70 1/200 4000" >"$work/proc/loadavg"
	echo "60000000 40000000" >"$FAKE/df"
	printf 'caddy\napi\nmigrate\nweb\npostgres\numami\n' >"$FAKE/services"
	printf '%s\n' "caddy c1 running " "api a1 running healthy" "migrate m1 exited " "web w1 running " \
		"postgres p1 running healthy" "umami u1 running " >"$FAKE/ps"
	rm -f "$FAKE"/inspect-*
	echo "0 $started_long_ago" >"$FAKE/inspect-a1"
	echo "52428800 6 100" >"$FAKE/psql"
	echo "${started_long_ago%%.*}.000000000Z {\"Message\":\"started\"}" >"$FAKE/logs"
	mkdir -p "$work/backups/heartbeat"
	echo "$((now - 3600))" >"$work/backups/heartbeat/last-good"
	rm -f "$work/backups/heartbeat/last-failed" "$work/deploy/maintenance/on"
}

failures=0
ok() { echo "ok   $1"; }
not_ok() {
	echo "FAIL $1"
	failures=$((failures + 1))
}

# Runs check.sh and compares the FAIL lines it prints with the expected ones (one per argument).
expect_failing() {
	name="$1"
	shift
	expected="$(if [ "$#" -gt 0 ]; then printf '%s\n' "$@"; fi)"
	set +e
	output="$("$work/deploy/check.sh" --no-alert)"
	status=$?
	set -e
	actual="$(echo "$output" | grep '^FAIL ' || true)"
	if [ -z "$expected" ]; then want=0; else want=1; fi
	if [ "$actual" = "$expected" ] && [ "$status" -eq "$want" ]; then
		ok "$name"
	else
		not_ok "$name"
		printf '     expected (exit %s):\n%s\n     actual (exit %s):\n%s\n' "$want" "$expected" "$status" "$output"
	fi
}

healthy
expect_failing "a healthy server passes every check"
if echo "$output" | grep -q '^ok   logs api (the oldest line .*, 1 days old)$'; then
	ok "the age of the API's oldest log line is reported"
else
	not_ok "the age of the API's oldest log line is reported"
fi

healthy
MONITOR_DISK_FREE_PERCENT=101 expect_failing "a disk threshold above the free space fails the disk" "FAIL disk /"
echo "$output" | grep -q '^     40 % free, .*; below 101 %$' && ok "the disk's reason names its numbers" ||
	not_ok "the disk's reason names its numbers"

healthy
echo "95000000 5000000" >"$FAKE/df"
expect_failing "5 % free disk fails" "FAIL disk /"

healthy
printf 'MemTotal:        4000000 kB\nMemAvailable:     200000 kB\n' >"$work/proc/meminfo"
echo "9.00 6.00 3.00 1/200 4000" >"$work/proc/loadavg"
expect_failing "low memory and a high load fail" "FAIL memory" "FAIL load"

healthy
printf '%s\n' "caddy c1 running " "api a1 running unhealthy" "migrate m1 exited " "web w1 restarting " \
	"postgres p1 running healthy" >"$FAKE/ps"
expect_failing "an unhealthy, a restarting and a missing container fail; a finished one-shot does not" \
	"FAIL container api" "FAIL container web" "FAIL container umami"

healthy
echo "3 $started_just_now" >"$FAKE/inspect-a1"
expect_failing "a container restarted a minute ago fails" "FAIL container api"
echo "3 $started_long_ago" >"$FAKE/inspect-a1"
expect_failing "one restarted a day ago passes"

healthy
echo "52428800 90 100" >"$FAKE/psql"
expect_failing "connections near the limit fail" "FAIL database connections"
MONITOR_DATABASE_MAX_MB=10 expect_failing "a database above its size fails" "FAIL database size" "FAIL database connections"
echo FAIL >"$FAKE/psql"
expect_failing "a database that does not answer fails" "FAIL database"

healthy
echo FAIL >"$FAKE/psql"
echo FAIL >"$FAKE/ps"
touch "$work/deploy/maintenance/on"
expect_failing "during maintenance the containers and the database are not checked"

healthy
rm "$work/backups/heartbeat/last-good"
expect_failing "no heartbeat fails the backup" "FAIL backup age"
echo "$((now - 27 * 3600))" >"$work/backups/heartbeat/last-good"
expect_failing "a heartbeat 27 hours old fails the backup" "FAIL backup age"
echo "$((now - 3600))" >"$work/backups/heartbeat/last-good"
echo "$((now - 60))" >"$work/backups/heartbeat/last-failed"
expect_failing "a run failed since the last good one fails" "FAIL backup last run"
echo "$((now - 30))" >"$work/backups/heartbeat/last-good"
expect_failing "a good run after it clears it"

# The alert, against the fake curl.
: >"$FAKE/telegram"
: >"$FAKE/curl-calls"
export MONITOR_STATE="$work/state" TELEGRAM_BOT_TOKEN=123:secret TELEGRAM_CHAT_ID=42 \
	TELEGRAM_API_BASE=http://127.0.0.1:9
alert() {
	printf '%s\n' "$@" >"$work/report"
	set +e
	"$work/deploy/alert.sh" "$work/report" >/dev/null 2>&1
	status=$?
	set -e
}
expect_sent() {
	actual="$(cat "$FAKE/telegram")"
	if [ "$actual" = "$2" ] && [ "$status" -eq "${3:-0}" ]; then
		ok "$1"
	else
		not_ok "$1"
		printf '     expected (exit %s):\n%s\n     actual (exit %s):\n%s\n' "${3:-0}" "$2" "$status" "$actual"
	fi
	: >"$FAKE/telegram"
}

alert "ok   disk / (40 % free)" "ok   backup age"
expect_sent "a passing report with nothing sent before sends nothing" ""

alert "FAIL disk /" "     5 % free; below 15 %" "ok   backup age"
expect_sent "a failure sends a message" "The server of site.example needs attention (RUNBOOK.md, section Monitoring):"
if grep -q secret "$FAKE/curl-calls" && ! grep -q 'sendMessage.*|.*secret' "$FAKE/curl-calls"; then
	ok "the token reaches curl on its standard input, not its arguments"
else
	not_ok "the token reaches curl on its standard input, not its arguments"
	cat "$FAKE/curl-calls"
fi

alert "FAIL disk /" "     4 % free; below 15 %" "ok   backup age"
expect_sent "the same failure with other numbers sends nothing" ""

echo FAIL >"$FAKE/curl-answer"
alert "FAIL disk /" "     4 % free; below 15 %" "FAIL backup last run" "     failed 3 min ago"
expect_sent "a message Telegram refuses fails the alert" "The server of site.example: what fails has changed (RUNBOOK.md, section Monitoring):" 1
rm "$FAKE/curl-answer"
alert "FAIL disk /" "     4 % free; below 15 %" "FAIL backup last run" "     failed 8 min ago"
expect_sent "and is sent on the next run" "The server of site.example: what fails has changed (RUNBOOK.md, section Monitoring):"

alert "ok   disk / (40 % free)" "ok   backup last run"
expect_sent "a passing report after a failure sends the recovery" "The server of site.example: every check passes again."
alert "ok   disk / (40 % free)" "ok   backup last run"
expect_sent "and then nothing" ""

unset TELEGRAM_BOT_TOKEN
alert "FAIL disk /" "     5 % free; below 15 %"
expect_sent "without a bot nothing is sent" ""

exit "$((failures > 0))"
