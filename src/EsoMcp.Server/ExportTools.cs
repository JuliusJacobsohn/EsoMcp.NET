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
    [Description("Create native CSPS text with two ability bars and the character's currently allocated CP. Every requested ability must be an observed purchased active skill. Optional championSlots has 12 CP star IDs in Craft/Warfare/Fitness order and may only slot stars that already have points. No skill purchases, gear, attributes or other sections are imported. CP point amounts mirror the last disk save, including unspent points. Import as CSPS text and apply Ability Bar and Champion Points only.")]
    public string AvailableLoadout(string characterKey, long[] frontBar, long[] backBar, long?[]? championSlots = null) => ToolResult.Json(() =>
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
        if (championSlots is not null && championSlots.Length != 12)
            throw new ArgumentException("championSlots must have twelve positions.");
        var allocated = allocations.Where(x => x.Points > 0).Select(x => x.Id).ToHashSet();
        if (championSlots is not null && championSlots.Any(id => id is > 0 && !allocated.Contains(id.Value)))
            throw new ArgumentException("championSlots may only contain stars with observed allocated points.");
        var cpBars = Enumerable.Range(0, 3).Select(bar =>
            (IReadOnlyList<long?>)Enumerable.Range(1, 4).Select(slot =>
            {
                var id = championSlots is null ? slots.GetProperty((bar * 4 + slot).ToString()).GetInt64()
                    : championSlots[bar * 4 + slot - 1] ?? 0;
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

    [McpServerTool(Name = "create_csps_respec_import", ReadOnly = true, OpenWorld = false)]
    [Description("Create one native CSPS import for a full skill respec, two bars, attributes and all Champion Points. Uses resolved game IDs supplied by the caller. Checks the character's observed total skill points and CP budget; it does not infer unlock requirements, apply the build in game or change equipment. Import as CSPS text, selecting Skills, Ability Bar, Stats and Champion Points only.")]
    public string Respec(string characterKey, CspsRespecPlan plan) => ToolResult.Json(() =>
    {
        var skillsView = database.CharacterState(characterKey, "skills");
        var championView = database.CharacterState(characterKey, "champion");
        if (!skillsView.Available || !championView.Available)
            throw new ArgumentException("Current skill and CP observations are required for this character.");
        var skills = (IReadOnlyDictionary<string, JsonElement>)skillsView.Data!;
        var champion = (JsonElement)championView.Data!;
        var totalSkills = skills["totalSkillPoints"].GetInt32();
        var totalCp = champion.GetProperty("spentPoints").GetInt32() + champion.GetProperty("unspentPoints").GetInt32();
        if (plan.Active.Length == 0 || plan.Active.Any(x => x.AbilityId <= 0 || x.Morph is < 0 or > 2)
            || plan.Active.Select(x => x.AbilityId).Distinct().Count() != plan.Active.Length)
            throw new ArgumentException("Active skills need distinct positive IDs and morph 0, 1 or 2.");
        if (plan.Passive.Any(x => x.AbilityId <= 0 || x.Rank is < 1 or > 3)
            || plan.Passive.Select(x => x.AbilityId).Distinct().Count() != plan.Passive.Length)
            throw new ArgumentException("Passive skills need distinct positive IDs and rank 1, 2 or 3.");
        if (plan.FrontBar.Length != 6 || plan.BackBar.Length != 6 ||
            plan.FrontBar.Concat(plan.BackBar).Any(id => id <= 0 || !plan.Active.Any(x => x.AbilityId == id)))
            throw new ArgumentException("Each bar needs six purchased active IDs.");
        if (plan.Health < 0 || plan.Magicka < 0 || plan.Stamina < 0 ||
            plan.Health + plan.Magicka + plan.Stamina != 64)
            throw new ArgumentException("Attributes must allocate exactly 64 points.");
        var plannedSkills = plan.Active.Sum(x => 1 + (x.Morph == 0 ? 0 : 1)) + plan.Passive.Sum(x => x.Rank);
        if (plannedSkills > totalSkills)
            throw new ArgumentException($"Plan needs {plannedSkills} skill points, but {totalSkills} are observed.");
        if (plan.ChampionPoints.Length == 0 || plan.ChampionPoints.Any(x => x.SkillId <= 0 || x.Points <= 0)
            || plan.ChampionPoints.Select(x => x.SkillId).Distinct().Count() != plan.ChampionPoints.Length)
            throw new ArgumentException("Champion allocations need distinct positive IDs and points.");
        var plannedCp = plan.ChampionPoints.Sum(x => x.Points);
        if (plannedCp != totalCp)
            throw new ArgumentException($"Plan allocates {plannedCp} CP, but {totalCp} are observed.");
        if (plan.ChampionSlots.Length != 12 || plan.ChampionSlots.Any(id => id is > 0 &&
            !plan.ChampionPoints.Any(x => x.SkillId == id)))
            throw new ArgumentException("Champion slots need twelve IDs, each with allocated points.");
        var build = new CspsBuild
        {
            Skills = new CspsSkills(plan.Active.Select(x => new ActiveSkill(x.AbilityId, x.Morph)).ToArray(),
                plan.Passive.Select(x => new PassiveSkill(x.AbilityId, x.Rank)).ToArray()),
            Bars = new IReadOnlyList<BarSlot?>[]
            {
                plan.FrontBar.Select(x => (BarSlot?)new BarSlot(x)).ToArray(),
                plan.BackBar.Select(x => (BarSlot?)new BarSlot(x)).ToArray()
            },
            Attributes = new EsoData.Models.Attributes(plan.Health, plan.Magicka, plan.Stamina),
            ChampionPoints = new CspsChampionPoints(
                plan.ChampionPoints.Select(x => new ChampionStar(x.SkillId, x.Points)).ToArray(),
                Enumerable.Range(0, 3).Select(bar => (IReadOnlyList<long?>)plan.ChampionSlots
                    .Skip(bar * 4).Take(4).ToArray()).ToArray())
        };
        return new
        {
            Format = "CSPS", Text = build.ToString(),
            Sections = new[] { "skills", "ability bar", "stats", "champion points" },
            TotalSkillPoints = totalSkills, PlannedSkillPoints = plannedSkills,
            RemainingSkillPoints = totalSkills - plannedSkills, PlannedChampionPoints = plannedCp,
            ObservedAt = skillsView.Observation!["observed_at"], AppliedInGame = false
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
