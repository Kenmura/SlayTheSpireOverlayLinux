# Product Catalog & Prioritized Feature Backlog (`FUTURE_ENHANCEMENTS.md`)

## 1. Product Vision & User Personas

**SlayTheSpireOverlayLinux** brings a native, zero-configuration, in-process companion and overlay experience to **Slay the Spire 2 (STS2)** on Linux, SteamOS / Steam Deck (Gamescope), Wayland, X11, and Windows/Proton. By running directly inside the STS2 `.NET 9.0` / Godot 4.5.1 process via Harmony patches rather than relying on external X11 windows or cross-process `ptrace` memory scraping, it works reliably across every display server and container boundary.

### Target User Personas
1. **The Climbing Strategist (Core Persona)**: Wants fast, accurate card/relic/potion evaluations, draw probabilities, and incoming damage math without alt-tabbing to spreadsheets or streams.
2. **The Steam Deck / Handheld / Controller Player**: Plays on a 1280×800 screen in Gamescope using a gamepad or touchscreen; cannot easily press function keys (`F6`–`F9`) or hover a mouse cursor over tiny badges.
3. **The Deckbuilder & Synergy Seeker**: Understands base card tiers, but wants context-aware insights (upgraded card evaluations, deck archetypes/tag counts, boss/act preparation, and post-run analytics).
4. **The Co-op / Multiplayer Tactician**: Plays STS2's new co-op mode and needs visibility into team block/incoming damage, shared shop economy, and coordinated deck tracking.
5. **The Custom / Challenge Runner**: Uses companion mods like `SlayTheSpireMapFog` for custom challenge runs and wants seamless configuration and zero UI conflicts.

---

## 2. Catalog of Existing Features

Before prioritizing new work, the following table catalogs all capabilities currently shipped in the repository across `SlayTheSpireOverlay` and `SlayTheSpireMapFog`.

### 2.1 Tier Badges & Expert Evaluations
| Feature | Status | Implementation Reference | Description |
| :--- | :---: | :--- | :--- |
| **Card Tier Badges & Scores** | Shipped | `TierBadgeFactory`, `NCardReadyPatch`, `NCardSetModelPatch` | Attaches color-coded tier (`S`–`F` / situational) and positional sub-score (`0.0`–`100.0`) badges to `NCard` nodes across rewards, shops, grids, and previews. Automatically suppresses badges on cards in the active combat hand/play queue (`IsCardInCombatHand`). |
| **Relic Tier Badges** | Shipped | `RelicBadgeFactory`, `NRelicReadyPatch`, `NRelicSetModelPatch` | Displays tier and score badges on `NRelic` nodes in treasure rooms, shops, and relic selection screens while suppressing badges in the persistent top bar (`IsItemInTopBar`). |
| **Potion Tier Badges** | Shipped | `RelicBadgeFactory.CreateOrUpdatePotionBadge`, `ModEntry.OnPotionReady` | Attaches tier badges to `NPotion` nodes in combat rewards and merchant slots while suppressing top-bar potion inventory slots. |
| **Combat Reward Button Badges** | Shipped | `ModEntry.OnRewardButtonReady`, `Sts2RuntimeInspector.ExtractRewardItemId` | Inspects `NRewardButton` (`RelicReward`, `PotionReward`, `SpecialCardReward`) and renders inline tier badges directly on the post-combat loot list before opening the reward. |
| **Event & Ancient Choice Badges** | Shipped | `NEventOptionButtonReadyPatch`, `OverlayCalculators.ExtractEventOptionId` | Extracts relic IDs, card hover-tip IDs, or localization `TextKey` suffixes from `NEventOptionButton` to rate Ancient blessings and event choices inline. |
| **Hover & Focus Commentary Popups** | Shipped | `RoomOverlayFactory.AttachHoverCommentaryPopup`, `PatchHolderFocusIfPresent` | Displays author commentary popups on mouse hover or controller/keyboard focus (`OnFocus`/`OnUnfocus` across `NCardHolder`, `NMerchantSlot`, `NEventOptionButton`, `NRelicBasicHolder`, `NTreasureRoomRelicHolder`, `NRewardButton`) with automatic viewport edge clamping. |

