#!/usr/bin/env bash
# The uptime check (#34, ADR 0013), run from outside the server by .github/workflows/monitor.yml:
#
#   ./scripts/monitor-check.sh https://site.example [https://stats.site.example ...]
#
# The first address is the site: its /health/ready must answer 200 with "Healthy" — that reaches
# Caddy, the API and PostgreSQL. Every address must answer over a valid TLS certificate that does not
# expire within MONITOR_CERT_DAYS (14; Caddy renews 30 days before expiry, so two weeks of failed
# renewals pass before this fires). A failing request is tried MONITOR_ATTEMPTS times, MONITOR_PAUSE
# seconds apart, so one dropped packet raises no alert.
#
# Prints one line per check: "ok   <check> <target>", or "FAIL <check> <target>" followed by an
# indented line saying why. The FAIL lines name what is wrong and nothing that changes from run to run;
# scripts/monitor-alert.sh compares them between runs. Exits 1 when any check failed.
set -uo pipefail

if [ "$#" -eq 0 ]; then
	echo "usage: $0 <site origin> [other origin ...]" >&2
	exit 2
fi

cert_days="${MONITOR_CERT_DAYS:-14}"
attempts="${MONITOR_ATTEMPTS:-3}"
pause="${MONITOR_PAUSE:-20}"

failed=0
pass() { echo "ok   $1"; }
fail() {
	echo "FAIL $1"
	echo "     $2"
	failed=1
}

# curl, tried until it succeeds; the output of the last try is in $answer.
fetch() {
	local try
	for ((try = 1; try <= attempts; try++)); do
		if answer="$(curl --silent --show-error --max-time 10 "$@" 2>&1)"; then
			return 0
		fi
		if ((try < attempts)); then sleep "$pause"; fi
	done
	return 1
}

check_health() {
	local url="${1%/}/health/ready"
	if fetch --fail "$url" && [[ $answer == *'"status":"Healthy"'* ]]; then
		pass "health $url"
	else
		fail "health $url" "${answer:0:300}"
	fi
}

# Any HTTP answer will do: what is checked is that the address is served over a certificate curl trusts.
check_reachable() {
	local url="${1%/}/"
	if fetch --output /dev/null "$url"; then
		pass "reachable $url"
	else
		fail "reachable $url" "${answer:0:300}"
	fi
}

check_certificate() {
	local host="${1#https://}" port=443 certificate end
	host="${host%%/*}"
	if [[ $host == *:* ]]; then
		port="${host##*:}"
		host="${host%:*}"
	fi

	certificate="$(openssl s_client -connect "$host:$port" -servername "$host" </dev/null 2>/dev/null |
		openssl x509 2>/dev/null)"
	if [ -z "$certificate" ]; then
		fail "certificate $host" "no certificate was presented on $host:$port"
		return
	fi

	end="$(openssl x509 -noout -enddate <<<"$certificate")"
	end="${end#notAfter=}"
	if openssl x509 -noout -checkend $((cert_days * 86400)) <<<"$certificate" >/dev/null; then
		pass "certificate $host (valid until $end)"
	elif ! openssl x509 -noout -checkend 0 <<<"$certificate" >/dev/null; then
		fail "certificate $host" "expired: $end"
	else
		fail "certificate $host" "expires within $cert_days days: $end"
	fi
}

site=1
for origin in "$@"; do
	if ((site)); then
		check_health "$origin"
		site=0
	else
		check_reachable "$origin"
	fi
	if [[ $origin == https://* ]]; then
		check_certificate "$origin"
	fi
done

exit "$failed"
