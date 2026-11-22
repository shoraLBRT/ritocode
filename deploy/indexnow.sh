#!/bin/sh
# Tells IndexNow (Yandex, Bing and the other engines that share it) which public pages a release
# changed (#303):
#
#   INDEXNOW_KEY=<key> SITE_DOMAIN=<domain> ./indexnow.sh <pages before> <pages after>
#
# Each file is a web image's page-hashes.txt, a line `<hash> <path>` per public page (written by the
# prerender, frontend/src/prerender/prerender.tsx). A page is sent when it is new, when its hash
# changed or when it is gone (IndexNow takes removed addresses too, so the engine sees the 404). An
# empty "before" — the first release, or an image without the file — sends every page. deploy/release.sh
# runs it after a release; without INDEXNOW_KEY it sends nothing. The edge serves the key at
# https://<domain>/<key>.txt (deploy/Caddyfile), which is how the engine checks the call is the site's.
# INDEXNOW_ENDPOINT overrides where the call goes (https://api.indexnow.org/indexnow by default).
# Needs curl. Tested by indexnow-test.sh against a fake curl.
set -eu

before="${1:?usage: ./indexnow.sh <pages before> <pages after>}"
after="${2:?usage: ./indexnow.sh <pages before> <pages after>}"
key="${INDEXNOW_KEY:-}"
endpoint="${INDEXNOW_ENDPOINT:-https://api.indexnow.org/indexnow}"

if [ -z "$key" ]; then
	echo "IndexNow: INDEXNOW_KEY is not set; nothing sent."
	exit 0
fi

# IndexNow's own rule for a key: 8 to 128 letters, digits and dashes.
length=${#key}
case "$key" in
*[!A-Za-z0-9-]*) valid=no ;;
*) if [ "$length" -ge 8 ] && [ "$length" -le 128 ]; then valid=yes; else valid=no; fi ;;
esac
if [ "$valid" = no ]; then
	echo "IndexNow: INDEXNOW_KEY must be 8 to 128 letters, digits and dashes; nothing sent." >&2
	exit 1
fi

domain="${SITE_DOMAIN:?set SITE_DOMAIN, such as ritocode.ru}"
[ -f "$before" ] || : >"$before"

# Added or changed: a line of "after" that "before" lacks. Gone: a path of "before" that "after" lacks.
changed="$(awk '
	FILENAME == ARGV[1] { if (NF == 2) { line[$0] = 1; path[$2] = 1 } next }
	NF == 2 { now[$2] = 1; if (!($0 in line)) print $2 }
	END { for (each in path) if (!(each in now)) print each }
' "$before" "$after" | LC_ALL=C sort -u)"

if [ -z "$changed" ]; then
	echo "IndexNow: no public page changed; nothing sent."
	exit 0
fi

body="$(mktemp)"
trap 'rm -f "$body"' EXIT
{
	printf '{"host":"%s","key":"%s","keyLocation":"https://%s/%s.txt","urlList":[' "$domain" "$key" "$domain" "$key"
	echo "$changed" | sed 's/[\\"]/\&/g' | awk -v site="https://$domain" '{ printf "%s\"%s%s\"", (NR > 1 ? "," : ""), site, $0 }'
	printf ']}\n'
} >"$body"

count="$(echo "$changed" | wc -l | tr -d ' ')"
if curl --fail --silent --show-error --max-time 30 \
	--header 'Content-Type: application/json; charset=utf-8' \
	--data-binary "@$body" "$endpoint" >/dev/null; then
	echo "IndexNow: sent $count changed address(es)."
else
	echo "IndexNow: the call to $endpoint failed; $count address(es) not sent." >&2
	exit 1
fi
