# Arrow Thing

Minimalist speed puzzle game prototype built in Unity.

Core pitch: clear winding grid-based arrows as fast as possible, then weaponize your speed in PvP by sending garbage.

## Project Status

- Current phase: MVP foundations
- Design docs:
  - [`docs/GDD.md`](docs/GDD.md) (game design)
  - [`docs/TechnicalDesign.md`](docs/TechnicalDesign.md) (technical architecture and class structure)
- Focus: deterministic board logic, procedural generation, and fast clear validation

## Tech Stack

- Unity `6000.3.8f1`
- C# models under `Assets/Scripts/Models`
- NUnit tests via Unity Test Framework in `Assets/Tests/EditMode`

## Local Development

1. Open this folder in Unity Hub using editor version `6000.3.8f1`.
2. Open the `Game` scene under `Assets/Scenes`.
3. Run tests via Unity's **Test Runner** window (Window > General > Test Runner, EditMode tab).
4. Set up the pre-commit hook:

```bash
git config core.hooksPath .githooks
```

This enables formatting checks (no tabs, no trailing whitespace, final newlines, no fully qualified `System.Collections.Generic` usage) on staged `.cs` files.

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md) for expectations around architecture, tests, and coverage standards.

## Licensing and Monetization

This project uses a source-available license inspired by the Aseprite model:

- Source code is available to read, modify, and build for personal/internal use.
- Redistribution of source code or binaries (original or modified) is not allowed without explicit written permission.
- Official distributable builds can be sold through storefronts.

See [`LICENSE`](LICENSE) for exact terms.

## Repository Layout

- `Assets/Scripts/Models` - Core board/arrow domain logic
- `Assets/Tests/EditMode` - Unit tests (Unity Test Framework)
- `docs/GDD.md` - Game design direction and scope
- `docs/TechnicalDesign.md` - Architecture and class-structure decisions
