using System.ComponentModel;
using System.Text.Json;
using EsoData.Accounts;
using EsoData.Builds;
using EsoData.Catalogs;
using EsoMcp.Import;
using ModelContextProtocol.Server;

namespace EsoMcp.Server;

public sealed class AccountQuery
{
    public string Section { get; set; } = "characters";
    public string? Character { get; set; }
    public string? Text { get; set; }
    public long[]? Ids { get; set; }
    public long[]? SetIds { get; set; }
    public string? Location { get; set; }
    public string? Category { get; set; }
    public bool? Known { get; set; }
    public bool UnfinishedOnly { get; set; }
    public string[]? Fields { get; set; }
    public bool Group { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; } = 20;
}

[McpServerToolType]
public sealed class AccountTools(AccountWorkspace workspace)
{
    [McpServerTool(Name = "inspect_account", ReadOnly = true, OpenWorld = false)]
    [Description("Load fresh local account objects and persist them in SQLite. With no queries, list accounts. Batch queries for characters, summary, skills, inventory, equipment, champion, knowledge, research, savedBuilds or sources. Exact character name/ID; account key includes server. Default 20 rows, limit 1..100; fields projects selected row properties. Unfinished skills use recorded morph XP. Missing data remains unknown. offline=true explicitly uses stored snapshots.")]
    public string Inspect(string? account = null, AccountQuery[]? queries = null, bool offline = false) => ToolResult.Json(() =>
    {
        var read = workspace.Read(offline);
        if (queries is null || queries.Length == 0) return (object)new { Accounts = read.Data.Accounts.Select(a => new { a.Key, a.Name, a.Server, Characters = a.Characters.Count }), read.Data.Diagnostics };
        if (queries.Length > 20) throw new ArgumentException("At most 20 queries per request.");
        var selected = AccountWorkspace.Select(read, account);
        return new { Account = selected.Key, Results = queries.Select(q => Query(selected, q, read.Catalog)).ToArray(),
            Diagnostics = read.Data.Diagnostics.Concat(selected.Sources.SelectMany(s => s.Diagnostics)).Distinct().ToArray() };
    });

    internal static object Query(EsoAccount account, AccountQuery query, GameCatalog? catalog = null)
    {
        if (query.Offset < 0 || query.Limit is < 1 or > 100) throw new ArgumentException("Use offset >= 0 and limit 1..100.");
        var character = query.Character is null ? null : account.Character(query.Character);
        EsoCharacter NeedCharacter() => character ?? throw new ArgumentException("Select a character for this section.");
        bool Text(string? value) => query.Text is null || value?.Contains(query.Text, StringComparison.OrdinalIgnoreCase) == true;
        IEnumerable<object> rows = query.Section switch
        {
            "characters" => account.Characters.Where(c => Text(c.Name)).Select(c => (object)new { c.Id, c.Name, c.Level, c.Class, c.Race }),
            "summary" => [new { NeedCharacter().Name, NeedCharacter().Level, NeedCharacter().Class, NeedCharacter().Race,
                NeedCharacter().Progress.TotalSkillPoints, NeedCharacter().Progress.UnspentSkillPoints, NeedCharacter().Progress.TotalChampionPoints,
                NeedCharacter().Progress.ChampionBudgets, Sections = NeedCharacter().Build.Sections.ToString() }],
            "skills" => (NeedCharacter().Progress.Skills ?? []).Where(s => Text(s.Name) && (query.Ids is null || query.Ids.Contains(s.AbilityId))
                && (!query.UnfinishedOnly || !s.IsPassive && !(s.Morph > 0 && s.Rank >= 4))).Cast<object>(),
            "skillLines" => (NeedCharacter().Progress.SkillLines ?? []).Where(s => Text(s.Key)).Select(s => (object)new { Name = s.Key, Rank = s.Value }),
            "inventory" => Inventory(),
            "equipment" => NeedCharacter().Build.Equipment.Select(p => (object)new { Slot = p.Key, Item = p.Value }),
            "champion" => [new { NeedCharacter().Build.ChampionPoints, NeedCharacter().Build.ChampionSlots, NeedCharacter().Progress.ChampionBudgets }],
            "research" => [new { NeedCharacter().Progress.Research, NeedCharacter().Progress.ResearchKnowledge }],
            "knowledge" => NeedCharacter().Progress.Knowledge.Where(k => query.Category is null || k.Key == query.Category).SelectMany(k =>
                k.Value.Where(e => (query.Ids is null || query.Ids.Contains(e.Key)) && (query.Known is null || e.Value == query.Known))
                    .Select(e => new { Category = k.Key, ItemId = e.Key, Name = catalog?.Items.GetValueOrDefault(e.Key)?.Name, Known = e.Value })
                    .Where(e => Text(e.Name)).Cast<object>()),
            "savedBuilds" => NeedCharacter().SavedBuilds.Select(b => (object)new { b.Id, b.Name, b.SavedAt, Sections = b.Build.Sections.ToString() }),
            "sources" => account.Sources.Cast<object>(),
            "collections" => (account.SetCollections ?? []).Where(c => query.SetIds is null || query.SetIds.Contains(c.Key))
                .Select(c => (object)new { SetId = c.Key, SlotMask = c.Value }),
            _ => throw new ArgumentException("Unknown account section.")
        };
        var all = rows.ToArray();
        var available = query.Section switch
        {
            "skills" => NeedCharacter().Progress.Skills is not null,
            "skillLines" => NeedCharacter().Progress.SkillLines is not null,
            "knowledge" => query.Category is null ? NeedCharacter().Progress.Knowledge.Count > 0 : NeedCharacter().Progress.Knowledge.ContainsKey(query.Category),
            "equipment" => NeedCharacter().Build.Sections.HasFlag(BuildSections.Equipment),
            "champion" => NeedCharacter().Build.Sections.HasFlag(BuildSections.ChampionPoints),
            "collections" => account.SetCollections is not null,
            _ => true
        };
        return new { query.Section, Available = available, Total = all.Length, query.Offset,
            HasMore = query.Offset + query.Limit < all.Length, Rows = all.Skip(query.Offset).Take(query.Limit).Select(r => Project(r, query.Fields)).ToArray() };

