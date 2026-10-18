#!/usr/bin/env bash
# Record (or update) your status for a piece of work so other agents do not step on you.
#   scripts/agent_claim.sh <key> <claimed|in-progress|blocked|handoff|done> "<one-line note>" [--dry-run]
# <key> is a GitHub issue number or a short slug (e.g. 16, fog-opacity). One status file per key.
# Status files live in the git common dir (<repo>/.git/agent-status/), shared by every worktree of
# this clone, never committed or pushed. Nothing here touches GitHub.
# Identity: set AGENT_NAME (e.g. "claude-code/opus-5"); SESSION_URL is optional.
set -euo pipefail

key="${1:?key (issue number or slug)}"; status="${2:?status: claimed|in-progress|blocked|handoff|done}"; note="${3:-}"; dry="${4:-}"
case "$status" in claimed|in-progress|blocked|handoff|done) ;; *) echo "bad status: $status" >&2; exit 2;; esac
case "$key" in *[!A-Za-z0-9._-]*|"") echo "key may only contain letters, digits, . _ -" >&2; exit 2;; esac
agent="${AGENT_NAME:?set AGENT_NAME, e.g. claude-code/opus-5}"

branch="$(git rev-parse --abbrev-ref HEAD)"
worktree="$(git rev-parse --show-toplevel)"
head="$(git rev-parse --short HEAD)"
files="$( { git diff --name-only origin/main...HEAD 2>/dev/null; git diff --name-only HEAD; git ls-files --others --exclude-standard; } | sort -u | sed 's/^/  - /' )"
[ -n "$files" ] || files="  - (none yet)"

body="# $key
**Agent:** $agent
**Status:** $status
**Updated:** $(date -u +%Y-%m-%dT%H:%MZ)
**Branch / worktree:** \`$branch\` @ \`$head\` in \`$worktree\`
**Session:** ${SESSION_URL:-n/a}
**Note:** $note
**Files touched (do not edit these concurrently):**
$files"

if [ "$dry" = "--dry-run" ]; then echo "$body"; exit 0; fi

dir="$(cd "$(git rev-parse --git-common-dir)" && pwd)/agent-status"
mkdir -p "$dir"
printf '%s\n' "$body" > "$dir/$key.md"
echo "Recorded '$status' for '$key' as $agent in $dir/$key.md"