### 2.2 Live Combat, Shop & Event HUD Overlays
| Feature | Status | Implementation Reference | Description |
| :--- | :---: | :--- | :--- |
| **Incoming Attack Summary & Lethal Warning** | Shipped | `OverlayCalculators.CalculateIncomingAttacks`, `RoomOverlayFactory.UpdateAttackSummary` | Aggregates single-hit and multi-hit (`DxN`) enemy attack intents, subtracts current player block, displays net unblocked damage, and highlights `⚠ LETHAL` turns in red or full block in green. |
| **Live Combat Deck Tracker & Hypergeometric Odds** | Shipped | `OverlayCalculators.CalculateDeckTracker`, `RoomOverlayFactory.UpdateDeckTracker` | Groups remaining cards in Draw/Discard/Hand by category (`Attack`, `Skill`, `Power`, `Status/Curse`), displays category draw percentages, per-card next-hand (5-card) hypergeometric draw odds, effective turn energy cost (`GetWithModifiers`), upgrade levels, reshuffle indicator (`↻`), and pile counts. |
| **Context-Aware Combat HUD Auto-Hide** | Shipped | `Sts2RuntimeInspector.IsCombatHudObscured` | Automatically hides the Combat Deck Tracker and Attack Summary when combat ends, when the Map screen is open, or when an overlay screen (Deck View, Card Reward, Grid Select) obscures the battlefield—while re-showing immediately if the player holds the **Peek** button. |
| **Merchant "Gold Left After Purchase"** | Shipped | `OverlayCalculators.CalculateGoldLeft`, `RoomOverlayFactory.UpdateMerchantGoldLeft` | Polls hovered shop items (cards, relics, potions, and card removal service) and displays remaining gold (`Gold Left: Xg (-Yg)`) or gold deficit (`Need Xg more`). |
| **Event Room Strategy Commentary** | Shipped | `RoomOverlayFactory.AttachEventCommentary`, `HttpTierListProvider.ParseEventCommentary` | Displays high-level event strategy notes from Untapped.gg's event commentary API (`EVENT_{id}`) in `NEventRoom`, automatically hiding if an `EmbeddedCombatRoom` starts inside the event. |

### 2.3 Data Pipeline, Caching & Configuration UX
| Feature | Status | Implementation Reference | Description |
| :--- | :---: | :--- | :--- |
| **Multi-Author Live Sync (`Baalorlord` & `Jorbs`)** | Shipped | `HttpTierListProvider` | Resolves live Untapped.gg STS2 tier lists across all 5 characters (Ironclad, Silent, Regent, Necrobinder, Defect) plus Colorless/Relics/Potions and Event Commentary, or loads from a custom JSON URL. |
| **Three-Tier Offline Resilience** | Shipped | `ModEntry.Initialize`, `LocalCacheManager` | 1) Embedded `baalorlord_tiers.json` manifest resource seeded on first boot; 2) Atomic `user://tier_list_cache*.json` disk cache with configurable TTL (24h default); 3) Stale-cache fallback if offline or API request fails. |
| **In-Game Mod Settings GUI (Main Menu & Mid-Run)** | Shipped | `RoomOverlayFactory.AttachOrUpdateModSettingsPanel`, `EnableModdingButtonInRun` | Injects an interactive 2-column toggle grid into `NModInfoContainer` and unlocks the Settings -> Modding screen mid-run via `NRunSubmenuStack` so players can toggle any feature without editing JSON files. |
| **Global Hotkeys & Toast Notifications** | Shipped | `RoomOverlayFactory.PollHotkeys`, `ShowAuthorToast` | Supports `F6` (switch Baalorlord/Jorbs), `F7` (collapse/expand deck tracker), `F8` (show/hide all overlays), and `F9` (force-refresh live tier lists) with non-intrusive top-right toast feedback. |
| **Companion Mod: Map Fog of War (`SlayTheSpireMapFog`)** | Shipped | `MapFogCalculator`, `Sts2MapInspector`, `MapFogSettingsFactory` | Standalone mod in the same solution that limits map visibility to configurable BFS `VisionDepth` (1–4 steps), supports `HiddenIcons` (`?` masking) vs `FullFog` (hidden nodes/edges), optional Boss visibility, and in-game settings. |

---

## 3. Prioritization Framework

To ensure our engineering effort delivers maximum value to players while respecting STS2's engine constraints (no `Godot.Node` subclassing, strict thread-safe `CallDeferred` mutations, and zero `GodotSharp` dependencies in `Core`), backlog items are prioritized into four tiers based on:

1. **Player Impact & Frequency**: How often a player encounters the problem during a standard 45-minute run.
2. **Platform Reach (Linux / Steam Deck / Proton)**: How well the feature serves handheld, controller, and high-DPI/ultrawide Linux setups.
3. **Decision-Making Value**: How much cognitive load or manual arithmetic the feature saves at critical decision points (Card Rewards, Combat Turns, Boss Prep, Shops).
4. **Implementation Feasibility**: Clean mapping to `MegaCrit.Sts2.Core` runtime objects and unit-testability in `SlayTheSpireOverlay.Core`.

---

## 4. Prioritized Feature Backlog & Acceptance Criteria

