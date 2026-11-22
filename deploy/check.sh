#!/bin/sh
# The server's own checks (#296, ADR 0020 decision 3), on the server, from deploy/, every five minutes
# from cron (deploy/README.md):
#
#   ./check.sh             checks, prints the report, then ./alert.sh sends Telegram if what fails changed
#   ./check.sh --no-alert  checks and prints the report only
#
# What it checks: free disk on each of MONITOR_DISK_PATHS, available memory, the five-minute load per
# CPU, every long-running service of compose.production.yml (running, not unhealthy, not restarted in
# the last MONITOR_RESTART_WINDOW seconds), the product database's size and connections, and the
# backup's heartbeat (backup.sh). While maintenance/on exists (a release, or maintenance by hand) the
# containers and the database are not checked: they are down on purpose. The age of the API's oldest
# log line is reported too, never alerted on (ADR 0020 decision 2).
#
# The report has the shape of scripts/monitor-check.sh's: "ok   <check> (<numbers>)", or
# "FAIL <check>" followed by an indented line saying why. The FAIL line names what is wrong and nothing
# that changes from run to run — alert.sh compares them between runs; the numbers are on the indented
# line. Exits 1 when any check failed.
#
# Every threshold is a variable, from the shell or production.env (production.env.example lists them):
# setting MONITOR_DISK_FREE_PERCENT=101 makes the disk check fail on purpose, to see an alert arrive.
set -u

cd "$(dirname "$0")"

# A setting: the shell's value, else production.env's, else the default.
setting() {
	eval "value=\${$1:-}"
	if [ -z "$value" ] && [ -f production.env ]; then
		value="$(sed -n "s/^$1=//p" production.env | tail -n 1)"
	fi
	printf '%s' "${value:-$2}"
}

disk_paths="$(setting MONITOR_DISK_PATHS /)"
disk_free="$(setting MONITOR_DISK_FREE_PERCENT 15)"
memory_free="$(setting MONITOR_MEMORY_FREE_PERCENT 10)"
load_per_cpu="$(setting MONITOR_LOAD_PER_CPU 2)"
restart_window="$(setting MONITOR_RESTART_WINDOW 900)"
database_max_mb="$(setting MONITOR_DATABASE_MAX_MB 5120)"
connections_percent="$(setting MONITOR_DATABASE_CONNECTIONS_PERCENT 80)"
backup_hours="$(setting MONITOR_BACKUP_MAX_HOURS 26)"
backup_dir="$(setting BACKUP_DIR /var/backups/ritocode)"
# For the tests: another /proc, CPU count and clock.
proc="${MONITOR_PROC:-/proc}"
cpus="${MONITOR_CPUS:-$(getconf _NPROCESSORS_ONLN 2>/dev/null || echo 1)}"
now="${MONITOR_NOW:-$(date -u +%s)}"
# Services that run once and exit (migrations, ingest), not kept running.
one_shot="migrate"

compose="docker compose -f compose.production.yml --env-file production.env"

failed=0
pass() { echo "ok   $1"; }
fail() {
	echo "FAIL $1"
	echo "     $2"
	failed=1
}
is_number() {
	case "$1" in
	'' | *[!0-9]*) return 1 ;;
	*) return 0 ;;
	esac
}

check_disk() {
	for path in $disk_paths; do
		# POSIX df: 1024-byte blocks; the third and fourth columns are used and available.
		set -- $(df -P "$path" 2>/dev/null | awk 'NR == 2 { print $3, $4 }')
		if [ "$#" -ne 2 ] || ! is_number "$1" || ! is_number "$2" || [ "$(($1 + $2))" -eq 0 ]; then
			fail "disk $path" "df could not read $path"
			continue
		fi
		percent=$(($2 * 100 / ($1 + $2)))
		numbers="$percent % free, $(($2 / 1048576)) of $((($1 + $2) / 1048576)) GB"
		if [ "$percent" -lt "$disk_free" ]; then
			fail "disk $path" "$numbers; below $disk_free %"
		else
			pass "disk $path ($numbers)"
		fi
	done
}

check_memory() {
	total="$(awk '/^MemTotal:/ { print $2 }' "$proc/meminfo" 2>/dev/null)"
	# MemFree where the kernel does not say MemAvailable (before 3.14, or Git for Windows): a lower bound.
	available="$(awk '/^MemAvailable:/ { a = $2 } /^MemFree:/ { f = $2 } END { print (a != "" ? a : f) }' "$proc/meminfo" 2>/dev/null)"
	if ! is_number "$total" || ! is_number "$available" || [ "$total" -eq 0 ]; then
		fail "memory" "$proc/meminfo could not be read"
		return
	fi
	percent=$((available * 100 / total))
	numbers="$percent % available, $((available / 1024)) of $((total / 1024)) MB"
	if [ "$percent" -lt "$memory_free" ]; then
		fail "memory" "$numbers; below $memory_free %"
	else
		pass "memory ($numbers)"
	fi
}

