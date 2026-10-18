# AGENTS.md — SlayTheSpireOverlayLinux

## 1. Project Overview

**SlayTheSpireOverlayLinux** is a Linux-first, Proton-compatible in-game overlay and companion mod for **Slay the Spire 2 (STS2)** built with **.NET 9.0**, **GodotSharp** (compiled against the 4.7.2 bindings; the game itself runs Godot 4.5.1), and **Harmony (`0Harmony`)**. It ships two independent Workshop mods: **Overlay for Tier List** (`SlayTheSpireOverlay.*`) and **Fog of War Map** (`SlayTheSpireMapFog.*`).

Unlike the official **Untapped.gg Companion** desktop application—which relies on an out-of-process Electron window (`untapped-node-native` X11 window tracking) and cross-process memory scraping (`untapped-scry`) that breaks under Wayland, Gamescope, `ptrace_scope` restrictions, and Steam Proton/Wine—this project runs **directly inside the Slay the Spire 2 process** as a native C# mod (`sts2.dll` + `GodotSharp.dll`). Because STS2 loads `.NET 9.0` mod assemblies identically under both Linux Native and Steam Proton builds, an in-game Godot/Harmony mod works on X11, Wayland, Gamescope/Steam Deck, and Proton without external processes or memory scraping.

---

## 1b. Coordination protocol (MANDATORY for every agent)

Several agents (Claude Code, Codex, Gemini, Copilot, humans) edit this repo at the same time, often in separate worktrees, so uncommitted work is invisible to others. Status is kept in **local files only** (never GitHub, never committed or pushed): one file per piece of work in `<repo>/.git/agent-status/`, which every worktree of this clone shares. Follow this for every task:

1. **Look first.** Run `./scripts/agent_claims.sh`. It prints every active claim (agent, status, time, branch, worktree, files touched; stale ones are flagged), all git worktrees, and uncommitted changes in your checkout. If a file you need is listed, or the work is already claimed by a live agent, do not start: pick other work, or ask the user.
2. **Claim before you edit.** Choose a key (a GitHub issue number, or a short slug like `fog-opacity`), then:
   ```bash
   export AGENT_NAME="<tool>/<model>"      # e.g. claude-code/opus-5, codex/gpt-5; add SESSION_URL if you have one
   ./scripts/agent_claim.sh <key> claimed "what I will do, and which areas/files I expect to touch"
   ```
   This writes `.git/agent-status/<key>.md` with the agent, status, UTC time, branch, worktree and the files you have touched so far.
3. **Work in your own worktree and branch** (`git worktree add ../<name> -b <branch> origin/main`), never in a checkout that has someone else's uncommitted changes. Add files by explicit path (never `git add -A`).
4. **Keep the status current.** Re-run `./scripts/agent_claim.sh <key> in-progress|blocked "<note>"` when you start a new area, when your file list grows, or when you are blocked (say on whom or what). A claim not updated for 24 hours is stale: ask the user before taking it over.
5. **Hand off or finish.** When you stop, run `./scripts/agent_claim.sh <key> handoff "<what is done, what is left, branch name>"`, or `done` when finished (done claims disappear from the listing). Never leave a claim behind.
6. **Do not touch other agents' files.** If your change needs a file listed in someone else's active claim, coordinate through the user. If you find uncommitted changes you did not make, leave them alone and do not commit them.

Statuses: `claimed` (reserved, not started), `in-progress`, `blocked`, `handoff` (paused, someone else may continue), `done`.

The protocol is local and voluntary: it only helps agents on this machine that follow it. Never post claims or status to public GitHub issues or PRs. The only agent-authored issue comments allowed are the `work-issues` pipeline's blocking questions and its one closing evidence comment.

