# Cryptid Companion content guide

The first playable version uses the reference image supplied by the project owner. The six tile orientations and all six structure positions/types were confirmed in chat. It is labeled **Reference expedition**, not assigned an unverified printed card number. It supports 3, 4 and 5 clue-holders and all 23 Normal Mode clue categories.

Every supported player count has a fixed, tested set of clues stored on the setup card. The player chooses only their color; the card assigns their clue. The test assignments are not asserted to match printed clue-book numbers. Opponent assignments remain hidden in the interface; no opponent takes independent turns.

## Resource files

All files are under `apps/CryptidCompanion/wwwroot/Resources` and load through Archive's attribute-discovered `PrototypeManager`, `DataDefinition`, `DataField`, and typed `ProtoId<T>` deserialization.

| File | Contents |
| --- | --- |
| `tiles.yml` | Six physical tiles, each with 18 row/column terrain/animal definitions |
| `companion-setups.yml` | Reference board, structures, and supported player counts |
| `clues.yml` | 10 terrain pairs, 5 terrain-distance clues, any-animal clue, 2 specific-animal clues, 2 structure-kind clues, 3 structure-color clues |
| `clue-books.yml` | Correct physical book colors and typed clue definitions |
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
    assignments:
      Alpha: water-desert
      Beta: near-animal
      Gamma: near-stone
```

This excerpt intentionally omits five tiles/structures; it is a schema illustration, not a complete import. Copying the existing full file and changing IDs is the quickest valid starting point.

## Set the card’s clues (the main manual edit)

Every `setups` entry must contain **one fixed clue for every listed book**. There is no clue picker and no randomized assignment. Changing the human’s color or the random seed never changes these assignments.

```yaml
  - playerCount: 3
    books: [Alpha, Beta, Gamma]
    assignments:
      Alpha: water-desert
      Beta: near-animal
      Gamma: near-stone
```

This is the complete, playable 3-player test assignment for the supplied reference board. In `companion-setups.yml`, change the values after the book names when adding a real card. Give each card its own ID and add analogous entries for 4/5 players. All book names in `books` must have exactly one assignment; no extra book names are allowed. Startup validates all references, legal opening spaces, and an intersection of exactly one habitat before offering the setup. Invalid or incomplete assignments never start a game.

Book colors are **Alpha red, Beta green, Gamma blue, Delta orange, Epsilon black**. Structure colors remain blue, green, white and are independent of player colors.

Available clue IDs are human-readable:

- Terrain pairs: `water-forest`, `water-desert`, `water-mountain`, `water-swamp`, `forest-desert`, `forest-mountain`, `forest-swamp`, `desert-mountain`, `desert-swamp`, `mountain-swamp`.
- Within 1 of terrain: `near-water`, `near-forest`, `near-desert`, `near-mountain`, `near-swamp`.
- Within 1 of any animal territory: `near-animal`.
- Within 2 of a specific animal territory: `near-bear`, `near-cougar`.
- Within 2 of a structure kind: `near-stone`, `near-shack`.
- Within 3 of a structure color: `near-blue`, `near-green`, `near-white`.

Import a complete `.yml`/`.yaml` pack using **Content packs & printed cards** at camp, before starting a game. Imports are validated transactionally and retained in this browser. A failed import keeps the previous working catalog. For shipped resources, add the file to the list in `Program.cs` (or append entries to an already-loaded file). Typed prototype discovery does not require individual type registration.

## Rules and validation

Normal Mode supports `TerrainPair`, `NearTerrain`, `NearAnimal`, `NearStructure`, and `NearColor`. Parameters live in YAML; the same rule evaluates every instance. Distance includes zero. Negative/black-structure clues and Advanced Mode are intentionally outside this first version.

Startup/import diagnostics include filenames and YAML entry lines, duplicate IDs, required fields, missing typed references, normal rule parameters, player-count/book mismatches, invalid cells, tile overlaps/gaps, and invalid structures. Missing, incomplete, duplicate, and non-unique card assignments are reported during content loading instead of starting a broken session.

## Saves and secrecy

Local browser saves are versioned and bound to a content fingerprint. They preserve assignments, deterministic random progress, public pieces, phase, owed human cubes and the journal. Reload does not reselect clues. Incompatible or malformed saves display a recovery notice rather than crashing the app. Starting a new game replaces an incompatible save.

Display preferences (pieces and coordinate labels) are saved separately from game content, so changing them does not change clue assignments or invalidate a game. Completed games appear under Past games at camp with a read-only Review link. Each archive includes its original YAML content and a stable session ID; even repeated seeded games are separate expeditions. Existing completed saves are archived when loaded. Reviews create an independent session and never replace an active expedition.

Legend highlights use only public terrain, animal/structure features, and recorded player pieces, never hidden clue rules. They support hover, keyboard focus, and tap/click pinning.

This is an entirely client-side app. Hidden assignments are not rendered or logged, but local storage and application memory are inspectable by the device owner; the app does not claim server-side secret protection. Do not inspect saves if you want to preserve the puzzle.

## Data provenance

Tile terrain/animal data was transcribed from the MIT-licensed [nicuveo/uncryptid board definitions](https://github.com/nicuveo/uncryptid/blob/3a234c12b1ae7bd0b68a8456bf9ff8a78b94cd0f/lib/uncryptid/board.rb). Its license is retained in [third-party/uncryptid-LICENSE.txt](third-party/uncryptid-LICENSE.txt). Tile 1's final forest cell is corrected to bear territory, corroborated by the supplied reference image and independent digital transcriptions. No game artwork is redistributed; the SVG reference board is rendered from domain data. Cryptid belongs to its designers and publisher; this is an independent companion for game owners.