### Priority 0 (P0): Critical Usability, Handheld/Steam Deck Ergonomics & Combat Accuracy
*Goal: Eliminate friction for Steam Deck/controller users, prevent UI overlap on non-1080p screens, and close accuracy gaps in combat math and card evaluations.*

#### P0-1. Steam Deck / Controller Ergonomics & On-Screen Quick Toggle Bar ([Issue #1](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/1))
- **User Story**: As a Steam Deck / handheld / controller player in Gamescope or Big Picture Mode, I want an on-screen clickable/focusable quick-action bar and configurable controller/keyboard bindings so that I can collapse/expand the Deck Tracker (`F7`), switch Tier List Author (`F6`), toggle Overlay Visibility (`F8`), and refresh tiers (`F9`) without needing a physical keyboard with function keys.
- **Target Components**: `OverlayConfig`, `RoomOverlayFactory`, `ModEntry`.
- **Complexity**: Low–Medium.
- **Acceptance Criteria**:
  - [ ] **AC1 (Interactive Deck Tracker Header)**: Clicking or tapping the `[F7 -]` / `[F7 +]` header region on `DeckTrackerPanel` toggles `IsDeckTrackerCollapsed` and immediately updates the panel layout.
  - [ ] **AC2 (Quick Toggle Pill)**: When `EnableQuickToggleBar` is enabled, a compact, non-intrusive quick-toggle control allows switching Tier List Author (`Baalorlord` / `Jorbs`) and hiding/showing the overlay via mouse click or touchscreen tap.
  - [ ] **AC3 (Configurable Keybindings)**: Users can override the default `F6`/`F7`/`F8`/`F9` keys in `overlay_config.json`, and invalid key names gracefully fall back to the defaults (`F6`–`F9`).
  - [ ] **AC4 (Controller Chord Support)**: Holding the configured controller modifier (e.g., `Back`/`Select`) + D-Pad Up/Down/Left/Right triggers the 4 overlay actions with single-press debouncing and displays the confirmation toast via `ShowAuthorToast`.
  - [ ] **AC5 (In-Game Settings Toggle)**: The In-Game Mod Settings panel (`OverlayModSettingsPanel`) includes a toggle for `Quick Toggle Bar: ON/OFF` that persists to `user://overlay_config.json`.
  - [ ] **AC6 (Unit & Architecture Tests)**: All configuration defaults and key parsing logic in `SlayTheSpireOverlay.Core` are covered by unit tests, and `DependencyTests` pass with 0 violations.

#### P0-2. Upgraded Card (`+`) & Enchantment-Aware Tier Evaluations ([Issue #2](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/2))
- **User Story**: As a Climbing Strategist evaluating upgraded card rewards, shop cards, or Rest Site Smithing choices, I want tier badges and hover commentary to recognize when a card is upgraded (`+`) or enchanted and highlight upgrade-specific commentary so that I don't undervalue cards whose upgraded form is significantly stronger than their base form.
- **Target Components**: `Sts2RuntimeInspector`, `TierBadgeFactory`, `OverlayCalculators`.
- **Complexity**: Medium.
- **Acceptance Criteria**:
  - [ ] **AC1 (Upgrade Detection on NCard)**: `Sts2RuntimeInspector` accurately extracts `_currentUpgradeLevel` (`int`) and `Enchantment?.Id.Entry` (`string?`) from `NCard.Model` without throwing if fields are null or absent.
  - [ ] **AC2 (Live Upgrade Preview Toggle)**: Toggling "Preview Upgrade" on card reward, deck view, or Rest Site Smithing screens immediately updates the attached `TierBadgePanel` and its `HoverText` metadata via the `NCard.UpdateVisuals` patch.
  - [ ] **AC3 (Tier Lookup with Upgrade Fallback)**: If a tier list dictionary contains an explicit upgraded key (`{CARD_ID}+`), `TierBadgeFactory` uses that entry; otherwise it falls back to `{CARD_ID}` and annotates the badge header with upgrade status.
  - [ ] **AC4 (Upgrade Commentary Callout)**: When `upgradeLevel > 0` and the card's commentary mentions upgrade keywords (`upgrade`, `upgraded`, `smith`, `+`), `FormatHoverCommentary` surfaces or marks the upgrade insight clearly in the hover card.
  - [ ] **AC5 (Unit Tests)**: xUnit tests in `SlayTheSpireOverlay.Tests` verify upgrade key fallback, commentary sentence extraction, and enchantment label formatting across edge cases.