        IEnumerable<object> Inventory()
        {
            var items = account.Inventory.Where(i => (character is null || i.CharacterId == character.Id) && Text(i.Name)
                && (query.Ids is null || query.Ids.Contains(i.ItemId)) && (query.SetIds is null || i.SetId.HasValue && query.SetIds.Contains(i.SetId.Value))
                && (query.Location is null || string.Equals(i.Location, query.Location, StringComparison.OrdinalIgnoreCase)));
            if (query.Group) return items.GroupBy(i => (i.SetId, i.Location, i.Quality, i.CharacterId)).Select(g => (object)new
            { g.Key.SetId, g.Key.Location, g.Key.Quality, g.Key.CharacterId, Count = g.Sum(i => i.Count), Stacks = g.Count() });
            return items.Select(i => (object)new { i.Reference, i.ItemId, i.Name, i.Count, i.Location, i.CharacterId, i.Quality, i.SetId, i.Trait, i.ArmorType, i.WeaponType });
        }
    }
    internal static object Project(object row, string[]? fields)
    {
        if (fields is null) return row;
        var json = JsonSerializer.SerializeToElement(row, AccountJson.Options);
        return fields.ToDictionary(f => f, f => json.TryGetProperty(f, out var value) ? value : throw new ArgumentException($"Unknown projected field '{f}'."));
    }

    [McpServerTool(Name = "resolve_definitions", ReadOnly = true, OpenWorld = false)]
    [Description("Batch exact or substring lookups against loaded local catalogs. kind: skills, sets, items, champion. Names and IDs are definitions, not ownership. Returns bounded rows; resolve exact skill names in edit_build without a separate lookup. No network request.")]
    public string Resolve(string kind, string[]? names = null, long[]? ids = null, int limit = 20, int offset = 0) => ToolResult.Json(() =>
    {
        if (limit is < 1 or > 100 || offset < 0) throw new ArgumentException("Invalid page.");
        var catalog = workspace.Read().Catalog;
        bool Match(long id, string? name) => (ids is null || ids.Contains(id)) && (names is null || names.Any(n => name?.Contains(n, StringComparison.OrdinalIgnoreCase) == true));
        object[] rows = kind switch
        {
            "skills" => catalog.Skills.Values.Where(s => Match(s.Id, s.Name)).Cast<object>().ToArray(),
            "items" => catalog.Items.Values.Where(s => Match(s.Id, s.Name)).Cast<object>().ToArray(),
            "sets" => catalog.Sets.Values.Where(s => Match(s.Id, s.Names.GetValueOrDefault("en"))).Select(s => (object)new { s.Id, Name = s.Names.GetValueOrDefault("en") }).ToArray(),
            "champion" => catalog.ChampionStars.Values.Where(s => Match(s.Id, s.Name)).Cast<object>().ToArray(),
            _ => throw new ArgumentException("kind must be skills, sets, items or champion.")
        };
        return new { Total = rows.Length, HasMore = offset + limit < rows.Length, Rows = rows.Skip(offset).Take(limit) };
    });
}
