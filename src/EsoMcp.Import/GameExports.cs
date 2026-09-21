using EsoData.Formats;
using EsoData.Items;
using EsoMcp.Core;

namespace EsoMcp.Import;

public sealed class GameExports : IGameExports
{
    public string CraftingImport(IReadOnlyList<long> itemIds, int level, int quality, int championPoints = 0,
        int styleId = 1, long enchantmentItemId = 0)
    {
        if (itemIds.Count is < 1 or > 100) throw new ArgumentException("Provide 1..100 resolved item IDs, preserving duplicates for repeated pieces.");
        return CraftingImport(itemIds.Select(id => new CraftingImportItem(id, enchantmentItemId)).ToArray(),
            level, quality, championPoints, styleId);
    }

    public string CraftingImport(IReadOnlyList<CraftingImportItem> items, int level, int quality, int championPoints = 0,
        int styleId = 1)
    {
        if (items.Count is < 1 or > 100) throw new ArgumentException("Provide 1..100 resolved items, preserving duplicates for repeated pieces.");
        return CraftingQueue.Write(items.Select(item => CraftedItem.Create(item.ItemId, level, (ItemQuality)quality,
            item.StyleId ?? styleId, championPoints, item.EnchantmentItemId,
            item.EnchantmentQuality is int enchantmentQuality ? (ItemQuality)enchantmentQuality : null)));
    }
}
