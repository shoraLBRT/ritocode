#!/bin/sh
# Tests of deploy/indexnow.sh (#303) against a fake curl, which keeps the body it was given and the
# address it was sent to instead of calling IndexNow. Run by the backend's tests (DeployScriptTests),
# so every pull request runs it. Needs a POSIX sh and awk (Linux, or Git for Windows); no network.
set -eu

here="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

mkdir "$work/bin" "$work/fake"
cat >"$work/bin/curl" <<'FAKE'
#!/bin/sh
# A fake curl: the body (`--data-binary @file`) goes to $FAKE/body, the address (the last argument) to
# $FAKE/address; $FAKE/answer holding FAIL makes the call fail.
for argument in "$@"; do
	case "$argument" in @*) cp "${argument#@}" "$FAKE/body" ;; esac
	address="$argument"
done
echo "$address" >"$FAKE/address"
[ "$(cat "$FAKE/answer" 2>/dev/null)" != FAIL ]
FAKE
chmod +x "$work/bin/curl"

export PATH="$work/bin:$PATH" FAKE="$work/fake" SITE_DOMAIN=ritocode.example INDEXNOW_KEY=0123abcd-key
failures=0

fail() {
	echo "FAIL: $1" >&2
	failures=$((failures + 1))
}

# Runs indexnow.sh on the two page lists; its output in $work/out, its exit code in $status.
run() {
	rm -f "$FAKE/body" "$FAKE/address"
	printf "$1" >"$work/before"
	printf "$2" >"$work/after"
	status=0
	sh "$here/indexnow.sh" "$work/before" "$work/after" >"$work/out" 2>&1 || status=$?
}

expect_body() {
	if [ "$(cat "$FAKE/body" 2>/dev/null)" != "$2" ]; then
		fail "$1: sent $(cat "$FAKE/body" 2>/dev/null || echo nothing), expected $2"
	fi
}

expect_nothing_sent() {
	if [ -f "$FAKE/body" ]; then fail "$1: sent $(cat "$FAKE/body"), expected nothing"; fi
}

# A changed page, a new one and a removed one are sent; an unchanged one is not.
run 'a1 /\nb1 /problems\nc1 /problems/old-card\nd1 /tasks\n' 'a1 /\nb2 /problems\nd1 /tasks\ne1 /tasks/new-task\n'
[ "$status" -eq 0 ] || fail "changed pages: exit $status: $(cat "$work/out")"
expect_body "changed pages" '{"host":"ritocode.example","key":"0123abcd-key","keyLocation":"https://ritocode.example/0123abcd-key.txt","urlList":["https://ritocode.example/problems","https://ritocode.example/problems/old-card","https://ritocode.example/tasks/new-task"]}'
[ "$(cat "$FAKE/address")" = https://api.indexnow.org/indexnow ] || fail "changed pages: sent to $(cat "$FAKE/address")"
grep -q "sent 3 changed" "$work/out" || fail "changed pages: said $(cat "$work/out")"

# The first release, with no list before it: every page.
run '' 'a1 /\nb1 /problems\n'
expect_body "first release" '{"host":"ritocode.example","key":"0123abcd-key","keyLocation":"https://ritocode.example/0123abcd-key.txt","urlList":["https://ritocode.example/","https://ritocode.example/problems"]}'

# A release that changed no page: no call.
run 'a1 /\nb1 /problems\n' 'a1 /\nb1 /problems\n'
[ "$status" -eq 0 ] || fail "nothing changed: exit $status"
expect_nothing_sent "nothing changed"

# Another endpoint, when set.
INDEXNOW_ENDPOINT=https://yandex.com/indexnow run 'a1 /\n' 'a2 /\n'
[ "$(cat "$FAKE/address")" = https://yandex.com/indexnow ] || fail "endpoint: sent to $(cat "$FAKE/address")"

# No key: nothing sent, and the release is not failed for it.
INDEXNOW_KEY='' run 'a1 /\n' 'a2 /\n'
[ "$status" -eq 0 ] || fail "no key: exit $status"
expect_nothing_sent "no key"

# A key IndexNow would refuse: nothing sent, and said so.
INDEXNOW_KEY='bad key!' run 'a1 /\n' 'a2 /\n'
[ "$status" -ne 0 ] || fail "bad key: exit 0"
expect_nothing_sent "bad key"

# IndexNow refusing the call fails the script, which release.sh reports without failing the release.
echo FAIL >"$FAKE/answer"
run 'a1 /\n' 'a2 /\n'
[ "$status" -ne 0 ] || fail "refused: exit 0"
grep -q "failed" "$work/out" || fail "refused: said $(cat "$work/out")"
rm "$FAKE/answer"

if [ "$failures" -gt 0 ]; then
	echo "$failures IndexNow check(s) failed." >&2
	exit 1
fi
echo "IndexNow: all checks passed."
