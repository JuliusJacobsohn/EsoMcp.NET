# EsoMcp.NET

[![CI](https://github.com/JuliusJacobsohn/EsoMcp.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/JuliusJacobsohn/EsoMcp.NET/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/JuliusJacobsohn/EsoMcp.NET)](https://github.com/JuliusJacobsohn/EsoMcp.NET/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> [!WARNING]
> Created with substantial help from **OpenAI Codex** and tested against my own use cases. Please do not treat this project as a measure of my abilities as a developer, for better or worse.

A local **.NET 10** MCP server for Elder Scrolls Online. It lets an AI assistant query your characters, account inventory, crafting knowledge and saved builds from SQLite. Before each database-backed tool call, changed addon files are automatically imported using [EsoData.NET](https://github.com/JuliusJacobsohn/EsoData.NET).

No hosted service, account signup, SQL Server, game injection or custom ESO addon is needed. Your data stays on your computer; no account data or game catalog is distributed in this repository.

## How it works

```text
SavedVariables + installed catalogs
                │ refresh changed files before queries
         EsoData.NET → Import
                │
              SQLite ← MCP queries
```

A shared request handler refreshes sources before database-backed tools run; the tools themselves use SQLite only. Content hashes skip unchanged files. Queries still work after sources become unavailable, using their last successful imports. The MCP tool classes know nothing about addon file layouts; only the import project references the parsing library.

SQLite retains the complete parsed inputs, including fields not yet modeled, plus searchable projections and timestamps. Lua tables preserve numeric versus string keys and sparse indices. Unknown game IDs are retained rather than rejected. The database is the latest successful import per source, **not a history of every game session**.

## Install and connect

1. Install the **.NET 10 runtime** (or SDK).
2. Download `eso-mcp-portable.zip` from [Releases](https://github.com/JuliusJacobsohn/EsoMcp.NET/releases), extract it to a stable directory, and keep the entire directory together.
3. Copy `settings.example.json` to a private `settings.json` and replace its paths with yours. Use absolute paths. Add more `locations` for other local saved-data directories, or `catalogPaths` for external EsoData.NET catalog JSON files.
4. In ESO, visit the characters/storage you want the addons to observe, then `/reloadui` or log out normally to save their data.
5. Register the server in your MCP client. Its first data query imports your sources automatically.

Example PowerShell, assuming the release is extracted to `C:/Tools/EsoMcp`:

```powershell
codex mcp add eso -- dotnet C:/Tools/EsoMcp/eso-mcp.dll --config C:/Tools/EsoMcp/settings.json
codex mcp get eso
```

Start a new Codex session or restart the app if an existing session does not discover the newly registered tools. Other stdio MCP clients use the equivalent configuration:

```json
{
  "mcpServers": {
    "eso": {
      "command": "dotnet",
      "args": ["C:/Tools/EsoMcp/eso-mcp.dll", "--config", "C:/Tools/EsoMcp/settings.json"]
    }
  }
}
```

Without `--config`, Windows defaults to the standard Documents/ESO/live folders and a database at `%LOCALAPPDATA%/EsoMcp/data.db`. On other platforms, provide paths to your locally accessible game data. Database-backed tool calls automatically refresh first, including `database_status` and `export_saved_build`. Protocol initialization/tool discovery and the standalone crafting-link encoder do not trigger imports. With `--config`, only the locations you explicitly list are used. Set `"autoRefresh": false` or pass `--no-auto-refresh` to refresh only on explicit requests. An empty `locations`/`catalogPaths` configuration or `--database-only` serves an existing database without import inputs.

The server uses stdio: the MCP client launches it as needed, and stdout carries only protocol messages. `--refresh`, `--status` and `--help` are standalone commands that print output and exit. CLI `--status` reads the existing database without refreshing; MCP `database_status` uses the automatic refresh setting. `--refresh` exits with code 1 if a source fails; absent optional addon files are reported as `missing` without failing the command.

## Available tools

| Tool | Purpose |
|---|---|
| `database_status` | Counts, source paths, save/import times, diagnostics and refresh outcomes |
| `refresh_database` | Optional manual refresh; `force: true` reprojects unchanged files |
| `list_characters` | Names and identities, filterable by name/account/server |
| `get_character_state` | Latest character summary or selected research, champion, statistics, skills or equipment section, with observation/source times |
| `search_inventory` | Owned stacks across characters and storage; filter by item/set/location owner |
| `get_knowledge` | Recipe, plan, motif, grimoire and script knowledge, including unknown results |
| `list_records` | Discover saved builds, observations, research, collections and metadata |
| `get_record` | Retrieve a selected detail record, optionally one top-level field |
| `find_sets` | Resolve set names and IDs from imported catalogs |
| `find_item_definitions` | Resolve item IDs, set membership and supplied trait/equipment metadata |
| `find_skill_definitions` | Resolve skill IDs when a skill catalog is supplied |
| `export_saved_build` | Return a stored profile as native CSPS text |
| `create_csps_equipment_import` | Create an equipment-only native CSPS import from structured slots |
| `create_hub_build_import` | Patch bars and CP in an ESO-Hub build link for CSPS Import Link |
| `create_crafting_import` | Encode resolved item IDs, level, quality, style and optional enchantment into Lazy Set Crafter links |
| `refresh_item_metadata` | Explicitly download item metadata for selected local LibSets set IDs into SQLite |
| `create_semantic_crafting_import` | Resolve set/piece/trait choices from SQLite and create one per-item-glyph crafting import |

List/search tools use `offset` and `limit` (1–200) and return `hasMore`. Character keys and record keys are opaque strings returned by the tools. Use canonical server `EU` or `NA`; `EU Megaserver`/`NA Megaserver` source labels are normalized during import. Other world labels are preserved. Names can change without changing identity. Accounts and servers are separate ownership pools.

Example requests to an assistant:

- “Show me which ESO characters are available.”
- “Find the crafter's known recipes and research records.”
- “Show my tank's unspent CP, empty champion slots and saved character stats.”
- “Find this set, then show all owned pieces on my account, including the bank.”
- “Export my saved tank build for CSPS.”
- “Generate purple level-32 crafting links for these resolved item IDs.”

## Data coverage

| Installed source | Imported data |
|---|---|
| IIfA | Character directory; item links, counts and observed locations; storage metadata |
| LibCharacterKnowledge | Names, recipes, plans, motifs, scribing, research flags and timers |
| Caro's Skill Point Saver | Characters with or without profiles; saved build text, skills/passives, bars, attributes, CP, gear, metadata |
| uespLog | Saved character observations: level, class/race, skill points, purchased skills/line ranks, CP budgets/stars/slots, research summaries/timers, current/per-bar/advanced stats, equipped gear and inventory |
| LibMultiAccountSets | Current/legacy account collection masks and scan times |
| Dolgubon's Lazy Set Crafter | Saved crafting requests; full source retained, including unprojected fields |
| Installed LibSets | Set names and item-ID membership |
| External EsoData.NET JSON catalogs | Additional item/skill definitions, collection-piece and research mappings, provenance |

Supported filenames are `IIfA.lua`, `LibCharacterKnowledge.lua`, `CarosSkillPointSaver.lua`, `uespLog.lua`, `LibMultiAccountSets.lua`, and `DolgubonsLazySetCrafter.lua`. Addons are optional and provide different, partial observations. Installing the server does not cause the game to record information its addons have never scanned.

Saved builds are **plans**, not proof that their allocations are applied. Observed skills/stats are exposed separately as `character_state` records when that data exists. Unknown knowledge remains unknown. Research timers expiring are not treated as evidence of newly learned traits. Collection masks need matching piece metadata to resolve individual slots; research indices likewise need a matching catalog/signature.

Use `list_characters` to obtain a `character_key`, then `get_character_state(characterKey, section)`:

- `summary` (default): basic character fields and available sections.
- `research`: per-craft and per-line known/total counts, open slots, original trait display labels and active timers with the research scan timestamp. Display labels are not numeric trait-ID mappings; `[bracketed]` traits can still be researching. This does not resolve the separate indexed LCK research flags.
- `champion`: spent/unspent points by discipline, named purchased stars with both champion skill and ability IDs, and slot assignments. An explicit slot value of zero means empty; an absent slots table means unobserved.
- `statistics`: current and saved-bar stats, separate addon computations and advanced flat/percent values. Original names and units are retained. Cached bars can reflect different moments/buffs; they have no individual timestamps.
- `skills`: purchased abilities, skill-line ranks and skill-point counts. A line rank does not mean all its skills are purchased.
- `equipment`: observed equipped item links. `all`: all normalized fields, without raw Lua details.

Every response includes `available`, character identity, observation/source timestamps and latest refresh status. Missing observations/sections return `available: false`, not zero progress. The tool selects one latest character-state observation; it does not fill gaps by silently merging older snapshots or saved builds. Raw detail records remain accessible through `get_record`.

LibSets supplies set membership, not complete crafting-piece/trait definitions or a full skill database. `refresh_item_metadata` explicitly fetches current UESP metadata for the selected installed LibSets set IDs, stores it in local SQLite, and leaves later resolution/database requests offline. Additional metadata can also be imported through `catalogPaths`; see [EsoData.NET catalogs](https://github.com/JuliusJacobsohn/EsoData.NET#resolve-ids-from-refreshable-catalogs). `create_semantic_crafting_import` accepts one structured selector per item and supports a distinct glyph/style for every item. The server does not infer craftability, learn skills, equip gear or submit crafting/mail actions.

## Refresh and freshness

- Saves are **last written disk data**, not live game memory. Source timestamps and observation timestamps are kept separately.
- Automatic refresh checks every configured source before each database-backed tool call. Only changed contents are reimported. The manual refresh tool runs once, without a redundant automatic refresh, and accepts `force: true` when needed.
- Each changed source is replaced transactionally. A malformed, unavailable or changing file leaves its previous successful import intact; inspect `database_status` for failures/staleness. There is no silent clearing of missing sources.
- Import fingerprints include content, projection revision, parser version and the configured world mapping. Upgrades that change projections, parser updates and mapping changes reprocess unchanged saves automatically. `refresh_database(force: true)` remains available for an explicit reimport. This does not pin or reject ESO API versions.
- Queries prefer one inventory source per account/server (inventory addon first, then newest source at equal priority) to avoid summing duplicate observations. `includeAlternateSources: true` exposes all sources for comparison; do not sum them together. This preference is per account, so secondary-only locations may require that option.
- uespLog records without a world remain explicitly `unresolved:…`. Set a location's `defaultServer` only when you know which world its otherwise unqualified records belong to.
- Removing a source from configuration does not delete its previous observations. For a fresh import from only the current configuration, select a new database path.

Complete parsed sources remain private in SQLite; the MCP API does not return full source documents. Detail projections omit common credential fields. Treat the database as personal account data and do not publish it. No uploads or downloads run as part of refresh or queries.

## Development and releases

```shell
dotnet build EsoMcp.sln -c Release
dotnet test EsoMcp.sln -c Release --no-build
python scripts/smoke_mcp.py
dotnet publish src/EsoMcp.Server -c Release -o artifacts/server -p:UseAppHost=false
```

The SDK is .NET 10; the protocol smoke test uses Python 3's standard library. Tests use synthetic inputs and temporary databases. The smoke test starts the actual server, initializes MCP, discovers/calls every tool, and checks invalid requests. It verifies that a normal query imports initial/changed files automatically, malformed or missing files retain previous data, and manual mode defers imports until requested. An explicit `--live-database` mode is available for local integration checks; it imports only paths supplied by the operator and never writes game files.

| Project | Responsibility |
|---|---|
| `EsoMcp.Core` | SQLite schema, records, queries and service interfaces; no addon dependency |
| `EsoMcp.Import` | EsoData.NET adapters, import orchestration and format encoding |
| `EsoMcp.Server` | CLI/composition root and MCP tools; query tools depend on Core interfaces |

CI builds, tests and exercises MCP on Windows and Linux. Push a `v<Version>` tag matching `Directory.Build.props` to publish a portable release ZIP. The server uses the published **EsoData.NET NuGet package**; no sibling checkout is required. The server itself is distributed as an app, not another NuGet library.

Database and private configuration files are ignored by Git. Code is MIT licensed; imported game/addon catalogs retain their own licenses and are not bundled. This is an unofficial project, unaffiliated with ZeniMax, Bethesda or the addon authors.