### Autonomous issue pipeline
When the user says **"begin working issues"**, read `docs/agents/work-issues.md` (the tool-neutral runbook; Claude Code also exposes it as the `work-issues` skill) and follow it end to end without asking between steps. This applies to every agent (Claude Code, Codex, Gemini, Copilot, ...). In short, per issue: claim, own worktree + branch, tests first for every acceptance criterion, implement, local gates, **mandatory fresh-context adversarial review (up to 3 rounds)**, PR with `Closes #N`, CI green, squash-merge, evidence comment. Ambiguous issues are labelled `blocked` with a question and skipped. Workshop publishing is never part of it.

### Automated guards (run in CI; run them locally before you push)
- `dotnet test tests/SlayTheSpireOverlay.Tests` includes `SourceHygieneTests`: no `src/**/*.cs` file may exceed 500 lines. Three legacy files are allowlisted at their current size; **splitting a file lets you delete its entry, but never raise a number or add an entry**.
- `./scripts/check_duplication.sh` (jscpd) fails if duplicated code exceeds the baseline in `.jscpd.json`. **Only lower the threshold, never raise it.** Before writing a helper, look in `src/SlayTheSpireMod.Common`; if both mods need it, put it there.

---

## 2. Solution Architecture & Boundaries

The solution (`SlayTheSpireOverlay.slnx`) is divided into the projects below; the Core/UI boundaries are enforced by automated architecture tests (`tests/SlayTheSpireOverlay.ArchitectureTests/DependencyTests.cs`):

1. **`src/SlayTheSpireOverlay.Core`** (`net9.0`)
   - **Responsibility**: Pure C# domain models, configuration, local caching, tier list & game data resolution, deck/combat/merchant state calculations, and HTTP API providers.
   - **Architectural Rule**: **MUST NOT** depend on `GodotSharp` or `sts2.dll`. All logic in `Core` must be 100% unit-testable outside the game engine.
2. **`src/SlayTheSpireOverlay.Godot`** (`net9.0`)
   - **Responsibility**: STS2 Mod entry point (`[MegaCrit.Sts2.Core.Modding.ModInitializer("Initialize")]`), Harmony patches against `MegaCrit.Sts2.Core.*`, and programmatic Godot 4.x UI factories (`SlayTheSpireOverlay.Godot.UI`).
   - **Architectural Rule**: Classes in `SlayTheSpireOverlay.Godot.UI` **MUST NOT** depend on `System.Net.Http` (verified by `DependencyTests.UI_ShouldNotDependOn_HttpClient`).
3. **`tests/SlayTheSpireOverlay.Tests`** (`net9.0`)
   - **Responsibility**: MSTest (4.0.1) unit tests for both `*.Core` projects and the shared `SlayTheSpireMod.Common` helpers (tests target `net10.0`; the container has no net9 runtime).
4. **`tests/SlayTheSpireOverlay.ArchitectureTests`** (`net9.0`)
   - **Responsibility**: NetArchTest rules verifying clean separation between `Core`, `Godot.UI`, and network layers.
5. **`src/SlayTheSpireMod.Common`** (source-linked, not a separate assembly)
   - **Responsibility**: Plumbing shared by both mods: `ReflectionAccess`, `JsonConfigStore`, `PatchRegistrar` (pure, unit-tested), and `GameHooks/` (Harmony and Godot helpers). Compiled into each mod DLL via `<Compile Include=... Link=...>`, exactly like `Core`.
   - **Architectural Rule**: Types directly in namespace `SlayTheSpireMod.Common` must not depend on Godot, Harmony or `sts2.dll`; those live in `SlayTheSpireMod.Common.GameHooks`.
6. **`tools/Sts2CompatCheck`** — checks that every game type/member the mods reference exists in a given `sts2.dll`.

---

## 3. Build, Test, & Tooling Commands

On this host (Fedora Atomic/Silverblue) the .NET SDK runs inside the `dotnet-dev` Distrobox container, which only has the 8.0 and 10.0 runtimes. `/home` is symlinked to `/var/home`. `LD_PRELOAD=""` stops Steam's 32-bit `gameoverlayrenderer.so` polluting child processes:

