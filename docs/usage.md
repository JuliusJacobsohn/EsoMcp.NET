# Tool usage guide

[Project overview and quick start](../README.md) · [Setup and configuration](setup.md).

## Check addon installation

Call `inspect_addons` with no arguments to inspect every configured installation. This works without an account, catalog or valid saved-data parser input. You can override the paths, for example:

```json
{"addonsPath":"C:/Users/YOUR_USER/Documents/Elder Scrolls Online/live/AddOns","savedVariablesPath":"C:/Users/YOUR_USER/Documents/Elder Scrolls Online/live/SavedVariables"}
```

See [setup diagnostics](setup.md#checking-installed-addons) for interpreting installation, dependency and saved-file results. The check always reads the local filesystem, including when the account tools use offline mode.

## Inventory traits

Inventory, equipment and loot trait queries honor the chosen trait in transmuted/reconstructed item links before catalog defaults. Account refresh also replaces previously stored default traits; saved build plans remain untouched.

## Character observations and combat reports

`inspect_account` supports character sections `attributes`, `bars`, `statistics` and `effects`. Effects retain the observed active buff names/IDs and the last recorded meal separately; the last meal alone does not prove its buff is active. Observations are the last addon scan saved to disk.

Use `section:"combatReports"` for saved Combat Metrics fights, newest first. Optional `character` filters the recorded name, and `text` searches labels, locations and names. Reports remain installation-wide: their format does not identify an account or server. Sources include file modification times and saved-fight counts. An installed source with zero fights differs from an unavailable source. Save a fight in CMX before `/reloadui`; unsaved recent fights exist only in game memory. This integration reads persisted summaries, not encoded ability/rotation details. Reports are retained in SQLite for explicit offline queries.

## Tools

### Retained group loot

`inspect_account` accepts `section:"lootHistory"`. Use `text` to search item/set names or recipients, `setIds` to select a set and `ids` to select ESO item IDs. `character` limits recipients to that character on the selected account. Rows are newest first, support standard pagination/projection, and include recipient, time, quantity, link, catalog metadata and source file time. Missing definitions remain null; `refresh_catalog` can fetch metadata for observed item IDs.

Collection queries (`section:"collections"`) include registered counts, total pieces, completion and reconstruction crystal cost. Use `includeDetails:true` for named collected/missing pieces. LibMultiAccountSets normally saves only bit masks; exact piece definitions must be supplied in a JSON catalog or captured once per set from the game API. The optional capture lives in the existing addon's SavedVariables and is read through EsoData.NET; the server never writes game files. Replace `331` in both places below with the desired set ID, run this in game chat and flush with `/reloadui`:

```text
/script local t={} for i=1,GetNumItemSetCollectionPieces(331) do local p,s=GetItemSetCollectionPieceInfo(331,i) t[i]={p,Id64ToNumber(s),GetItemSetCollectionPieceItemLink(p)} end LibMultiAccountSetsSavedVariables.EsoDataPieces=LibMultiAccountSetsSavedVariables.EsoDataPieces or {} LibMultiAccountSetsSavedVariables.EsoDataPieces[331]=t
```

This captures definitions, not account ownership. Collection progress continues to refresh automatically from the addon. Repeat the capture only if that set's available piece types change. Names fall back to the normal refreshable item catalog when the game returns unlabeled links. Unknown piece definitions or unmapped mask bits leave completion/cost unknown. Quality upgrade materials are separate from the reported crystal cost.

Loot Log is optional. Its retained history is installation/server scoped and never establishes inventory ownership. The addon prunes older entries according to its history setting, so this is not a permanent record of every trial. Newly recorded drops appear after `/reloadui` or normal logout; `offline:true` queries the last SQLite snapshot.

For other players' item drops, each row includes `whisper` with `recipient`, `itemLink`, `message` and a copyable `command`. Copy the command into ESO chat and send it yourself. The link preserves the exact observed variant/enchantments, with its display name filled from the catalog when needed. Own-account drops and non-item events have no draft. Project `fields:["name","recipientAccount","receivedAt","whisper"]` for a compact list. Formatting does not establish whether the item remains tradeable.

Use `section:"lootHistory", group:true` for one row per player, with their matching `drops` and combined `whispers`. Each whisper contains complete item links and is split conservatively so its entire command fits 350 characters. Commands prefer the observed account (`/w @account message`, without a comma); when no usable account is recorded, they fall back to the latest observed character name (`/w Character Name, message`). Group filters apply to drops before grouping, and pagination counts players. Add `known:false` to request only confirmed uncollected pieces for the selected account; `known:true` selects collected pieces. Each drop reports `collected` (true/false/null). Trait variants resolve to the same captured collection slot using item metadata. Unknown mappings or missing account collection data are excluded by either filter, rather than guessed; unbound items in inventory do not count as registered. For any matching drop, omit `known`; for specific traits (even if collected), also supply `traits:[...]` with ESO trait values from item definitions. Trait and collection filters can be combined and apply before whisper grouping. `traits` also filters inventory. Missing trait metadata is excluded when a trait is requested. Character names with spaces are supported. See [ESO's documented whisper syntax](https://help.elderscrollsonline.com/app/answers/detail/a_id/2556/).

### Local TTC pricing

`query_prices` searches the **entire** downloaded TTC market catalog, including items not owned by any account. Example: `{"region":"EU","text":"dreugh wax","limit":5}`. Filters include `ttcItemIds` (not ESO IDs), ESO `quality` (1–5), `marketLevel` (50+CP), `ttcTrait` and the `extra` variant path. Listing statistics and sale statistics remain separate; results include source dates and bounded pagination.

The server reads TTC under configured `addonsPath`, with `priceLanguage` (default `EN`) selecting the lookup matching your saved inventory names. Normal requests reread local files; `offline:true` uses the SQLite catalog snapshot. No requests go to TTC's website and the server never launches its updater.

`inspect_account` inventory rows include `price` and `estimatedStackPrice`; `section:"prices"` reports association coverage and the partial priced-stack estimate. Use field projection for compact output, such as `fields:["name","count","price","estimatedStackPrice"]`. Every item has a status, including unknown/unlisted items. Missing trait/level/master-writ metadata yields `NeedsMetadata`, not a guessed price. A price does not prove an owned/bound item can be sold.

The full catalog is stored separately in SQLite `price_catalogs`; account items retain their price associations. No market data is bundled. Missing optional TTC files produce unavailable pricing; malformed existing files fail visibly. Catalog refresh does not modify build plans.

For the ten highest-value owned stacks, use `inspect_account` with:

```json
{"queries":[{"section":"inventory","priceStatus":"Matched","sort":"stackPriceDesc","limit":10,"fields":["name","count","location","characterId","estimatedStackPrice"]}]}
```

Use `sort:"unitPriceDesc"` to rank by price per item instead. Both sorts work with character, location, name, item-ID and set-ID filters, plus `offset` pagination. Filtering and sorting happen before pagination. Price sorting ranks individual stacks; it does not combine separate stacks of an item or support `group:true`.

### Tool reference

| Tool | Purpose |
|---|---|
| `inspect_addons` | Find supported local addon installations, check required dependencies and locate saved files |
| `query_prices` | Search all local TTC variants independently of account ownership |
| `inspect_account` | Discover accounts or batch compact queries for characters, budgets, skills, inventory, equipment, knowledge, research, collections, saved builds (optional typed details) and source coverage |
| `resolve_definitions` | Batch name/ID searches in local skill, set, item or champion catalogs; CP names fall back to installed CSPS data |
| `edit_build` | Create/copy/read/list/update/delete named targets and working builds with revision protection |
| `analyze_build` | Validation, differences, prerequisite status, equipment candidates, set counts, leveling candidates or crafting shortages |
| `export_build` | Export explicitly selected CSPS sections or crafting orders, with a validation report |
| `verify_build` | Compare an exported plan revision with newly saved game state, returning differences only |
| `refresh_catalog` | Explicitly fetch selected UESP definitions; account information is never sent |

Names must resolve unambiguously. IDs are also accepted. Query results have counts, pagination and optional field selection; raw Lua, item-link blobs and repeated provenance do not accompany ordinary rows.

### Existing gear and gold glyphs

Inventory rows expose `requiredLevel` and `requiredChampionPoints`, decoded from each item's link variant (including CP160 quest rewards). Unknown variants return null. `includeDetails:true` also exposes the exact `link` for inspection and sharing; all three fields support projection.

Use `inspect_account` inventory queries with `includeDetails:true` to inspect the observed enchant effect and quality. Applied glyphs take precedence over the built-in enchant; built-in effects require refreshed item metadata. `refresh_catalog` accepts `itemIds` for precise metadata refreshes. Missing definitions remain unknown.

Resolve `kind:"glyphs"` through `resolve_definitions`, using the installed **LibLazyCrafting** rune tables. Save glyph orders in a plan's `crafting` list, then call `export_build` with `format:"enchanting"`. It returns exact rune requirements, account-wide stock/shortages and pasteable ESO chat commands. Paste each line once on the crafter, visit an enchanting station, then apply the loose glyphs manually. Finish before reload/logout because the queue is held in game memory. Lazy Set Crafter's **Import Links** rejects standalone glyphs; `format:"crafting"` rejects known glyph orders to prevent unusable imports. Existing dropped gear must be upgraded through the in-game improvement UI.

Call `refresh_catalog` with no arguments to download Grimoire/script names for the IDs in local knowledge coverage. Both learned and unlearned entries are included; only definition IDs are sent to UESP. Subsequent `knowledge` queries include names and support `text` filtering. Unknown definitions retain a null name. Learned scripts establish availability, not which combination was actually scribed or whether a quest was completed.

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

Crafting orders contain item ID, level/CP, quality, style, quantity and per-item enchantment ID/quality. `export_build` with `format:"crafting"` generates Lazy Set Crafter equipment links. `analyze_build` with `section:"crafting"` and `crafter` compares exact external-catalog recipes with account materials and knowledge. If exact recipe/cost data is unavailable, it returns that gap instead of inventing a material shopping list.

For food/drinks, save the **learnable recipe item ID** in each crafting order and call `export_build` with `format:"provisioning"`. The named crafter must have observed, learned recipe knowledge. The exporter generates LibLazyCrafting chat queue commands that resolve recipe indices and recheck knowledge in game; paste once on that crafter and visit a cooking fire. Quantity means **craft iterations**, not servings: yield depends on provisioning passives. Recipe quality and level are fixed; equipment quality/level fields do not override them. Ingredients and skill requirements are checked in game, and the queue is lost on reload/logout. Lazy Set Crafter rejects recipe imports; potion queues are unsupported.

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
