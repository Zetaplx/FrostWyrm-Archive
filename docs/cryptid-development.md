# Develop, test, and publish Cryptid Companion

Use .NET SDK 10.0 or newer. No credentials, database, backend, or API is required. The app is standalone Blazor WebAssembly.

```sh
dotnet restore FrostWyrm-Archive.slnx
dotnet build FrostWyrm-Archive.slnx --no-restore
dotnet test FrostWyrm-Archive.slnx --no-build --no-restore
dotnet run --project apps/CryptidCompanion --launch-profile http
```

The development profile uses port 5002. In this prepared cloud environment, activate the workspace SDK and writable cache/runtime settings with `source /workspace/.cloud-setup/env.sh` before commands.

`GameManager` owns the session and legal actions. Board geometry and clue evaluation live in `Domain`, content in YAML prototypes, persistence in `Services`, and presentation in Razor components. Cryptid-specific rules stay out of Archive. `FrostWyrm.Math.HexCoordinates` supplies the reusable offset/axial boundary, alongside the existing vertex-capable `Hex` API.

The .NET suite exercises all 23 rules over all 108 cells against an independent graph-traversal oracle, confirmed image placements, negative-index coordinate round trips, valid clue intersections, opening cubes, question/search legality, first-no stopping, penalties, deterministic sessions, save/restore and content validation. Existing Archive tests remain in place.

## Browser tests

Requires Node.js and Playwright Chromium. After building:

```sh
npm ci --prefix tests/browser
npm exec --prefix tests/browser -- playwright install chromium
npm test --prefix tests/browser
```

The suite starts a development server on port 5010 unless one is already running. Set `CHROMIUM_EXECUTABLE_PATH` to use a system Chromium installation. On this cloud machine, also use `npm --cache /workspace/.npm` (or export `npm_config_cache=/workspace/.npm`) because the home directory is read-only.

Browser tests play complete 3-, 4- and 5-clue-holder games, test question/search penalties and wins, verify simulated clues are absent from the UI, check reload continuity, check a phone viewport, and exercise malformed saves/imports. The offline case is explicitly skipped against development assets, which intentionally use a pass-through worker.

## Production / offline validation

```sh
dotnet publish apps/CryptidCompanion -c Release -o artifacts/cryptid
python3 scripts/serve.py artifacts/cryptid/wwwroot --port 5011
```

In another terminal:

```sh
CRYPTID_BASE_URL=http://127.0.0.1:5011 CRYPTID_PUBLISHED=1 npm test --prefix tests/browser
```

This runs all browser cases, including resuming a cached game with browser networking disabled. The production worker verifies manifest integrity during precaching. It caches application assets and YAML, uses SPA navigation fallback, and retains old assets until the next worker activates. It does not force a running game to reload during an update.

Deploy the contents of the published `wwwroot` to a static HTTPS host with navigation fallback to `index.html` and `.wasm` served as `application/wasm`. This repository does not automatically deploy a public site. Service workers require HTTPS or localhost. Initial loading must complete online before offline use.

Reflection-based Archive discovery requires retaining the assemblies' types, constructors, properties and attributes. `PublishTrimmed=false` explicitly preserves them in this version. The published output is larger; native AOT/trimming is a separate optimization task. `wasm-tools` is optional for this interpreted/JIT workflow.

## Current scope

The one confirmed reference layout is playable from physical setup through a winning search for 3–5 clue-holders. All Normal Mode clue categories are present. More printed setup cards and clue-number mappings remain content work, as agreed for this first version; Advanced Mode is outside scope. The companion simulates only clue responses and required random cube placements, never independent opponent turns or strategy.
