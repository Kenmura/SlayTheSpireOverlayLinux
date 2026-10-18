# Patch Notes & Changelog - Overlay for Tier List

All notable changes, fixes, and feature additions for the **Overlay for Tier List** mod in Slay the Spire 2.

---

## [Unreleased]

### 🆕 New Overlays
* **Map route path helper:** The map screen shows a compact summary of reachable encounter nodes (`💀 Elites`, `⛺ Rest`, `💰 Shops`, `? Unknown`) across travelable DAG branches. Strictly respects Fog of War when the Fog mod is active without leaking fogged nodes. Toggle with `EnableMapPathSummary`.
* **Potion drop chance:** The reward screen shows `🧪 Potion Drop Chance: X%`, the odds for your next normal combat read straight from the game. Toggle with `EnablePotionDropChance` or **Potion Chance** in the in-game settings.
* **Relic proc counters:** Pen Nib, Happy Flower, Kunai, Shuriken and Stone Calendar show a small counter (`[9/10]`, `[T:5/6]`) in the top bar, highlighted gold one tick before the proc. Toggle with `EnableRelicProcCounters` or **Relic Counters** in the in-game settings.

---

## [v1.1.0] - 2026-10-17

### ⚙️ In-Game Settings
* **Settings panel in the Mods screen:** Select the mod in the Mods list (main menu, or Settings during a run) to toggle features without editing files. Changes save to `overlay_config.json` and apply immediately.
* **Tier list author switch:** Choose which tier list author's ratings are shown, with a one-click **Force Refresh** to re-download tier data.
* **Per-feature toggles:** Master overlay, card badges, relic badges, potion badges, hover commentary, attack summary, deck tracker, merchant gold-left, and event commentary.

### 🆕 New Overlays
* **Potion badges** on potions in rewards, shops, and events.
* **Hover commentary** on cards, relics, and potions.
* **Combat attack summary:** Total incoming damage and hit count from enemy intents.
* **Live combat deck tracker:** Draw, discard, and exhaust pile contents during combat.
* **Merchant "gold left":** Shows remaining gold after shop purchases.
* **Event commentary** panel in event rooms.
* **Reward screen badges** on card/relic reward buttons.

### 🔧 Fixes & Maintenance
* Tier data refreshed from the latest ratings.
* Removed `deps.json` from the Workshop package (the game logged it as an invalid mod manifest on 0.107).
* Workshop package trimmed to the mod DLL only (no longer bundles redundant GodotSharp / dependency DLLs).
* Compatible alongside the Fog of War Map mod (separate settings panels, shared in-run Mods screen).

---

## [v1.0.0] - 2026-08-19

### 🎨 Visual & Branding Improvements
* **Unaffiliated & Generic Mod Title:** Renamed mod manifest `name` and Workshop `title` to **`Overlay for Tier List`**.
* **Cleaned Tooltip & Commentary Text:** Removed all external site and author branding (`Baalorlord`, `Untapped.gg`) from item commentary tooltips, manifest files, and log outputs for a clean, generic in-game presentation.
* **Glassmorphic Single-Line Pill Design:** Re-architected badge overlays into compact horizontal pills (`[ Tier | Score ]`) featuring dark glassmorphism backgrounds (`rgba(8, 9, 12, 0.92)`), rounded corners, and subtle white borders.
* **Top-Right Header Positioning:** Positioned card badges at `Vector2(70, -145)` to avoid obscuring card artwork, attack/defense banners, or description text.

---

### 🏛️ Ancient Relics & Choice Options Support
* **Ancient Choice Button Overlay:** Added Harmony postfix patches on `MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton._Ready` and `set_Option` to render rating badges directly on choice buttons in Ancient rooms (Neow, Act 2 Ancient, Act 3 Ancient).
* **Multi-Layer Model Resolution:** Implemented fallback model extraction in `NEventOptionButtonReadyPatch` to retrieve IDs from `EventOption.Relic`, `CardHoverTip.Card`, `HoverTip.CanonicalModel`, and `EventOption.TextKey`.
* **Option Pill Positioning:** Created `RelicBadgeFactory.CreateOrUpdateOptionBadge` to anchor rating pills to the right edge of Ancient choice buttons.

---

### 🗿 Relic Overlay System (`NRelic`)
* **Universal Relic Support:** Added Harmony postfix patches on `MegaCrit.Sts2.Core.Nodes.Relics.NRelic._Ready` and `set_Model` to cover top bar inventory relics, shop relic slots, and chest reward screens.
* **802 Database Entries:** Expanded internal evaluations database from 513 to 802 entries, adding complete ratings for all 299 Slay the Spire 2 relics.

