# Agent-Friendly Architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the structural problems that make this repo hard for agents to change safely: untestable game-facing code, silent reflection failures, copy-pasted plumbing between the two mods, oversized files, a build that only works on one machine, and stale instructions.

**Architecture:** Keep the existing layering (`Core` pure, `Godot` adapter, architecture tests). Add one source-linked shared project (`src/SlayTheSpireMod.Common`) compiled into each mod DLL, exactly like `Core` is today, so each mod still ships as a single DLL. Move decision logic out of Godot code into `Core` as pure functions with tests. Report instead of hide patch failures, and add an automated check that every game type and member the mods use exists in a given `sts2.dll`.

**Tech Stack:** .NET 9 (mods), .NET 10 (tests, tools), MSTest 4.0.1, NetArchTest 1.3.2, Harmony (`0Harmony`), GodotSharp 4.7.2 (compile-time only), bash, GitHub Actions.

**Spec:** There is no separate spec document. The requirements are the architecture review and the GitHub issues it produced; the findings are restated in "Context" below. Related issues: #13 (fog verification), #15 (relic log spam), #16 (compat checker + per-patch handling), #17 (Deck troubleshooting), #18 (README/CI).

## Context (the findings this plan fixes)

| ID | Finding | Evidence | Fixed by |
|---|---|---|---|
| F1 | Game-facing code has no tests; agents can only verify by launching the game | 0 tests reach `*.Godot` code; ~3,000 lines there | Tasks 8, 9, 11 |
| F2 | 112 string-named reflection lookups and patches that skip silently | `GetMember("...")`, `PatchIfPresent` | Tasks 3, 5, 7, 10 |
| F3 | The two mods copy-paste their plumbing | `GetOrCreateRunModdingScreen` byte-identical; `IsOurMod` differs by one string | Tasks 5, 6, 7, 10 |
| F4 | Very large files with static mutable state | `RoomOverlayFactory.cs` 718 lines at HEAD (1,260 with in-flight work), 12 static fields | Tasks 12, 13, 14 |
| F5 | Godot projects only build with a local game install; opaque error | `Sts2DataDir` default path | Tasks 2, 15 |
| F6 | Docs and scripts have drifted | AGENTS.md paths/versions/test framework; hard-coded `/var/home/nickmarc` in a script; duplicated tier JSON | Tasks 1, 4 |
| F7 | Build traps | nested third-party uploader inherits analyzers; tests net10 vs mods net9 | Tasks 1, 4, 15 |
| F8 | Several agents edit one working tree | uncommitted changes from another session are present | Task 0 |
| F9 | (found while planning) `NModInfoContainer.Clear` does not exist in game v0.107.1, so both mods' "Clear" hook is dead code | Compat checker run | Task 10 |

## Global Constraints

