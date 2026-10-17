---
name: adversarial-review
description: >-
  Perform an adversarial multi-perspective code, architecture, or plan review to
  catch edge cases, race conditions, silent failures, and discrepancies between
  user intent and implementation. Use when asked for an adversarial review or
  before shipping critical changes.
---

# Adversarial Review Skill

An adversarial review stress-tests code, architecture, and documentation by actively attempting to disprove assumptions rather than rubber-stamping them.

## Workflow

### Phase 1: Establish Intent vs. Reality
1. Read the actual `git diff`, build/test outputs, and project rules (`AGENTS.md`) directly—never trust summary claims without verifying the underlying files.
2. List the explicit requirements and invariants (e.g., engine constraints, network behavior, zero-tracking guarantees, thread safety).

### Phase 2: Red-Team Attack Passes
Evaluate the target from three adversarial angles:
1. **The Skeptic (Correctness & Edge Cases)**:
   - What inputs, null references, missing fields, or unexpected API payloads will cause silent failures or exceptions?
   - Does the code actually do what it claims (e.g., are remote URLs real or placeholders? Do regexes match real game strings)?
2. **The Runtime Breaker (Concurrency, Lifecycle & Engine Constraints)**:
   - Are there main-thread vs. background-thread violations, memory/node leaks across pooled UI objects, or blocking I/O calls on the render loop?
3. **The Minimalist (Bloat & Privacy Audit)**:
   - Is there dead code, unnecessary abstraction, or unintended telemetry/network activity?

### Phase 3: Cross-Examination & Refutation
- Challenge every candidate finding against the actual codebase:
  - Discard speculative or purely stylistic nitpicks.
  - Retain only concrete, reproducible defects or architectural violations backed by file/line evidence.

### Phase 4: Verdict & Remediation
- Report findings ordered by severity (`Critical`, `High`, `Medium`, `Low`) with exact file and line links.
- Fix all `Critical` and `High` issues immediately and re-verify with automated tests.
