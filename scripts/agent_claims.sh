#!/usr/bin/env bash
# Show who is working on what. Run this BEFORE starting any work.
set -euo pipefail
echo "=== Issues marked in-progress (latest AGENT-STATUS comment each) ==="
for n in $(gh issue list --label in-progress --state open --json number -q '.[].number'); do
  echo "--- #$n $(gh issue view "$n" --json title -q .title)"
  gh issue view "$n" --json comments -q '[.comments[] | select(.body | contains("<!-- AGENT-STATUS -->"))] | last | .body // "(no status comment)"' | grep -v '^<!--'
done
echo; echo "=== Git worktrees ==="; git worktree list
echo; echo "=== Uncommitted changes in this checkout (someone may own them) ==="; git status --short
