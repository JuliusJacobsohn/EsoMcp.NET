using EsoData.Formats;
using EsoData.Items;
using EsoMcp.Core;

namespace EsoMcp.Import;

public sealed class GameExports : IGameExports
{
    private static readonly int[] CspsEquipmentSlots = [0, 3, 2, 16, 6, 8, 9, 1, 11, 12, 4, 5, 20, 21];

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

    public string CspsEquipmentImport(IReadOnlyList<CspsEquipmentItem> items)
    {
        if (items.Count is < 1 or > 14) throw new ArgumentException("Provide 1..14 supported equipment slots.");
        var duplicate = items.GroupBy(x => x.EquipSlot).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null) throw new ArgumentException($"Equipment slot {duplicate.Key} is repeated.");

        var gear = new CspsGearSlot?[16];
        foreach (var item in items)
        {
            var position = Array.IndexOf(CspsEquipmentSlots, item.EquipSlot);
            if (position < 0) throw new ArgumentException($"Equipment slot {item.EquipSlot} is not supported for gear imports.");
            if (item.SetId <= 0 || item.Type < 0 || item.Trait < 0 || item.Quality is < 1 or > 5 || item.EnchantmentEffectId < 0)
                throw new ArgumentException($"Equipment slot {item.EquipSlot} has invalid set, type, trait, quality or enchantment values.");
            gear[position] = new(item.SetId, item.Type, item.Trait, item.Quality, item.EnchantmentEffectId);
        }

        var build = new CspsBuild { Gear = gear };
        return build.ToString();
    }

    public (string Text, string Url) HubBuildImport(HubBuildPatch patch)
    {
        var build = HubBuild.Parse(patch.Template);
        if (patch.FrontBar is not null)
            build.FrontBar = patch.FrontBar.Select(x => x is null ? null : new HubBarSlot(x.AbilityId, x.Scripts)).ToArray();
        if (patch.BackBar is not null)
            build.BackBar = patch.BackBar.Select(x => x is null ? null : new HubBarSlot(x.AbilityId, x.Scripts)).ToArray();
        if (patch.SlottedChampionPoints is not null)
            build.SlottedChampionPoints = patch.SlottedChampionPoints.Select(x => x is null ? null : new ChampionStar(x.SkillId, x.Points)).ToArray();
        if (patch.OtherChampionPoints is not null)
            build.OtherChampionPoints = patch.OtherChampionPoints.Select(x => new ChampionStar(x.SkillId, x.Points)).ToArray();
        return (build.ToString(), build.ToUrl());
    }
}
