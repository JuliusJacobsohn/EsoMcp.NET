using EsoData.Accounts;
using EsoData.Builds;
using EsoData.Items;
using EsoMcp.Import;

namespace EsoMcp.Server;

/// <summary>Provisioning quantities are craft iterations; recipe quality and yield are fixed in game.</summary>
internal static class ProvisioningExport
{
    public static object Create(BuildPlan plan, EsoAccount account, WorkspaceRead read)
    {
        if (plan.Crafting.Count is < 1 or > 100)
            throw new ArgumentException("A provisioning export requires 1..100 recipe orders.");
        var crafter = account.Character(plan.CharacterId);
        var recipes = plan.Crafting.Select(order =>
        {
            if (order.ItemId <= 0 || order.Quantity is < 1 or > 1000 || order.EnchantmentItemId != 0)
                throw new ArgumentException("Use a recipe item ID, 1..1000 craft iterations and no enchantment.");
            // Recipe knowledge is keyed by the learnable recipe, not the produced food/drink.
            if (!crafter.Progress.Knowledge.TryGetValue("recipes", out var knowledge)
                || !knowledge.TryGetValue(order.ItemId, out var known) || known is null)
                throw new ArgumentException($"Recipe {order.ItemId} is unobserved on the crafter. Refresh recipe knowledge before exporting.");
            if (known != true) throw new ArgumentException($"Recipe {order.ItemId} is not learned by the crafter.");
            var fields = new long[21]; fields[0] = order.ItemId; fields[1] = 3; fields[2] = 1;
            var link = new ItemLink(fields).WithoutLabel();
            var command = $"/script local l,r=GetItemLinkGrantedRecipeIndices(\"{link}\") if l and r and GetRecipeInfo(l,r) then LLC_UserRequests:CraftProvisioningItemByRecipeIndex(l,r,{order.Quantity},true,\"EsoData\") else d(\"Recipe not learned\") end";
            if (command.Length > 350) throw new ArgumentException("Provisioning command exceeds ESO chat length.");
            return new { RecipeItemId = order.ItemId, Name = read.Catalog.Items.GetValueOrDefault(order.ItemId)?.Name,
                CraftIterations = order.Quantity, Known = known, ChatCommand = command };
        }).ToArray();
        return new { plan.Id, plan.Revision, Format = "LibLazyCrafting provisioning chat queue", Crafter = crafter.Name,
            Recipes = recipes, Text = string.Join(Environment.NewLine, recipes.Select(r => r.ChatCommand)),
            Note = "Paste each line once into ESO chat on the named crafter, then visit a cooking fire. Quantity is craft iterations, not resulting servings; passives affect yield. Recipe controls quality and level. Ingredients and skill requirements are checked in game. Queue is lost on reload/logout. Lazy Set Crafter Import Links does not accept recipes." };
    }
}
