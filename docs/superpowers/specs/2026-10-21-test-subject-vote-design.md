# Test Subject Vote: design

Status: draft for approval. Date: 2026-10-21.

## Intent

In a Slay the Spire 2 multiplayer run, the Test Subject boss punishes Skill plays: its `EnragePower` gives it Strength (`EnrageAmount`: 3 with the Deadly Enemies ascension, 2 without; the user's "3 Strength" is the Deadly Enemies value) every time any player plays a Skill. Today one teammate can wreck the whole team by playing a Skill without asking. This mod makes that a table decision: when a player tries to play a Skill during the Test Subject's first form, everyone else votes, and a failed vote sends the card back to the player's hand.

The point is a shared, funny moment of "are we really doing this?", not a punishment. The tone is light-hearted and cheeky.

## Goals

- Pause a Skill play during the Test Subject's first form and let the other players vote Allow or Reject before it resolves.
- A rejected play is cancelled: the card returns to the hand and the player keeps their energy.
- An approved play proceeds exactly as vanilla, including the vanilla Strength gain. The mod never adds or removes Strength itself.
- A native-looking pop-up (see "Pop-up") that is fun to read.
- Ship as a third standalone Workshop mod next to Overlay for Tier List and Fog of War Map.

## Non-goals

- No votes on Attacks, Powers, potions, relics or any other action.
- No votes in the Test Subject's second or third form, or against any other enemy.
- No single-player behaviour (a single-player run is never affected).
- No timeout, auto-resolve or "vote skipping" setting in v1.
- No custom sounds, art or animations created for the mod; reuse existing game assets only.
- No Workshop publishing as part of building this (publishing is a separate, explicit step).

## Anti-goals (a failure even if it technically works)

- Desync between clients: any state where players disagree on whether a card was played.
- Energy or a card lost because of a rejected vote.
- The mod changing the Test Subject's Strength numbers or any other vanilla balance.
- A crash or hang that outlives the vote (for example a pop-up that never closes).

## Rules (decided with the user)

- Scope: only `CardType.Skill`, only the Test Subject's first form (before its first revive; `Respawns == 0`; equivalently it still has `AdaptablePower` and does not yet have `PainfulStabsPower`).
- Trigger: only a manual play (a `PlayCardAction` the player initiated), not auto-played cards.
- Voters: every player in the run except the one playing the card. Dead or disconnected players: see "Left to the builder".
- Outcome: Reject wins only when Reject votes strictly outnumber Allow votes. A tie allows the play (the card player acts as tiebreaker by not voting). In a 2-player run the single other player decides alone. In a 3-player run a block needs both voters to reject.
- No timeout: the vote waits for every voter, indefinitely.
- Replay: after a rejection the player may replay the same card at once and trigger a fresh vote. There is no lockout.
- Requirement: every player in the lobby has the mod installed. If a peer does not have it, the mod must not hold plays that peer cannot answer (see "Left to the builder").

## Pop-up (mockup C, chosen by the user)

- Voters see a centred, dimmed-background panel in the style of the game's own panels, with: who is playing, a large preview of the card, a cost line ("If allowed: Test Subject gains N Strength", with N read from the Test Subject's `EnragePower.Amount` at runtime, never hard-coded, so it shows 2 or 3 correctly), Allow and Reject buttons, and a live "N of M votes in" line using thumbs-up and thumbs-down pips.
- The card player sees a waiting panel with the live tally and no buttons.
- Tone: playful copy. Sample copy (the builder may improve it): "Player 2 wants to play Defend. Are we feeling lucky?", buttons "Let it happen" and "Absolutely not", cost line "The Test Subject will get 3 Strength. Hope you like pain.", waiting line rotating through lines like "Your teammates are deciding your fate...". A rejected card gets a "DENIED" stamp and flies back to the hand; an approved card gets a short quip before it resolves.
- Built only from built-in Godot nodes (no custom `Control` subclasses), `MouseFilter` set correctly, scene-tree changes only on the main thread.

## What the game gives us (reverse-engineering findings, sts2.dll 0.107.x)

- `EnragePower.AfterCardPlayed` applies Strength when `cardPlay.Card.Type == CardType.Skill`, after the card resolves. So the interception has to be earlier.
- `PlayCardAction.ExecuteAction()` is async: look up the card, check it is in hand and playable, then `SpendResources()` (energy), then `OnPlayWrapper`. Before `SpendResources` nothing has been spent, which is the interception point. `Cancel()`/`CancelAction()` already removes the card from `NCardPlayQueue` and returns it to hand, so a rejection refunds energy for free.
- `MessageTypes.Initialize()` includes `ReflectionHelper.GetSubtypesInMods<INetMessage>()`, so mod-defined `INetMessage` types are registered by the game. `INetGameService` offers `SendMessage<T>` and `RegisterMessageHandler<T>`.
- `TestSubject` has private `Respawns`; read it via `SlayTheSpireMod.Common.ReflectionAccess` or infer the form from the creature's powers.

## Constraints

Follow `AGENTS.md`: Core has no Godot or `sts2.dll` dependency; vote rules are pure functions in `*.Core` with MSTest unit tests; the Godot project is a thin adapter; game internals only through `ReflectionAccess`; patches registered through `PatchRegistrar` with per-patch resilience and a startup summary; `./scripts/check_sts2_compat.sh`, `SourceHygieneTests` (500-line file limit) and `./scripts/check_duplication.sh` must pass; reuse `SlayTheSpireMod.Common` before writing helpers. Claim the work with `agent_claim.sh` and use a worktree.

## Left to the builder

- Mod name, id and Workshop metadata (working name: "Test Subject Vote"; follow the layout of `SlayTheSpireMapFog.*` and `workshop-fog/`).
- How to hook the async `ExecuteAction` (prefix returning a custom `Task`, a different earlier hook, etc.), and how every client pauses and resumes in the same order.
- Exact `INetMessage` types and who is authoritative for counting (host-counted versus each-client-counted).
- Edge cases: a voter who disconnects or dies mid-vote, two Skills queued at once, a Skill played when the form changes mid-vote, what happens when a peer lacks the mod, the vote still open when the turn or combat ends. Choose safe behaviour and document it.
- Exact copy, layout and which existing sounds and effects to reuse.
- A test strategy: vote tally and "is this play subject to a vote" are pure and tested; the game-facing parts are verified manually in a live two-player run.