#### P0-3. Dynamic Draw Count & Relic/Power-Adjusted Hypergeometric Odds ([Issue #3](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/3))
- **User Story**: As a player using the Live Combat Deck Tracker during combat, I want next-hand draw probabilities (`NextHandPercent`) to reflect my actual next-turn card draw count (accounting for relics, powers, buffs, and debuffs) so that the displayed hypergeometric percentages are mathematically accurate when I draw more or fewer than 5 cards per turn.
- **Target Components**: `Sts2RuntimeInspector.InspectDeckTracker`, `OverlayCalculators.CalculateDeckTracker`, `RoomOverlayFactory.UpdateDeckTracker`.
- **Complexity**: Low.
- **Acceptance Criteria**:
  - [ ] **AC1 (Parameterized Sample Size in Core)**: `OverlayCalculators.CalculateDeckTracker` accepts `int nextHandSampleSize = 5` (clamped to `>= 0`) and computes `NextHandPercent` for both direct draw (`inDraw > 0`) and reshuffle draw (`drawTotal < nextHandSampleSize && discardTotal > 0`) using `nextHandSampleSize`.
  - [ ] **AC2 (Reshuffle Indicator Accuracy)**: `RoomOverlayFactory.UpdateDeckTracker` displays the reshuffle symbol (`↻`) whenever `state.DrawCount < state.NextHandSampleSize && state.DiscardCount > 0`.
  - [ ] **AC3 (Runtime Draw Modifier Inspection)**: `Sts2RuntimeInspector.InspectDeckTracker()` reads base draw count and active draw-modifying powers/relics on the local player, falling back to `5` on reflection failure.
  - [ ] **AC4 (Unit Tests)**: Unit tests verify `CalculateDeckTracker` with `nextHandSampleSize` values of `0`, `3`, `5`, `7`, and `10`, including partial-draw reshuffle math when `drawTotal < nextHandSampleSize`.

#### P0-4. End-of-Turn Passive Damage & Block Accounting in Attack Summary ([Issue #4](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/4))
- **User Story**: As a player calculating survival during complex combat turns, I want the Incoming Attack Summary to account for guaranteed end-of-turn passive block, end-of-turn self-damage (e.g., Burns/Curses in hand), and enemies dying to DoT before attacking so that the `Unblocked` damage and `⚠ LETHAL` warnings reflect the true net HP loss I will take when pressing End Turn.
- **Target Components**: `OverlayCalculators.CalculateIncomingAttacks`, `Sts2RuntimeInspector.InspectCombatAttacks`, `RoomOverlayFactory.UpdateAttackSummary`.
- **Complexity**: Medium.
- **Acceptance Criteria**:
  - [ ] **AC1 (Doomed Enemy Exclusion)**: Enemies whose `WillDieBeforeAttack` is true (or `CurrentHp <= startOfEnemyTurnDot`) are excluded from incoming attack damage and hit counts.
  - [ ] **AC2 (Passive End-of-Turn Block)**: Guaranteed end-of-turn block (`passiveEndTurnBlock`) is added to `playerBlock` when computing `UnblockedDamage` and `IsLethal`.
  - [ ] **AC3 (End-of-Turn Self-Damage)**: Guaranteed end-of-turn self-damage from unplayed status/curse cards in hand or debuffs is included in net HP loss and lethal detection (`IsLethal`).
  - [ ] **AC4 (UI Breakdown Clarity)**: When `PassiveBlock > 0` or `SelfDamage > 0`, `AttackSummaryPanel` clearly annotates the adjustment (e.g., `Unblocked: 4 DMG (+6 EOT Block)`).
  - [ ] **AC5 (Unit Tests)**: Comprehensive unit tests in `CompanionFeaturesTests` verify lethal and non-lethal scenarios combining multi-hit attacks, passive block, hand status self-damage, and DoT-doomed enemies.

#### P0-5. UI Scale Slider & Configurable Panel Anchors / Positions ([Issue #5](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/5))
- **User Story**: As a player on a Steam Deck (1280×800), 4K monitor (3840×2160), or 21:9 ultrawide display, I want to adjust the overlay UI scale (`0.8x`–`1.5x`) and reposition or anchor the HUD panels (`DeckTrackerPanel`, `AttackSummaryPanel`, `EventCommentaryPanel`) so that text is legible on handheld screens and panels never overlap native game elements or sit too far in the periphery on ultrawide monitors.
- **Target Components**: `OverlayConfig`, `RoomOverlayFactory`, `TierBadgeFactory`, `RelicBadgeFactory`.
- **Complexity**: Medium.
- **Acceptance Criteria**:
  - [ ] **AC1 (Configurable UI Scale)**: `OverlayConfig.UiScale` defaults to `1.0f`, clamps safely between `0.75f` and `1.50f`, and scales font sizes and panel dimensions across `DeckTrackerPanel`, `AttackSummaryPanel`, `EventCommentaryPanel`, `MerchantGoldLeftPanel`, and hover popups.
  - [ ] **AC2 (In-Game Settings Control)**: `OverlayModSettingsPanel` includes a `UI Scale` button that cycles through preset scale steps (`85%`, `100%`, `115%`, `130%`, `150%`), saves to `overlay_config.json`, and immediately refreshes active scene panels.
  - [ ] **AC3 (Custom Panel Offsets & Viewport Clamping)**: Custom panel offsets configured in `overlay_config.json` are respected and clamped so panels never render off-screen at 1280×800, 1920×1080, 2560×1440, 3440×1440, or 3840×2160.
  - [ ] **AC4 (Unit Tests)**: Unit tests verify scale clamping and viewport-safe coordinate calculations across standard, handheld (16:10), and ultrawide (21:9) aspect ratios.

