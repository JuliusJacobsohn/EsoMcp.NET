# EsoMcp.NET

[![CI](https://github.com/JuliusJacobsohn/EsoMcp.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/JuliusJacobsohn/EsoMcp.NET/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/JuliusJacobsohn/EsoMcp.NET)](https://github.com/JuliusJacobsohn/EsoMcp.NET/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> [!WARNING]
> Created with substantial help from **OpenAI Codex** and tested against my own use cases. Please do not treat this project as a measure of my abilities as a developer, for better or worse.

A local **.NET 10 MCP server** for Elder Scrolls Online. Load your account, maintain named target builds, edit them incrementally, compare them with what you own and have learned, and export CSPS or crafting imports.

[EsoData.NET](https://github.com/JuliusJacobsohn/EsoData.NET) owns parsing, account assembly, typed models, planning and formats. SQLite persists refreshed account documents and authored build plans separately. Every relevant request reads the local addon saves again; refreshing never overwrites plans. No account data is uploaded and no game files are modified.

## Install

1. Install the .NET 10 runtime or SDK.
2. Download and extract `eso-mcp-portable.zip` from [Releases](https://github.com/JuliusJacobsohn/EsoMcp.NET/releases). Keep its files together.
3. Copy `settings.example.json` to `settings.json` and configure source directories if the Windows defaults are unsuitable.
4. Register the server:

```powershell
codex mcp add eso -- dotnet C:/Tools/EsoMcp/eso-mcp.dll --config C:/Tools/EsoMcp/settings.json
```

Other clients use the equivalent stdio command and arguments. Without a configuration file, Windows uses Documents/Elder Scrolls Online/live and `%LOCALAPPDATA%/EsoMcp/data.db`. On other systems, provide local source paths. Save in ESO with `/reloadui` or logout after visiting relevant characters/storage. Files contain the last saved observations, not live game memory.

Existing MCP sessions may retain their old process/tool schemas; reconnect the server or start a new session after upgrading. The server logs to stderr and reserves stdout for MCP messages.

## Tools

| Tool | Purpose |
|---|---|
| `inspect_account` | Discover accounts or batch compact queries for characters, budgets, skills, inventory, equipment, knowledge, research, collections, saved builds and source coverage |
| `resolve_definitions` | Batch name/ID searches in local skill, set, item or champion catalogs |
| `edit_build` | Create/copy/read/list/update/delete named targets and working builds with revision protection |
| `analyze_build` | Validation, differences, prerequisite status, equipment candidates, set counts, leveling candidates or crafting shortages |
| `export_build` | Export explicitly selected CSPS sections or crafting orders, with a validation report |
| `verify_build` | Compare an exported plan revision with newly saved game state, returning differences only |
| `refresh_catalog` | Explicitly fetch selected UESP definitions; account information is never sent |

Names must resolve unambiguously. IDs are also accepted. Query results have counts, pagination and optional field selection; raw Lua, item-link blobs and repeated provenance do not accompany ordinary rows.

## Typical workflow

Call `inspect_account` without arguments to discover accounts. Then batch the information needed for a decision:

```json
{
  "account": "EU/@EXAMPLE",
  "queries": [
    {"section":"summary", "character":"Example Character"},
    {"section":"skills", "character":"Example Character", "unfinishedOnly":true},
    {"section":"inventory", "setIds":[123], "limit":20}
  ]
}
```

Create a working build through `edit_build`:

```json
{"action":"create", "account":"EU/@EXAMPLE", "character":"Example Character", "name":"Exploration"}
```

The response contains the plan ID and revision. Modify only what changed:

```json
{
  "action":"update", "id":"RETURNED_ID", "expectedRevision":1,
  "patch":{
    "skills":{"Undo":{"rank":1,"morph":0,"isPassive":false}},
    "barSlots":{"front.6":"Undo"},
    "attributes":{"health":0,"magicka":64,"stamina":0},
    "constraints":{"fullRespec":true,"requireFullBars":true,"avoidMaxedMorphs":true,"reserveSkillPoints":5}
  }
}
```

Skill names resolve through the local catalog. Bar slots are `front.1`–`front.6` and `back.1`–`back.6`; slot 6 is the ultimate. CP slots use 1–12 in Craft/Warfare/Fitness order. Dictionary entries set to null remove/clear them; omitted patch properties remain unchanged. Supplied constraints, requirements, guides and crafting arrays replace their corresponding sections. Edits are atomic, and a stale revision cannot overwrite another chat's work.

Create from existing native CSPS text using `nativeCsps`. Read selected plan sections with `action:"read"` and `section:"build"`, `"requirements"`, `"guides"`, `"constraints"` or `"crafting"`. The default read is a compact summary. A target can contain unmet future requirements without claiming they are currently available.

Export through `export_build`:

```json
{"id":"RETURNED_ID", "sections":"Skills, Bars, Attributes"}
```

Native **CSPS text** is the default format. Select only the listed sections in the addon. Structural errors block export; `requireReady:true` also blocks unknown/unmet requirements. By default, future targets can be exported with explicit findings. Generating/importing text is not proof that ESO applied it.

After applying and saving in ESO, call `verify_build`:

```json
{"id":"RETURNED_ID", "expectedRevision":2, "sections":"Skills, Bars, Attributes"}
```

It compares the chosen revision with newly observed data and returns differences, including unobserved sections.

## Targets, requirements and crafting

`edit_build` accepts `patch.target`, a typed guide setup with abilities/scripts, passives, equipment choices, champion selections, masteries and consumables. Creating a guide target starts with an empty executable allocation so the current character's unrelated settings cannot be mistaken for the guide. `action:"read", section:"target"` returns the saved setup. `analyze_build` automatically compares it for `validation` or `differences`; use `targetSection` to filter domains such as `abilities`, `equipment` or `champion`. Unspecified guide point amounts stay null. Trait alternatives stay alternatives. Set-only matches are reported as partial. Export requires a separate, resolved executable build rather than silently exporting incomplete guide data.

A plan stores guide URLs with retrieval dates and variant names. Add explicit requirements with IDs, kinds, priorities, optional flags and dependency IDs. Supported kinds are `Skill`, `SkillLine`, `Knowledge`, `Item`, `Allocation` and `Manual`. Missing source information produces `unknown`. Manual milestones can have a completion value and evidence. Dependencies must refer to existing requirements and cannot form cycles. GitHub issue creation/updating remains the assistant's separate responsibility.

Skill progression and purchased allocation are separate. A refunded skill is not automatically unlearned, but sources do not reveal every unpurchased morph's progression. A maxed base ability does not satisfy a requirement for its morph. Respec analysis uses total points and exposes currently unspent points separately.

Crafting orders contain item ID, level/CP, quality, style, quantity and per-item enchantment ID/quality. `export_build` with `format:"crafting"` generates Lazy Set Crafter links. `analyze_build` with `section:"crafting"` and `crafter` compares exact external-catalog recipes with account materials and knowledge. If exact recipe/cost data is unavailable, it returns that gap instead of inventing a material shopping list. Food/potion queue exports are not implied by support for equipment links.

## Catalogs and known data limits

Installed LibSets provides set membership. Previously downloaded UESP definitions remain usable in the existing database; `refresh_catalog` retrieves more in batches. `catalogPaths` accepts EsoData.NET JSON definitions, including CP rules and exact crafting recipes. Definitions are independent of the compiled assembly.

- Inventory is assembled by location from available providers. Source diagnostics and scan times are available through `sources`; absent coverage is not proof of non-ownership.
- The current sources do not expose all unlocks, free-skill costs, crafting bills, binding information or CP prerequisite rules. Reports distinguish these gaps from known failures.
- UESP includes automatically granted skills. When modeled costs do not reconcile with the game's recorded spent total, incremental/refundable costs are null with a diagnostic.
- CP validation can preserve an observed allocation without inventing new rules. Changed allocations require catalog cap/discipline/prerequisite evidence to be reported ready.
- Equipment candidates are observed owned variants, not a guarantee of transferability, enchantment equivalence or optimal damage. Recorded character statistics are not recomputed combat simulations.
- Scribed native ability IDs are represented as negative IDs in bar edit selectors; script choices live in `scribedSkills`. They are distinct from ordinary ability IDs.

## Persistence and migration

SQLite stores account JSON in `account_documents` and authored plans in `build_plans`. A successful full configured-source load replaces the account snapshot set in one transaction; plans are independent. Parsing/read failures do not replace snapshots. Missing optional sources are reported. Explicit offline mode uses the last persisted documents and is visibly labeled.

The 1.1 tool surface replaces the earlier per-record database tools. Existing SQLite catalog/source tables are retained for downloaded metadata and compatibility, but the new account query path does not reproject personal data into those tables. Saved CSPS profiles remain accessible under each character. Existing generated import files can be brought into named plans with `nativeCsps`.

CLI options:

```text
--config FILE          JSON configuration
--database FILE        SQLite path
--saved-variables DIR  Source directory override
--addons DIR           Installed addon directory (with --saved-variables)
--server NAME          Default world for otherwise unqualified sources
--database-only        Explicit offline use of persisted account documents
--no-auto-refresh      Explicit offline mode
--refresh              Load/persist accounts and print a compact summary
--status               Inspect persisted account/plan summaries
--help                 Full help
```

## Development and verification

```powershell
pwsh -File scripts/Restore-EsoData.ps1
dotnet test EsoMcp.sln -c Release
dotnet run --project samples/EsoMcp.Smoke -c Release
```

The smoke program uses the official .NET MCP client against the real stdio server. It verifies discovery, account reads, draft mutation, stale-write rejection, export, comparison and persistence across server restarts. Its default fixture is synthetic. Add `-- <server.dll> --live` to exercise local game data; it creates and deletes only its own temporary verification draft and never changes game files. `--request FILE` executes one explicit JSON `{tool,arguments}` request for integration diagnostics.

For simultaneous library/server development with sibling repositories, `-p:UseLocalEsoData=true` references the local library. Releases and CI use the published NuGet package. Windows and Ubuntu CI run tests and the .NET protocol smoke check.

See [architecture](docs/account-architecture.md) and the [library](https://github.com/JuliusJacobsohn/EsoData.NET) for domain details. MIT licensed; personal game data and downloaded catalogs are not distributed.
