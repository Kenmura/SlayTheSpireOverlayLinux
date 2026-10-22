# Test Subject Vote Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A third standalone STS2 mod: in multiplayer, a manual Skill play during the Test Subject's first form is held until the other players vote; a failed vote cancels the play and returns the card to hand.

**Architecture:** Pure vote rules in `SlayTheSpireTestSubjectVote.Core` (unit-tested). A thin Godot/Harmony adapter patches `PlayCardAction.ExecuteAction` to await a vote before energy is spent, exchanges votes as mod-defined `INetMessage`s, and shows a native-looking pop-up built from built-in Godot nodes. Every client runs the same deterministic tally from broadcast votes, so no host authority is needed (my call; the spec leaves it to the builder).

**Tech Stack:** .NET 9, GodotSharp (built-in nodes only), Harmony (`0Harmony`), MSTest 4.0.1, `SlayTheSpireMod.Common` (`ReflectionAccess`, `PatchRegistrar`).

**Spec:** `docs/superpowers/specs/2026-10-21-test-subject-vote-design.md`

## Global Constraints

- Only `CardType.Skill`, only manual plays (not `isAutoPlay`), only the Test Subject's first form (`Respawns == 0`; i.e. it has `AdaptablePower` and not `PainfulStabsPower`), only in multiplayer.
- Voters = every player except the card player. Reject only if Reject votes **strictly outnumber** Allow votes; a tie allows. No timeout: wait indefinitely.
- Rejection cancels the play before `SpendResources()`; energy is not spent. Replay allowed immediately, no lockout.
- The mod never adds or removes Strength. The cost line shows N read from the Test Subject's `EnragePower.Amount` at runtime (2 or 3), never hard-coded.
- Core must not depend on Godot or `sts2.dll`. No custom `Control`/`Node` subclasses. Scene-tree changes only on the main thread via `Callable.From(...).CallDeferred()` with `IsInstanceValid` checks. `Godot.UI` must not use `System.Net.Http`.
- Game internals only via `ReflectionAccess`; patches via `PatchRegistrar` (per-patch resilience, "applied N, skipped M" summary). `min_game_version` `0.107.0`.
- No `src/**/*.cs` file over 500 lines. `./scripts/check_duplication.sh` must pass (never raise the threshold). Add files by explicit path. Use the claim `test-subject-vote` (`./scripts/agent_claim.sh`).
- Publishing to the Workshop is not part of this plan.

## Review Focus

- A voter disconnects or dies mid-vote: they stop counting, the vote resolves with the rest; if no voters remain the play is allowed. (Task 1)
- A vote message arrives before this client reaches the play's `ExecuteAction`, or arrives late/duplicated after resolution: buffered or ignored, never crashes or double-counts. (Task 2)
- The card player's own vote message (or any non-voter's) is ignored. (Task 1)
- Two Skills queued back to back: each gets its own vote key and pop-up, resolved in order. (Task 2)
- Combat or turn ends while a vote is open: the pop-up closes and the held play is cancelled. (Task 6)
- A peer without the mod: no vote is held, the play proceeds as vanilla and a warning is logged. (Task 3)

---

### Task 1: Vote rules (Core)

**Files:**
- Create: `src/SlayTheSpireTestSubjectVote.Core/SlayTheSpireTestSubjectVote.Core.csproj` (copy `SlayTheSpireMapFog.Core.csproj`)
- Create: `src/SlayTheSpireTestSubjectVote.Core/Voting/VoteTally.cs`
- Create: `src/SlayTheSpireTestSubjectVote.Core/Voting/VoteEligibility.cs`
- Modify: `SlayTheSpireOverlay.slnx` (add the project), `tests/SlayTheSpireOverlay.Tests/SlayTheSpireOverlay.Tests.csproj` (project reference)
- Test: `tests/SlayTheSpireOverlay.Tests/VoteTallyTests.cs`, `tests/SlayTheSpireOverlay.Tests/VoteEligibilityTests.cs`

**Interfaces:**
- Produces (namespace `SlayTheSpireTestSubjectVote.Core.Voting`):
  - `enum VoteOutcome { Pending, Allowed, Rejected }`
  - `sealed class VoteTally { VoteTally(IEnumerable<ulong> voterIds); bool Cast(ulong voterId, bool allow); void Remove(ulong voterId); int Allow; int Reject; int Total; int Waiting; VoteOutcome Outcome; }` — `Cast` returns false (no change) for unknown or already-voted ids.
  - `static class VoteEligibility { static bool NeedsVote(bool isSkill, bool isManualPlay, bool isMultiplayer, bool isFirstForm, bool allPeersHaveMod); static IReadOnlyList<ulong> VoterIds(IEnumerable<ulong> allPlayerIds, ulong cardPlayerId); }`

