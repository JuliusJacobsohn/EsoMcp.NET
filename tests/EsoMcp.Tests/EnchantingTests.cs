using System.Text.Json;
using EsoData.Accounts;
using EsoMcp.Core;
using EsoMcp.Import;
using EsoMcp.Server;

namespace EsoMcp.Tests;

public sealed class EnchantingTests
{
    [Fact]
    public void ExportUsesInstalledRuneIdsAndAccountWideStocksWithoutTouchingGear()
    {
        using var w = new TestWorkspace();
        var folder = Path.Combine(w.Folder, "LibLazyCrafting"); Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Enchanting.lua"), """
            local glyphInfo={{10,11,900001,900002,"Negative","Positive",ITEMTYPE_GLYPH_WEAPON,ITEMTYPE_GLYPH_ARMOR,900003}}
            local enchantLevelInfo={{1,900004,366,50,lvl=nil,cp=160}}
            local qualityItemIdInfo={900005,900006,900007,900008,900009}
            local cpQualityInfo={[160]={366,367,368,369,370}}
            """);
        var store = new WorkspaceStore(w.Database.Path);
        store.SaveAccounts([new EsoAccount { Name = "@Example", Server = "EU", Characters = [new() { Id = "1", Name = "Crafter" }],
            SharedStorage = [new() { Location = "Bank", Items = [new() { ItemId = 900003, Name = "Essence", Count = 1 }] }] }]);
        var options = new ImportOptions { Locations = [new(w.Folder, w.Folder)] };
        var workspace = new AccountWorkspace(store, w.Database, options, offlineDefault: true);
        var tools = new BuildTools(workspace, store);
        using var created = JsonDocument.Parse(tools.Edit("create", character: "Crafter", patch: new()
            { Crafting = [new() { ItemId = 900002, Quantity = 2, Quality = 5 }] }));
        var id = created.RootElement.GetProperty("plan").GetProperty("id").GetString()!;
        using var exported = JsonDocument.Parse(tools.Export(id, "enchanting"));
        var essence = exported.RootElement.GetProperty("materials").EnumerateArray().Single(m => m.GetProperty("itemId").GetInt64() == 900003);
        Assert.Equal(1, essence.GetProperty("missing").GetInt64());
        Assert.Contains("CraftEnchantingItemId(900004,900003,900009", exported.RootElement.GetProperty("text").GetString());
        Assert.Throws<ModelContextProtocol.McpException>(() => tools.Export(id, "crafting"));
        var account = new AccountTools(workspace, options);
        Assert.Contains("Positive", account.Resolve("glyphs", names: ["Positive"]));
    }
}