```bash
dn() { LD_PRELOAD="" distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet "$@"; }
```

**Game files.** The `*.Godot` projects reference `sts2.dll` and `0Harmony.dll` from the property `Sts2DataDir` (default `$(HOME)/.local/share/Steam/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64`; override with `-p:Sts2DataDir=...`). Without game files only `tests/SlayTheSpireOverlay.Tests` builds; that is what CI runs.

### Fast checks (no game files needed)
```bash
dn test tests/SlayTheSpireOverlay.Tests
```

### Full test run (needs game files)
```bash
dn test SlayTheSpireOverlay.slnx
```

### Game-version compatibility
```bash
./scripts/check_sts2_compat.sh
```
Run after any game update and before publishing. It fails if a game type/member the mods use no longer exists.

### Publish to Steam Workshop (both items are private)
```bash
./scripts/upload_to_workshop.sh        # Overlay for Tier List
./scripts/upload_fog_to_workshop.sh    # Fog of War Map (workspace: workshop-fog/)
```
Set the release text in `workshop/workshop.json` / `workshop-fog/workshop.json` (`changeNote`) first; keep it in sync with `CHANGELOG.md`.

### Refresh Tier Lists (`workshop/baalorlord_tiers.json`)
```bash
./scripts/update_tiers.sh
```

---

## 4. Critical Godot 4.x & Harmony Modding Constraints

Any agent modifying `src/SlayTheSpireOverlay.Godot` **MUST** adhere to these engine rules discovered through debugging STS2:

1. **NEVER Subclass `Godot.Node` or `Godot.Control` in Mod Assemblies**
   - Defining `public partial class MyWidget : Control` in an external mod `.dll` that was not compiled by the game's original Godot C# source generator causes `Godot.Bridge.ScriptManagerBridge` to fail script lookup and spam `ERROR: Invalid Task ID` / crash on instantiation.
   - **Pattern**: Always instantiate built-in Godot C++ engine classes directly (`new PanelContainer()`, `new HBoxContainer()`, `new VBoxContainer()`, `new MarginContainer()`, `new Label()`, `new RichTextLabel()`, `new Button()`, `new StyleBoxFlat()`) via static factory classes.
2. **Thread-Safe Scene Tree Mutations**
   - Never add, remove, or mutate Godot `Node` properties from background `Task.Run` / `HttpClient` threads.
   - Always marshal scene tree updates onto the Godot main loop via `Callable.From(() => { ... }).CallDeferred()` and verify `GodotObject.IsInstanceValid(node)` before mutating.
3. **Node Pooling & Lifecycle Hooks**
   - STS2 pools and reuses UI nodes (`NCard`, `NRelic`, `NEventOptionButton`, `NMerchantSlot`). Hooking only `_Ready()` causes stale badges when a pooled node is reassigned to a different model.
   - Always patch both `_Ready` and the model setter (`set_Model`, `set_Option`, or `UpdateVisuals`) and replace any existing overlay child node by name (`GetNodeOrNull("TierBadge")`) before attaching a new one (`RemoveChild` + `QueueFree`).
4. **Mouse Input Pass-Through**
   - Passive overlay widgets attached to cards, relics, or rooms must set `MouseFilter = Control.MouseFilterEnum.Ignore` on every container and label so they never block clicking or hovering the underlying game UI.
   - Interactive overlay drawers/buttons must explicitly set `MouseFilter = Control.MouseFilterEnum.Stop` only on the interactive button/header.
5. **Cross-Platform / Proton Path Resolution**
   - Always use `Godot.ProjectSettings.GlobalizePath("user://...")` for configuration and cache paths. Under Steam Proton, `user://` automatically resolves inside the Wine prefix (`compatdata/2868840/pfx/drive_c/users/steamuser/AppData/Roaming/SlayTheSpire2/`).
