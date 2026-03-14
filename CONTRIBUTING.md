# Contributing

This project is early-stage, so keep contributions small, focused, and easy to review.

## Core Expectations

- Prefer Unity-independent implementations for game rules, board logic, and generation logic.
- Keep Unity-facing code thin (input/view/adapters), with domain behavior in plain C# classes where possible.
- Add or update tests with every behavior change to Unity-independent classes.
- Add a regression test for every bug fix.

## Testing Standards

- Changes to Unity-independent classes must include NUnit coverage in `Assets/Tests/EditMode/`.
- Run tests via Unity's **Test Runner** window (Window > General > Test Runner, EditMode tab).
- Pull requests should not reduce coverage for touched Unity-independent classes.
- Target at least `90%` line coverage on touched Unity-independent classes, and aim higher for core rules.
- If a class cannot be covered well, document why in the pull request.

## Docs Consistency

- If you spot a docs inconsistency, either fix it in the same PR or call it out in the PR and open an issue.
- Keep `docs/TechnicalDesign.md` aligned with architecture/class-structure changes.

## Technical Design Document (TDDoc)

- We use "TDD" to mean **Technical Design Document**, not test-driven development.
- `docs/TechnicalDesign.md` is the source of truth for technical decisions:
  - architecture boundaries
  - core class responsibilities
  - key invariants and data flow
- If your change affects architecture or class structure, update `docs/TechnicalDesign.md` in the same pull request.
