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

This enables:
- Formatting checks (no tabs, no trailing whitespace, final newlines, no fully qualified `System.Collections.Generic`) on staged `.cs` files
- File size gate (rejects files >= 100 MB)
- Meta file sync (ensures added/removed Assets have matching `.meta` files)
- Post-merge cleanup (removes empty directories to prevent orphan `.meta` files)

5. (Optional) Set up Unity SmartMerge for better YAML conflict resolution:

```bash
git config merge.unityyamlmerge.driver '<path-to-Unity>/Editor/Data/Tools/UnityYAMLMerge merge -p %O %A %B %P'
```

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md) for expectations around architecture, tests, and coverage standards.

## Licensing and Monetization

This project uses a source-available license inspired by the Aseprite model:

- Source code is available to read, modify, and build for personal/internal use.
- Redistribution of source code or binaries (original or modified) is not allowed without explicit written permission.
- Official distributable builds can be sold through storefronts.

See [`LICENSE`](LICENSE) for exact terms.

## Acknowledgements

Git configuration (`.gitattributes`, `.gitignore`, git hooks) is based on [NYU Game Center's Unity-Git-Config](https://github.com/NYUGameCenter/Unity-Git-Config) — a great open resource for Unity project setup.

## Repository Layout

- `Assets/Scripts/Models` - Core board/arrow domain logic
- `Assets/Tests/EditMode` - Unit tests (Unity Test Framework)
- `docs/GDD.md` - Game design direction and scope
- `docs/TechnicalDesign.md` - Architecture and class-structure decisions
