# Superpowers (obra/superpowers) for every agent

[Superpowers](https://github.com/obra/superpowers) is the skills library this repo's workflow assumes: `brainstorming`, `test-driven-development`, `systematic-debugging`, `verification-before-completion`, `requesting-code-review`, `using-git-worktrees`, `writing-plans`, and more. It installs **per user, per agent tool**; a repo file cannot force it on. This repo does what it can:

- **Claude Code:** `.claude/settings.json` enables `superpowers@claude-plugins-official` for this project, so Claude Code offers to install it on first open.
- **Every other agent:** install it once with the command below (the human runs it, or an agent runs it **after the user says yes**; it changes the user's tool setup).

| Agent | Install |
| :--- | :--- |
| Claude Code | `/plugin install superpowers@claude-plugins-official` (already enabled for this repo in `.claude/settings.json`) |
| Codex CLI / App | `/plugins`, search `superpowers`, Install Plugin (official Codex plugin marketplace) |
| Gemini CLI | `gemini extensions install https://github.com/obra/superpowers` |
| GitHub Copilot CLI | `copilot plugin marketplace add obra/superpowers-marketplace` then `copilot plugin install superpowers@superpowers-marketplace` |
| Antigravity (`agy`) | `agy plugin install https://github.com/obra/superpowers` |
| Cursor | `/add-plugin superpowers` |
| OpenCode | Tell it: `Fetch and follow instructions from https://raw.githubusercontent.com/obra/superpowers/refs/heads/main/.opencode/INSTALL.md` |

Other harnesses (Pi, Kimi, Devin, Factory Droid, Qwen, Hermes, Muse, Grok) are listed in the plugin's README: https://github.com/obra/superpowers#installation

Verify an install by asking the agent to list its skills; `using-superpowers` should be present. If your agent has no Superpowers, the `docs/agents/work-issues.md` runbook still works on its own; the skills are accelerators, not requirements.
