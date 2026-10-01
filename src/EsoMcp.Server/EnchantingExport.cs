using EsoData.Accounts;
using EsoData.Builds;
using EsoMcp.Import;

namespace EsoMcp.Server;

internal static class EnchantingExport
{
    public static object Create(BuildPlan plan, EsoAccount account, WorkspaceRead read)
    {
        var enchanting = read.Enchanting ?? throw new InvalidOperationException("Install LibLazyCrafting to resolve glyph recipes.");
        if (plan.Crafting.Count is < 1 or > 100) throw new ArgumentException("An enchanting export requires 1..100 glyph orders.");
        var recipes = plan.Crafting.Select(enchanting.Recipe).ToArray();
        var materials = recipes.SelectMany(r => r.Materials).GroupBy(m => m.Key).Select(g =>
        {
            var owned = account.Inventory.Where(i => i.ItemId == g.Key).ToArray();
            var required = g.Sum(m => m.Value); var available = owned.Sum(i => i.Count);
            return new { ItemId = g.Key, Name = owned.Select(i => i.Name).FirstOrDefault(n => n is not null)
                ?? read.Catalog.Items.GetValueOrDefault(g.Key)?.Name,
                Required = required, Owned = available, Missing = Math.Max(0, required - available) };
        }).ToArray();
        return new { plan.Id, plan.Revision, Format = "LibLazyCrafting chat queue", Recipes = recipes,
            Materials = materials, Text = string.Join(Environment.NewLine, recipes.Select(r => r.ChatCommand)),
            Note = "Paste each line once into ESO chat on the crafter, then visit an enchanting station. Creates loose glyphs; apply manually. Queue is in memory and is lost on reload/logout. Lazy Set Crafter Import Links does not accept standalone glyphs." };
    }
}