- Run all dotnet commands through the container: `dn() { LD_PRELOAD="" distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet "$@"; }` (the container only has the 8.0 and 10.0 runtimes; a net9.0 app cannot be `dotnet run`).
- `Directory.Build.props` sets `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`: every new file must build with zero warnings. Reserved-word namespaces (`Shared`) fail CA1716; this plan uses `SlayTheSpireMod.Common`.
- Tests use **MSTest** (`[TestClass]`/`[TestMethod]`), not xUnit. Each test project already has an `MSTestSettings.cs` that enables parallelization.
- `SlayTheSpireOverlay.Core` and `SlayTheSpireMapFog.Core` must not depend on `Godot`, `GodotSharp`, `MegaCrit`, `HarmonyLib` or either `*.Godot` project (architecture test).
- Classes in `*.Godot.UI` must not depend on `System.Net.Http`.
- Never subclass `Godot.Node` or `Godot.Control` in mod assemblies; instantiate built-in classes. Scene-tree mutation only on the main thread.
- Each mod ships as **one DLL** (plus `.json` manifest and `.pdb`). Shared code is source-linked, never a separate assembly.
- Workshop items stay `"visibility": "private"`.
- Commit messages end with the trailer `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- Do not modify files you did not change in this plan; another session may have uncommitted edits (Task 0).

## Review Focus

Failure modes the plan implies that no single feature test would catch (each has a pinning test or check in the owning task):

1. **Missing or corrupt config file** must yield defaults, never an exception at game start (Task 6: `Load_MissingFile_ReturnsDefaults`, `Load_CorruptFile_ReturnsDefaults`).
2. **A game update renames a member**: the mod must still load, skip only that patch, and say which one (Task 7 test; Task 3 negative check proves the checker fails on a bad name).
3. **Both mods installed together** must still share exactly one hidden Mods-screen node (Task 10 in-game check).
4. **Fog with nothing revealed / boss position unknown** must not throw or draw garbage (Task 8: `Compute_NothingRevealed_ReturnsNull`, `Compute_UnknownBoss_DefaultsToUpward`).
5. **Held hotkey / repeated polls in one frame** must fire exactly once (Task 9 tests).
6. **Scripts run from any directory and any clone location** (Task 1 check).

---

## Task 0: Isolate work and sequence around in-flight changes

**Why:** Another session currently has uncommitted edits to `ModEntry.cs`, `Sts2RuntimeInspector.cs`, `RoomOverlayFactory.cs`, `TierBadgeFactory.cs`, `RelicBadgeFactory.cs`, `OverlayConfig.cs`, `OverlayCalculators.cs`, `CompanionFeaturesTests.cs`, `HttpTierListProvider.cs`, and the Fog files `MapFogConfig.cs`, `MapFogCalculator.cs`, `Sts2MapInspector.cs`, `MapFogSettingsFactory.cs` (a `FogOpacity` setting, issue #13), plus new `DeckSynergyAnalyzer.cs`/tests. Editing the same files in the same checkout loses work.

**Files:** none (process only).

- [ ] **Step 1: Check for in-flight work**

```bash
git status --short
git log origin/main..HEAD --oneline
```

Expected: if any `M`/`??` entries are files you did not touch, treat them as owned by someone else.

- [ ] **Step 2: Work in a worktree**

```bash
git fetch origin
git worktree add ../SlayTheSpireOverlayLinux-arch -b arch/agent-friendly origin/main
cd ../SlayTheSpireOverlayLinux-arch
```

All remaining tasks run in this worktree. Never `git add -A`; add exact paths.

- [ ] **Step 3: Respect the order**

Run **Tasks 1-7 and 9 first**: they only create new files or edit files nobody else is changing (`*.sh`, `AGENTS.md`, csproj files, new `Common`/`Core` files and their tests). **Task 8 and Tasks 10-14 touch in-flight files** (`Sts2MapInspector.cs`, `ModEntry.cs`, `Sts2RuntimeInspector.cs`, `RoomOverlayFactory.cs`): start them only after the other session's work is committed and merged to `origin/main`, then `git rebase origin/main` and re-read the current file before editing. Line numbers in this plan are for commit `0f6bdca`; always locate code by method name.

- [ ] **Step 4: No commit** (nothing changed).

---

## Task 1: Make scripts location-independent, move them into `scripts/`, drop the duplicated tier JSON

**Files:**
- Move: `upload_to_workshop.sh`, `upload_fog_to_workshop.sh`, `update_tiers.sh` -> `scripts/`
- Modify: `scripts/upload_to_workshop.sh`, `scripts/upload_fog_to_workshop.sh`, `scripts/update_tiers.sh`, `scripts/update_tiers.py`
- Delete: `src/SlayTheSpireOverlay.Godot/baalorlord_tiers.json` (write-only copy; the DLL embeds `workshop/baalorlord_tiers.json`)

**Interfaces:**
- Produces: scripts runnable as `./scripts/<name>.sh` from any directory, no machine-specific paths.

- [ ] **Step 1: Prove the problem**

```bash
grep -n "/var/home" *.sh scripts/*.sh
```

Expected: one hit, `upload_to_workshop.sh:8:BASE_DIR="/var/home/nickmarc/repos/SlayTheSpireOverlayLinux"`.

- [ ] **Step 2: Move the scripts**

```bash
git mv upload_to_workshop.sh upload_fog_to_workshop.sh update_tiers.sh scripts/
```

- [ ] **Step 3: Fix base-directory resolution**

In `scripts/upload_to_workshop.sh` replace the line
`BASE_DIR="/var/home/nickmarc/repos/SlayTheSpireOverlayLinux"` with:

```bash
BASE_DIR="$(cd "$(dirname "$0")/.." && pwd)"
```

In `scripts/upload_fog_to_workshop.sh` replace `BASE_DIR="$(cd "$(dirname "$0")" && pwd)"` with:

```bash
BASE_DIR="$(cd "$(dirname "$0")/.." && pwd)"
```

In `scripts/update_tiers.sh` replace its body with:

```bash
#!/usr/bin/env bash
set -e
cd "$(dirname "$0")/.."
python3 scripts/update_tiers.py "$@"
```

- [ ] **Step 4: Remove the duplicated tier JSON**

In `scripts/update_tiers.py`:
1. Delete the line `EMBEDDED_JSON = os.path.join(REPO_DIR, "src", "SlayTheSpireOverlay.Godot", "baalorlord_tiers.json")`.
2. Replace
```python
    with open(WORKSHOP_JSON, "w") as f:
        json.dump(new_db, f, indent=2, sort_keys=True)
    with open(EMBEDDED_JSON, "w") as f:
        json.dump(new_db, f, indent=2, sort_keys=True)
```
with
```python
    with open(WORKSHOP_JSON, "w") as f:
        json.dump(new_db, f, indent=2, sort_keys=True)
```
3. Replace `run_command("git add workshop/baalorlord_tiers.json src/SlayTheSpireOverlay.Godot/baalorlord_tiers.json workshop/workshop.json")` with `run_command("git add workshop/baalorlord_tiers.json workshop/workshop.json")`.

```bash
git rm src/SlayTheSpireOverlay.Godot/baalorlord_tiers.json
```

- [ ] **Step 5: Verify**

```bash
bash -n scripts/upload_to_workshop.sh scripts/upload_fog_to_workshop.sh scripts/update_tiers.sh
python3 -m py_compile scripts/update_tiers.py
grep -n "EMBEDDED_JSON\|/var/home" scripts/*
# resolve BASE_DIR from another directory without uploading anything:
sed 's|^cd "\$BASE_DIR"|echo "BASE=$BASE_DIR"; exit 0|' scripts/upload_fog_to_workshop.sh > scripts/.probe.sh
(cd / && bash "$OLDPWD/scripts/.probe.sh"); rm scripts/.probe.sh
dn build src/SlayTheSpireOverlay.Godot -c Release 2>&1 | tail -3
```

Expected: no `grep` output; `BASE=<absolute path to this worktree>`; the build succeeds (the embedded resource still comes from `workshop/baalorlord_tiers.json`).

- [ ] **Step 6: Commit**

```bash
git add scripts/ .gitignore
git commit -m "build: move scripts under scripts/, resolve paths relative to the repo, drop duplicate tier JSON

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task 2: Clear error when the game DLLs are missing

**Files:**
- Create: `Directory.Build.targets`
- Modify: `src/SlayTheSpireOverlay.Godot/SlayTheSpireOverlay.Godot.csproj`, `src/SlayTheSpireMapFog.Godot/SlayTheSpireMapFog.Godot.csproj`

**Interfaces:**
- Produces: MSBuild property `UsesSts2` (set `true` by projects that reference `sts2.dll`).

- [ ] **Step 1: Reproduce the unhelpful failure**

```bash
dn build src/SlayTheSpireMapFog.Godot -p:Sts2DataDir=/nonexistent 2>&1 | grep -E "error" | head -3
```

Expected: a cryptic compiler error such as `CS0246: The type or namespace name 'MegaCrit'...` instead of an explanation.

- [ ] **Step 2: Add the guard**

`Directory.Build.targets`:

```xml
<Project>
  <Target Name="RequireSts2Dlls" BeforeTargets="ResolveAssemblyReferences" Condition="'$(UsesSts2)' == 'true' And !Exists('$(Sts2DataDir)/sts2.dll')">
    <Error Text="sts2.dll not found in '$(Sts2DataDir)'. Install Slay the Spire 2 via Steam or pass -p:Sts2DataDir=&lt;path to the data_sts2_* folder&gt;. Without game files only tests/SlayTheSpireOverlay.Tests can be built." />
  </Target>
</Project>
```

In both Godot csproj files, inside the existing `<PropertyGroup>` that sets `TargetFramework`, add:

```xml
    <UsesSts2>true</UsesSts2>
```

- [ ] **Step 3: Verify both directions**

```bash
dn build src/SlayTheSpireMapFog.Godot -p:Sts2DataDir=/nonexistent 2>&1 | grep -E "error" | head -2
dn build src/SlayTheSpireMapFog.Godot 2>&1 | tail -3
```

Expected: first shows `sts2.dll not found in '/nonexistent'...`; second `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add Directory.Build.targets src/SlayTheSpireOverlay.Godot/SlayTheSpireOverlay.Godot.csproj src/SlayTheSpireMapFog.Godot/SlayTheSpireMapFog.Godot.csproj
git commit -m "build: fail with a clear message when sts2.dll is missing

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task 3: Game-version compatibility checker

Implements the core of issue #16. Verified against game v0.107.1: the checker passes on current targets and **fails (exit 1) when a target is bogus**, including bogus names on Godot-derived game types, because it follows the inheritance chain into `GodotSharp.dll`.

**Files:**
- Create: `tools/Sts2CompatCheck/Sts2CompatCheck.csproj`, `tools/Sts2CompatCheck/Program.cs`, `tools/Sts2CompatCheck/patch-targets.txt`, `scripts/check_sts2_compat.sh`
- Modify: `SlayTheSpireOverlay.slnx`

**Interfaces:**
- Produces: `scripts/check_sts2_compat.sh` (exit 0 = everything resolves, 1 = something missing, 2 = bad usage). Used by Tasks 10 and 15.

- [ ] **Step 1: Create the project**

`tools/Sts2CompatCheck/Sts2CompatCheck.csproj`:


```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

`tools/Sts2CompatCheck/Program.cs`:


```csharp
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Usage: Sts2CompatCheck <sts2.dll> <patch-targets.txt> <mod.dll> [<mod.dll> ...]
// Exit codes: 0 = everything resolves, 1 = something is missing, 2 = bad arguments.
if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: Sts2CompatCheck <sts2.dll> <patch-targets.txt> <mod.dll> [<mod.dll> ...]");
    return 2;
}

using var sts2Pe = new PEReader(File.OpenRead(args[0]));
var peReaders = new List<PEReader> { sts2Pe };
string? godotPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0])) ?? ".", "GodotSharp.dll");
if (File.Exists(godotPath)) peReaders.Add(new PEReader(File.OpenRead(godotPath)));
var game = new GameIndex(peReaders.Select(p => p.GetMetadataReader()).ToArray());
int problems = 0;

Console.WriteLine("=== string-based patch targets");
foreach (string line in File.ReadAllLines(args[1]))
{
    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2 || parts[0].StartsWith('#')) continue;
    problems += Report(game.CheckMember(parts[0], parts[1]), $"{parts[0]}::{parts[1]}");
}

foreach (string modPath in args.Skip(2))
{
    Console.WriteLine($"=== compile-time references in {Path.GetFileName(modPath)}");
    using var pe = new PEReader(File.OpenRead(modPath));
    var mod = pe.GetMetadataReader();

    foreach (var handle in mod.TypeReferences)
    {
        string? name = Sts2TypeName(mod, handle);
        if (name != null) problems += Report(game.CheckType(name), name);
    }

    foreach (var handle in mod.MemberReferences)
    {
        var member = mod.GetMemberReference(handle);
        string? typeName = Sts2TypeName(mod, member.Parent);
        if (typeName == null) continue;
        string memberName = mod.GetString(member.Name);
        problems += Report(game.CheckMember(typeName, memberName), $"{typeName}::{memberName}");
    }
}

Console.WriteLine(problems == 0
    ? "OK: every referenced game type and member exists in this sts2.dll."
    : string.Create(CultureInfo.InvariantCulture, $"FAILED: {problems} missing reference(s)."));
return problems == 0 ? 0 : 1;

static int Report(Check result, string what)
{
    switch (result)
    {
        case Check.Ok:
            return 0;
        case Check.InheritedOutsideGame:
            Console.WriteLine($"  note: {what} is inherited from outside sts2.dll (not verified)");
            return 0;
        default:
            Console.WriteLine($"  MISSING {what}");
            return 1;
    }
}

static string? Sts2TypeName(MetadataReader reader, EntityHandle handle)
{
    if (handle.Kind != HandleKind.TypeReference) return null;
    var typeRef = reader.GetTypeReference((TypeReferenceHandle)handle);
    if (typeRef.ResolutionScope.Kind != HandleKind.AssemblyReference) return null;
    var assembly = reader.GetAssemblyReference((AssemblyReferenceHandle)typeRef.ResolutionScope);
    if (reader.GetString(assembly.Name) != "sts2") return null;
    return reader.GetString(typeRef.Namespace) + "." + reader.GetString(typeRef.Name);
}

internal enum Check
{
    Ok,
    InheritedOutsideGame,
    Missing
}

internal sealed class GameIndex
{
    private readonly Dictionary<string, (MetadataReader Reader, TypeDefinitionHandle Handle)> _types = new(StringComparer.Ordinal);

    // The first reader is the game assembly; the rest (GodotSharp) only resolve inherited members.
    public GameIndex(MetadataReader[] readers)
    {
        foreach (var reader in readers)
        {
            foreach (var handle in reader.TypeDefinitions)
            {
                _types.TryAdd(FullName(reader, reader.GetTypeDefinition(handle)), (reader, handle));
            }
        }

        GameReader = readers[0];
    }

    private MetadataReader GameReader { get; }

    public Check CheckType(string fullName) =>
        _types.TryGetValue(fullName, out var entry) && entry.Reader == GameReader ? Check.Ok : Check.Missing;

    public Check CheckMember(string typeName, string memberName)
    {
        if (!_types.TryGetValue(typeName, out var entry) || entry.Reader != GameReader) return Check.Missing;
        return Find(entry.Reader, entry.Handle, memberName);
    }

    private Check Find(MetadataReader reader, TypeDefinitionHandle handle, string memberName)
    {
        var type = reader.GetTypeDefinition(handle);
        foreach (var method in type.GetMethods())
        {
            if (reader.GetString(reader.GetMethodDefinition(method).Name) == memberName) return Check.Ok;
        }

        foreach (var field in type.GetFields())
        {
            if (reader.GetString(reader.GetFieldDefinition(field).Name) == memberName) return Check.Ok;
        }

        foreach (var property in type.GetProperties())
        {
            if (reader.GetString(reader.GetPropertyDefinition(property).Name) == memberName) return Check.Ok;
        }

        var baseType = type.BaseType;
        if (baseType.IsNil) return Check.Missing;
        if (baseType.Kind == HandleKind.TypeDefinition) return Find(reader, (TypeDefinitionHandle)baseType, memberName);
        if (baseType.Kind == HandleKind.TypeReference)
        {
            var reference = reader.GetTypeReference((TypeReferenceHandle)baseType);
            string baseName = reader.GetString(reference.Namespace) + "." + reader.GetString(reference.Name);
            if (_types.TryGetValue(baseName, out var resolved)) return Find(resolved.Reader, resolved.Handle, memberName);
            if (baseName == "System.Object") return Check.Missing;
        }

        // Base lives in the runtime libraries (System.Object etc.) or is generic: cannot verify by name.
        return Check.InheritedOutsideGame;
    }

    private static string FullName(MetadataReader reader, TypeDefinition type)
    {
        string name = reader.GetString(type.Name);
        var declaring = type.GetDeclaringType();
        if (!declaring.IsNil) return FullName(reader, reader.GetTypeDefinition(declaring)) + "/" + name;
        string ns = reader.GetString(type.Namespace);
        return ns.Length > 0 ? ns + "." + name : name;
    }
}
```

- [ ] **Step 2: List the string-based patch targets**

`tools/Sts2CompatCheck/patch-targets.txt` (one `FullTypeName member` per line; these are the names the mods pass as strings to Harmony or reflection, and the compile-time references are checked automatically). `NModInfoContainer Clear` is intentionally absent: it does not exist in v0.107.1 and Task 10 removes the dead hook.

```text
# Type member: string-based Harmony targets used by the mods (see each ModEntry.cs)

MegaCrit.Sts2.Core.Nodes.Cards.NCard UpdateVisuals
MegaCrit.Sts2.Core.Nodes.Relics.NRelic Reload
MegaCrit.Sts2.Core.Nodes.Potions.NPotion Reload
MegaCrit.Sts2.Core.Nodes.Rewards.NRewardButton Reload
MegaCrit.Sts2.Core.Nodes.Rewards.NRewardButton SetReward
MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder OnFocus
MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder OnUnfocus
MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantSlot OnFocus
MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton OnFocus
MegaCrit.Sts2.Core.Nodes.Relics.NRelicBasicHolder OnFocus
MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic.NTreasureRoomRelicHolder OnFocus
MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom _Ready
MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom _Ready
MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom _Ready
MegaCrit.Sts2.Core.Nodes.Rooms.NTreasureRoom _Ready
MegaCrit.Sts2.Core.Nodes.Rooms.NRestSiteRoom _Ready
MegaCrit.Sts2.Core.Nodes.Rooms.NMapRoom _Ready
MegaCrit.Sts2.Core.Nodes.NRun _Ready
MegaCrit.Sts2.Core.Nodes.NGame _Ready
MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModInfoContainer Fill
MegaCrit.Sts2.Core.Nodes.Screens.Settings.NSettingsScreen _Ready
MegaCrit.Sts2.Core.Nodes.Screens.NRunSubmenuStack GetSubmenuType
MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModdingScreen Create
MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen SetMap
MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen RecalculateTravelability
MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen RefreshAllPointVisuals
MegaCrit.Sts2.Core.Nodes.Screens.Map.NNormalMapPoint UpdateIcon
MegaCrit.Sts2.Core.Nodes.Screens.Map.NNormalMapPoint RefreshMarkedIconVisibility
MegaCrit.Sts2.Core.Nodes.Screens.Map.NNormalMapPoint OnHighlightPointType
MegaCrit.Sts2.Core.Nodes.Screens.Map.NNormalMapPoint IconPath
MegaCrit.Sts2.Core.Nodes.Screens.Map.NNormalMapPoint OutlinePath

```


- [ ] **Step 3: Add the runner script**

`scripts/check_sts2_compat.sh`:

```bash
#!/usr/bin/env bash
# Verifies every game type/member the mods use exists in the installed sts2.dll.
# Override the game folder with STS2_DATA_DIR=/path/to/data_sts2_windows_x86_64
set -euo pipefail
export LD_PRELOAD=""
BASE_DIR="$(cd "$(dirname "$0")/.." && pwd)"
DATA_DIR="${STS2_DATA_DIR:-$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64}"

dn() { distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet "$@"; }

dn build "$BASE_DIR/src/SlayTheSpireOverlay.Godot" -c Release -p:Sts2DataDir="$DATA_DIR" --nologo -v q
dn build "$BASE_DIR/src/SlayTheSpireMapFog.Godot" -c Release -p:Sts2DataDir="$DATA_DIR" --nologo -v q

dn run --project "$BASE_DIR/tools/Sts2CompatCheck" -c Release -- \
    "$DATA_DIR/sts2.dll" \
    "$BASE_DIR/tools/Sts2CompatCheck/patch-targets.txt" \
    "$BASE_DIR/src/SlayTheSpireOverlay.Godot/bin/Release/net9.0/SlayTheSpireOverlay.Godot.dll" \
    "$BASE_DIR/src/SlayTheSpireMapFog.Godot/bin/Release/net9.0/SlayTheSpireMapFog.Godot.dll"
```

```bash
chmod +x scripts/check_sts2_compat.sh
```

In `SlayTheSpireOverlay.slnx`, add before `</Solution>`:

```xml
  <Folder Name="/tools/">
    <Project Path="tools/Sts2CompatCheck/Sts2CompatCheck.csproj" />
  </Folder>
```

- [ ] **Step 4: Run it on the installed game (expect PASS)**

```bash
./scripts/check_sts2_compat.sh; echo "exit=$?"
```

Expected: ends with `OK: every referenced game type and member exists in this sts2.dll.` and `exit=0`. (If the installed game is a different version and something is missing, the output names it: that is the tool working. Resolve per Task 10 before continuing.)

- [ ] **Step 5: Prove it can fail (negative check)**

```bash
echo "MegaCrit.Sts2.Core.Nodes.Cards.NCard NoSuchMethod" > /tmp/neg-targets.txt
DATA="$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64"
dn run --project tools/Sts2CompatCheck -c Release -- "$DATA/sts2.dll" /tmp/neg-targets.txt src/SlayTheSpireMapFog.Godot/bin/Release/net9.0/SlayTheSpireMapFog.Godot.dll; echo "exit=$?"
```

Expected: `MISSING MegaCrit.Sts2.Core.Nodes.Cards.NCard::NoSuchMethod`, `FAILED: 1 missing reference(s).`, `exit=1`. If it prints `OK` the checker is broken: do not proceed.

- [ ] **Step 6: Commit**

```bash
git add tools/Sts2CompatCheck scripts/check_sts2_compat.sh SlayTheSpireOverlay.slnx
git commit -m "feat(tools): add sts2.dll compatibility checker

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task 4: Correct AGENTS.md

**Files:**
- Modify: `AGENTS.md`

- [ ] **Step 1: Fix the stale claims** (use exact-string replacements; each `old` appears once)

1. In section 1, replace `built with **.NET 9.0**, **GodotSharp (4.5.1)**, and **Harmony (`0Harmony`)**.` with:
   `built with **.NET 9.0**, **GodotSharp** (compiled against the 4.7.2 bindings; the game itself runs Godot 4.5.1), and **Harmony (`0Harmony`)**. It ships two independent Workshop mods: **Overlay for Tier List** (`SlayTheSpireOverlay.*`) and **Fog of War Map** (`SlayTheSpireMapFog.*`).`
2. In section 2, replace `is strictly divided into four projects, enforced by automated architecture tests` with `is divided into the projects below; the Core/UI boundaries are enforced by automated architecture tests`.
3. In section 2, replace the test project bullet text `xUnit + Moq unit and integration tests for `SlayTheSpireOverlay.Core`.` with `MSTest (4.0.1) unit tests for both `*.Core` projects and the shared `SlayTheSpireMod.Common` helpers (tests target `net10.0`; the container has no net9 runtime).`
4. After the architecture-tests bullet in section 2, add:

```markdown
5. **`src/SlayTheSpireMod.Common`** (source-linked, not a separate assembly)
   - **Responsibility**: Plumbing shared by both mods: `ReflectionAccess`, `JsonConfigStore`, `PatchRegistrar` (pure, unit-tested), and `GameHooks/` (Harmony and Godot helpers). Compiled into each mod DLL via `<Compile Include=... Link=...>`, exactly like `Core`.
   - **Architectural Rule**: Types directly in namespace `SlayTheSpireMod.Common` must not depend on Godot, Harmony or `sts2.dll`; those live in `SlayTheSpireMod.Common.GameHooks`.
6. **`tools/Sts2CompatCheck`** — checks that every game type/member the mods reference exists in a given `sts2.dll`.
```

5. Replace the whole of section 3 (from `## 3. Build, Test, & Tooling Commands` up to, not including, `---` before section 4) with:

````markdown
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
````

6. Add a new section before section 5:

```markdown
## 4b. Working in parallel (agents)

- Use one **git worktree and branch per task** (`git worktree add ../<name> -b <branch> origin/main`). Never share a checkout between agents; check `git status` first and do not edit files you did not change.
- Add files by explicit path; never `git add -A`.
- `RoomOverlayFactory`, `ModEntry` and `Sts2RuntimeInspector` are large and shared by many features: keep edits small and rebase often.
- Game-facing code cannot be unit-tested. Put decisions (math, layout geometry, edge detection) in `*.Core` as pure functions and keep Godot code a thin adapter.
- Access game internals only through `SlayTheSpireMod.Common.ReflectionAccess` and register patches through `PatchRegistrar`; then run `./scripts/check_sts2_compat.sh`.
```

- [ ] **Step 2: Verify**

```bash
grep -n "xUnit\|4\.5\.1)\|strictly divided\|scripts/upload_to_workshop" AGENTS.md
```

Expected: only the unchanged section-5 research lines mentioning `STS2_GODOT_VERSION = "4.5.1"` remain; no other hit for `xUnit` or `strictly divided`.

- [ ] **Step 3: Commit**

```bash
git add AGENTS.md
git commit -m "docs: correct AGENTS.md (scripts, test framework, versions, Fog mod, parallel work)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task 5: `ReflectionAccess` (shared, tested)

**Files:**
- Create: `src/SlayTheSpireMod.Common/ReflectionAccess.cs`
- Create: `tests/SlayTheSpireOverlay.Tests/ReflectionAccessTests.cs`
- Modify: `tests/SlayTheSpireOverlay.Tests/SlayTheSpireOverlay.Tests.csproj`

**Interfaces:**
- Produces: `static class SlayTheSpireMod.Common.ReflectionAccess` with `Type? GetCachedType(string fullName)`, `MethodInfo? GetCachedMethod(Type, string)`, `object? GetMember(object? target, params string[] names)`, `object? GetStaticMember(Type, params string[] names)`. Used by Tasks 7 and 10.

- [ ] **Step 1: Link the shared folder into the test project**

In `tests/SlayTheSpireOverlay.Tests/SlayTheSpireOverlay.Tests.csproj` add a new `ItemGroup` (top-level files only, so the Godot-dependent `GameHooks/` folder is excluded):

```xml
  <ItemGroup>
    <Compile Include="..\..\src\SlayTheSpireMod.Common\*.cs" Link="Common\%(Filename)%(Extension)" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing tests**

`tests/SlayTheSpireOverlay.Tests/ReflectionAccessTests.cs`:


```csharp
using SlayTheSpireMod.Common;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class ReflectionAccessTests
{
    private class Base
    {
        private readonly string _baseSecret = "base";

        public string BaseSecretForTest => _baseSecret;
    }

    private sealed class Sample : Base
    {
#pragma warning disable CA1822
        private readonly int _hidden = 7;
        public static string StaticName => "static-value";
        public int HiddenForTest => _hidden;
        public string? NullProp => null;
        public string Named => "prop";
#pragma warning restore CA1822
    }

    [TestMethod]
    public void GetMember_ReadsPublicPropertyAndPrivateField()
    {
        var sample = new Sample();
        Assert.AreEqual("prop", ReflectionAccess.GetMember(sample, "Named"));
        Assert.AreEqual(7, ReflectionAccess.GetMember(sample, "_hidden"));
    }

    [TestMethod]
    public void GetMember_FindsMembersDeclaredOnBaseTypes()
    {
        Assert.AreEqual("base", ReflectionAccess.GetMember(new Sample(), "_baseSecret"));
    }

    [TestMethod]
    public void GetMember_SkipsNullAndMissingNamesAndUsesTheFirstNonNull()
    {
        var sample = new Sample();
        Assert.AreEqual("prop", ReflectionAccess.GetMember(sample, "Missing", "NullProp", "Named"));
        Assert.IsNull(ReflectionAccess.GetMember(sample, "Missing"));
    }

    [TestMethod]
    public void GetMember_NullTargetReturnsNull()
    {
        Assert.IsNull(ReflectionAccess.GetMember(null, "Anything"));
    }

    [TestMethod]
    public void GetStaticMember_ReadsStaticOnlyAndIgnoresInstanceMembers()
    {
        Assert.AreEqual("static-value", ReflectionAccess.GetStaticMember(typeof(Sample), "StaticName"));
        Assert.IsNull(ReflectionAccess.GetStaticMember(typeof(Sample), "Named"));
    }

    [TestMethod]
    public void GetCachedType_ResolvesLoadedTypesAndReturnsNullForUnknown()
    {
        Assert.AreEqual(typeof(string), ReflectionAccess.GetCachedType("System.String"));
        Assert.IsNull(ReflectionAccess.GetCachedType("No.Such.Type"));
    }

    [TestMethod]
    public void GetCachedMethod_FindsInheritedPrivateAndMissingMethods()
    {
        Assert.IsNotNull(ReflectionAccess.GetCachedMethod(typeof(Sample), "get_BaseSecretForTest"));
        Assert.IsNull(ReflectionAccess.GetCachedMethod(typeof(Sample), "NoSuchMethod"));
    }
}
```

- [ ] **Step 3: Run to verify they fail**

```bash
dn test tests/SlayTheSpireOverlay.Tests 2>&1 | grep -E "error|Passed|Failed" | head -3
```

Expected: build error `CS0234`/`CS0246` ... `ReflectionAccess` does not exist.

- [ ] **Step 4: Implement**

`src/SlayTheSpireMod.Common/ReflectionAccess.cs`:


```csharp
using System.Collections.Concurrent;
using System.Reflection;

namespace SlayTheSpireMod.Common;

/// <summary>
/// Name-based access to game internals. Mods cannot reference every game member at compile time
/// without breaking across game versions, so lookups are by string and return null when missing.
/// </summary>
public static class ReflectionAccess
{
    private const BindingFlags AnyDeclared =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(Type, string), MethodInfo?> MethodCache = new();
    private static readonly ConcurrentDictionary<(Type, string, bool), MemberInfo?> MemberCache = new();

    public static Type? GetCachedType(string fullName) =>
        TypeCache.GetOrAdd(fullName, static name =>
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(name, throwOnError: false);
                if (type != null) return type;
            }
            return null;
        });

    public static MethodInfo? GetCachedMethod(Type type, string methodName) =>
        MethodCache.GetOrAdd((type, methodName), static key =>
        {
            for (var t = key.Item1; t != null; t = t.BaseType)
            {
                foreach (var method in t.GetMethods(AnyDeclared))
                {
                    if (method.Name == key.Item2) return method;
                }
            }
            return null;
        });

    /// <summary>Returns the first non-null value among the named instance properties/fields (property wins over field per name).</summary>
    public static object? GetMember(object? target, params string[] names)
    {
        if (target == null) return null;
        var type = target.GetType();
        foreach (string name in names)
        {
            var value = Read(Find(type, name, wantStatic: false), target);
            if (value != null) return value;
        }
        return null;
    }

    /// <summary>Returns the first non-null value among the named static properties/fields.</summary>
    public static object? GetStaticMember(Type type, params string[] names)
    {
        foreach (string name in names)
        {
            var value = Read(Find(type, name, wantStatic: true), null);
            if (value != null) return value;
        }
        return null;
    }

    private static MemberInfo? Find(Type type, string name, bool wantStatic) =>
        MemberCache.GetOrAdd((type, name, wantStatic), static key =>
        {
            for (var t = key.Item1; t != null; t = t.BaseType)
            {
                var property = t.GetProperty(key.Item2, AnyDeclared);
                if (property?.GetMethod != null && property.GetMethod.IsStatic == key.Item3) return property;
                var field = t.GetField(key.Item2, AnyDeclared);
                if (field != null && field.IsStatic == key.Item3) return field;
            }
            return null;
        });

    private static object? Read(MemberInfo? member, object? target) => member switch
    {
        PropertyInfo p => p.GetValue(target),
        FieldInfo f => f.GetValue(target),
        _ => null
    };
}
```

- [ ] **Step 5: Run to verify they pass**

```bash
dn test tests/SlayTheSpireOverlay.Tests 2>&1 | grep -E "error|Passed|Failed"
```

Expected: `Passed!` with zero failed and no warnings-as-errors.

- [ ] **Step 6: Commit**

```bash
git add src/SlayTheSpireMod.Common/ReflectionAccess.cs tests/SlayTheSpireOverlay.Tests/ReflectionAccessTests.cs tests/SlayTheSpireOverlay.Tests/SlayTheSpireOverlay.Tests.csproj
git commit -m "feat(common): add tested ReflectionAccess shared by both mods

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task 6: `JsonConfigStore` (shared, tested)

**Files:**
- Create: `src/SlayTheSpireMod.Common/JsonConfigStore.cs`, `tests/SlayTheSpireOverlay.Tests/JsonConfigStoreTests.cs`

**Interfaces:**
- Produces: `static class JsonConfigStore` with `T Load<T>(string path) where T : new()` (never throws; missing/corrupt -> defaults) and `bool Save<T>(string path, T value, Action<string>? onError = null)` (temp file then atomic move). Used by Task 10.

- [ ] **Step 1: Write the failing tests** (`tests/SlayTheSpireOverlay.Tests/JsonConfigStoreTests.cs`)


```csharp
using SlayTheSpireMod.Common;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class JsonConfigStoreTests
{
    public sealed class Cfg
    {
        public bool Enabled { get; set; } = true;

        public int Depth { get; set; } = 1;
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), "sts2cfg-" + Guid.NewGuid().ToString("N"), "cfg.json");

    [TestMethod]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var cfg = JsonConfigStore.Load<Cfg>(TempPath());
        Assert.IsTrue(cfg.Enabled);
        Assert.AreEqual(1, cfg.Depth);
    }

    [TestMethod]
    public void SaveThenLoad_RoundTripsAndLeavesNoTempFile()
    {
        string path = TempPath();
        Assert.IsTrue(JsonConfigStore.Save(path, new Cfg { Enabled = false, Depth = 3 }));
        var loaded = JsonConfigStore.Load<Cfg>(path);
        Assert.IsFalse(loaded.Enabled);
        Assert.AreEqual(3, loaded.Depth);
        Assert.IsFalse(File.Exists(path + ".tmp"));
    }

    [TestMethod]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        string path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");
        Assert.AreEqual(1, JsonConfigStore.Load<Cfg>(path).Depth);
    }

    [TestMethod]
    public void Save_UnwritablePath_ReportsErrorAndReturnsFalse()
    {
        string? error = null;
        string blocker = Path.Combine(Path.GetTempPath(), "sts2cfg-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocker, "i am a file, not a directory");
        bool ok = JsonConfigStore.Save(Path.Combine(blocker, "cfg.json"), new Cfg(), e => error = e);
        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }
}
```

- [ ] **Step 2: Run to verify they fail**

```bash
dn test tests/SlayTheSpireOverlay.Tests 2>&1 | grep -E "error" | head -2
```

Expected: `JsonConfigStore` does not exist.

- [ ] **Step 3: Implement** (`src/SlayTheSpireMod.Common/JsonConfigStore.cs`)


```csharp
using System.Text.Json;

namespace SlayTheSpireMod.Common;

/// <summary>Loads and atomically saves a mod's JSON config. A missing or corrupt file yields defaults.</summary>
public static class JsonConfigStore
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    public static T Load<T>(string path)
        where T : new()
    {
        try
        {
            if (!File.Exists(path)) return new T();
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? new T();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new T();
        }
    }

    /// <summary>Writes via a temp file then moves it into place. Returns false (and reports) on failure.</summary>
    public static bool Save<T>(string path, T value, Action<string>? onError = null)
    {
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, IndentedOptions));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            onError?.Invoke($"Error saving {Path.GetFileName(path)}: {ex.Message}");
            return false;
        }
    }
}
```

- [ ] **Step 4: Run to verify they pass**

```bash
dn test tests/SlayTheSpireOverlay.Tests 2>&1 | grep -E "error|Passed|Failed"
```

Expected: `Passed!`.

- [ ] **Step 5: Commit**

```bash
git add src/SlayTheSpireMod.Common/JsonConfigStore.cs tests/SlayTheSpireOverlay.Tests/JsonConfigStoreTests.cs
git commit -m "feat(common): add JsonConfigStore with defaults-on-corruption and atomic save

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task 7: `PatchRegistrar` (shared, tested)

**Files:**
- Create: `src/SlayTheSpireMod.Common/PatchRegistrar.cs`, `tests/SlayTheSpireOverlay.Tests/PatchRegistrarTests.cs`

**Interfaces:**
- Produces: `sealed class PatchRegistrar(string logPrefix, Action<string> log)` with `bool TryApply(string name, Action apply)`, `int AppliedCount`, `int SkippedCount`, `IReadOnlyList<(string Name, string Reason)> Skipped`, `string Summary()`. Used by Task 10.

- [ ] **Step 1: Write the failing test** (`tests/SlayTheSpireOverlay.Tests/PatchRegistrarTests.cs`)


```csharp
using SlayTheSpireMod.Common;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class PatchRegistrarTests
{
    [TestMethod]
    public void TryApply_OneFailureDoesNotStopOthersAndIsReported()
    {
        var log = new List<string>();
        var registrar = new PatchRegistrar("[T]", log.Add);
        int ran = 0;

        Assert.IsTrue(registrar.TryApply("a", () => ran++));
        Assert.IsFalse(registrar.TryApply("b", () => throw new MissingMemberException("NCard.Nope")));
        Assert.IsTrue(registrar.TryApply("c", () => ran++));

        Assert.AreEqual(2, ran);
        Assert.AreEqual(2, registrar.AppliedCount);
        Assert.AreEqual(1, registrar.SkippedCount);
        Assert.AreEqual("b", registrar.Skipped[0].Name);
        Assert.AreEqual("[T] patch 'b' skipped: NCard.Nope", log[0]);
        Assert.AreEqual("[T] patches: applied 2, skipped 1", registrar.Summary());
    }
}
```

- [ ] **Step 2: Run to verify it fails**

```bash
dn test tests/SlayTheSpireOverlay.Tests 2>&1 | grep -E "error" | head -2
```

Expected: `PatchRegistrar` does not exist.

- [ ] **Step 3: Implement** (`src/SlayTheSpireMod.Common/PatchRegistrar.cs`)


```csharp
namespace SlayTheSpireMod.Common;

/// <summary>
/// Runs patch-application steps independently so one missing game member cannot stop the rest,
/// and reports exactly which patches were skipped and why.
/// </summary>
public sealed class PatchRegistrar
{
    private readonly Action<string> _log;
    private readonly string _prefix;
    private readonly List<string> _applied = [];
    private readonly List<(string Name, string Reason)> _skipped = [];

    public PatchRegistrar(string logPrefix, Action<string> log)
    {
        _prefix = logPrefix;
        _log = log;
    }

    public int AppliedCount => _applied.Count;

    public int SkippedCount => _skipped.Count;

    public IReadOnlyList<(string Name, string Reason)> Skipped => _skipped;

    public bool TryApply(string name, Action apply)
    {
        try
        {
            apply();
            _applied.Add(name);
            return true;
        }
        catch (Exception ex)
        {
            _skipped.Add((name, ex.Message));
            _log($"{_prefix} patch '{name}' skipped: {ex.Message}");
            return false;
        }
    }

    public string Summary() => $"{_prefix} patches: applied {AppliedCount}, skipped {SkippedCount}";
}
```

- [ ] **Step 4: Run to verify it passes**

```bash
dn test tests/SlayTheSpireOverlay.Tests 2>&1 | grep -E "error|Passed|Failed"
```

Expected: `Passed!`.

- [ ] **Step 5: Commit**

```bash
git add src/SlayTheSpireMod.Common/PatchRegistrar.cs tests/SlayTheSpireOverlay.Tests/PatchRegistrarTests.cs
git commit -m "feat(common): add PatchRegistrar so one failing patch is reported, not fatal

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task 8: Move fog geometry into `Core` and test it

Moves the frontier/rectangle math out of `Sts2MapInspector.ApplyDarkFogOverlay` (untestable) into a pure function. **Runs after the gate** (another session is editing `Sts2MapInspector.cs`). When you rebase onto its work, the dark-fog method may already differ from the version below (for example `DarkFogColor` taking its alpha from `config.FogOpacity`, issue #13). Keep that behavior: build the color from the config inside `ApplyDarkFogOverlay` and pass it to `CreateFadeRect`/the solid rect instead of the constant, then confirm with the tests that geometry is unchanged. The geometry numbers (`FadeLength`, `FrontierPadding`, `Extent`) are the only things that move into `Core`.

**Files:**
- Create: `src/SlayTheSpireMapFog.Core/Services/FogGeometry.cs`, `tests/SlayTheSpireOverlay.Tests/FogGeometryTests.cs`
- Modify: `src/SlayTheSpireMapFog.Godot/Sts2MapInspector.cs`

**Interfaces:**
- Produces: `FogGeometry.Compute(IReadOnlyCollection<float> revealedNodeYs, float? bossY, float minX, float maxX) -> FogPlan?`; records `FogRect(float X, float Y, float Width, float Height)` and `FogPlan(FogRect Fade, FogRect Solid, bool FogAbove)`.

- [ ] **Step 1: Write the failing tests** (`tests/SlayTheSpireOverlay.Tests/FogGeometryTests.cs`)


```csharp
using SlayTheSpireMapFog.Core.Services;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class FogGeometryTests
{
    [TestMethod]
    public void Compute_NothingRevealed_ReturnsNull()
    {
        Assert.IsNull(FogGeometry.Compute([], bossY: 0f, minX: 0f, maxX: 100f));
    }

    [TestMethod]
    public void Compute_BossAbove_FogSpreadsUpwardFromFrontier()
    {
        var plan = FogGeometry.Compute([500f, 400f], bossY: -2000f, minX: 100f, maxX: 300f)!.Value;
        Assert.IsTrue(plan.FogAbove);
        Assert.AreEqual(new FogRect(-2900f, 70f, 6200f, 260f), plan.Fade);
        Assert.AreEqual(new FogRect(-2900f, -5930f, 6200f, 6000f), plan.Solid);
    }

    [TestMethod]
    public void Compute_BossBelow_FogSpreadsDownward()
    {
        var plan = FogGeometry.Compute([100f, 200f], bossY: 1000f, minX: 100f, maxX: 300f)!.Value;
        Assert.IsFalse(plan.FogAbove);
        Assert.AreEqual(270f, plan.Fade.Y);
        Assert.AreEqual(530f, plan.Solid.Y);
    }

    [TestMethod]
    public void Compute_UnknownBoss_DefaultsToUpward()
    {
        Assert.IsTrue(FogGeometry.Compute([300f], bossY: null, minX: 0f, maxX: 10f)!.Value.FogAbove);
    }
}
```

- [ ] **Step 2: Run to verify they fail**

```bash
dn test tests/SlayTheSpireOverlay.Tests 2>&1 | grep -E "error" | head -2
```

Expected: `FogGeometry` does not exist.

- [ ] **Step 3: Implement** (`src/SlayTheSpireMapFog.Core/Services/FogGeometry.cs`)


```csharp
namespace SlayTheSpireMapFog.Core.Services;

public readonly record struct FogRect(float X, float Y, float Width, float Height);

/// <summary>Fade is the soft edge next to the explored area; Solid is the fully dark region beyond it.</summary>
public readonly record struct FogPlan(FogRect Fade, FogRect Solid, bool FogAbove);

public static class FogGeometry
{
    public const float FadeLength = 260f;
    public const float FrontierPadding = 70f;
    public const float Extent = 6000f;

    /// <summary>
    /// Computes the dark-fog rectangles. Fog spreads from the explored frontier toward the boss;
    /// when the boss position is unknown it spreads upward (the map normally climbs upward).
    /// Returns null when nothing is revealed yet.
    /// </summary>
    public static FogPlan? Compute(IReadOnlyCollection<float> revealedNodeYs, float? bossY, float minX, float maxX)
    {
        ArgumentNullException.ThrowIfNull(revealedNodeYs);
        if (revealedNodeYs.Count == 0) return null;

        float revealedMinY = revealedNodeYs.Min();
        float revealedMaxY = revealedNodeYs.Max();
        bool fogAbove = bossY is null || bossY.Value <= (revealedMinY + revealedMaxY) / 2f;
        float frontier = fogAbove ? revealedMinY - FrontierPadding : revealedMaxY + FrontierPadding;
        float left = minX - (Extent / 2f);
        float width = maxX - minX + Extent;

        float fadeTop = fogAbove ? frontier - FadeLength : frontier;
        float solidTop = fogAbove ? fadeTop - Extent : fadeTop + FadeLength;
        return new FogPlan(
            new FogRect(left, fadeTop, width, FadeLength),
            new FogRect(left, solidTop, width, Extent),
            fogAbove);
    }
}
```

- [ ] **Step 4: Run to verify they pass**

```bash
dn test tests/SlayTheSpireOverlay.Tests 2>&1 | grep -E "error|Passed|Failed"
```

Expected: `Passed!`.

- [ ] **Step 5: Make the Godot adapter thin**

In `src/SlayTheSpireMapFog.Godot/Sts2MapInspector.cs`, replace everything from the line `private const string DarkFogOverlayName` up to (not including) `public static void ApplyMaskToNormalPoint(` with:


```csharp
    private const string DarkFogOverlayName = "MapFogDarkOverlay";
    private static readonly Color DarkFogColor = new(0.03f, 0.04f, 0.07f, 0.9f);

    private static void ApplyDarkFogOverlay(
        List<(FogCoord Coord, Control Node)> pointNodes,
        List<FogCoord> bossCoords,
        MapFogVisibility visibility,
        MapFogConfig config)
    {
        Node? parent = pointNodes.Count > 0 ? pointNodes[0].Node.GetParent() : null;
        if (parent == null || !GodotObject.IsInstanceValid(parent)) return;

        var existing = parent.GetNodeOrNull(DarkFogOverlayName);
        if (existing != null && GodotObject.IsInstanceValid(existing))
        {
            parent.RemoveChild(existing);
            existing.QueueFree();
        }

        if (!config.Enabled || !config.DarkFog || config.Mode != FogMode.FullFog) return;

        var bosses = new HashSet<FogCoord>(bossCoords);
        float minX = float.MaxValue, maxX = float.MinValue;
        float? bossY = null;
        var revealedYs = new List<float>();
        foreach (var (coord, node) in pointNodes)
        {
            if (node.GetParent() != parent) continue;
            minX = Math.Min(minX, node.Position.X);
            maxX = Math.Max(maxX, node.Position.X);
            if (bosses.Contains(coord)) bossY = node.Position.Y;
            else if (visibility.IsRevealed(coord)) revealedYs.Add(node.Position.Y);
        }

        var plan = FogGeometry.Compute(revealedYs, bossY, minX, maxX);
        if (plan == null) return;

        var overlay = new Control { Name = DarkFogOverlayName, MouseFilter = Control.MouseFilterEnum.Ignore };
        overlay.AddChild(CreateFadeRect(plan.Value));
        overlay.AddChild(new ColorRect
        {
            Name = "FogSolid",
            Color = DarkFogColor,
            Position = new Vector2(plan.Value.Solid.X, plan.Value.Solid.Y),
            Size = new Vector2(plan.Value.Solid.Width, plan.Value.Solid.Height),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        parent.AddChild(overlay);
        parent.MoveChild(overlay, 0);
    }

    private static TextureRect CreateFadeRect(FogPlan plan)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(DarkFogColor.R, DarkFogColor.G, DarkFogColor.B, 0f));
        gradient.SetColor(1, DarkFogColor);
        var texture = new GradientTexture2D
        {
            Gradient = gradient,
            Width = 4,
            Height = 256,
            FillFrom = new Vector2(0f, plan.FogAbove ? 1f : 0f),
            FillTo = new Vector2(0f, plan.FogAbove ? 0f : 1f)
        };
        return new TextureRect
        {
            Name = "FogFade",
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Position = new Vector2(plan.Fade.X, plan.Fade.Y),
            Size = new Vector2(plan.Fade.Width, plan.Fade.Height),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }
```

- [ ] **Step 6: Build and run all tests**

```bash
dn build src/SlayTheSpireMapFog.Godot -c Release 2>&1 | tail -3
dn test tests/SlayTheSpireOverlay.Tests 2>&1 | grep -E "Passed|Failed"
```

Expected: `Build succeeded`, tests pass. (The constants `FogFadeLength`, `FogFrontierPadding`, `FogExtent` no longer exist in the Godot file; they live in `FogGeometry`.)

- [ ] **Step 7: Commit**

```bash
git add src/SlayTheSpireMapFog.Core/Services/FogGeometry.cs src/SlayTheSpireMapFog.Godot/Sts2MapInspector.cs tests/SlayTheSpireOverlay.Tests/FogGeometryTests.cs
git commit -m "refactor(fog): move dark-fog geometry into Core with tests

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task 9: Hotkey edge detection in `Core` (pure part)

The adapter change is Task 11 (it edits `RoomOverlayFactory.cs`, which is in flight). This task only adds and tests the pure pieces so Task 11 is a small mechanical edit.

**Files:**
- Create: `src/SlayTheSpireOverlay.Core/Services/InputEdge.cs`, `tests/SlayTheSpireOverlay.Tests/InputEdgeTests.cs`

**Interfaces:**
- Produces: `sealed class EdgeDetector { bool Update(bool down) }` (true only on the not-down -> down transition) and `sealed class FrameGate { bool ShouldProcess(ulong frame) }` (false for a repeated non-zero frame; frame 0 always passes). Used by Task 11.

- [ ] **Step 1: Write the failing tests** (`tests/SlayTheSpireOverlay.Tests/InputEdgeTests.cs`)


```csharp
using SlayTheSpireOverlay.Core.Services;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class InputEdgeTests
{
    [TestMethod]
    public void EdgeDetector_FiresOncePerPress()
    {
        var edge = new EdgeDetector();
        Assert.IsFalse(edge.Update(false));
        Assert.IsTrue(edge.Update(true));
        Assert.IsFalse(edge.Update(true));
        Assert.IsFalse(edge.Update(false));
        Assert.IsTrue(edge.Update(true));
    }

    [TestMethod]
    public void FrameGate_BlocksRepeatsWithinAFrameButNotFrameZero()
    {
        var gate = new FrameGate();
        Assert.IsTrue(gate.ShouldProcess(10));
        Assert.IsFalse(gate.ShouldProcess(10));
        Assert.IsTrue(gate.ShouldProcess(11));
        Assert.IsTrue(gate.ShouldProcess(0));
        Assert.IsTrue(gate.ShouldProcess(0));
    }
}
```

- [ ] **Step 2: Run to verify they fail**

```bash
dn test tests/SlayTheSpireOverlay.Tests 2>&1 | grep -E "error" | head -2
```

Expected: `EdgeDetector`/`FrameGate` do not exist.

- [ ] **Step 3: Implement** (`src/SlayTheSpireOverlay.Core/Services/InputEdge.cs`)


```csharp
namespace SlayTheSpireOverlay.Core.Services;

/// <summary>Turns a held/not-held signal into a single "just pressed" pulse.</summary>
public sealed class EdgeDetector
{
    private bool _previous;

    public bool Update(bool down)
    {
        bool pressed = down && !_previous;
        _previous = down;
        return pressed;
    }
}

/// <summary>Lets polling code run at most once per engine frame. Frame 0 (outside the loop) always passes.</summary>
public sealed class FrameGate
{
    private ulong _lastFrame;

    public bool ShouldProcess(ulong frame)
    {
        if (frame != 0 && frame == _lastFrame) return false;
        _lastFrame = frame;
        return true;
    }
}
```

- [ ] **Step 4: Run to verify they pass**

```bash
dn test tests/SlayTheSpireOverlay.Tests 2>&1 | grep -E "error|Passed|Failed"
```

Expected: `Passed!`.

- [ ] **Step 5: Commit**

```bash
git add src/SlayTheSpireOverlay.Core/Services/InputEdge.cs tests/SlayTheSpireOverlay.Tests/InputEdgeTests.cs
git commit -m "feat(core): add EdgeDetector and FrameGate for hotkey polling

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## ===== GATE: Tasks 8 and 10-14 edit files the other session is changing. Wait until its work is merged, then `git fetch && git rebase origin/main` and re-read each file before editing. =====

---

## Task 10: Adopt the shared helpers in both mods (and remove the dead `Clear` hook)

**Files:**
- Create: `src/SlayTheSpireMod.Common/GameHooks/HarmonyPatchHelper.cs`, `src/SlayTheSpireMod.Common/GameHooks/ModScreenSupport.cs`
- Modify: both `*.Godot.csproj`, `src/SlayTheSpireMapFog.Godot/ModEntry.cs`, `src/SlayTheSpireMapFog.Godot/Sts2MapInspector.cs`, `src/SlayTheSpireOverlay.Godot/ModEntry.cs`, `src/SlayTheSpireOverlay.Godot/Sts2RuntimeInspector.cs`, `tests/SlayTheSpireOverlay.ArchitectureTests/DependencyTests.cs`

**Interfaces:**
- Consumes: `ReflectionAccess`, `JsonConfigStore`, `PatchRegistrar` (Tasks 5-7).
- Produces: `HarmonyPatchHelper.Postfix(Harmony, string typeName, string[] methodNames, Type handlerType, string handlerName)`, `.Prefix(Harmony, string typeName, string methodName, Type handlerType, string handlerName)`, `.PrefixOverload(Harmony, string typeName, string methodName, Type[] parameterTypes, Type handlerType, string handlerName)`, `.PatchAttributeClasses(Harmony, Assembly, PatchRegistrar)`; `ModScreenSupport.IsOurMod(object? mod, string idFragment)`, `.EnableModdingButtonInRun(object?)`, `.GetOrCreateRunModdingScreen(object?)`, constant `RunModdingScreenNodeName = "RunModdingScreen"`.

- [ ] **Step 1: Add the Godot-dependent shared files**

`src/SlayTheSpireMod.Common/GameHooks/HarmonyPatchHelper.cs`:


```csharp
using System.Reflection;
using HarmonyLib;

namespace SlayTheSpireMod.Common.GameHooks;

/// <summary>
/// Harmony registration that reports problems instead of silently skipping. Each call patches every
/// method it can find, then throws <see cref="MissingMemberException"/> naming what was absent, so a
/// <see cref="PatchRegistrar"/> can log it without stopping other patches.
/// </summary>
public static class HarmonyPatchHelper
{
    public static void Postfix(Harmony harmony, string typeName, string[] methodNames, Type handlerType, string handlerName)
    {
        var handler = new HarmonyMethod(RequireHandler(handlerType, handlerName));
        PatchEach(typeName, methodNames, method => harmony.Patch(method, postfix: handler));
    }

    public static void Prefix(Harmony harmony, string typeName, string methodName, Type handlerType, string handlerName)
    {
        var handler = new HarmonyMethod(RequireHandler(handlerType, handlerName));
        PatchEach(typeName, [methodName], method => harmony.Patch(method, prefix: handler));
    }

    /// <summary>Patches a method that is selected by parameter types (for overloaded game methods).</summary>
    public static void PrefixOverload(Harmony harmony, string typeName, string methodName, Type[] parameterTypes, Type handlerType, string handlerName)
    {
        var type = ReflectionAccess.GetCachedType(typeName) ?? throw new MissingMemberException(typeName);
        var method = AccessTools.Method(type, methodName, parameterTypes)
            ?? throw new MissingMemberException($"{typeName}.{methodName}");
        harmony.Patch(method, prefix: new HarmonyMethod(RequireHandler(handlerType, handlerName)));
    }

    /// <summary>Applies every [HarmonyPatch] class in the assembly separately so one broken class cannot block the rest.</summary>
    public static void PatchAttributeClasses(Harmony harmony, Assembly assembly, PatchRegistrar registrar)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length == 0) continue;
            registrar.TryApply(type.Name, () => harmony.CreateClassProcessor(type).Patch());
        }
    }

    private static MethodInfo RequireHandler(Type handlerType, string handlerName) =>
        AccessTools.Method(handlerType, handlerName)
        ?? throw new MissingMemberException($"{handlerType.Name}.{handlerName} (handler)");

    private static void PatchEach(string typeName, string[] methodNames, Action<MethodBase> patch)
    {
        var type = ReflectionAccess.GetCachedType(typeName) ?? throw new MissingMemberException(typeName);
        var missing = new List<string>();
        foreach (string name in methodNames)
        {
            var method = AccessTools.Method(type, name);
            if (method == null) missing.Add(name);
            else patch(method);
        }

        if (missing.Count > 0) throw new MissingMemberException($"{typeName}.{string.Join('/', missing)}");
    }
}
```

`src/SlayTheSpireMod.Common/GameHooks/ModScreenSupport.cs`:


```csharp
using Godot;

namespace SlayTheSpireMod.Common.GameHooks;

/// <summary>
/// Plumbing every mod needs to show its own settings inside the game's Mods screen, including during a run.
/// Both mods share one hidden Mods-screen node (<see cref="RunModdingScreenNodeName"/>) so it is only created once.
/// </summary>
public static class ModScreenSupport
{
    public const string RunModdingScreenNodeName = "RunModdingScreen";

    private const string ModdingScreenType = "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModdingScreen";

    /// <summary>True when the mod's manifest id (or pck name) contains <paramref name="idFragment"/>.</summary>
    public static bool IsOurMod(object? mod, string idFragment)
    {
        if (mod == null) return false;
        try
        {
            var manifest = ReflectionAccess.GetMember(mod, "manifest", "Manifest");
            string? id = manifest != null ? ReflectionAccess.GetMember(manifest, "id", "Id") as string : null;
            if (!string.IsNullOrEmpty(id) && id.Contains(idFragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string? pck = ReflectionAccess.GetMember(mod, "pckName", "PckName") as string;
            return !string.IsNullOrEmpty(pck) && pck.Contains(idFragment, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void EnableModdingButtonInRun(object? settingsScreen)
    {
        if (settingsScreen is not Node screenNode || !GodotObject.IsInstanceValid(screenNode)) return;
        try
        {
            var moddingRow = screenNode.GetNodeOrNull<CanvasItem>("%Modding");
            if (moddingRow != null && GodotObject.IsInstanceValid(moddingRow)) moddingRow.Visible = true;

            var moddingDivider = screenNode.GetNodeOrNull<CanvasItem>("%ModdingDivider");
            if (moddingDivider != null && GodotObject.IsInstanceValid(moddingDivider)) moddingDivider.Visible = true;

            var button = ReflectionAccess.GetMember(settingsScreen, "_moddingScreenButton", "ModdingScreenButton")
                ?? screenNode.GetNodeOrNull("%ModdingButton");
            if (button != null)
            {
                ReflectionAccess.GetCachedMethod(button.GetType(), "Enable")?.Invoke(button, null);
            }
        }
        catch (Exception)
        {
            // The settings screen layout is game-owned; failing to unhide the button must never break the game.
        }
    }

    public static object? GetOrCreateRunModdingScreen(object? submenuStack)
    {
        if (submenuStack is not Node stackNode || !GodotObject.IsInstanceValid(stackNode)) return null;
        try
        {
            var existing = stackNode.GetNodeOrNull(RunModdingScreenNodeName);
            if (existing != null && GodotObject.IsInstanceValid(existing))
            {
                return existing;
            }

            var screenType = ReflectionAccess.GetCachedType(ModdingScreenType);
            var create = screenType != null ? ReflectionAccess.GetCachedMethod(screenType, "Create") : null;
            if (create?.Invoke(null, null) is Control screen && GodotObject.IsInstanceValid(screen))
            {
                screen.Name = RunModdingScreenNodeName;
                screen.Visible = false;
                stackNode.AddChild(screen);
                return screen;
            }
        }
        catch (Exception)
        {
            // Fall through: the game will build its own submenu if we cannot.
        }

        return null;
    }
}
```

- [ ] **Step 2: Link the shared folder into both mod projects**

In **both** `*.Godot.csproj` files add (next to the existing `Core` link):

```xml
  <ItemGroup>
    <Compile Include="..\SlayTheSpireMod.Common\**\*.cs" Link="Common\%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>
```

- [ ] **Step 3: Fog mod: replace `ModEntry.cs` (verified: builds with zero warnings, 316 -> 91 lines)**

Replace the whole file `src/SlayTheSpireMapFog.Godot/ModEntry.cs` with:


```csharp
using Godot;
using HarmonyLib;
using SlayTheSpireMapFog.Core.Options;
using SlayTheSpireMod.Common;
using SlayTheSpireMod.Common.GameHooks;

namespace SlayTheSpireMapFog.Godot;

[MegaCrit.Sts2.Core.Modding.ModInitializer("Initialize")]
public static class ModEntry
{
    private const string LogPrefix = "[STS2 MapFog]";
    private const string ModIdFragment = "SlayTheSpireMapFog";
    private const string MapScreenType = "MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen";
    private const string MapPointType = "MegaCrit.Sts2.Core.Nodes.Screens.Map.NNormalMapPoint";
    private const string ModInfoType = "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModInfoContainer";
    private const string SettingsScreenType = "MegaCrit.Sts2.Core.Nodes.Screens.Settings.NSettingsScreen";
    private const string SubmenuStackType = "MegaCrit.Sts2.Core.Nodes.Screens.NRunSubmenuStack";

    private static string _configPath = string.Empty;

    public static MapFogConfig Config { get; private set; } = new();

    public static void Initialize()
    {
        GD.Print($"{LogPrefix} ModEntry.Initialize called!");

        _configPath = System.IO.Path.Combine(ProjectSettings.GlobalizePath("user://"), "map_fog_config.json");
        bool configExisted = System.IO.File.Exists(_configPath);
        Config = JsonConfigStore.Load<MapFogConfig>(_configPath);
        if (!configExisted) SaveConfig();

        var harmony = new Harmony("SlayTheSpireMapFog.Godot");
        var registrar = new PatchRegistrar(LogPrefix, message => GD.Print(message));

        registrar.TryApply("NMapScreen", () => HarmonyPatchHelper.Postfix(
            harmony, MapScreenType, ["SetMap", "RecalculateTravelability", "RefreshAllPointVisuals"], typeof(ModEntry), nameof(OnMapScreenUpdated)));
        registrar.TryApply("NNormalMapPoint.icons", () => HarmonyPatchHelper.Postfix(
            harmony, MapPointType, ["UpdateIcon", "RefreshMarkedIconVisibility"], typeof(ModEntry), nameof(OnNormalMapPointIconUpdated)));
        registrar.TryApply("NNormalMapPoint.highlight", () => HarmonyPatchHelper.Prefix(
            harmony, MapPointType, "OnHighlightPointType", typeof(ModEntry), nameof(OnNormalMapPointHighlightPrefix)));
        registrar.TryApply("NModInfoContainer.Fill", () => HarmonyPatchHelper.Postfix(
            harmony, ModInfoType, ["Fill"], typeof(ModEntry), nameof(OnModInfoFill)));
        registrar.TryApply("NSettingsScreen._Ready", () => HarmonyPatchHelper.Postfix(
            harmony, SettingsScreenType, ["_Ready"], typeof(ModEntry), nameof(OnSettingsScreenReady)));
        registrar.TryApply("NRunSubmenuStack.GetSubmenuType", () => HarmonyPatchHelper.PrefixOverload(
            harmony, SubmenuStackType, "GetSubmenuType", [typeof(Type)], typeof(ModEntry), nameof(OnRunSubmenuStackGetSubmenuType)));
        HarmonyPatchHelper.PatchAttributeClasses(harmony, typeof(ModEntry).Assembly, registrar);

        GD.Print(registrar.Summary());
    }

    public static void OnMapScreenUpdated(object __instance) => Sts2MapInspector.ApplyMapFog(__instance, Config);

    public static void OnNormalMapPointIconUpdated(object __instance) => Sts2MapInspector.ApplyMaskToNormalPoint(__instance);

    public static bool OnNormalMapPointHighlightPrefix(object __instance) => !Sts2MapInspector.IsPointFogHidden(__instance);

    public static void OnSettingsScreenReady(object __instance) => ModScreenSupport.EnableModdingButtonInRun(__instance);

    public static bool OnRunSubmenuStackGetSubmenuType(object __instance, Type type, ref object? __result)
    {
        if (type?.FullName != "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModdingScreen") return true;
        var screen = ModScreenSupport.GetOrCreateRunModdingScreen(__instance);
        if (screen == null) return true;
        __result = screen;
        return false;
    }

    public static void OnModInfoFill(object __instance, object? mod)
    {
        if (__instance is not Node container || !GodotObject.IsInstanceValid(container)) return;
        if (!ModScreenSupport.IsOurMod(mod, ModIdFragment))
        {
            UI.MapFogSettingsFactory.HideModSettingsPanel(container);
            return;
        }

        UI.MapFogSettingsFactory.AttachOrUpdateModSettingsPanel(
            container,
            Config,
            () =>
            {
                SaveConfig();
                Sts2MapInspector.RefreshActiveMapInScene(container, Config);
            });
    }

    private static void SaveConfig() =>
        JsonConfigStore.Save(_configPath, Config, message => GD.PrintErr($"{LogPrefix} {message}"));
}
```

- [ ] **Step 4: Fog mod: remove the duplicated plumbing from `Sts2MapInspector.cs`**

Delete these members and nothing else: `IsOurMod`, `EnableModdingButtonInRun`, `GetOrCreateRunModdingScreen`, `GetCachedType`, `GetCachedMethod`, `GetMember`, and the three `ConcurrentDictionary` cache fields (`TypeCache`, `MethodCache`, `MemberCache`). Then replace the `using System.Collections.Concurrent;` and `using System.Reflection;` lines with nothing and add `using static SlayTheSpireMod.Common.ReflectionAccess;` below the other usings, so existing calls to `GetMember(...)`, `GetCachedType(...)`, `GetCachedMethod(...)` keep compiling unchanged.

```bash
dn build src/SlayTheSpireMapFog.Godot -c Release 2>&1 | tail -3
```

Expected: `Build succeeded`, 0 warnings.

- [ ] **Step 5: Overlay mod: the same three edits**

In `src/SlayTheSpireOverlay.Godot/Sts2RuntimeInspector.cs` delete `IsOurMod`, `EnableModdingButtonInRun`, `GetOrCreateRunModdingScreen`, `GetCachedType`, `GetCachedMethod`, `GetStaticMember`, `GetMember` and their cache dictionaries (`TypeCache`, `MethodCache`, `StaticMemberCache`, `MemberCache`); add `using static SlayTheSpireMod.Common.ReflectionAccess;`.

In `src/SlayTheSpireOverlay.Godot/ModEntry.cs`:
1. Add `using SlayTheSpireMod.Common;` and `using SlayTheSpireMod.Common.GameHooks;`.
2. Replace the config load block in `Initialize` (the `if (File.Exists(_configPath)) {...} else { SaveConfig(); }` section) with:

```csharp
        bool configExisted = System.IO.File.Exists(_configPath);
        Config = JsonConfigStore.Load<OverlayConfig>(_configPath);
        if (!configExisted) SaveConfig();
```

and replace the body of `SaveConfig` with `JsonConfigStore.Save(_configPath, Config, message => GD.PrintErr($"[STS2 Overlay] {message}"));`. Delete the now-unused `IndentedJsonOptions` field.
3. Replace the patch-registration `try { ... }` block in `Initialize` (from `var harmony = new Harmony(...)` through the `GD.Print("... patches applied successfully!")` line and its `catch`) with the table-driven version below, and delete `PatchIfPresent`, `PatchHolderFocusIfPresent`, `PatchRunSubmenuStackIfPresent`, and the `OnModInfoClear` handler:

```csharp
        var harmony = new Harmony("SlayTheSpireOverlay.Godot");
        var registrar = new PatchRegistrar("[STS2 Overlay]", message => GD.Print(message));

        const string nodes = "MegaCrit.Sts2.Core.Nodes.";
        (string Type, string[] Methods, string Handler)[] postfixes =
        [
            (nodes + "Cards.NCard", ["UpdateVisuals"], nameof(OnCardVisualsUpdated)),
            (nodes + "Relics.NRelic", ["Reload"], nameof(OnRelicReloaded)),
            (nodes + "Potions.NPotion", ["_Ready", "set_Model", "Reload"], nameof(OnPotionReady)),
            (nodes + "Rewards.NRewardButton", ["_Ready", "Reload", "SetReward"], nameof(OnRewardButtonReady)),
            (nodes + "Rooms.NCombatRoom", ["_Ready"], nameof(OnCombatRoomReady)),
            (nodes + "Rooms.NMerchantRoom", ["_Ready"], nameof(OnMerchantRoomReady)),
            (nodes + "Rooms.NEventRoom", ["_Ready"], nameof(OnEventRoomReady)),
            (nodes + "Rooms.NTreasureRoom", ["_Ready"], nameof(OnGenericRoomReady)),
            (nodes + "Rooms.NRestSiteRoom", ["_Ready"], nameof(OnGenericRoomReady)),
            (nodes + "Rooms.NMapRoom", ["_Ready"], nameof(OnGenericRoomReady)),
            (nodes + "NRun", ["_Ready"], nameof(OnGenericRoomReady)),
            (nodes + "NGame", ["_Ready"], nameof(OnGenericRoomReady)),
            (nodes + "Screens.ModdingScreen.NModInfoContainer", ["Fill"], nameof(OnModInfoFill)),
            (nodes + "Screens.Settings.NSettingsScreen", ["_Ready"], nameof(OnSettingsScreenReady))
        ];
        foreach (var (type, methods, handler) in postfixes)
        {
            registrar.TryApply($"{type[(type.LastIndexOf('.') + 1)..]}.{string.Join('/', methods)}",
                () => HarmonyPatchHelper.Postfix(harmony, type, methods, typeof(ModEntry), handler));
        }

        string[] focusHolders =
        [
            nodes + "Cards.Holders.NCardHolder",
            nodes + "Screens.Shops.NMerchantSlot",
            nodes + "Events.NEventOptionButton",
            nodes + "Relics.NRelicBasicHolder",
            nodes + "Screens.TreasureRoomRelic.NTreasureRoomRelicHolder",
            nodes + "Rewards.NRewardButton"
        ];
        foreach (string holder in focusHolders)
        {
            registrar.TryApply($"{holder[(holder.LastIndexOf('.') + 1)..]}.focus", () =>
            {
                HarmonyPatchHelper.Postfix(harmony, holder, ["OnFocus"], typeof(ModEntry), nameof(OnItemHolderFocused));
                HarmonyPatchHelper.Postfix(harmony, holder, ["OnUnfocus"], typeof(ModEntry), nameof(OnItemHolderUnfocused));
            });
        }

        registrar.TryApply("NRunSubmenuStack.GetSubmenuType", () => HarmonyPatchHelper.PrefixOverload(
            harmony, nodes + "Screens.NRunSubmenuStack", "GetSubmenuType", [typeof(Type)], typeof(ModEntry), nameof(OnRunSubmenuStackGetSubmenuType)));
        HarmonyPatchHelper.PatchAttributeClasses(harmony, typeof(ModEntry).Assembly, registrar);
        GD.Print(registrar.Summary());
```

4. In the three handlers `OnSettingsScreenReady`, `OnRunSubmenuStackGetSubmenuType`, `OnModInfoFill` replace `Sts2RuntimeInspector.EnableModdingButtonInRun(...)`, `Sts2RuntimeInspector.GetOrCreateRunModdingScreen(...)`, `Sts2RuntimeInspector.IsOurMod(mod)` with `ModScreenSupport.EnableModdingButtonInRun(...)`, `ModScreenSupport.GetOrCreateRunModdingScreen(...)`, `ModScreenSupport.IsOurMod(mod, "SlayTheSpireOverlay")`.

If the file at your rebase point has more `PatchIfPresent(...)` calls than the table above, add them to the table; the compat checker in Step 7 tells you if anything is missed.

- [ ] **Step 6: Add the architecture rule for the pure shared layer**

Append to `tests/SlayTheSpireOverlay.ArchitectureTests/DependencyTests.cs` (inside the class; verified against NetArchTest 1.3.2, including that the same rule fails on `GameHooks`):

```csharp
    [TestMethod]
    public void Common_PureLayer_Should_Not_Depend_On_Godot_Harmony_Or_Sts2()
    {
        foreach (var asm in ModAssemblies)
        {
            var result = Types.InAssembly(asm)
                .That()
                .ResideInNamespaceMatching(@"^SlayTheSpireMod\.Common$")
                .ShouldNot()
                .HaveDependencyOnAny("Godot", "GodotSharp", "MegaCrit", "HarmonyLib")
                .GetResult();

            Assert.IsTrue(
                result.IsSuccessful,
                $"{asm.GetName().Name}: pure Common types depend on engine code: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }
```

- [ ] **Step 7: Verify everything**

```bash
dn test SlayTheSpireOverlay.slnx 2>&1 | grep -E "error|warn|Passed!|Failed"
./scripts/check_sts2_compat.sh; echo "exit=$?"
```

Expected: all test projects pass; the compat check prints `OK ...`, `exit=0`.

- [ ] **Step 8: In-game check (cannot be automated)**

Copy both mods into the game's `mods/` folder, launch, and confirm in `godot.log`: a line `[STS2 Overlay] patches: applied N, skipped 0` and `[STS2 MapFog] patches: applied N, skipped 0` (a nonzero `skipped` must name the patch and reason). Open Settings -> Mods during a run: **both** settings panels appear and each shows only on its own mod; there is exactly one `RunModdingScreen` node (both mods installed together). Change one setting in each panel and confirm the corresponding `*_config.json` updates.

- [ ] **Step 9: Commit**

```bash
git add src/SlayTheSpireMod.Common/GameHooks src/SlayTheSpireOverlay.Godot src/SlayTheSpireMapFog.Godot tests/SlayTheSpireOverlay.ArchitectureTests/DependencyTests.cs
git commit -m "refactor: share plumbing between mods; report skipped patches; drop dead Clear hook

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task 11: Use `EdgeDetector`/`FrameGate` in `PollHotkeys`

**Files:**
- Modify: `src/SlayTheSpireOverlay.Godot/UI/RoomOverlayFactory.cs`

**Interfaces:**
- Consumes: `EdgeDetector.Update(bool)`, `FrameGate.ShouldProcess(ulong)` (Task 9).

- [ ] **Step 1: Replace the state fields**

Replace the `_prevF8Down`, `_prevF7Down`, `_prevF6Down`, `_prevF9Down`, `_lastPolledFrame` fields with:

```csharp
    private static readonly FrameGate PollGate = new();
    private static readonly EdgeDetector F8Edge = new();
    private static readonly EdgeDetector F7Edge = new();
    private static readonly EdgeDetector F6Edge = new();
    private static readonly EdgeDetector F9Edge = new();
```

(add `using SlayTheSpireOverlay.Core.Services;` if missing.)

- [ ] **Step 2: Replace the edge logic, keeping everything else in the method** (key resolution, controller-chord checks, toast) exactly as it is on your rebase point. For each of the four keys, the existing pattern

```csharp
bool xDown = <existing expression>;
if (xDown && !_prevXDown) { <action> }
_prevXDown = xDown;
```

becomes

```csharp
bool xDown = <existing expression>;
if (XEdge.Update(xDown)) { <action> }
```

and the frame guard

```csharp
ulong frame = Engine.GetProcessFrames();
if (frame != 0 && frame == _lastPolledFrame) return;
_lastPolledFrame = frame;
```

becomes

```csharp
if (!PollGate.ShouldProcess(Engine.GetProcessFrames())) return;
```

- [ ] **Step 3: Verify**

```bash
dn build src/SlayTheSpireOverlay.Godot -c Release 2>&1 | tail -3
grep -n "_prevF\|_lastPolledFrame" src/SlayTheSpireOverlay.Godot/UI/RoomOverlayFactory.cs
```

Expected: `Build succeeded`; no `grep` output. In game: F6/F7/F8/F9 (and controller chords if present) each fire once per press, including when held.

- [ ] **Step 4: Commit**

```bash
git add src/SlayTheSpireOverlay.Godot/UI/RoomOverlayFactory.cs
git commit -m "refactor(overlay): use tested EdgeDetector/FrameGate for hotkey polling

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task 12: Split `RoomOverlayFactory` into partial files (no behavior change)

`public static class RoomOverlayFactory` becomes `public static partial class` spread over focused files, so no call site changes. This is a pure move: the verification is that the set of members is identical before and after.

**Files:**
- Modify: `src/SlayTheSpireOverlay.Godot/UI/RoomOverlayFactory.cs`
- Create: `RoomOverlayFactory.Combat.cs`, `RoomOverlayFactory.Merchant.cs`, `RoomOverlayFactory.Events.cs`, `RoomOverlayFactory.Toast.cs`, `RoomOverlayFactory.Hover.cs`, `RoomOverlayFactory.Settings.cs`, `RoomOverlayFactory.Styling.cs` (all in `src/SlayTheSpireOverlay.Godot/UI/`)

- [ ] **Step 1: Record the member inventory before touching anything**

```bash
INV='(public|private|internal) static [^=;{(]+\('
cat src/SlayTheSpireOverlay.Godot/UI/RoomOverlayFactory.cs | grep -oE "$INV" | sed 's/ *($//' | sort > /tmp/rof-before.txt
wc -l /tmp/rof-before.txt
```

- [ ] **Step 2: Make the class partial and move members by group**

Change the declaration to `public static partial class RoomOverlayFactory`. Create each new file with the header

```csharp
using Godot;
// (copy the same using directives the original file has, then remove any a file does not need)

namespace SlayTheSpireOverlay.Godot.UI;

public static partial class RoomOverlayFactory
{
    // moved members here, unchanged
}
```

and cut-and-paste these members (by name) unchanged:

| File | Members |
|---|---|
| `RoomOverlayFactory.cs` (stays) | state fields, `IsOverlayHidden`, `IsDeckTrackerCollapsed`, `PollHotkeys`, and anything else not listed below |
| `.Combat.cs` | `UpdateAttackSummary`, `UpdateDeckTracker`, `BuildDeckStateSignature`, `AddTrackerLine` |
| `.Merchant.cs` | `UpdateMerchantGoldLeft` |
| `.Events.cs` | `AttachEventCommentary` |
| `.Toast.cs` | `ShowAuthorToast` |
| `.Hover.cs` | `FormatHoverCommentary`, `AttachHoverCommentaryPopup`, `SetHoverCommentaryVisibleRecursive`, `SetHoverCommentaryVisible` |
| `.Settings.cs` | `HideModSettingsPanel`, `AttachOrUpdateModSettingsPanel`, `CreateSettingsButton`, and (if present) `UpdateQuickToggleBar` |
| `.Styling.cs` | `CreatePanel`, `GetViewportSize`, other small layout/style helpers |

- [ ] **Step 3: Verify nothing was lost or changed**

```bash
cat src/SlayTheSpireOverlay.Godot/UI/RoomOverlayFactory*.cs | grep -oE "$INV" | sed 's/ *($//' | sort > /tmp/rof-after.txt
diff /tmp/rof-before.txt /tmp/rof-after.txt && echo "MEMBERS IDENTICAL"
dn build src/SlayTheSpireOverlay.Godot -c Release 2>&1 | tail -3
dn test SlayTheSpireOverlay.slnx 2>&1 | grep -E "Passed!|Failed"
wc -l src/SlayTheSpireOverlay.Godot/UI/RoomOverlayFactory*.cs
```

Expected: `MEMBERS IDENTICAL`, `Build succeeded`, tests pass, and no resulting file over about 400 lines.

- [ ] **Step 4: Commit**

```bash
git add src/SlayTheSpireOverlay.Godot/UI/
git commit -m "refactor(overlay): split RoomOverlayFactory into partial files (no behavior change)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task 13: Move the `[HarmonyPatch]` classes out of `ModEntry.cs` (no behavior change)

**Files:**
- Modify: `src/SlayTheSpireOverlay.Godot/ModEntry.cs`
- Create: `src/SlayTheSpireOverlay.Godot/Patches/CardPatches.cs`, `RelicPatches.cs`, `EventOptionPatches.cs`

- [ ] **Step 1: Record the patch classes**

```bash
grep -n "^\[HarmonyPatch\|^public static class" src/SlayTheSpireOverlay.Godot/ModEntry.cs | sort > /tmp/patches-before.txt
cat /tmp/patches-before.txt
```

- [ ] **Step 2: Move the classes by name**

Cut these top-level classes (with their `[HarmonyPatch(...)]` attribute lines) out of `ModEntry.cs` unchanged, keep the `namespace SlayTheSpireOverlay.Godot;`, and put them in:

| File | Classes |
|---|---|
| `Patches/CardPatches.cs` | `NCardReadyPatch`, `NCardSetModelPatch` |
| `Patches/RelicPatches.cs` | `NRelicReadyPatch`, `NRelicSetModelPatch` |
| `Patches/EventOptionPatches.cs` | `NEventOptionButtonReadyPatch`, `NEventOptionButtonSetOptionPatch` |

Each new file starts with the same `using` directives the moved code needs.

- [ ] **Step 3: Verify**

```bash
grep -n "^\[HarmonyPatch\|^public static class" src/SlayTheSpireOverlay.Godot/ModEntry.cs src/SlayTheSpireOverlay.Godot/Patches/*.cs | sed 's/^[^:]*:[0-9]*://' | sort > /tmp/patches-after.txt
diff <(sed 's/^[0-9]*://' /tmp/patches-before.txt | sort) /tmp/patches-after.txt && echo "PATCH CLASSES IDENTICAL"
dn build src/SlayTheSpireOverlay.Godot -c Release 2>&1 | tail -3
./scripts/check_sts2_compat.sh | tail -2
```

Expected: `PATCH CLASSES IDENTICAL`, `Build succeeded`, `OK: every referenced game type...`. In game: the log line reports the same applied-patch count as before the move.

- [ ] **Step 4: Commit**

```bash
git add src/SlayTheSpireOverlay.Godot/
git commit -m "refactor(overlay): move Harmony patch classes out of ModEntry (no behavior change)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task 14: Split `Sts2RuntimeInspector` into partial files (no behavior change)

**Files:**
- Modify: `src/SlayTheSpireOverlay.Godot/Sts2RuntimeInspector.cs`
- Create: `Sts2RuntimeInspector.Combat.cs`, `Sts2RuntimeInspector.Shop.cs`, `Sts2RuntimeInspector.Ids.cs` (same folder)

- [ ] **Step 1: Inventory**

```bash
INV='(public|private|internal) static [^=;{(]+\('
cat src/SlayTheSpireOverlay.Godot/Sts2RuntimeInspector.cs | grep -oE "$INV" | sed 's/ *($//' | sort > /tmp/insp-before.txt
```

- [ ] **Step 2: Make it `public static partial class` and move by name**

| File | Members |
|---|---|
| `Sts2RuntimeInspector.cs` (stays) | cache/static fields, `IsCombatHudObscured`, `IsCardInCombatHand`, `IsItemInTopBar`, `GetLocalPlayer` |
| `.Combat.cs` | `InspectCombatAttacks`, `InspectDeckTracker`, `GetLocalPlayerHpAndBlock`, `CountPile`, `ReadPile`, `EvaluateEffectiveCost`, `GetEmbeddedCombatRoom` |
| `.Shop.cs` | `InspectMerchantGoldLeft`, `AddSlot` |
| `.Ids.cs` | `InspectEventId`, `ExtractNodeModelEntryId`, `ExtractModelEntryId`, `ExtractRewardItemId` |

- [ ] **Step 3: Verify**

```bash
cat src/SlayTheSpireOverlay.Godot/Sts2RuntimeInspector*.cs | grep -oE "$INV" | sed 's/ *($//' | sort > /tmp/insp-after.txt
diff /tmp/insp-before.txt /tmp/insp-after.txt && echo "MEMBERS IDENTICAL"
dn build src/SlayTheSpireOverlay.Godot -c Release 2>&1 | tail -3
dn test SlayTheSpireOverlay.slnx 2>&1 | grep -E "Passed!|Failed"
```

Expected: `MEMBERS IDENTICAL`, build and tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/SlayTheSpireOverlay.Godot/
git commit -m "refactor(overlay): split Sts2RuntimeInspector into partial files (no behavior change)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Task 15: CI that runs what can run without the game

Implements part of issue #18. The architecture tests and Godot projects need `sts2.dll`, so CI runs only `tests/SlayTheSpireOverlay.Tests` (Core + Common logic). This is documented, not hidden.

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: Write the workflow**

```yaml
name: ci

on:
  push:
    branches: [main]
  pull_request:

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            9.0.x
            10.0.x
      # Godot projects and architecture tests reference sts2.dll from a Steam install, so they cannot build here.
      - run: dotnet test tests/SlayTheSpireOverlay.Tests/SlayTheSpireOverlay.Tests.csproj --configuration Release
```

- [ ] **Step 2: Prove the CI command works without game files**

```bash
dn test tests/SlayTheSpireOverlay.Tests --configuration Release -p:Sts2DataDir=/nonexistent 2>&1 | grep -E "error|Passed!|Failed"
```

Expected: `Passed!` (the test project does not use `sts2.dll`; the `Sts2DataDir` override proves it does not depend on it).

- [ ] **Step 3: Commit and push, then watch the first run**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: run unit tests on push and PR

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
git push origin HEAD
gh run watch --exit-status
```

Expected: the `unit-tests` job is green. If an action tag is rejected, pin to the current major version shown on the action's page.

---

## Task 16: Close the loop on issues and records

**Files:** none (GitHub housekeeping).

- [ ] **Step 1: Link the plan and mark progress**

```bash
gh issue comment 16 --body "Implemented by Tasks 3, 7 and 10 of docs/superpowers/plans/2026-10-17-agent-friendly-architecture.md. Note: the compat checker found that NModInfoContainer.Clear does not exist in game v0.107.1 (the hook was dead); it is removed in Task 10."
gh issue comment 18 --body "CI portion implemented by Task 15 (unit tests only; Godot projects and architecture tests need sts2.dll). README and license remain open."
```

- [ ] **Step 2: Open follow-up issues only for work this plan intentionally defers**

- Architecture tests in CI via stub reference assemblies for `sts2.dll`/`0Harmony.dll`.
- Move remaining decision logic (panel layout clamping, hover-popup placement) from `RoomOverlayFactory.*` into `*.Core` as pure functions with tests.

- [ ] **Step 3: Clean up the worktree after merge**

```bash
cd ../SlayTheSpireOverlayLinux
git worktree remove ../SlayTheSpireOverlayLinux-arch
git branch -d arch/agent-friendly
```

---

## Self-Review

**Spec coverage** (every finding -> task): F1 -> 8, 9, 11; F2 -> 3, 5, 7, 10; F3 -> 5, 6, 7, 10; F4 -> 12, 13, 14; F5 -> 2, 15; F6 -> 1, 4; F7 -> 1, 4, 15; F8 -> 0, 4; F9 -> 3 (found), 10 (fixed). Issues #16, #18 (CI part) are covered by Tasks 3/7/10/15 and recorded in Task 16.

**Placeholder scan:** every code step contains the code (Tasks 3, 5-10 embed files that were compiled under the repo's analyzer settings and, for the pure parts, executed: 18 tests passed; the checker was run against the real `sts2.dll` with a positive run and a failing negative run; the Fog `ModEntry`/inspector refactor and the architecture rule were built/run in scratch projects). Tasks 10 (Overlay part), 11, 12, 13, 14 describe moves/edits of files that are being changed by another session, so they are specified by member name with a before/after verification script rather than copied text; this is deliberate and each has a mechanical check.

**Type consistency:** `ReflectionAccess`, `JsonConfigStore`, `PatchRegistrar`, `EdgeDetector`, `FrameGate`, `FogGeometry.Compute`, `FogPlan`/`FogRect`, `HarmonyPatchHelper.{Postfix,Prefix,PrefixOverload,PatchAttributeClasses}`, `ModScreenSupport.{IsOurMod,EnableModdingButtonInRun,GetOrCreateRunModdingScreen}` use identical names and signatures wherever they appear (the Fog `ModEntry` in Task 10 compiles against them).

**Review Focus:** each of the six lines maps to a test or an explicit check (Tasks 3, 6, 7, 8, 9, 10).

**Known unverified items** (cannot be checked outside the game): in-game behavior after Task 10 (Step 8), hotkey behavior after Task 11, and the first CI run (Task 15).