- [ ] **Step 1: Write failing tests** in `VoteTallyTests` (names and assertions):
  - `Tie_Allows`: 2 voters, one allow one reject → `Allowed`.
  - `SingleVoter_Reject_Rejects` (2-player run): 1 voter rejects → `Rejected`; allows → `Allowed`.
  - `ThreePlayers_BlockNeedsBothRejects`: 2 voters, one reject, one allow → `Allowed`; both reject → `Rejected`.
  - `Pending_UntilEveryVoterHasVoted`: 3 voters, 2 cast → `Pending`, `Waiting == 1`; no timeout path exists.
  - `RejectMajorityResolvesOnlyWhenAllIn`: outcome is `Pending` until the last voter casts, even if rejects already lead.
  - `NonVoterAndDuplicateVotesIgnored`: `Cast` of unknown id and second cast by same id return false and leave counts unchanged.
  - `RemovedVoterStopsCounting`: `Remove` a waiting voter shrinks `Total`; resolves when the rest are in; removing the last remaining voter gives `Allowed`.
  - In `VoteEligibilityTests`: `NeedsVote` true only when all five flags are true (one theory row per flag false → false); `VoterIds` excludes the card player and keeps order.
- [ ] **Step 2: Run** `dn test tests/SlayTheSpireOverlay.Tests --filter "VoteTally|VoteEligibility"` → FAIL (types missing).
- [ ] **Step 3: Implement** the two types per the signatures above in the Core project; `Outcome` is `Pending` while any voter is waiting, else `Rejected` iff `Reject > Allow`, else `Allowed` (zero voters → `Allowed`).
- [ ] **Step 4: Run** the same command → PASS.
- [ ] **Step 5: Commit** (`git add` the explicit files): `feat(vote): pure vote tally and eligibility rules`.

### Task 2: Vote registry and play keys (Core)

**Files:**
- Create: `src/SlayTheSpireTestSubjectVote.Core/Voting/VoteBook.cs`
- Test: `tests/SlayTheSpireOverlay.Tests/VoteBookTests.cs`

**Interfaces:**
- Consumes: `VoteTally`.
- Produces: `readonly record struct PlayKey(ulong PlayerId, uint CombatCardIndex, int Attempt)`; `sealed class VoteBook { PlayKey NextKey(ulong playerId, uint combatCardIndex); VoteTally Open(PlayKey key, IEnumerable<ulong> voterIds); bool RecordEarlyVote(PlayKey key, ulong voterId, bool allow); bool Cast(PlayKey key, ulong voterId, bool allow); void Close(PlayKey key); }`
- `NextKey` increments a per-(player, card) attempt counter that is identical on every client because plays are synchronized actions. `RecordEarlyVote` stores a vote for a key not yet opened; `Open` replays stored early votes into the new tally.

