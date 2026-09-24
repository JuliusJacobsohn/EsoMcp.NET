using System.ComponentModel;
using System.Text.Json;
using EsoData.Formats;
using EsoMcp.Core;
using ModelContextProtocol.Server;

namespace EsoMcp.Server;

[McpServerToolType]
public sealed class ExportTools(Database database, IGameExports exports)
{
    [McpServerTool(Name = "export_saved_build", ReadOnly = true, OpenWorld = false)]
    [Description("Return the native CSPS import text for a stored build record. Includes the skills/passives, bars, attributes, CP and other sections saved in that profile. Does not fill missing allocations or apply anything in-game. Use list_records(kind='build') first.")]
    public string Build(string recordKey) => ToolResult.Json(() =>
    {
        var record = database.Record(recordKey);
        if (record.ValueKind != JsonValueKind.Object || !record.TryGetProperty("nativeText", out var native))
            throw new ArgumentException("Select a build record from list_records(kind='build').");
        return new { Format = "CSPS", Text = native.GetString(), AppliedInGame = false };
    });

    [McpServerTool(Name = "create_crafting_import", ReadOnly = true, OpenWorld = false)]
    [Description("Encode 1..100 resolved item IDs into Lazy Set Crafter import links. quality: 1 normal, 2 fine, 3 superior, 4 epic/purple, 5 legendary. Item IDs select the actual set, piece and trait; encoding cannot discover or validate those. Level must be a supported crafting level (e.g. 32, not 33). For CP gear use level=50 and championPoints. Does not craft, send mail or edit game files.")]
    public string Crafting(long[] itemIds, int level, int quality, int championPoints = 0, int styleId = 1, long enchantmentItemId = 0) =>
        ToolResult.Json(() => new { Format = "Lazy Set Crafter", Text = exports.CraftingImport(itemIds, level, quality, championPoints, styleId, enchantmentItemId) });

    [McpServerTool(Name = "create_csps_equipment_import", ReadOnly = true, OpenWorld = false)]
    [Description("Create an equipment-only native CSPS import from structured slots. Other CSPS sections are left empty, so import with Equipment selected. equipSlot uses ESO API slot IDs; type is armor type for body slots, weapon type for hand slots, and 0 for jewelry. quality: 1 normal, 2 fine, 3 superior, 4 epic/purple, 5 legendary. enchantmentEffectId is the glyph's default enchant ID, not its item ID. Does not equip items or edit game files.")]
    public string CspsEquipment(CspsEquipmentItem[] items) => ToolResult.Json(() => new
    {
        Format = "CSPS",
        Text = exports.CspsEquipmentImport(items),
        Sections = new[] { "equipment" },
        AppliedInGame = false
    });

    [McpServerTool(Name = "create_csps_available_loadout_import", ReadOnly = true, OpenWorld = false)]
    [Description("Create native CSPS text with two ability bars and the character's currently allocated CP. Every requested ability must be an observed purchased active skill; no skill purchases, gear, attributes or other sections are imported. CP mirrors the last disk save, including its unspent points. Import as CSPS text and apply Ability Bar and Champion Points only.")]
    public string AvailableLoadout(string characterKey, long[] frontBar, long[] backBar) => ToolResult.Json(() =>
    {
        if (frontBar.Length != 6 || backBar.Length != 6)
            throw new ArgumentException("Each ability bar needs five skills and one ultimate.");
        var skillsView = database.CharacterState(characterKey, "skills");
        var championView = database.CharacterState(characterKey, "champion");
        if (!skillsView.Available || !championView.Available)
            throw new ArgumentException("Current skill and CP observations are required for this character.");
        var skills = (IReadOnlyDictionary<string, JsonElement>)skillsView.Data!;
        var purchased = skills["skills"].EnumerateArray()
            .Where(x => !x.GetProperty("isPassive").GetBoolean() && x.GetProperty("rank").GetInt32() > 0)
            .Select(x => x.GetProperty("abilityId").GetInt64()).ToHashSet();
        var missing = frontBar.Concat(backBar).Where(id => id <= 0 || !purchased.Contains(id)).Distinct().ToArray();
        if (missing.Length != 0)
            throw new ArgumentException($"Not observed as purchased active skills: {string.Join(", ", missing)}.");
        var champion = (JsonElement)championView.Data!;
        var allocations = champion.GetProperty("stars").EnumerateArray()
            .Select(x => new ChampionStar(x.GetProperty("skillId").GetInt64(), x.GetProperty("points").GetInt32()))
            .ToArray();
        var slots = champion.GetProperty("slots");
        var cpBars = Enumerable.Range(0, 3).Select(bar =>
            (IReadOnlyList<long?>)Enumerable.Range(1, 4).Select(slot =>
            {
                var id = slots.GetProperty((bar * 4 + slot).ToString()).GetInt64();
                return id == 0 ? (long?)null : id;
            }).ToArray()).ToArray();
        var build = new CspsBuild
        {
            Bars = new IReadOnlyList<BarSlot?>[]
            {
                frontBar.Select(id => (BarSlot?)new BarSlot(id)).ToArray(),
                backBar.Select(id => (BarSlot?)new BarSlot(id)).ToArray()
            },
            ChampionPoints = new CspsChampionPoints(allocations, cpBars)
        };
        return new
        {
            Format = "CSPS",
            Text = build.ToString(),
            Sections = new[] { "ability bar", "champion points" },
            ObservedAt = skillsView.Observation!["observed_at"],
            SpentChampionPoints = champion.GetProperty("spentPoints").GetInt32(),
            UnspentChampionPoints = champion.GetProperty("unspentPoints").GetInt32(),
            SkillPurchases = 0,
            AppliedInGame = false
        };
    });

    [McpServerTool(Name = "create_hub_build_import", ReadOnly = true, OpenWorld = false)]
    [Description("Patch bars and Champion Points in an existing ESO-Hub addondata string or build-editor URL, then return import text and URL for CSPS Import Link. Six positions per bar, twelve CP slots in Craft/Warfare/Fitness order. Omitted sections stay as in the template. Does not apply or validate the build in game.")]
    public string HubBuild(HubBuildPatch patch) => ToolResult.Json(() =>
    {
        var (text, url) = exports.HubBuildImport(patch);
        return new { Format = "ESO-Hub build-editor", Text = text, Url = url, AppliedInGame = false };
    });
}