---

### Priority 1 (P1): Context-Aware Deckbuilding & Decision Support
*Goal: Help players make smarter strategic decisions beyond static vacuum tier lists by analyzing their current deck, act boss, and shop combos.*

#### P1-1. Deck Composition & Synergy Tag Analyzer on Card Reward Screens ([Issue #6](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/6))
- **User Story**: As a Deckbuilder & Synergy Seeker choosing between cards on a Card Reward screen or in a Shop, I want to see a compact summary of my current deck's size, Attack/Skill/Power distribution, average energy curve, and top mechanic/synergy tags (e.g., Block, Vulnerable, Weak, Exhaust, Poison, Shiv, Summon/Osty, Stars, Orbs) so that I can evaluate "Needs Synergy" (`C Tier`) cards in the context of my actual run rather than in a vacuum.
- **Target Components**: `SlayTheSpireOverlay.Core.Services.DeckSynergyAnalyzer` (new), `Sts2RuntimeInspector`, `RoomOverlayFactory`.
- **Complexity**: Medium.
- **Acceptance Criteria**:
  - [ ] **AC1 (Pure Core Synergy Calculator)**: `DeckSynergyAnalyzer` accurately computes card type counts, average energy cost, and mechanic tag counts from deck card models with zero `GodotSharp` or `sts2.dll` dependencies.
  - [ ] **AC2 (Reward & Shop Screen Display)**: Opening a Card Reward screen or Merchant Room displays `DeckSynergyPanel` showing total cards, `Attack`/`Skill`/`Power` breakdown, average cost, and the top 3–5 mechanic tags present in the player's master deck.
  - [ ] **AC3 (Dynamic Refresh on Deck Change)**: Selecting a card reward or purchasing/removing a card in the shop updates the synergy summary immediately.
  - [ ] **AC4 (Visibility & Settings Integration)**: `DeckSynergyPanel` respects `F8` (Overlay Hidden) and can be toggled on/off via `EnableDeckSynergyBar` in `OverlayModSettingsPanel`.
  - [ ] **AC5 (Unit Tests)**: xUnit tests verify `DeckSynergyAnalyzer` across Ironclad, Silent, Regent, Necrobinder, and Defect sample decks.

#### P1-2. Act Boss & Elite Preparation Banner ([Issue #7](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/7))
- **User Story**: As a Climbing Strategist drafting cards or choosing between Rest/Smith at a Rest Site, I want a subtle reminder of the current Act Boss and a 1-line strategic preparation tip on Card Reward and Rest Site screens so that I don't forget which Act Boss I am preparing for without having to open and scroll the Map screen every floor.
- **Target Components**: `Sts2RuntimeInspector`, `RoomOverlayFactory`, `OverlayConfig`.
- **Complexity**: Low–Medium.
- **Acceptance Criteria**:
  - [ ] **AC1 (Boss Encounter Inspection)**: `Sts2RuntimeInspector` safely extracts the active Act Boss ID and localized title from `RunManager.Instance.State` (returning `null` gracefully if unavailable).
  - [ ] **AC2 (Reward & Rest Site Banner)**: When `EnableBossPrepBanner` is enabled, Card Reward and `NRestSiteRoom` screens display a compact banner showing `Act Boss: {BossName}` and a concise strategic tip if available.
  - [ ] **AC3 (Map Fog Compatibility)**: If `SlayTheSpireMapFog` is active and configured with `ShowBoss = false` while the boss node is still unrevealed in fog, the banner respects fog-of-war and masks the boss identity as `Act Boss: ??? (Fogged)`.
  - [ ] **AC4 (Settings & Hotkey Support)**: Respects `F8` global visibility toggle and includes an `Act Boss Reminder: ON/OFF` button in `OverlayModSettingsPanel`.
  - [ ] **AC5 (Unit Tests)**: Unit tests cover boss ID normalization, tip lookup, and fog-masked state formatting.

