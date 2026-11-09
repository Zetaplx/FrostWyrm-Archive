# Development parity with FrostWyrm

The implementation baseline is `Zetaplx/FrostWyrm` at commit [`ffa990b43176acc3dbbf423fee2289e8387a9aed`](https://github.com/Zetaplx/FrostWyrm/tree/ffa990b43176acc3dbbf423fee2289e8387a9aed) (`WIP tiles`). The port retains Archive's existing documentation, license, Core project path, and main test project path.

## Imported capabilities

| Capability | Implementation |
| --- | --- |
| Runtime initialization | `FrostWyrm.Initialize` discovers core and supplied assemblies; `Resolve<T>` retrieves dependencies |
| Dependencies | Sealed-instance registration, assignable-type lookup, field/property injection, and assembly discovery |
| YAML | Data definitions, required fields, scalar/collection/dictionary deserialization, custom serializers, and propagation flags |
| Prototypes | Assembly-discovered types, YAML loading, typed IDs, filtering, and type/ID lookup |
| Blazor | `AddFrostWyrm` bridges the runtime's instances into `IServiceCollection` |
| Math | Hex coordinates, distance, adjacent-cell access, structural coordinates, grid joins, and YAML deserialization |
| Example | CryptidCompanion WebAssembly app, setup screens, and source YAML resources |
| Validation | All 51 source core tests and the source GitHub Actions workflow |

The Core project retains its `FrostWyrm.Archive.Core` assembly name. Public namespaces remain `FrostWyrm.Archive`, so imported callers keep their existing API names. References from the imported projects point to the Core project. The source's empty Blazor test project is replaced with an integration suite covering the bridge, hex utilities, and sample-card loading and selection.

## Integration repairs

The baseline app references a missing `GameManager` and has no prototype type for its `setupCard` YAML. This port adds a manager and the setup-card data definitions, selects the shipped card 4 by default, validates card/player selections, and displays the selected clue references. Invalid selections preserve the previous setup. The setup route also handles a fresh visit without existing state. An unused link to a nonexistent scoped stylesheet is removed.

## Remaining scope

Neither starting point implements entity/component storage, an ECS query engine, system scheduling, an event bus, or a resource manager. Archive's `App` lifecycle and CLI/notes example remain roadmap items.

Prototype parent-data inheritance remains unfinished. Assignable CLR-type lookup is implemented, but is separate from merging YAML parent definitions. YAML support currently reads definitions; it does not write snapshots or provide round-trip serialization.

CryptidCompanion contains one setup card, empty clue/clue-book resource placeholders, and unfinished board data types. Its setup flow demonstrates the infrastructure; full clue content, board assembly, and game behavior remain future work.

The runtime continues to use the source's singleton and reflection-based registration model. It does not yet provide multiple independent app instances. Native AOT and trimmed deployment compatibility have not been established.
