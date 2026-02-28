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
- NUnit model tests in `tests/ArrowThing.Model.Tests` (`net8.0`)

## Local Development

1. Open this folder in Unity Hub using editor version `6000.3.8f1`.
2. Open the `Game` or `MainMenu` scene under `Assets/Scenes`.
3. Run model tests from the repo root:

```powershell
dotnet test tests/ArrowThing.Model.Tests/ArrowThing.Model.Tests.csproj --configuration Debug --nologo
```

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
- `tests/ArrowThing.Model.Tests` - Unit tests for model behavior
- `docs/GDD.md` - Game design direction and scope
- `docs/TechnicalDesign.md` - Architecture and class-structure decisions
