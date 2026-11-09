# FrostWyrm-Archive
FrostWyrm Archive is a C# foundation for data-driven tools and applications, with an Entity Component System (ECS) planned as its next layer.

This repository brings forward the implemented infrastructure from [Zetaplx/FrostWyrm](https://github.com/Zetaplx/FrostWyrm/tree/ffa990b43176acc3dbbf423fee2289e8387a9aed), while retaining Archive's Core project and ECS roadmap. See [development parity](docs/development-parity.md) for the port's scope and limitations.

## What is implemented

- Custom dependency registration, resolution, and attribute-based injection.
- YAML data definitions, custom deserializers, typed prototype IDs, and prototype loading and lookup.
- A Blazor service-registration bridge using the same Archive manager instances.
- Hex coordinates, hex-grid storage, and YAML hex deserialization.
- A Blazor WebAssembly CryptidCompanion example that loads a setup card and selects clue references for 3–5 players.
- Core behavior and integration tests, run by GitHub Actions on pushes and pull requests to `master`.

Entity/component storage, system scheduling, queries, prototype inheritance, and snapshot serialization are still planned. The example is a setup demonstration, not a complete game companion.

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

The development server uses port 5002. The sample data contains **card 4**, with setups for 3, 4, and 5 players. Unknown cards display an error; visiting the setup page before choosing a card prompts for a selection. No credentials, database, or external application services are needed.

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
