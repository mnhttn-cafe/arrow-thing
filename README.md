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
4. Install tools and hooks:

```bash
dotnet tool restore
git config core.hooksPath .githooks
```

The pre-commit hook runs:
- [CSharpier](https://csharpier.com/) formatting check on staged `.cs` files
- File size gate (rejects files >= 100 MB)
- Asset `.meta` file sync

The post-merge hook removes empty directories to prevent orphan `.meta` files.

To auto-fix formatting: `dotnet csharpier format Assets/Scripts/ Assets/Tests/`

5. (Optional) Set up Unity SmartMerge for better YAML conflict resolution:

```bash
git config merge.unityyamlmerge.driver '<path-to-Unity>/Editor/Data/Tools/UnityYAMLMerge merge -p %O %A %B %P'
```

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md) for expectations around architecture, tests, and coverage standards.

## Licensing and Monetization

This project uses a source-available license:

- Source code is available to read, modify, build, and share for any non-commercial purpose.
- Commercial distribution of source code or builds (original or modified) requires written permission.
- Contributions are licensed under the same or more permissive terms (see the license for details).
- Official distributable builds are sold through storefronts.

See [`LICENSE`](LICENSE) for exact terms.

## Acknowledgements

Git configuration (`.gitattributes`, `.gitignore`, git hooks) is based on [NYU Game Center's Unity-Git-Config](https://github.com/NYUGameCenter/Unity-Git-Config) — a great open resource for Unity project setup.

## Repository Layout

- `Assets/Scripts/Models` - Core board/arrow domain logic
- `Assets/Tests/EditMode` - Unit tests (Unity Test Framework)
- `docs/GDD.md` - Game design direction and scope
- `docs/TechnicalDesign.md` - Architecture and class-structure decisions