6. **Per-Patch Resilience & Game Version Compatibility**
   - Mod manifests declare `"min_game_version": "0.107.0"`. On older or mismatched game versions where internal types or methods may change, patches must be applied independently (never let one failing patch abort initialization). Each skipped patch must be logged individually, and a startup summary logged (`applied N, skipped M`). Run `./scripts/check_sts2_compat.sh` to statically verify all compile-time and dynamic string targets against any target `sts2.dll`.

---

## 4b. Working in parallel (agents)

- Use one **git worktree and branch per task** (`git worktree add ../<name> -b <branch> origin/main`). Never share a checkout between agents; check `git status` first and do not edit files you did not change.
- Add files by explicit path; never `git add -A`.
- `RoomOverlayFactory`, `ModEntry` and `Sts2RuntimeInspector` are large and shared by many features: keep edits small and rebase often.
- Game-facing code cannot be unit-tested. Put decisions (math, layout geometry, edge detection) in `*.Core` as pure functions and keep Godot code a thin adapter.
- Access game internals only through `SlayTheSpireMod.Common.ReflectionAccess` and register patches through `PatchRegistrar`; then run `./scripts/check_sts2_compat.sh`.

---

## 5. Reverse-Engineered Untapped.gg Companion (`v3.11.0-latest`) Reference

The executables in `executables/` (`untapped-companion_stable_amd64.deb`, `Untapped.gg Companion-stable-x86_64.AppImage`, `Untapped.gg Companion Stable Installer.exe`) were reverse-engineered to extract the full specification of Untapped.gg Companion's Slay the Spire 2 integration.

### 5.1 Why Untapped.gg Companion Fails on Modern Linux / Proton
1. **Electron + X11 Native Module (`untapped-node-native v2.6.0`)**:
   - Uses `lib/linuxX11.js` to query `_NET_CLIENT_LIST`, `WM_NAME = "Slay the Spire 2"`, and `WM_CLASS = "Godot_Engine"`.
   - Missing Linux implementations for `isWindowInForeground`, `setWindowAcceptsNonActivatingClicks`, `getWindowClientRect`, and `getWindowDpiScale`.
   - Under Proton, the window class is `steam_app_2868840` rather than `Godot_Engine`, and under Wayland/Gamescope X11 global coordinate positioning and click-through fail.
2. **Out-of-Process Memory Scraper (`untapped-scry v6.15.2`)**:
   - Connects via `process_vm_readv` / `ptrace` (`DotNetCoreScry` + `GodotScry`) with hardcoded version expectations (`STS2_GODOT_VERSION = "4.5.1"`, `STS2_DOTNET_VERSION = "9.0.2"`, `STS2_MODULE_NAME = "sts2"`).
   - Fails under Linux `yama/ptrace_scope` restrictions or when STS2 runs inside a Proton/Wine container.

### 5.2 Remote APIs & Default Tier List UUIDs
- **Static Game Data (`Sts2JsonManager`)**:
  - `https://sts2json.untapped.gg/data/{branch}/{version}/{lang}/sts_data.json` (e.g. `https://sts2json.untapped.gg/data/public/latest/eng/sts_data.json`)
  - Returns `{ version, cards: [...], relics: [...], potions: [...], keywords: [...] }`.
- **Tier List Resolution (`Sts2TierListManager`)**:
  - `https://api.sts2.untapped.gg/api/v1/tier-lists/{uuid}/resolve/?game_version={gameVersion}` (e.g. `game_version=public`)
  - Returns `{ game_version, tier_list: { name, author: { display_name }, updated, tiers: [{ name, order, color, cards: [{ card_id, order, commentary }], relics: [{ item_id, commentary }], potions: [{ item_id, commentary }] }] } }`.
- **Event Commentary**:
  - `https://sts2.untapped.gg/api/v1/commentary/events`
  - Returns `{ Baalorlord: { [eventId]: commentary } }`.