#### P1-3. Multi-Item Shop Budget Planner ("Pin to Cart") ([Issue #8](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/8))
- **User Story**: As a player shopping in `NMerchantRoom`, I want to pin/mark multiple shop items (cards, relics, potions, card removal) to a virtual "shopping cart" and see the combined cost and remaining gold so that I can plan multi-item purchase combinations without doing mental subtraction across 3–4 prices.
- **Target Components**: `OverlayCalculators.CalculateGoldLeft`, `Sts2RuntimeInspector.InspectMerchantGoldLeft`, `RoomOverlayFactory`.
- **Complexity**: Medium.
- **Acceptance Criteria**:
  - [ ] **AC1 (Multi-Slot Budget Math in Core)**: `OverlayCalculators` accurately calculates single-hover gold left, pinned-cart total cost/gold left, and combined (pinned + hovered) gold left, ignoring unstocked (`IsStocked == false`) slots.
  - [ ] **AC2 (Slot Pin Toggle & Visual Indicator)**: Players can toggle the pinned state on any stocked shop item (including Card Removal), displaying a subtle cart/pin badge on pinned slots.
  - [ ] **AC3 (Auto-Clear on Purchase)**: Purchasing a pinned item (`IsStocked` transitions to `false`) automatically removes it from the pinned budget calculation so remaining gold is never double-counted.
  - [ ] **AC4 (Updated Shop Overlay Panel)**: `MerchantGoldLeftPanel` displays the pinned cart summary alongside the hovered item cost, coloring affordable totals in gold/green and unaffordable totals (`RemainingGold < 0`) in red with the exact gold deficit (`Need Xg more`).
  - [ ] **AC5 (Unit Tests)**: Unit tests in `CompanionFeaturesTests` verify empty cart, single hover, multi-pinned affordable, multi-pinned deficit, and purchased-item auto-exclusion cases.

#### P1-4. Side-by-Side Author Consensus Mode (`Baalorlord + Jorbs`) ([Issue #9](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/9))
- **User Story**: As a player who values multiple high-level perspectives, I want an optional `"Both"` (Consensus) author mode that displays both Baalorlord's and Jorbs's tier ratings on badges and combines their commentary in the hover popup so that I can immediately see where both experts agree or disagree without pressing `F6` back and forth on every reward screen.
- **Target Components**: `HttpTierListProvider`, `TierBadgeFactory`, `RoomOverlayFactory`, `OverlayConfig`.
- **Complexity**: Medium.
- **Acceptance Criteria**:
  - [ ] **AC1 (Three-State Author Cycling)**: Pressing `F6` or clicking the `Tier List:` button in `OverlayModSettingsPanel` cycles cleanly through `Baalorlord` -> `Jorbs` -> `Both` -> `Baalorlord` and persists the selection to `overlay_config.json`.
  - [ ] **AC2 (Dual Badge Formatting)**: In `"Both"` mode, if both authors rate an item the same tier (e.g., `S`), the badge displays a consensus rating (e.g., `S · S` or `B:S J:S`); if they differ, it displays `B:S J:A` colored by the average/primary tier score.
  - [ ] **AC3 (Combined Hover Commentary)**: Hovering or focusing an item in `"Both"` mode shows both Baalorlord's and Jorbs's commentary blocks clearly separated in the popup card.
  - [ ] **AC4 (Independent Cache Resilience)**: In `"Both"` mode, `HttpTierListProvider` utilizes both `tier_list_cache.json` (Baalorlord) and `tier_list_cache_jorbs.json` (Jorbs) without overwriting one cache with the other.
  - [ ] **AC5 (Unit Tests)**: Unit tests verify dual-author merging, consensus score calculation, and combined commentary formatting.

---

### Priority 2 (P2): Co-op / Multiplayer & Deep Combat Intelligence
*Goal: Support STS2's flagship co-op multiplayer mode and provide deeper turn-planning tools.*