---

### 🏷️ Custom Text & Non-Numeric Tier Ratings
* **Text-Only Evaluation Pills:** Supported non-numeric tier evaluations (e.g. `Map Dependent`, `Inconsistent`, `Always Amazing`, `Always Good`, `Needs Synergy`, `Almost Never`).
* **Conditional Score Hiding:** Automatically hides numeric score values and the `|` divider whenever `Score <= 0`, displaying custom tier text cleanly.
* **Tier Color Palette:**
  * `S Tier` / `S` / `Always Amazing` $\rightarrow$ Crimson Red (`#FF5959`)
  * `A Tier` / `A` / `Always Good` $\rightarrow$ Bright Amber (`#FF9E40`)
  * `B Tier` / `B` / `Great` / `Good` $\rightarrow$ Golden Yellow (`#F2F24D`)
  * `C Tier` / `C` / `Needs Synergy` $\rightarrow$ Sky Cyan (`#73D9FF`)
  * `D Tier` / `D` / `Inconsistent` $\rightarrow$ Soft Purple (`#D980FF`)
  * `Map Dependent` / `Situational` $\rightarrow$ Warm Amber (`#FFB34D`)
  * `Almost Never` / `Never Take` / `Skip` $\rightarrow$ Muted Grey (`#A6A6A6`)

---

### 🤖 Standalone Automated Scraper & Diff Engine
* **Automated Scraper Script ([`scripts/update_tiers.py`](file:///var/home/nickmarc/repos/SlayTheSpireOverlayLinux/scripts/update_tiers.py)):** Built a standalone Python automation tool that scrapes tier list data from Untapped.gg without browser dependencies.
* **Diff Detection Engine:** Compares newly scraped data against current JSON database and logs human-readable summary of modified or added ratings. Exits early without rebuilding if no changes exist.
* **Deterministic Alphabetical Sorting:** Enforces `sort_keys=True` so `baalorlord_tiers.json` is always stored in alphabetical order, preventing false git diffs.
* **Shortcut Runner ([`update_tiers.sh`](file:///var/home/nickmarc/repos/SlayTheSpireOverlayLinux/update_tiers.sh)):** One-line bash runner for manual updates or daily cron jobs (`0 9 * * *`).

---

### 🐛 Bug Fixes & Technical Refactoring
* **Fixed Godot `ERROR: Invalid Task ID`:** Solved Godot `ScriptManagerBridge` crash caused by C# subclassing of `Godot.Control`/`Godot.Node` in external mod DLLs. Replaced subclassing with factories (`TierBadgeFactory`, `RelicBadgeFactory`) that directly instantiate built-in C++ Godot controls. Converted `ModEntry` to a pure static class.
* **Fixed Node Pooling / Rating Mismatches:** Fixed recycled node rating shifts by patching `set_Model` and `set_Option` setters across `NCard`, `NRelic`, and `NEventOptionButton`.
* **Fixed Steam Proton Disk Cache Stale Data:** Discovered Proton stores `user://` at `AppData/Roaming/SlayTheSpire2/`. Updated `ModEntry.cs` to automatically update the disk cache on launch whenever the embedded resource has newer/more items.
* **CamelCase ID Normalizer:** Added CamelCase to `UPPER_SNAKE_CASE` converter and `_OPTION` suffix stripper in ID normalization logic.

---

# Patch Notes & Changelog - Fog of War Map

## [v1.1.0] - 2026-10-18

* **Fog Strength setting:** Tunable dark-fog opacity, cycled from the Mods screen settings panel.

## [v1.0.0] - 2026-10-17

* **Fog of War on the Act Map:** Only the next step ahead is revealed by default; the Act Boss stays visible.
* **Passed nodes stay exposed:** Every node on your current floor or below remains visible so you can see the paths you didn't take (toggleable).
* **Dark fog:** Unexplored map area is covered by a dark fog instead of a bare background (toggleable).
* **In-game settings** (Mods screen): Fog on/off, Full Fog vs Hidden Icons (`?`) mode, Act Boss visibility, vision depth (1-3 steps), Passed Nodes, Dark Fog. Saved to `map_fog_config.json`.
* Marked as affecting gameplay (reduced map information).
* Removed `deps.json` from the Workshop package (the game logged it as an invalid mod manifest on 0.107).