- **Default Author Tier List UUIDs**:
  - **Baalorlord**:
    - Ironclad: `004de170-026a-4dd4-a280-3b904be0b5d6`
    - Silent: `6d61ea21-0552-4c49-8bb5-a5c15530fc00`
    - Regent: `0e6c1e23-bec6-4887-a9e0-dbf49ede974d`
    - Necrobinder: `43d0b41f-7d6d-4ce9-928e-c1310a413983`
    - Defect: `5a512e04-4583-4a16-9271-d46864c6cb4c`
    - Colorless (includes Relics & Potions): `0a626c22-dc49-433e-ac6e-76cc9abf5684`
  - **Jorbs**:
    - Ironclad: `6b3390b7-bcbc-4ebc-ae78-ae9a400c66dc`
    - Silent: `2cd72d4b-eeb4-42ab-89ca-4ac682137672`
    - Regent: `c47dbfda-4983-4107-8c9c-c33d5ae32b23`
    - Necrobinder: `3ae661e6-6190-4a45-b00e-63e8400fff94`
    - Defect: `5f8d3e04-1f50-4b56-b51a-d8650b000ce3`
    - Colorless (includes Relics & Potions): `81684acf-26a7-4e44-a649-84f4c2824ec0`

### 5.3 In-Process `MegaCrit.Sts2.Core` Class Mapping
Every feature that `untapped-scry` scrapes from memory maps directly to `MegaCrit.Sts2.Core` classes in `sts2.dll`:

| Companion Feature | `sts2.dll` Classes & Members |
| :--- | :--- |
| **Card Badges & Commentary** | `MegaCrit.Sts2.Core.Nodes.Cards.NCard` (`Model.Id.Entry`, `Model.Title`, `_currentUpgradeLevel`, `Enchantment?.Id.Entry`), `NGridCardHolder`, `NPreviewCardHolder` |
| **Relic Badges & Commentary** | `MegaCrit.Sts2.Core.Nodes.Relics.NRelic` (`Model.Id.Entry`, `Model.Title`), `NMerchantRelic` |
| **Potion Badges & Commentary** | `MegaCrit.Sts2.Core.Nodes.Potions.NPotion` (`Model.Id.Entry`), `NMerchantPotion` |
| **Ancient / Event Choice Badges** | `MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton` (`Option.TextKey` last segment after `.`, `Option.Relic?.Id.Entry`, `Event.Id.Entry`), `NEventRoom`, `NAncientEventLayout` |
| **Merchant "Gold Left" & Shop Badges** | `MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom` (`Inventory` -> `_characterCardContainer`, `_colorlessCardContainer`, `_relicContainer`, `_potionContainer`, `_cardRemovalNode`), `NMerchantSlot` (`Entry.Cost`, `Entry.IsStocked`, `_isHovered`) |
| **Combat Attack Summary (Incoming Damage & Hits)** | `MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom` (and `NEventRoom.EmbeddedCombatRoom`) -> `_enemyContainer` -> `NCreature` (`Entity.CurrentHp > 0`, `IntentContainer._intents` -> `NIntent`: `_intent` `SingleAttackIntent`/`DeathBlowIntent`/`MultiAttackIntent`, `_valueLabel.Text`) |
| **Live Combat Deck Tracker** | `NCombatRoom` / `CombatManager` -> `Player.PlayerCombatState` (`DrawPile._cards`, `DiscardPile._cards`, `ExhaustPile._cards`, `Hand._cards`), `CardModel` (`Id.Entry`, `Type`, `EnergyCost.GetWithModifiers()`, `_currentUpgradeLevel`, `_deckVersion != null`) |
| **Event Room Commentary** | `MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom` (`_event.Id.Entry`) |
| **Run Context (Character, Act, Gold, HP)** | `MegaCrit.Sts2.Core.Runs.RunManager` (`State.Players`, `State.CurrentActIndex`, `State.CurrentRoomCount`), `LocalContext.NetId` |
