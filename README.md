# Arrow Thing

Minimalist speed puzzle game prototype built in Unity.

Core pitch: clear winding grid-based arrows as fast as possible, then weaponize your speed in PvP by sending garbage.

## Project Status

- Playable on https://arrow-thing.com/
- Design docs:
  - [`docs/GDD.md`](docs/GDD.md) (game design)
  - [`docs/TechnicalDesign.md`](docs/TechnicalDesign.md) (technical architecture and class structure)
  - [`docs/OnlineRoadmap.md`](docs/OnlineRoadmap.md) (online features plan)

## Tech Stack

- Unity `6000.3.8f1`
- C# domain logic under `Assets/Scripts/Domain`
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

## License

This project is licensed under the [MIT License](LICENSE).

## Acknowledgements

Git configuration (`.gitattributes`, `.gitignore`, git hooks) is based on [NYU Game Center's Unity-Git-Config](https://github.com/NYUGameCenter/Unity-Git-Config) — a great open resource for Unity project setup.

## Repository Layout

- `Assets/Scripts/Domain` - Core board/arrow domain logic
- `Assets/Scripts/View` - Unity rendering, input, UI
- `Assets/Tests/EditMode` - Unit tests (Unity Test Framework)
- `docs/GDD.md` - Game design direction and scope
- `docs/TechnicalDesign.md` - Architecture and class-structure decisions
- `docs/OnlineRoadmap.md` - Online features plan
- `docs/BoardGeneration.md` - Board generation algorithm
