# Slay the Spire 2 Overlay & Map Fog (Linux & Proton)

A native, Linux-first in-game companion and utility suite for **Slay the Spire 2 (STS2)** built with **.NET 9.0**, **GodotSharp (4.5.1)**, and **Harmony (`0Harmony`)**.

Unlike external companion apps that rely on out-of-process Electron overlays, X11-only window tracking, and cross-process memory scraping (`ptrace`)—which break under Wayland, Gamescope, Steam Deck gaming mode, and Proton/Wine—these mods execute **directly inside the Slay the Spire 2 game process**. Because STS2 loads `.NET 9.0` assemblies identically on both Linux Native and Steam Proton builds, both mods function seamlessly across X11, Wayland, Gamescope/Steam Deck, and Proton without external windows or memory reading.

---

## Included Mods

This repository contains two distinct mods:

### 1. Overlay for Tier List
* **Steam Workshop**: [Overlay for Tier List (Item #3786603221)](https://steamcommunity.com/sharedfiles/filedetails/?id=3786603221)
* **Status**: Pure UI overlay, does not alter game rules or save files.
* **Key Features**:
  * **Card, Relic, Potion, and Ancient Choice Tier Badges**: Displays evaluations directly over items during card selection, rewards, shops, and event choices.
  * **Author Consensus Mode**: Supports Baalorlord and Jorbs tier lists, as well as a dual-author consensus mode displaying side-by-side grades (e.g. `B:S J:A`) and merged commentary.
  * **Deck Archetype & Keyword Synergy Highlighting**: Real-time synergy bar displayed during card rewards highlighting matching archetypes (Strength, Poison, Shivs, Frost, etc.) and keyword counts.
  * **Act Boss Preparation Banner**: Inspects the current Act boss and provides a counter-readiness score and strategy cues during card rewards and rest sites (with fog-aware masking).
  * **Shop Budget Planner ("Pin to Cart")**: Pin multiple items in the merchant shop using **F5** or middle-click to calculate total planned cost and gold surplus/deficit.
  * **Combat Attack Summary**: Displays incoming enemy attack totals, hit counts, and lethal damage warnings each turn.
  * **Live Combat Deck Tracker**: Real-time tracker for draw, discard, and exhaust piles with compact and expanded views.
  * **Event Room Commentary**: In-game strategy commentary panel for narrative events.
  * **Ergonomic Quick-Toggle Bar & Hotkeys**: On-screen toggle buttons and hotkeys with controller chord support.
  * **UI Scale Slider & Dock Anchoring**: Configurable UI scaling (0.75x to 1.5x) and top-left/top-right dock anchors.

### 2. Fog of War Map
* **Steam Workshop**: [Fog of War Map (Item #3821403222)](https://steamcommunity.com/sharedfiles/filedetails/?id=3821403222)
* **Status**: Gameplay mod (restricts map vision).
* **Key Features**:
  * **Act Map Fog of War**: Hides unexplored map nodes, revealing paths dynamically as your character advances.
  * **Configurable Vision Depth**: Choose to reveal 1 step ahead (default), 2 steps ahead, or customize sight range.
  * **Tunable Fog Strength**: Dark fog over unexplored map nodes with adjustable opacity (30% to 100%) so you can still admire the underlying map art or black it out entirely.
  * **Act Boss Visibility**: Keep the final Act Boss revealed by default while masking intermediate paths.
  * **In-Game Settings**: Configurable directly from the in-game Mods menu and pause screen.

### 3. Test Subject Vote
* **Status**: Co-op gameplay mod, not yet published to the Workshop (the workspace is `workshop-vote/`, set to public).
* **Every player needs it installed.** Without it on every client no vote is held and the fight plays as vanilla.
* **What it does**: In a multiplayer run, a Skill played against the Test Subject's first form is held before energy is spent. Everyone except the card player votes `Let it happen` or `Absolutely not`. If rejections strictly outnumber approvals the card goes back to the hand; a tie allows it. The vote waits for every voter (no timeout); if one gets stuck the host can press `F9` or click **Skip vote** to let the play through for everyone. An approved Skill resolves normally, including the vanilla Strength gain; the mod never changes the numbers.
* **Scope**: Skills only, first form only. Attacks, Powers, later forms and other enemies are untouched.

---

## Controls & Hotkeys

All hotkeys can be rebound in `overlay_config.json` or triggered via the on-screen **Quick Toggle Bar**.

| Action | Keyboard | Steam Deck / Gamepad | Description |
| :--- | :--- | :--- | :--- |
| **Cycle Tier Author** | `F6` | Via Quick Toggle Bar | Cycles between Baalorlord, Jorbs, and Consensus Both. |
| **Toggle Deck View** | `F7` | Via Quick Toggle Bar | Cycles deck tracker between Compact and Expanded view. |
| **Toggle Overlay Visibility** | `F8` | `L3` + `R3` (Thumbstick click chord) | Hides or restores all overlay badges and panels. |
| **Force Refresh Tiers** | `F9` | Via Quick Toggle Bar | Re-fetches the latest tier lists from remote APIs. |
| **Pin Shop Item** | `F5` or Middle-Click | Via Quick Toggle Bar | Adds/removes hovered shop item to the Cart Budget Planner. |

---

## Configuration & Storage Paths

Configurations and local caches use Godot's virtual `user://` filesystem. Under Steam Proton, `user://` automatically resolves inside the game's Wine prefix.

* **Linux Native**:
  * `~/.local/share/SlayTheSpire2/overlay_config.json`
  * `~/.local/share/SlayTheSpire2/map_fog_config.json`
  * `~/.local/share/SlayTheSpire2/tier_list_cache.json`
* **Steam Proton / Steam Deck**:
  * `~/.local/share/Steam/steamapps/compatdata/2868840/pfx/drive_c/users/steamuser/AppData/Roaming/SlayTheSpire2/overlay_config.json`
  * `~/.local/share/Steam/steamapps/compatdata/2868840/pfx/drive_c/users/steamuser/AppData/Roaming/SlayTheSpire2/map_fog_config.json`

---

## Steam Deck & Troubleshooting Guide

If a mod does not appear in-game or does not seem to load on Steam Deck or a secondary PC, check the following common causes:

### 1. Mod Enablement in the "Mods" Menu
Slay the Spire 2 maintains a per-mod enabled state. Even when a mod is subscribed to and downloaded, it may start disabled:
1. Launch **Slay the Spire 2**.
2. Select **Mods** on the main menu.
3. Verify that **Overlay for Tier List**, **Fog of War Map** and (if installed) **Test Subject Vote** have their toggles set to **Enabled** (green).
4. If a mod was disabled, the game logs `set to disabled in settings`.

### 2. Workshop Item Visibility
During testing and development, workshop items may have restricted visibility:
* **Overlay for Tier List** is currently **Private** (accessible to the owning Steam account).
* **Fog of War Map** is currently **Friends Only** (accessible to Steam friends of the author).
* If running on a second Steam Deck logged into a different account, the mod will not download from the Workshop until the account is granted access.

### 3. Local Mods vs. Workshop Conflicts
If you manually placed mod files into the game's local `mods/` folder, Slay the Spire 2 detects the duplicate mod ID and disables the Workshop version:
* Look for `loaded both via Steam and local mods directory` in the game logs.
* Remove the local copies from the game directory's `mods/` folder to let the Workshop copy load cleanly.

### 4. Inspecting Game Logs
Check the game's startup log for diagnostic messages:
* **Native Linux**: `~/.local/share/SlayTheSpire2/logs/godot.log`
* **Proton / Steam Deck**: `~/.local/share/Steam/steamapps/compatdata/2868840/pfx/drive_c/users/steamuser/AppData/Roaming/SlayTheSpire2/logs/godot.log`

Search the log file for:
```bash
grep -E "\[STS2 Overlay\]|\[STS2 MapFog\]|\[STS2 TestSubjectVote\]|3786603221|3821403222|disabled in settings" godot.log
```
Expected healthy startup lines:
```text
[STS2 Overlay] ModEntry.Initialize called!
[STS2 Overlay] Harmony initialization complete: 35 applied, 0 skipped
[STS2 MapFog] ModEntry.Initialize called!
[STS2 MapFog] Harmony initialization complete: 8 applied, 0 skipped
[STS2 TestSubjectVote] patches: applied 2, skipped 0
```

---

## Development & Building

On Fedora Atomic / Silverblue / Bazzite hosts, the .NET SDK runs inside the `dotnet-dev` Distrobox container:

### Run Unit & Architecture Tests
```bash
distrobox enter dotnet-dev -- dotnet test SlayTheSpireOverlay.slnx
```

### Build & Publish Mod Assemblies
```bash
distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet publish src/SlayTheSpireOverlay.Godot/SlayTheSpireOverlay.Godot.csproj -c Release
distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet publish src/SlayTheSpireMapFog.Godot/SlayTheSpireMapFog.Godot.csproj -c Release
```

### Static Game-Version Compatibility Checker
Verifies that all compile-time types and dynamic Harmony patch targets exist in a given `sts2.dll` to prevent silent breakages when the game updates:
```bash
./scripts/check_sts2_compat.sh [path-to-sts2.dll]
```

### CI Build & Offline Compilation
Because `sts2.dll` is proprietary game data, GitHub Actions CI uses the reference stub project `tools/Sts2Stub/` to produce type signatures without game code:
```bash
dotnet build tools/Sts2Stub/Sts2Stub.csproj -c Release -o stubs/
dotnet test SlayTheSpireOverlay.slnx -p:Sts2DataDir=$PWD/stubs
```

---

## License

This project is licensed under the [MIT License](LICENSE).
Slay the Spire 2 is a trademark of Mega Crit Games. This project is an independent companion mod and is not affiliated with or endorsed by Mega Crit Games or Untapped.gg.
