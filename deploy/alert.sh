#!/bin/sh
# The server's alert (#296, ADR 0020 decision 4), run by check.sh after its checks:
#
#   ./alert.sh <report of check.sh>
#
# One Telegram message when the set of FAIL lines changes — the first failure, another one, one
# gone — and one when none is left; nothing while the report says what it said last time (#34's
# rule). The FAIL lines last sent are kept in MONITOR_STATE (monitor-state, beside this script); it is
# written only once the message has gone, so a message Telegram refused is tried again five minutes
# later. A message names the checks and their numbers, never a user id, an e-mail or a request body:
# the report holds nothing else.
#
# The bot is a dedicated one (TELEGRAM_BOT_TOKEN, TELEGRAM_CHAT_ID in production.env). Neither set:
# nothing is sent and nothing is recorded. TELEGRAM_API_BASE replaces https://api.telegram.org, for
# a local stub.
set -eu

report="${1:?usage: $0 <report of check.sh>}"
cd "$(dirname "$0")"

setting() {
	eval "value=\${$1:-}"
	if [ -z "$value" ] && [ -f production.env ]; then
		value="$(sed -n "s/^$1=//p" production.env | tail -n 1)"
	fi
	printf '%s' "${value:-$2}"
}

state="$(setting MONITOR_STATE monitor-state)"
token="$(setting TELEGRAM_BOT_TOKEN '')"
chat="$(setting TELEGRAM_CHAT_ID '')"
api="$(setting TELEGRAM_API_BASE https://api.telegram.org)"
name="$(setting SITE_DOMAIN "$(hostname)")"

failing="$(grep '^FAIL ' "$report" | sort || true)"
previous="$(cat "$state" 2>/dev/null || true)"

if [ "$failing" = "$previous" ]; then
	echo "Alert: nothing has changed"
	exit 0
fi

# Each FAIL line with the line saying why.
details="$(awk '/^FAIL / { keep = 1; print; next } /^ok / { keep = 0; next } keep' "$report")"
if [ -z "$failing" ]; then
	text="The server of $name: every check passes again."
elif [ -z "$previous" ]; then
	text="The server of $name needs attention (RUNBOOK.md, section Monitoring):

$details"
else
	text="The server of $name: what fails has changed (RUNBOOK.md, section Monitoring):

$details"
fi

if [ -z "$token" ] || [ -z "$chat" ]; then
	echo "Alert: TELEGRAM_BOT_TOKEN or TELEGRAM_CHAT_ID is not set; nothing sent" >&2
	exit 0
fi

# The token goes to curl on its standard input, not on its command line, where ps would show it.
# Telegram takes up to 4096 characters.
text="$(printf '%s' "$text" | head -c 3500)"
if ! printf 'url = "%s/bot%s/sendMessage"\n' "$api" "$token" |
	curl --silent --show-error --fail --max-time 20 --output /dev/null --config - \
		--data-urlencode "chat_id=$chat" --data-urlencode "text=$text"; then
	echo "Alert: Telegram did not take the message; it is tried again on the next run" >&2
	exit 1
fi

printf '%s' "$failing" >"$state"
if [ -z "$failing" ]; then
	echo "Alert: sent the recovery"
else
	echo "Alert: sent what fails"
fi