#### P2-1. Co-op / Multiplayer Ally Attack Summary & Shared Intent Breakdown ([Issue #10](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/10))
- **User Story**: As a Co-op / Multiplayer Tactician playing Slay the Spire 2 with teammates, I want the Incoming Attack Summary to show per-player or team-wide incoming damage and highlight if any ally is facing lethal damage (`⚠ ALLY LETHAL`) so that my team can coordinate targeted block cards, potions, and enemy interrupts effectively.
- **Target Components**: `Sts2RuntimeInspector`, `OverlayCalculators`, `RoomOverlayFactory`.
- **Complexity**: Medium–High.
- **Acceptance Criteria**:
  - [ ] **AC1 (Single-Player Unchanged)**: When `Players.Count == 1`, `AttackSummaryPanel` renders the exact same compact single-player display with zero extra lines or visual clutter.
  - [ ] **AC2 (Multiplayer Ally Inspection)**: When `Players.Count > 1`, `Sts2RuntimeInspector` inspects each living ally's `CurrentHp`, `Block`, and targeted incoming attacks.
  - [ ] **AC3 (Ally Lethal Alert)**: If any ally's unblocked incoming damage is `>= Ally.CurrentHp`, `AttackSummaryPanel` displays a clear `⚠ {AllyName}: LETHAL (-X HP)` warning line.
  - [ ] **AC4 (Unit Tests)**: Unit tests in `CompanionFeaturesTests` verify multi-ally incoming attack aggregation and individual lethal flags.

#### P2-2. Outgoing Damage Calculator (Hand Lethal & Energy Optimizer) ([Issue #11](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/11))
- **User Story**: As a Climbing Strategist planning my turn in combat, I want an optional Outgoing Damage summary that calculates the maximum single-target damage available in my current hand given my current Energy so that I can immediately spot "Hand Lethal" against an elite or boss without manually summing 5–8 modified attack cards.
- **Target Components**: `Sts2RuntimeInspector`, `OverlayCalculators`, `RoomOverlayFactory`.
- **Complexity**: Medium.
- **Acceptance Criteria**:
  - [ ] **AC1 (Energy-Constrained Knapsack Calculation)**: `CalculateMaxOutgoingHandDamage` accurately computes the highest damage combination of Attack cards in hand that fits within `availableEnergy` (including `0`-cost, modified-cost, and `X`-cost attacks).
  - [ ] **AC2 (Enemy Block & HP Comparison)**: Compares max outgoing damage against enemy `CurrentHp + Block` to detect single-target lethal and full-board lethal.
  - [ ] **AC3 (HUD Integration & Settings Toggle)**: When `EnableOutgoingDamageSummary` is enabled in `OverlayConfig`, the Combat HUD displays available hand damage and highlights `⚔ LETHAL` when an enemy can be defeated with current energy.
  - [ ] **AC4 (Unit Tests)**: xUnit tests verify knapsack selection across mixed `0`/`1`/`2`/`3`/`X`-cost attacks, unplayable cards, and enemy block thresholds.

#### P2-3. Interactive Draw / Discard Pile Filtering & Hover Card Previews ([Issue #12](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/12))
- **User Story**: As a newer or returning Slay the Spire 2 player using the Live Combat Deck Tracker, I want to hover over a card row in the expanded Deck Tracker (when interactive mode is active) to preview what that card does so that I don't have to open the full-screen Draw Pile overlay every time I forget a new STS2 card's exact effect or numbers.
- **Target Components**: `RoomOverlayFactory.UpdateDeckTracker`, `Sts2RuntimeInspector`.
- **Complexity**: Medium.
- **Acceptance Criteria**:
  - [ ] **AC1 (Card Description Extraction)**: `Sts2RuntimeInspector.ReadPile` safely extracts dynamic/localized description text (with BBCode stripped via `OverlayCalculators.StripBbCode`) for each `CombatCard`.
  - [ ] **AC2 (Hover Preview Popup)**: Hovering a card line in the expanded `DeckTrackerPanel` displays a clean tooltip card to the right of the tracker (`offsetX = panel.Size.X + 8`) with the card's title, cost, type, and description.
  - [ ] **AC3 (Non-Blocking Input)**: Card rows use `MouseFilterEnum.Pass` so mouse clicks and scroll events continue to pass through to the underlying combat room.
  - [ ] **AC4 (Settings Toggle)**: `EnableDeckTrackerCardHover` can be toggled in `overlay_config.json` and `OverlayModSettingsPanel`.
  - [ ] **AC5 (Unit Tests)**: Unit tests verify description propagation and BBCode stripping in `DeckCardGroup`.

---

### Priority 3 (P3): Run Analytics, Ecosystem Integration & Community Extensibility
*Goal: Provide long-term progression insights and unify the modding ecosystem.*

#### P3-1. Local Run History & Card Pick Analytics (`user://run_history_stats.json`)
- **User Need**: Untapped.gg's desktop app tracks win rates and pick rates in the cloud, which Linux/offline players miss out on. Players want to see their own personal win rate by character, most-picked cards, and floor-by-floor HP/Gold graphs locally without requiring an account or cloud telemetry.
- **Proposed Solution**:
  - Hook run completion/abandonment and card reward selections to log lightweight local run summaries in `user://overlay_run_stats.json`.
  - Provide a summary view or exportable Markdown/JSON report showing personal pick rate vs. win rate per card/relic.
