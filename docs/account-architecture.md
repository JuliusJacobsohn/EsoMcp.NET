# MCP as an account and build workspace

Replacement design, 2026-09-25; not implemented in the released server. Breaking changes are permitted. The design follows the [account-first library](https://github.com/JuliusJacobsohn/EsoData.NET/blob/main/docs/account-architecture.md).

## Purpose

Let an AI inspect an account, maintain several proposed builds, make small edits, compare them with current possessions/progression, export them, and verify application. The AI should not repeatedly retrieve raw character dumps, join catalogs by numeric IDs, recalculate budgets, or resend an entire build to change one slot.

## Execution and persistence

```text
Configured sources + local catalogs
              |
       EsoData account loader (fresh per request)
              |
       typed EsoAccount + shared library operations
              |
        compact MCP projections

Local named draft JSON <-- edit / analyze / export / compare
```

Remove SQLite projections from the required request path. Remove the import-project/database/tool split that forces gameplay knowledge into server methods. MCP calls library operations over typed objects. Addon knowledge stays in library adapters and source configuration.

One request loads at most one account graph and shares it across all batched operations. Do not return the entire graph automatically. Local reads are fresh on each request; no loaded account cache or watcher is required. Network catalog updates are explicit and persist definition files, independently of personal account data.

Only authored work needs persistence: named drafts, target requirements and their guide references. Use ordinary local JSON files with atomic replacement. A draft records account/character identity, base build, desired build, constraints and optional target requirements. It is a detached working copy, not a claim about the applied game state. It survives server/client restarts.

A draft revision prevents two chats from accidentally overwriting each other's edits. This is one optimistic revision check, not an event store. Separate drafts can belong to the same character. Loading a newer account never silently replaces draft edits. Explicit rebasing reports changes affecting the plan and preserves intentional edits.

No automatic write-back to SavedVariables. No game actions, mail or purchases are implied by editing a draft. The export artifact is how the proposed change reaches an addon.

## Small tool surface

Names below are target API names, not currently available tools.

| Tool | Operation |
|---|---|
| `inspect_account` | Discover/select an account, summarize it, or query typed collections with filters, selected fields and grouped counts. Batch independent queries in one request. |
| `resolve_definitions` | Resolve many skill/set/item names or IDs with requested fields. Return ambiguity candidates or missing metadata, not giant catalog rows. |
| `edit_build` | Create, copy, list, remove or edit a named draft. Batch typed edits to skills, bars, CP, equipment, consumables and goals; return revision, changed fields and budget summary. |
| `analyze_build` | Compare draft with fresh account: budgets, leveling candidates, gear gaps, craftability, material shortages and prerequisite readiness. Request only relevant sections. |
| `export_build` | Validate and export chosen parts as native CSPS or crafting links. Return import text once or a local artifact path plus a compact preview. |
| `verify_build` | Compare a fresh observed character with the chosen draft and exported sections. Return only differences and unobserved sections. |
| `refresh_catalog` | Explicit batch refresh of missing/outdated definitions from configured sources. |

Source coverage and diagnostics are selectable through `inspect_account`; no separate verbose database-status tool is necessary. Tool schemas describe typed nested operations once; they do not expose arbitrary Lua/SQL. Start with a few plain DTOs, not a custom query language or a general workflow engine.

Library users still have ordinary C# access to the whole account. MCP offers structured batched queries and edits because an MCP call cannot pass a CLR object reference to the model. Arbitrary C# execution is not needed for the default workflow; a local .NET program can use the same library when a genuinely novel analysis requires it.

## Selectors and output contract

- Accept exact character/skill/set names when unique. Return short candidates for ambiguous selectors. Provide stable IDs as an alternative; never require the AI to remember 64-character database record hashes.
- Resolve item instances from the current account, including owner/location/variant. Do not silently substitute a similar item. Selection references without a native stable ID carry a load identity and must be resolved again after changes.
- Return numbers and names needed for the decision: for example `total`, `allocated`, `remaining`, `refundable`, and missing requirements. Do not return raw tables, full item links, repeated account names, source paths, hashes or verbose success disclaimers by default.
- Default to relevant differences and summaries. Selectable fields and batch filters are available for general inspection. One whole-account response remains possible as an explicit file/export or requested projection.
- Use bounded pages with total/matched count and continuation. Never hide truncation. Large validation reports return error counts plus a page of actionable errors; valid results need not enumerate every passed check.
- Responses stay readable JSON objects with descriptive keys. No opaque positional encoding invented merely to save tokens.
- Source details are opt-in except when staleness, conflicts or missing coverage materially affect the answer. Put the source/coverage summary once per response.
- Define response character/token budgets in regression tests. Measure actual serialized payloads, including MCP text/structured content duplication if the SDK emits both. Do not claim a token reduction solely from returning fewer rows.

## Mutations

`edit_build` edits a stored typed build, not a serialized CSPS string. Useful operations include `setAttributes`, `setSkillAllocation`, `removeSkillAllocation`, `setBarSlot`, `setChampionAllocation`, `setChampionSlot`, `setEquipment`, and `setConstraints`. Exact schema names may change during implementation; operations remain small and typed.

Apply each batch atomically to a copy, resolve selectors, then save a new revision. Unknown properties or ambiguous selectors reject the batch without partially saving it. A draft is allowed to be incomplete or a future target; unmet game requirements are validation findings, not a reason to forbid storing the idea.

Edits return their diff and a short report. The caller does not resend the other 100 allocations. A mutation need not reload account sources if it purely renames a draft; any analysis of current affordability/availability uses a fresh account loaded once for that request.

Constraints store user intent so it survives context compaction: current gear only, full respec permitted, desired free skill points, target quality, bar completeness, required utility, and leveling priorities. These are draft-specific. A tank target and an exploration build must not silently inherit each other's allocation policy.

The AI selects a build strategy from the guide and user intent. Code checks budgets, eligibility, ownership, set counts and supplied constraints. It cannot prove an arbitrary build maximizes DPS. A useful passive may be purchased without being a leveling candidate; a maxed ability can be forbidden as an XP filler while explicitly allowed if the user requests it for combat utility.

## Representative workflows

### Leveling and exploration while progressing a tank target

1. Inspect the character with only budgets, equipment, incomplete target skills and available leveling candidates.
2. Create a named exploration draft from the current character and attach priorities: core tank progression, optional progression, other unfinished abilities. Store the requested utility/damage and free-point constraints. Do not retain tank-only purchases merely because the eventual target is a tank.
3. Batch edit purchases/passives, both bars and CP by resolved selectors. Analysis computes full-respec affordability and per-discipline CP, flags empty slots, unavailable morphs and already-maxed fillers, and lists actionable missing metadata.
4. Export selected sections as one consistent CSPS format. The same export response contains compact validation and point totals; no separate mandatory validate call.
5. After an actual game save, verify differences against that revision. Distinguish import into CSPS from applying in game.

Design budget: approximately 4-6 calls for a routine prepared-catalog workflow, rather than dozens of per-ID lookups. This is an acceptance target, not a measured current capability. First-time catalog acquisition and user strategy changes can require more calls.

### Final gear and an immediately usable interim setup

A saved target contains slot specifications, alternatives and source references. One analysis joins account-wide possessions, binding, set counts by weapon bar, collection unlocks and crafting knowledge. It separates owned/equipped, owned elsewhere, craftable, reconstructable, missing and unknown. The final target and interim build are separate named drafts. The AI can ask for the exact missing pieces without rereading every owned item.

### Craft gear, glyphs, food and potions

Set target level/CP, quality, traits, enchants, quantities and crafter on a draft crafting request. Analysis resolves exact requirements and compares available materials across the account. The output lists owned/needed/short quantities and source gaps. Purple target quality cannot accidentally imply gold improvement materials. Unknown recipes or improvement rules remain explicit. Export only the formats actually supported by the crafting addon; do not imply food/potion queues are supported merely because gear links are.

### Progress and GitHub tickets

Save explicit target requirements and guide URLs once. Analysis reports satisfied/unmet/unknown requirements plus actual prerequisites. Optionality and priority are separate from dependencies. A skill-line prerequisite does not block an unrelated scribing quest; assembling final gear does not block farming each individual piece.

GitHub integration stays outside the ESO library/MCP. The assistant maps requirement IDs to issues and applies native issue dependencies through GitHub tools. Progress checks can query all requirements in one batch and update only changed tickets. Historical completion evidence is not erased merely because a temporary respec removes a purchased passive; current-allocation acceptance and unlock/level milestones are different requirements.

### Bank cleanup and writs

Grouped inventory queries return counts by set/quality/location plus exact selected instances when requested. Protect equipment used by saved drafts. Markets and judgments about obsolete sets need current external evidence. Writ decoding becomes a typed library adapter feature; the server must report unsupported decoded requirements rather than inviting the AI to infer voucher rewards from an item name.

## Implementation sequence

1. Implement the library graph/loader and build operations first, independently of MCP.
2. Replace database-facing tools with compact account inspection and definition resolution.
3. Add local draft JSON, batched edits, analysis and export using library methods.
4. Add verify-after-save and target requirement comparison; remove obsolete query/import tools.
5. Release both packages, install the MCP, then replay representative build/craft/ticket workflows through the registered MCP. Verify running version and tool discovery separately from copying binaries.

Existing user data and authored builds are not disposable merely because code compatibility is unnecessary. Preserve user-authored plans; provide one-time extraction from the old store where required. Old projection databases can be archived without being queried by the new path. Do not remove real SavedVariables or installed addons.

## Acceptance evidence

- Synthetic regression cases for the actual past failures: wrong subclass identity, wrong bar ID, unobserved morph, missing ultimate/slot, summed CP within total but wrong discipline, respec budget confused with unspent points, and purple/gold material confusion.
- A compact mutation response does not echo the entire build or account.
- An equipment question does not require fetching all character skills, raw profiles or catalog documents.
- A second request after source changes sees the changed save; an existing draft survives unchanged.
- Two revisions prevent lost edits between simultaneous chats; server restart preserves drafts.
- Returned coverage distinguishes missing data from a verified negative.
- Benchmark new full load + requested analysis + projection against the existing baseline. Add caching only for a demonstrated bottleneck, not as a prerequisite.
- A real in-game application check remains distinct from format/unit/protocol tests.
