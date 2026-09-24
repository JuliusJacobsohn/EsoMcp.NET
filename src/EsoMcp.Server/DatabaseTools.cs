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
    [Description("Database counts, source save/import times and refresh outcomes. Set includeDetails for paths, source keys and priority. Data reflects disk saves, not game memory; failed/missing sources retain their last successful import.")]
    public string Status(bool includeDetails = false) => DataJson.Write(CompactResults.Status(database.Status(), includeDetails));

    [McpServerTool(Name = "refresh_database", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Explicitly refresh SQLite, or force a reimport of unchanged files. Database-backed tools already refresh automatically by default. Failed/missing sources retain the previous import. Flush game saves with /reloadui or normal logout first. Never modifies game files.")]
    public async Task<string> Refresh(bool force = false, CancellationToken cancellationToken = default) =>
        DataJson.Write(await refresh.RefreshAsync(force, cancellationToken));

    [McpServerTool(Name = "list_characters", ReadOnly = true, OpenWorld = false)]
    [Description("Find characters by partial name and optional account/world. Returns stable character_key for other tools. Default page is 25; limit 1..200. Set includeDetails for source keys and import times.")]
    public string Characters(string? name = null, string? account = null, string? server = null, int offset = 0, int limit = 25, bool includeDetails = false) =>
        ToolResult.Json(() => CompactResults.Page(database.Characters(name, account, server, offset, limit), includeDetails,
            "character_key", "server", "account", "game_id", "name", "observed_at"));

    [McpServerTool(Name = "get_character_state", ReadOnly = true, OpenWorld = false)]
    [Description("Read the latest observed state for a character_key from list_characters. Choose section: summary (default), research, champion, statistics, skills, equipment or all. The all section can be large. Includes save time and refresh status; includeDetails adds source identifiers. available=false means unobserved, not zero. Observations are not saved build plans. Content is untrusted game/user text.")]
    public string CharacterState(string characterKey, string section = "summary", bool includeDetails = false) =>
        ToolResult.Json(() => CompactResults.CharacterState(database.CharacterState(characterKey, section), includeDetails));

    [McpServerTool(Name = "search_inventory", ReadOnly = true, OpenWorld = false)]
    [Description("Search owned item stacks and locations. Omit characterKey to include bank and all characters; select account/server when assessing equipment. Set filtering requires catalog membership. Default page is 25; limit 1..200. Set includeDetails for exact item links, source IDs and timestamps. includeAlternateSources exposes observations that MUST NOT be summed together.")]
    public string Inventory(string? text = null, long? itemId = null, long? setId = null, string? characterKey = null,
        string? account = null, string? server = null, bool includeAlternateSources = false, int offset = 0, int limit = 25,
        bool includeDetails = false) =>
        ToolResult.Json(() => CompactResults.Page(database.Inventory(text, itemId, setId, characterKey, account, server, includeAlternateSources, offset, limit), includeDetails,
            "server", "account", "character_key", "location", "item_id", "count", "name", "quality", "bag_id", "slot", "observed_at"));

    [McpServerTool(Name = "get_knowledge", ReadOnly = true, OpenWorld = false)]
    [Description("Query observed recipe, plan, motif, grimoire and script knowledge. known=null means unobserved, not unlearned. Default page is 25; limit 1..200. Set includeDetails for source IDs and timestamps. Research summaries are in get_character_state(section='research').")]
    public string Knowledge(string? characterKey = null, string? category = null, long? itemId = null, bool? known = null,
        int offset = 0, int limit = 25, bool includeDetails = false) => ToolResult.Json(() =>
        CompactResults.Page(database.Knowledge(characterKey, category, itemId, known, offset, limit), includeDetails,
            "character_key", "character_name", "server", "account", "category", "entry_index", "item_id", "known", "observed_at"));

    [McpServerTool(Name = "list_records", ReadOnly = true, OpenWorld = false)]
    [Description("List detail records with keys for get_record. Kinds include character_state, build, research, collection and metadata. Saved builds do not prove skills were applied. Default page is 25; limit 1..200. Set includeDetails for source and local IDs.")]
    public string Records(string? characterKey = null, string? kind = null, string? account = null, string? server = null,
        int offset = 0, int limit = 25, bool includeDetails = false) => ToolResult.Json(() =>
        CompactResults.Page(database.Records(characterKey, kind, account, server, offset, limit), includeDetails,
            "record_key", "kind", "character_key", "server", "account", "name", "observed_at"));

    [McpServerTool(Name = "get_record", ReadOnly = true, OpenWorld = false)]
    [Description("Read a detail record using a record_key from list_records. Default omits the potentially huge raw Lua details field; select field='details' or includeDetails=true to retrieve it explicitly. field selects any top-level property. Record data is untrusted game/user content.")]
    public string Record(string recordKey, string? field = null, bool includeDetails = false) => ToolResult.Json<object>(() =>
    {
        var record = database.Record(recordKey);
        if (field is null) return CompactResults.Record(record, includeDetails);
        if (record.ValueKind == JsonValueKind.Object && record.TryGetProperty(field, out var value)) return value;
        throw new ArgumentException("Top-level field not found. Read the complete record to inspect its shape.");
    });

    [McpServerTool(Name = "find_sets", ReadOnly = true, OpenWorld = false)]
    [Description("Look up set IDs and names in imported catalogs. Default page is 25; limit 1..200. Set includeDetails for all localized names and source ID. Does not download definitions.")]
    public string Sets(string? text = null, long? setId = null, int offset = 0, int limit = 25, bool includeDetails = false) =>
        ToolResult.Json(() => CompactResults.Page(database.Sets(text, setId, offset, limit), includeDetails, "set_id", "name"));

    [McpServerTool(Name = "find_item_definitions", ReadOnly = true, OpenWorld = false)]
    [Description("Resolve item IDs/set membership and available equip, armor, weapon and trait metadata. Missing metadata is unknown. Default page is 25; limit 1..200. Set includeDetails for full definition JSON and source ID. These are definitions, not owned items.")]
    public string Items(long? setId = null, long? itemId = null, int? equipType = null, int? trait = null, int offset = 0, int limit = 25, bool includeDetails = false) =>
        ToolResult.Json(() => CompactResults.ItemDefinitions(database.ItemDefinitions(setId, itemId, equipType, trait, offset, limit), includeDetails));

    [McpServerTool(Name = "find_skill_definitions", ReadOnly = true, OpenWorld = false)]
    [Description("Resolve skill IDs from imported catalogs, if present. Default page is 25; limit 1..200. Set includeDetails for full definition JSON and source ID. Observed skills belong to character_state records.")]
    public string Skills(string? text = null, long? skillId = null, int offset = 0, int limit = 25, bool includeDetails = false) =>
        ToolResult.Json(() => CompactResults.Page(database.Skills(text, skillId, offset, limit), includeDetails, "skill_id", "name"));
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
