using System.ComponentModel;
using EsoMcp.Core;
using ModelContextProtocol.Server;

namespace EsoMcp.Server;

[McpServerToolType]
public sealed class CraftingPlanTools(ICraftingCatalog catalog, IGameExports exports)
{
    [McpServerTool(Name = "refresh_item_metadata", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Explicitly download current item definitions only for the supplied local LibSets set IDs, then store them in local SQLite. This is the metadata update step; ordinary MCP requests do not access the network. Use find_sets first. It never changes ESO files.")]
    public async Task<string> RefreshItemMetadata(long[] setIds, CancellationToken cancellationToken = default) =>
        DataJson.Write(await catalog.RefreshItemMetadataAsync(setIds, cancellationToken));

    [McpServerTool(Name = "create_semantic_crafting_import", ReadOnly = true, OpenWorld = false)]
    [Description("Resolve structured set/piece/trait choices from local SQLite and generate one Lazy Set Crafter Import Links text with per-item glyphs and styles. Run refresh_item_metadata for every set first. equipType, armorType, weaponType and trait values are visible in find_item_definitions after refresh; omission is allowed only when the remaining fields identify exactly one item. quality: 1 normal, 2 fine, 3 superior, 4 epic/purple, 5 legendary. CP gear needs level=50 and championPoints=160.")]
    public string CreateSemanticCraftingImport(CraftingPlanItem[] items, int level, int quality, int championPoints = 0,
        int styleId = 1)
    {
        var ids = catalog.Resolve(items);
        var encoded = items.Select((item, index) => new CraftingImportItem(ids[index], item.EnchantmentItemId,
            item.EnchantmentQuality, item.StyleId)).ToArray();
        return DataJson.Write(new { Format = "Lazy Set Crafter", Text = exports.CraftingImport(encoded, level, quality, championPoints, styleId) });
    }
}
