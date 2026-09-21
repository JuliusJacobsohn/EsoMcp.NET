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
        return CraftingQueue.Write(itemIds.Select(id => CraftedItem.Create(id, level, (ItemQuality)quality,
            styleId, championPoints, enchantmentItemId)));
    }
}