check_load() {
	load="$(awk '{ print $2 }' "$proc/loadavg" 2>/dev/null)"
	if [ -z "$load" ]; then
		fail "load" "$proc/loadavg could not be read"
		return
	fi
	numbers="$load over five minutes on $cpus CPUs"
	if awk -v average="$load" -v cpus="$cpus" -v per="$load_per_cpu" 'BEGIN { exit !(average > cpus * per) }'; then
		fail "load" "$numbers; above $load_per_cpu per CPU"
	else
		pass "load ($numbers)"
	fi
}

check_containers() {
	if ! services="$($compose config --services 2>&1)"; then
		fail "docker" "docker compose could not read compose.production.yml: $(echo "$services" | head -n 1)"
		return
	fi
	if ! states="$($compose ps --all --format '{{.Service}} {{.ID}} {{.State}} {{.Health}}' 2>&1)"; then
		fail "docker" "docker compose ps failed: $(echo "$states" | head -n 1)"
		return
	fi

	for service in $services; do
		case " $one_shot " in *" $service "*) continue ;; esac
		row="$(echo "$states" | awk -v service="$service" '$1 == service { print; exit }')"
		if [ -z "$row" ]; then
			fail "container $service" "no container: the service is not running"
			continue
		fi
		set -- $row
		id="$2" state="$3" health="${4:-}"
		if [ "$service" = api ]; then api_id="$id"; fi
		case "$state" in
		running) ;;
		*)
			fail "container $service" "$state"
			continue
			;;
		esac
		if [ "$health" = unhealthy ]; then
			fail "container $service" "running, unhealthy"
			continue
		fi
		set -- $(docker inspect --format '{{.RestartCount}} {{.State.StartedAt}}' "$id" 2>/dev/null)
		restarts="${1:-0}"
		started="$(date -u -d "${2:-}" +%s 2>/dev/null || echo 0)"
		if is_number "$restarts" && [ "$restarts" -gt 0 ] && [ "$((now - started))" -lt "$restart_window" ]; then
			fail "container $service" "restarted $restarts times, the last $((now - started)) s ago"
		else
			pass "container $service ($state${health:+, $health})"
		fi
	done
}

check_database() {
	query="select pg_database_size('ritocode'),
		(select count(*) from pg_stat_activity where backend_type = 'client backend'),
		current_setting('max_connections')"
	if ! answer="$($compose exec -T postgres psql --username ritocode --dbname ritocode -At -F ' ' -c "$query" 2>&1)"; then
		fail "database" "psql did not answer: $(echo "$answer" | head -n 1)"
		return
	fi
	set -- $answer
	if [ "$#" -ne 3 ] || ! is_number "$1" || ! is_number "$2" || ! is_number "$3" || [ "$3" -eq 0 ]; then
		fail "database" "psql answered something else: $(echo "$answer" | head -n 1)"
		return
	fi
	size_mb=$(($1 / 1048576))
	if [ "$size_mb" -gt "$database_max_mb" ]; then
		fail "database size" "$size_mb MB; above $database_max_mb MB"
	else
		pass "database size ($size_mb MB)"
	fi
	percent=$(($2 * 100 / $3))
	if [ "$percent" -ge "$connections_percent" ]; then
		fail "database connections" "$2 of $3; $connections_percent % or more"
	else
		pass "database connections ($2 of $3)"
	fi
}

check_backup() {
	good="$(cat "$backup_dir/heartbeat/last-good" 2>/dev/null)"
	bad="$(cat "$backup_dir/heartbeat/last-failed" 2>/dev/null)"
	is_number "$good" || good=""
	is_number "$bad" || bad=""

	if [ -z "$good" ]; then
		fail "backup age" "no good backup recorded in $backup_dir/heartbeat/last-good"
	elif [ "$((now - good))" -gt "$((backup_hours * 3600))" ]; then
		fail "backup age" "the last good backup is $(((now - good) / 3600)) h old; above $backup_hours h"
	else
		pass "backup age (the last good one $(((now - good) / 3600)) h ago)"
	fi

	if [ -n "$bad" ] && { [ -z "$good" ] || [ "$bad" -gt "$good" ]; }; then
		fail "backup last run" "failed $(((now - bad) / 60)) min ago; its log says why"
	else
		pass "backup last run"
	fi
}

# Information only: how many days of the API's log Docker keeps (ADR 0020 decision 2).
report_log_age() {
	[ -n "${api_id:-}" ] || return 0
	first="$(docker logs --timestamps "$api_id" 2>&1 | head -n 1 | cut -c 1-19)"
	since="$(date -u -d "$first" +%s 2>/dev/null)" || return 0
	pass "logs api (the oldest line ${first%%T*}, $(((now - since) / 86400)) days old)"
}

run_checks() {
	check_disk
	check_memory
	check_load
	if [ -e maintenance/on ]; then
		pass "containers (maintenance is on: not checked)"
		pass "database (maintenance is on: not checked)"
	else
		check_containers
		check_database
		report_log_age
	fi
	check_backup
}

if [ "${1:-}" = --no-alert ]; then
	run_checks
	exit "$failed"
fi

report="$(mktemp)"
trap 'rm -f "$report"' EXIT
run_checks >"$report"
cat "$report"
# 0 and 1 are the checks' answers; 2 says the alert could not be sent.
./alert.sh "$report" || exit 2
exit "$failed"
