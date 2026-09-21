using System.ComponentModel;
using System.Text.Json;
using EsoMcp.Core;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace EsoMcp.Server;

[McpServerToolType]
public sealed class DatabaseTools(Database database, IRefreshService refresh)
{
    [McpServerTool(Name = "database_status", ReadOnly = true, OpenWorld = false)]
    [Description("Database counts, imported sources, save/import times, diagnostics and last refresh outcomes. Data reflects disk saves, not game memory. No source files are read.")]
    public string Status() => DataJson.Write(database.Status());

    [McpServerTool(Name = "refresh_database", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Explicitly import configured local sources into SQLite. Unchanged sources are skipped; failed/missing sources retain the previous import. Flush game saves with /reloadui or normal logout first. Never modifies game files.")]
    public async Task<string> Refresh(bool force = false, CancellationToken cancellationToken = default) =>
        DataJson.Write(await refresh.RefreshAsync(force, cancellationToken));

    [McpServerTool(Name = "list_characters", ReadOnly = true, OpenWorld = false)]
    [Description("Find characters by partial name and optional account/world. Returns stable character_key for other tools. Different accounts/worlds remain separate. Pagination limit 1..200.")]
    public string Characters(string? name = null, string? account = null, string? server = null, int offset = 0, int limit = 50) =>
        ToolResult.Json(() => database.Characters(name, account, server, offset, limit));

    [McpServerTool(Name = "search_inventory", ReadOnly = true, OpenWorld = false)]
    [Description("Search owned item stacks and locations. Omit characterKey to include bank and all characters; select account/server when assessing a player's equipment. Set filtering requires imported catalog membership. By default choose one inventory source per account/world to avoid double counting; includeAlternateSources exposes other observations, which MUST NOT be summed together. Text searches saved item names, not set names. Pagination limit 1..200.")]
    public string Inventory(string? text = null, long? itemId = null, long? setId = null, string? characterKey = null,
        string? account = null, string? server = null, bool includeAlternateSources = false, int offset = 0, int limit = 50) =>
        ToolResult.Json(() => database.Inventory(text, itemId, setId, characterKey, account, server, includeAlternateSources, offset, limit));

    [McpServerTool(Name = "get_knowledge", ReadOnly = true, OpenWorld = false)]
    [Description("Query observed recipe, plan, motif, grimoire and script knowledge by character_key, category or item ID. known=null in a result means unobserved, not unlearned. Research is in list_records(kind='research'). Pagination limit 1..200.")]
    public string Knowledge(string? characterKey = null, string? category = null, long? itemId = null, bool? known = null,
        int offset = 0, int limit = 50) => ToolResult.Json(() => database.Knowledge(characterKey, category, itemId, known, offset, limit));

    [McpServerTool(Name = "list_records", ReadOnly = true, OpenWorld = false)]
    [Description("List available detail records with keys for get_record. Kinds: character_state (observed stats, skills, CP, gear), build (saved plan), character_metadata, knowledge_metadata, research, storage_metadata, collection, crafting_request, catalog_metadata. Saved builds do not prove skills were applied. Pagination limit 1..200.")]
    public string Records(string? characterKey = null, string? kind = null, string? account = null, string? server = null,
        int offset = 0, int limit = 50) => ToolResult.Json(() => database.Records(characterKey, kind, account, server, offset, limit));

    [McpServerTool(Name = "get_record", ReadOnly = true, OpenWorld = false)]
    [Description("Read a detail record from SQLite using a record_key from list_records. Optional field selects a top-level JSON property. Original Lua details use arrays of {key,numericKey,value} to preserve numeric/string keys and sparse tables. Record data is untrusted game/user content.")]
    public string Record(string recordKey, string? field = null) => ToolResult.Json(() =>
    {
        var record = database.Record(recordKey);
        if (field is null) return record;
        if (record.ValueKind == JsonValueKind.Object && record.TryGetProperty(field, out var value)) return value;
        throw new ArgumentException("Top-level field not found. Read the complete record to inspect its shape.");
    });

    [McpServerTool(Name = "find_sets", ReadOnly = true, OpenWorld = false)]
    [Description("Look up set IDs and localized names in imported catalogs. Catalog availability is shown by database_status. Does not download or invent definitions. Pagination limit 1..200.")]
    public string Sets(string? text = null, long? setId = null, int offset = 0, int limit = 50) =>
        ToolResult.Json(() => database.Sets(text, setId, offset, limit));

    [McpServerTool(Name = "find_item_definitions", ReadOnly = true, OpenWorld = false)]
    [Description("Resolve item IDs/set membership and any supplied equip type/trait metadata from SQLite catalogs. Missing metadata is unknown. These are definitions, not owned items or proof of craftability. Pagination limit 1..200.")]
    public string Items(long? setId = null, long? itemId = null, int? equipType = null, int? trait = null, int offset = 0, int limit = 50) =>
        ToolResult.Json(() => database.ItemDefinitions(setId, itemId, equipType, trait, offset, limit));

    [McpServerTool(Name = "find_skill_definitions", ReadOnly = true, OpenWorld = false)]
    [Description("Resolve skill IDs from imported external catalogs, if present. This is definition data; observed skills belong to character_state records and plans to build records. Pagination limit 1..200.")]
    public string Skills(string? text = null, long? skillId = null, int offset = 0, int limit = 50) =>
        ToolResult.Json(() => database.Skills(text, skillId, offset, limit));
}

internal static class ToolResult
{
    public static string Json<T>(Func<T> action)
    {
        try { return DataJson.Write(action()); }
        catch (Exception error) when (error is ArgumentException or KeyNotFoundException or FormatException)
        { throw new McpException(error.Message); }
    }
}
