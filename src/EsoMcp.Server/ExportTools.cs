using System.ComponentModel;
using System.Text.Json;
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
}
