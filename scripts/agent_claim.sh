#!/usr/bin/env bash
# Record (or update) your status on a GitHub issue so other agents do not step on you.
#   scripts/agent_claim.sh <issue#> <claimed|in-progress|blocked|handoff|done> "<one-line note>" [--dry-run]
# Identity: set AGENT_NAME (e.g. "claude-code/opus-5", "codex/gpt-5"); SESSION_URL is optional.
# Files touched are computed from git (diff vs origin/main plus uncommitted changes).
set -euo pipefail

issue="${1:?issue number}"; status="${2:?status: claimed|in-progress|blocked|handoff|done}"; note="${3:-}"; dry="${4:-}"
case "$status" in claimed|in-progress|blocked|handoff|done) ;; *) echo "bad status: $status" >&2; exit 2;; esac
agent="${AGENT_NAME:?set AGENT_NAME, e.g. claude-code/opus-5}"

branch="$(git rev-parse --abbrev-ref HEAD)"
worktree="$(git rev-parse --show-toplevel)"
head="$(git rev-parse --short HEAD)"
files="$( { git diff --name-only origin/main...HEAD 2>/dev/null; git diff --name-only HEAD; git ls-files --others --exclude-standard; } | sort -u | sed 's/^/  - /' )"
[ -n "$files" ] || files="  - (none yet)"

body="<!-- AGENT-STATUS -->
**Agent:** $agent
**Status:** $status
**Updated:** $(date -u +%Y-%m-%dT%H:%MZ)
**Branch / worktree:** \`$branch\` @ \`$head\` in \`$worktree\`
**Session:** ${SESSION_URL:-n/a}
**Note:** $note
**Files touched (do not edit these concurrently):**
$files"

if [ "$dry" = "--dry-run" ]; then echo "$body"; exit 0; fi

gh label create in-progress --description "An agent is actively working on this" --color fbca04 2>/dev/null || true
gh issue comment "$issue" --body "$body" >/dev/null
case "$status" in
  claimed|in-progress|blocked) gh issue edit "$issue" --add-label in-progress >/dev/null ;;
  handoff|done) gh issue edit "$issue" --remove-label in-progress >/dev/null || true ;;
esac
echo "Recorded '$status' on #$issue as $agent"
