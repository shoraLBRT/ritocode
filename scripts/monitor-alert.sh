#!/usr/bin/env bash
# The alert of the uptime check (#34, ADR 0013), run by .github/workflows/monitor.yml after
# scripts/monitor-check.sh:
#
#   GH_TOKEN=… GITHUB_REPOSITORY=owner/repo GITHUB_REPOSITORY_OWNER=owner ./scripts/monitor-alert.sh <report>
#
# The alert is one open issue with the label MONITOR_LABEL (monitoring-alert), mentioning the owner so
# GitHub notifies them. A failing report opens it if none is open; while it stays open, a new comment
# — another mention — is added only when the set of FAIL lines changes, so a check that fails every
# five minutes sends one notification, not one a run. A report with no FAIL line closes it.
# MONITOR_RUN_URL, when set, links the run that saw it.
set -euo pipefail

report="${1:?usage: $0 <report of monitor-check.sh>}"
label="${MONITOR_LABEL:-monitoring-alert}"
owner="${GITHUB_REPOSITORY_OWNER:?set GITHUB_REPOSITORY_OWNER}"
run="${MONITOR_RUN_URL:-}"

failing="$(grep '^FAIL ' "$report" | sort || true)"
open="$(gh issue list --label "$label" --state open --json number --jq '.[0].number // empty')"

if [ -z "$failing" ]; then
	if [ -n "$open" ]; then
		gh issue comment "$open" --body "Every check passes again.${run:+ ($run)}"
		gh issue close "$open"
		echo "Closed alert #$open"
	fi
	exit 0
fi

# The FAIL lines are kept in the issue's body, in a comment GitHub does not render, so the next run
# can tell whether what is wrong has changed.
body="@$owner the site needs attention. RUNBOOK.md, section *Monitoring*, says what to look at.

\`\`\`
$(cat "$report")
\`\`\`
${run:+
Seen by $run}

<!-- monitor-failing
$failing
-->"

if [ -z "$open" ]; then
	gh label create "$label" --color B60205 --description "An alert of the uptime check (#34)" --force >/dev/null
	gh issue create --title "Monitoring: the site needs attention" --label "$label" --body "$body"
	exit 0
fi

previous="$(gh issue view "$open" --json body --jq .body | tr -d '\r' |
	sed -n '/^<!-- monitor-failing$/,/^-->$/p' | sed '1d;$d')"
if [ "$previous" = "$failing" ]; then
	echo "Alert #$open is open and says the same"
	exit 0
fi

gh issue edit "$open" --body "$body"
gh issue comment "$open" --body "@$owner what fails has changed:

\`\`\`
$failing
\`\`\`"
echo "Updated alert #$open"