- [ ] **Step 1: Write failing tests:** `EarlyVote_IsAppliedWhenVoteOpens`; `ReplayOfSameCard_GetsNewKey` (attempt 0 then 1; old key's votes do not leak into the new tally); `VoteAfterClose_IsIgnored` (`Cast` returns false, no throw); `TwoPlayersSameTime_AreIndependent`.
- [ ] **Step 2: Run** `--filter VoteBook` → FAIL. **Step 3: Implement.** **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat(vote): vote registry with early-vote buffering`.

### Task 3: Copy and mod handshake rules (Core)

**Files:**
- Create: `src/SlayTheSpireTestSubjectVote.Core/Voting/VoteCopy.cs`, `src/SlayTheSpireTestSubjectVote.Core/Voting/PeerRoster.cs`
- Test: `tests/SlayTheSpireOverlay.Tests/VoteCopyTests.cs`, `tests/SlayTheSpireOverlay.Tests/PeerRosterTests.cs`

**Interfaces:**
- Produces: `static class VoteCopy { string Headline(string playerName, string cardName); string CostLine(int strength); string WaitingLine(int tick); string DeniedStamp; string AllowLabel; string RejectLabel; string ApprovedQuip(int pick); string Progress(int votesIn, int total); }`; `sealed class PeerRoster { void MarkHasMod(ulong playerId); bool AllHaveMod(IEnumerable<ulong> playerIds); }`.
- Copy (spec samples; may be improved): headline "`{player}` wants to play `{card}`. Are we feeling lucky?"; cost line "The Test Subject will get `{N}` Strength. Hope you like pain."; buttons "Let it happen" / "Absolutely not"; stamp "DENIED"; `WaitingLine` cycles through at least 3 lines starting "Your teammates are deciding your fate..."; `ApprovedQuip` cycles through at least 3 lines starting "Bold choice...".

- [ ] **Step 1: Failing tests:** `CostLine_ShowsRuntimeAmount` (2 → contains "2 Strength", 3 → contains "3 Strength", never a fixed 3); `WaitingLine_CyclesAndNeverEmpty` (ticks 0..20); `Progress_Format` ("1 of 2 votes in"); `AllHaveMod_FalseWhenAnyPeerMissing`, `AllHaveMod_TrueForEmptyList`.
- [ ] **Step 2–4:** run → FAIL, implement, run → PASS. **Step 5: Commit** `feat(vote): playful copy and peer roster`.

### Task 4: Godot project scaffold, manifest, patch plumbing

**Files:**
- Create: `src/SlayTheSpireTestSubjectVote.Godot/SlayTheSpireTestSubjectVote.Godot.csproj` (copy the Fog `*.Godot.csproj`, linking Core and `SlayTheSpireMod.Common`), `SlayTheSpireTestSubjectVote.Godot.json` (id `SlayTheSpireTestSubjectVote.Godot`, name "Test Subject Vote", `min_game_version` `0.107.0`, `affects_gameplay: true`, `has_dll: true`, `has_pck: false`), `ModEntry.cs`
- Modify: `SlayTheSpireOverlay.slnx`, `scripts/check_sts2_compat.sh` (build and check the new DLL), `tools/Sts2CompatCheck/patch-targets.txt`, `tests/SlayTheSpireOverlay.ArchitectureTests` (if its rules enumerate namespaces, add the new `Godot.UI` namespace to the no-`HttpClient` rule)

**Interfaces:**
- Produces: `ModEntry.Initialize()` annotated `[MegaCrit.Sts2.Core.Modding.ModInitializer("Initialize")]`, registering each patch through `PatchRegistrar` and logging `PatchRegistrar.Summary()`.

- [ ] **Step 1:** Create the files; `ModEntry` registers zero patches yet and logs the summary.
- [ ] **Step 2:** `dn build SlayTheSpireOverlay.slnx -c Release` (needs game files) → builds clean (warnings are errors).
- [ ] **Step 3:** `./scripts/check_sts2_compat.sh` → "OK".
- [ ] **Step 4:** `dn test tests/SlayTheSpireOverlay.Tests` and `dn test tests/SlayTheSpireOverlay.ArchitectureTests` → PASS.
- [ ] **Step 5: Commit** `feat(vote): scaffold Test Subject Vote mod project`.

### Task 5: Network messages and game state readers

**Files:**
- Create: `src/SlayTheSpireTestSubjectVote.Godot/Net/VoteMessages.cs`, `Net/VoteNetwork.cs`, `Game/TestSubjectState.cs`
- Modify: `tools/Sts2CompatCheck/patch-targets.txt`

**Interfaces:**
- Consumes: `PlayKey`, `VoteBook`, `PeerRoster`.
- Produces: `INetMessage` types `VoteCastMessage { ulong PlayerId; uint CombatCardIndex; int Attempt; bool Allow }` and `ModHelloMessage { }` (follow the serialization pattern of an existing `INetMessage` in `sts2.dll`, found by decompiling with ilspycmd); `VoteNetwork { void Broadcast(VoteCastMessage m); void SendHello(); event Action<VoteCastMessage> OnVote; }` registering handlers through `INetGameService.RegisterMessageHandler<T>`; `static class TestSubjectState { static bool IsFirstForm(object combatState); static int EnrageAmount(object combatState); static bool IsMultiplayer(); }` using `ReflectionAccess` only (first form = Test Subject creature has `AdaptablePower` and no `PainfulStabsPower`; amount from `EnragePower.Amount`).

- [ ] **Step 1:** Verify the registration path: after `MessageTypes.Initialize()` the mod types resolve (`MessageTypes.TryGetMessageType`); add string targets for every touched game member to `patch-targets.txt`.
- [ ] **Step 2:** Implement the three files per the signatures; handler registration is guarded so a failure logs and disables the gate rather than throwing.
- [ ] **Step 3:** `./scripts/check_sts2_compat.sh` → "OK". **Step 4: Commit** `feat(vote): vote network messages and Test Subject state readers`.

### Task 6: The gate: hold, vote, resume or cancel

**Files:**
- Create: `src/SlayTheSpireTestSubjectVote.Godot/Patches/PlayCardGatePatch.cs`, `Patches/VoteGate.cs`

**Interfaces:**
- Consumes: Tasks 1–5.
- Produces: a Harmony prefix on `MegaCrit.Sts2.Core.GameActions.PlayCardAction.ExecuteAction` that, when `VoteEligibility.NeedsVote(...)` is true, returns `false` and sets `__result` to `VoteGate.RunAsync(action)`. `RunAsync` opens the tally, shows the pop-up (Task 7), awaits the outcome, then on **Allowed** marks the action approved and invokes the original `ExecuteAction` (the prefix lets approved actions through), on **Rejected** calls the action's `Cancel()` so the existing cancel path returns the card to hand with energy untouched. Also resolves any open vote as cancelled and closes the pop-up when combat or the turn ends, and drops a voter from the tally when the game reports them disconnected or dead.

- [ ] **Step 1:** Decompile `GameAction` (`Cancel`, state fields) and the action queue with ilspycmd to confirm what a cancelled or never-finishing action must do so the queue does not stall; record the finding in a code comment on `VoteGate`.
- [ ] **Step 2:** Implement. Eligibility is evaluated once, when the action starts (a form change mid-vote does not re-evaluate).
- [ ] **Step 3 (manual, two real players or two game instances):** play a Skill vs the Test Subject first form: pop-up appears for the other player; Reject returns the card and energy and no Strength is gained; Allow plays it and Strength rises; Attack and Power cards never prompt; replay works; leaving mid-vote does not hang. Record results in the PR.
- [ ] **Step 4:** `./scripts/check_sts2_compat.sh` → OK. **Step 5: Commit** `feat(vote): hold Skill plays for a vote`.

### Task 7: Native-looking pop-up (mockup C)

**Files:**
- Create: `src/SlayTheSpireTestSubjectVote.Godot/UI/VotePopupFactory.cs`, `UI/VotePopupView.cs`

**Interfaces:**
- Consumes: `VoteCopy`, `VoteTally`.
- Produces: `static class VotePopupFactory { VotePopupView CreateForVoter(string playerName, object cardModel, int strength, Action<bool> onVote); VotePopupView CreateForWaiting(string cardName); }` and `VotePopupView { void Update(VoteTally tally); void ShowDenied(); void ShowApproved(string quip); void Close(); }`, all built from `Control`/`PanelContainer`/`VBoxContainer`/`Button`/`Label`/`TextureRect`/`ColorRect` instances (no subclasses), dimmed background, large card preview using the game's own card node via `ReflectionAccess`, panel styled like the game's panels (reuse the game's theme or fonts), thumbs pips for votes, DENIED stamp tween, passive labels `MouseFilterEnum.Ignore` and only the two buttons `Stop`. Marshals all changes through `CallDeferred`.

- [ ] **Step 1:** Implement the factory. Keep each file under 500 lines and reuse existing Common/UI helpers before writing new ones.
- [ ] **Step 2:** `dn build` and `./scripts/check_duplication.sh` → pass.
- [ ] **Step 3 (manual):** compare against mockup C in a live run for voter and waiting views, both themes the game offers, and at a non-16:9 resolution. **Step 4: Commit** `feat(vote): vote pop-up UI`.

### Task 8: Packaging and docs

**Files:**
- Create: `workshop-vote/workshop.json` (visibility `private`, tags `Mod`, `Gameplay`, `changeNote`), `scripts/upload_vote_to_workshop.sh` (copy `upload_fog_to_workshop.sh`, workspace `workshop-vote`)
- Modify: `README.md`, `CHANGELOG.md`, `AGENTS.md` (§1 and §2 list the third mod)

- [ ] **Step 1:** Add the files; the script's private-visibility safety check must stay.
- [ ] **Step 2:** `dn test SlayTheSpireOverlay.slnx`, `./scripts/check_sts2_compat.sh`, `./scripts/check_duplication.sh` → all pass. **Step 3: Commit** `feat(vote): packaging and docs`. Do not run the upload script.

---

## Self-review notes

- Spec coverage: scope/trigger/voters/outcome/no-timeout/replay → Tasks 1, 6; no Strength changes + runtime amount → Tasks 3, 5; pop-up and tone → Tasks 3, 7; mod-required handling → Tasks 3, 5; non-goals have no tasks (intended); constraints → Global Constraints and Task 4 and 8.
- Name consistency checked: `VoteTally`, `VoteBook`, `PlayKey`, `VoteEligibility`, `PeerRoster`, `VoteCopy`, `VoteGate`, `VotePopupFactory`.
- Biggest risk: Task 6 Step 1 (async hook and action-queue behavior). Task 6 first uses a bare placeholder prompt (two plain `Button`s in a `PanelContainer`, same `Action<bool>` callback) so the hook can be proven live before any art work; Task 7 then replaces the placeholder with `VotePopupFactory`.
