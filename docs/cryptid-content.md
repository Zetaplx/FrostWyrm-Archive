# Cryptid Companion content guide

The first playable version uses the reference image supplied by the project owner. The six tile orientations and all six structure positions/types were confirmed in chat. It is labeled **Reference expedition**, not assigned an unverified printed card number. It supports 3, 4 and 5 clue-holders and all 23 Normal Mode clue categories.

The companion generates unique-solution clue assignments for this board. It does not pretend that generated assignments correspond to printed clue-book numbers. Choose the human clue by its rule description. Opponent assignments remain hidden in the interface; no opponent takes independent turns.

## Resource files

All files are under `apps/CryptidCompanion/wwwroot/Resources` and load through Archive's attribute-discovered `PrototypeManager`, `DataDefinition`, `DataField`, and typed `ProtoId<T>` deserialization.

| File | Contents |
| --- | --- |
| `tiles.yml` | Six physical tiles, each with 18 row/column terrain/animal definitions |
| `companion-setups.yml` | Reference board, structures, and supported player counts |
| `clues.yml` | 10 terrain pairs, 5 terrain-distance clues, any-animal clue, 2 specific-animal clues, 2 structure-kind clues, 3 structure-color clues |
| `clue-books.yml` | Player colors and typed clue pools |
| `setup-cards.yml` | Historical printed-card-4 clue numbers, without verified board/book mapping; not offered for play |

Tile cell coordinates, tile origins, and structure positions are **zero-based** in YAML. The UI labels rows A–I and columns 1–12. The reference board has flat-top hexes with odd columns shifted down; there are no wraparound edges. `flipped: true` rotates the tile 180 degrees, reversing both local row and column. The shared `HexCoordinates` abstraction converts offset positions to integer axial coordinates; the original structural conversion's truncation behavior is preserved, including negative indices.

The confirmed tile rows, left to right, are **5 flipped / 4 upright**, **1 flipped / 6 flipped**, **2 upright / 3 flipped**. Structures: **A4 white stone, D4 white shack, B9 blue shack, E8 blue stone, E9 green shack, I8 green stone**.

## Add a layout

Copy the complete board and setup-card entries from `companion-setups.yml`, give each a new ID, and change the layout fields below. Keep all six placements and all six structures in your actual definition:

```yaml
- type: board
  id: my-board
  orientation: FlatTop
  rows: 9
  columns: 12
  tiles:
  - tile: tile5
    origin: {row: 0, column: 0}
    flipped: true
  # Five more tile placements, covering the 9 × 12 rectangle exactly.
  structures:
  - position: {row: 0, column: 3}
    kind: Stone
    color: White
  # Five more structures: one stone and one shack in each normal color.

- type: setupCard
  id: my-layout
  name: My layout
  source: Describe the verified source of this layout here.
  board: my-board
  setups:
  - playerCount: 3
    books: [Alpha, Beta, Gamma]
  - playerCount: 4
    books: [Alpha, Beta, Gamma, Delta]
  - playerCount: 5
    books: [Alpha, Beta, Gamma, Delta, Epsilon]
```

This excerpt intentionally omits five tiles/structures; it is a schema illustration, not a complete import. Copying the existing full file and changing IDs is the quickest valid starting point.

For a fixed printed assignment, a setup can restrict each book's candidate clues:

```yaml
  - playerCount: 3
    books: [Alpha, Beta, Gamma]
    candidates:
      Alpha: [water-forest]
      Beta: [near-bear, near-cougar]
      Gamma: [near-white]
```

Keys must name books in that setup; clue IDs must exist in those books. A singleton pool fixes a book's clue. An omitted pool uses that book's entire clue list. Only human clues compatible with at least one unique-solution combination are offered; an unsatisfiable combination never starts a game.

Import a complete `.yml`/`.yaml` pack using **Content packs & printed cards** at camp, before starting a game. Imports are validated transactionally and retained in this browser. A failed import keeps the previous working catalog. For shipped resources, add the file to the list in `Program.cs` (or append entries to an already-loaded file). Typed prototype discovery does not require individual type registration.

## Rules and validation

Normal Mode supports `TerrainPair`, `NearTerrain`, `NearAnimal`, `NearStructure`, and `NearColor`. Parameters live in YAML; the same rule evaluates every instance. Distance includes zero. Negative/black-structure clues and Advanced Mode are intentionally outside this first version.

Startup/import diagnostics include filenames and YAML entry lines, duplicate IDs, required fields, missing typed references, normal rule parameters, player-count/book mismatches, invalid cells, tile overlaps/gaps, and invalid structures. An unsatisfiable human clue is filtered out of clue selection. A pack without any playable combination displays an explicit selection error instead of starting a broken session.

## Saves and secrecy

Local browser saves are versioned and bound to a content fingerprint. They preserve assignments, deterministic random progress, public pieces, phase, owed human cubes and the journal. Reload does not reselect clues. Incompatible or malformed saves display a recovery notice rather than crashing the app. Starting a new game replaces an incompatible save.

This is an entirely client-side app. Hidden assignments are not rendered or logged, but local storage and application memory are inspectable by the device owner; the app does not claim server-side secret protection. Do not inspect saves if you want to preserve the puzzle.

## Data provenance

Tile terrain/animal data was transcribed from the MIT-licensed [nicuveo/uncryptid board definitions](https://github.com/nicuveo/uncryptid/blob/3a234c12b1ae7bd0b68a8456bf9ff8a78b94cd0f/lib/uncryptid/board.rb). Its license is retained in [third-party/uncryptid-LICENSE.txt](third-party/uncryptid-LICENSE.txt). Tile 1's final forest cell is corrected to bear territory, corroborated by the supplied reference image and independent digital transcriptions. No game artwork is redistributed; the SVG reference board is rendered from domain data. Cryptid belongs to its designers and publisher; this is an independent companion for game owners.
