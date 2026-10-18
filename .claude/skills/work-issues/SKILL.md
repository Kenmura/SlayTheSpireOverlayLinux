---
name: work-issues
description: Use when the user says "begin working issues", "work the issues", or asks the agent to take over GitHub issue implementation. Runs the full autonomous pipeline per issue - claim, worktree, tests first, implement, adversarial review, PR, CI, squash-merge, close with evidence.
---

The full runbook is tool-neutral and lives in `docs/agents/work-issues.md`. Read it now and follow it end to end without asking the user between steps. The reviewer prompt is `docs/agents/adversarial-review-prompt.md`.