- **Target Components**: `SlayTheSpireOverlay.Core.Services.LocalRunTracker` (new), `ModEntry`.
- **Complexity**: Medium–High.

#### P3-2. Map Route Optimizer & Path Comparison Overlay (Integrated with `SlayTheSpireMapFog`)
- **User Need**: When opening `NMapScreen`, players manually trace paths to count how many Elites, Rest Sites (`🔥`), Shops (`$`), and Events (`?`) lie on each branch—and when running `SlayTheSpireMapFog`, any path helper must strictly respect the active `MapFogVisibility` horizon so it never leaks fogged nodes.
- **Proposed Solution**:
  - Add a **Path Node Counter** widget on `NMapScreen` that summarizes the min/max Elites, Fires, and Shops reachable from each currently travelable starting node (bounded by `MapFogVisibility` if `SlayTheSpireMapFog` is active).
- **Target Components**: `SlayTheSpireMapFog.Core`, `SlayTheSpireOverlay.Core`, `NMapScreen` patch.
- **Complexity**: Medium.

#### P3-3. Community Tier List & Localization Manager
- **User Need**: As the STS2 community grows, more streamers/high-level players will publish Untapped.gg tier list UUIDs or custom community JSON files, and non-English players need localized UI labels.
- **Proposed Solution**:
  - Allow registering custom Author profiles (`Name` + per-character `UUID` list or URL) in `overlay_config.json` and cycling through N authors in the Mod Settings UI and via `F6`.
  - Support localized static data via `https://sts2json.untapped.gg/data/public/latest/{lang}/sts_data.json`.
- **Target Components**: `OverlayConfig`, `HttpTierListProvider`, `RoomOverlayFactory`.
- **Complexity**: Low–Medium.

---

## 5. Summary Roadmap Matrix

| ID | GitHub Issue | Feature Name | Priority | Primary Persona | Effort | Architectural Layer |
| :--- | :---: | :--- | :---: | :--- | :---: | :--- |
| **P0-1** | [#1](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/1) | Steam Deck / Controller Quick Toggle Bar & Keybinds | **P0** | Handheld / Controller Player | S | `Core` + `Godot.UI` |
| **P0-2** | [#2](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/2) | Upgraded Card (`+`) & Enchantment Tier Evaluations | **P0** | Climbing Strategist | M | `Core` + `Godot` |
| **P0-3** | [#3](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/3) | Dynamic Draw Count in Hypergeometric Odds | **P0** | Climbing Strategist | S | `Core` + `Godot` |
| **P0-4** | [#4](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/4) | End-of-Turn Passive Block & DoT in Attack Summary | **P0** | Climbing Strategist | M | `Core` + `Godot` |
| **P0-5** | [#5](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/5) | UI Scale Slider & Anchorable/Draggable Panels | **P0** | Handheld / Ultrawide Player | M | `Core` + `Godot.UI` |
| **P1-1** | [#6](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/6) | Deck Composition & Synergy Tag Analyzer | **P1** | Deckbuilder & Synergy Seeker | M | `Core` + `Godot.UI` |
| **P1-2** | [#7](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/7) | Act Boss & Elite Preparation Banner | **P1** | Climbing Strategist | S | `Core` + `Godot.UI` |
| **P1-3** | [#8](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/8) | Multi-Item Shop Budget Planner ("Pin to Cart") | **P1** | Climbing Strategist | M | `Core` + `Godot.UI` |
| **P1-4** | [#9](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/9) | Side-by-Side Author Consensus Mode (`Baalorlord + Jorbs`) | **P1** | Climbing Strategist | M | `Core` + `Godot.UI` |
| **P2-1** | [#10](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/10) | Co-op / Multiplayer Ally Attack Summary | **P2** | Co-op Tactician | L | `Core` + `Godot` |
| **P2-2** | [#11](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/11) | Outgoing Hand Damage Calculator | **P2** | Climbing Strategist | M | `Core` + `Godot` |
| **P2-3** | [#12](https://github.com/Kenmura/SlayTheSpireOverlayLinux/issues/12) | Interactive Draw Pile Hover Previews | **P2** | New / Learning Player | M | `Godot.UI` |
| **P3-1** | — | Local Run History & Personal Win-Rate Analytics | **P3** | Deckbuilder & Synergy Seeker | L | `Core` + `Godot` |
| **P3-2** | — | Fog-Aware Map Route & Path Counter | **P3** | Custom / Challenge Runner | M | `Core` + `MapFog` |
| **P3-3** | — | Custom Community Author Profiles & Localization | **P3** | All Players | S | `Core` + `Godot.UI` |
