# FrostWyrm-Archive
FrostWyrm Archive provides reusable C# infrastructure and **Cryptid Companion**, a playable solo companion for your physical Cryptid board. The app holds hidden clues for absent players; you make every decision and take every turn.

This repository brings forward the implemented infrastructure from [Zetaplx/FrostWyrm](https://github.com/Zetaplx/FrostWyrm/tree/ffa990b43176acc3dbbf423fee2289e8387a9aed), while retaining Archive's Core project and ECS roadmap. See [development parity](docs/development-parity.md) for the port's scope and limitations.

## What is implemented

- Custom dependency registration, resolution, and attribute-based injection.
- YAML data definitions, custom deserializers, typed prototype IDs, and prototype loading and lookup.
- A Blazor service-registration bridge using the same Archive manager instances.
- Hex coordinates, hex-grid storage, and YAML hex deserialization.
- A standalone Blazor WebAssembly/PWA with the confirmed reference board, all 23 Normal Mode clue categories, fixed card-defined unique-solution clues, opening cubes, questions, legal searches and penalties, a winning-game flow, local saves, completed-game reviews, interactive legends, saved display settings, and offline support for 3–5 clue-holders.
- Core behavior and integration tests, run by GitHub Actions on pushes and pull requests to `master`.

Entity/component storage, system scheduling, queries, prototype inheritance, and snapshot serialization are still planned. The confirmed reference layout is playable end to end; additional printed card/book catalogs and Advanced Mode remain outside this first release.

## Develop

Install the .NET 10 SDK, then run from the repository root:

```sh
dotnet restore FrostWyrm-Archive.slnx
dotnet build FrostWyrm-Archive.slnx --no-restore
dotnet test FrostWyrm-Archive.slnx --no-build --no-restore
```

Run the example with:

```sh
dotnet run --project apps/CryptidCompanion --launch-profile http
```

The development server uses port 5002. Choose **Reference expedition**, build the board shown, choose your color, and follow the opening/game screens. The tile orientations and structures match the supplied reference image, including the corrected stone/shack types. No credentials, database, or external application services are needed.

See [content authoring](docs/cryptid-content.md) for easy YAML layout changes, and [development and offline testing](docs/cryptid-development.md) for browser tests, production publishing, and PWA instructions.

## Layout

| Directory | Purpose |
| --- | --- |
| `src/FrostWyrm.Archive.Core` | Runtime bootstrap, dependencies, prototypes, and YAML deserialization |
| `src/FrostWyrm.Blazor` | Integration with Blazor's service collection |
| `src/FrostWyrm.Math` | Hex coordinates and grid operations |
| `apps/CryptidCompanion` | Example WebAssembly application and YAML resources |
| `tests/FrostWyrm.Archive.Test` | Core behavior tests |
| `tests/FrostWyrm.Integration.Tests` | Blazor bridge, shipped setup-card flow, and hex tests |
| `docs` | Architecture, terminology, development parity, and roadmap |

The Core assembly preserves the `FrostWyrm.Archive` namespaces and `FrostWyrm.Initialize()` / `Resolve<T>()` APIs from FrostWyrm. Initialization creates a process-wide singleton and may only run once. The documented per-instance `App` container remains a future milestone.

## GitHub Pages

The deployment workflow publishes the `master` branch to GitHub Pages after solution and browser checks pass. See [Pages setup and deployment](docs/github-pages.md) for the target URL, repository settings, subdirectory routing, and offline validation.
