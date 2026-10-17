# AGENTS.md — SlayTheSpireOverlayLinux

## 1. Project Overview

**SlayTheSpireOverlayLinux** is a Linux-first, Proton-compatible in-game overlay and companion mod for **Slay the Spire 2 (STS2)** built with **.NET 9.0**, **GodotSharp (4.5.1)**, and **Harmony (`0Harmony`)**.

Unlike the official **Untapped.gg Companion** desktop application—which relies on an out-of-process Electron window (`untapped-node-native` X11 window tracking) and cross-process memory scraping (`untapped-scry`) that breaks under Wayland, Gamescope, `ptrace_scope` restrictions, and Steam Proton/Wine—this project runs **directly inside the Slay the Spire 2 process** as a native C# mod (`sts2.dll` + `GodotSharp.dll`). Because STS2 loads `.NET 9.0` mod assemblies identically under both Linux Native and Steam Proton builds, an in-game Godot/Harmony mod works on X11, Wayland, Gamescope/Steam Deck, and Proton without external processes or memory scraping.

---

## 2. Solution Architecture & Boundaries

The solution (`SlayTheSpireOverlay.slnx`) is strictly divided into four projects, enforced by automated architecture tests (`tests/SlayTheSpireOverlay.ArchitectureTests/DependencyTests.cs`):

1. **`src/SlayTheSpireOverlay.Core`** (`net9.0`)
   - **Responsibility**: Pure C# domain models, configuration, local caching, tier list & game data resolution, deck/combat/merchant state calculations, and HTTP API providers.
   - **Architectural Rule**: **MUST NOT** depend on `GodotSharp` or `sts2.dll`. All logic in `Core` must be 100% unit-testable outside the game engine.
2. **`src/SlayTheSpireOverlay.Godot`** (`net9.0`)
   - **Responsibility**: STS2 Mod entry point (`[MegaCrit.Sts2.Core.Modding.ModInitializer("Initialize")]`), Harmony patches against `MegaCrit.Sts2.Core.*`, and programmatic Godot 4.x UI factories (`SlayTheSpireOverlay.Godot.UI`).
   - **Architectural Rule**: Classes in `SlayTheSpireOverlay.Godot.UI` **MUST NOT** depend on `System.Net.Http` (verified by `DependencyTests.UI_ShouldNotDependOn_HttpClient`).
3. **`tests/SlayTheSpireOverlay.Tests`** (`net9.0`)
   - **Responsibility**: xUnit + Moq unit and integration tests for `SlayTheSpireOverlay.Core`.
4. **`tests/SlayTheSpireOverlay.ArchitectureTests`** (`net9.0`)
   - **Responsibility**: NetArchTest rules verifying clean separation between `Core`, `Godot.UI`, and network layers.

---

## 3. Build, Test, & Tooling Commands

On this host (Fedora Atomic/Silverblue), the .NET 9.0 SDK runs inside the `dotnet-dev` Distrobox container, and `/home` is symlinked to `/var/home`.

### Run All Unit & Architecture Tests
```bash
distrobox enter dotnet-dev -- dotnet test SlayTheSpireOverlay.slnx
```

### Build & Publish the Mod DLL
Note: `LD_PRELOAD=""` prevents Steam's 32-bit `gameoverlayrenderer.so` from polluting child processes when building from a terminal launched by Steam.
```bash
distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet publish src/SlayTheSpireOverlay.Godot/SlayTheSpireOverlay.Godot.csproj -c Release
```

### Refresh Tier Lists & Static Data (`baalorlord_tiers.json`)
```bash
./scripts/update_tiers.sh
```

### Publish to Steam Workshop
```bash
./scripts/upload_to_workshop.sh "Release notes"
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
