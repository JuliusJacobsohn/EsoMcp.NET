using System.Text.Json;
using EsoData.Accounts;
using EsoMcp.Core;
using EsoMcp.Import;
using EsoMcp.Server;

namespace EsoMcp.Tests;

public sealed class ProvisioningTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void ExportRequiresObservedLearnedRecipeAndUsesCraftIterations(bool? known)
    {
        using var w = new TestWorkspace();
        var store = new WorkspaceStore(w.Database.Path);
        var crafter = new EsoCharacter { Id = "1", Name = "Crafter" };
        if (known.HasValue) crafter.Progress.Knowledge["recipes"] = new() { [900001] = known.Value };
        store.SaveAccounts([new EsoAccount { Name = "@Example", Server = "EU", Characters = [crafter] }]);
        var workspace = new AccountWorkspace(store, w.Database, new(), offlineDefault: true);
        var tools = new BuildTools(workspace, store);
        using var created = JsonDocument.Parse(tools.Edit("create", character: "Crafter", patch: new()
            { Crafting = [new() { ItemId = 900001, Quantity = 25 }] }));
        var id = created.RootElement.GetProperty("plan").GetProperty("id").GetString()!;
        if (known != true)
        {
            Assert.Throws<ModelContextProtocol.McpException>(() => tools.Export(id, "provisioning"));
            return;
        }
        using var exported = JsonDocument.Parse(tools.Export(id, "provisioning"));
        var command = exported.RootElement.GetProperty("text").GetString()!;
        Assert.Contains("GetItemLinkGrantedRecipeIndices", command);
        Assert.Contains("GetRecipeInfo(l,r)", command);
        Assert.Contains("CraftProvisioningItemByRecipeIndex(l,r,25,true", command);
        Assert.True(command.Length <= 350);
        Assert.Equal(25, exported.RootElement.GetProperty("recipes")[0].GetProperty("craftIterations").GetInt32());
        Assert.Throws<ModelContextProtocol.McpException>(() => tools.Export(id, "crafting"));
    }
}
