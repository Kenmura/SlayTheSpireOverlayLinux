#!/usr/bin/env bash
# Show who is working on what. Run this BEFORE starting any work.
# Reads the local status files written by agent_claim.sh (shared by all worktrees of this clone).
set -euo pipefail
dir="$(cd "$(git rev-parse --git-common-dir)" && pwd)/agent-status"
echo "=== Active claims ($dir) ==="
found=0
if [ -d "$dir" ]; then
  for f in "$dir"/*.md; do
    [ -e "$f" ] || continue
    grep -q '^\*\*Status:\*\* done' "$f" && continue
    found=1
    stale=""; [ -n "$(find "$f" -mmin +1440 2>/dev/null)" ] && stale="   [STALE: no update for 24h+ - comment/ask before taking over]"
    cat "$f"; echo "$stale"; echo
  done
fi
[ "$found" = 1 ] || echo "(none)"
echo "=== Git worktrees ==="; git worktree list
echo; echo "=== Uncommitted changes in this checkout (someone may own them) ==="; git status --short
