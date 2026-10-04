using System.Text.Json;
using EsoData.Catalogs;
using EsoMcp.Core;
using EsoMcp.Import;
using EsoMcp.Server;

namespace EsoMcp.Tests;

public class ItemTraitTests
{
    [Theory]
    [InlineData(31, 21)]
    [InlineData(18, 16)]
    [InlineData(3, 4)]
    public void FreshInventoryEquipmentAndFiltersUseChosenTrait(int chosen, int original)
    {
        using var w = new TestWorkspace();
        var link = $"|H0:item:900001:364:50:0:0:0:{chosen}:25:0:0:0:0:0:0:2049:4:0:1:0:0:0|h|h";
        w.Write("IIfA.lua", """
            IIFA_DATABASE={['@Example']={servers={EU={CharIdToName={['1']='Example'},DBv3={
              ['%LINK%']={itemName='Test',itemQuality=5,locations={['1']={bagID=0,bagSlot={[1]=1}}}}
            }}}}}
            """.Replace("%LINK%", link));
        w.Write("uespLog.lua", """
            uespLogSavedVars={data={CharName='Example',CharId='1',AccountName='@Example',Server='EU',TimeStamp=100,
              EquipSlots={[1]={link='%LINK%'}}}}
            """.Replace("%LINK%", link));
        var catalog = new GameCatalog { Items = { [900001] = new(900001, "Test", 7, Trait: original) } };
        var path = Path.Combine(w.Folder, "catalog.json"); catalog.Write(path);
        var store = new WorkspaceStore(w.Database.Path);
        var options = new ImportOptions { Locations = [new(w.Folder)], CatalogPaths = [path] };
        var tools = new AccountTools(new(store, w.Database, options));
        using var result = JsonDocument.Parse(tools.Inspect(queries:
            [new() { Section = "inventory", Traits = [chosen] }, new() { Section = "equipment", Character = "Example" }]));
        var inventory = result.RootElement.GetProperty("results")[0];
        Assert.Equal(1, inventory.GetProperty("total").GetInt32());
        Assert.Equal(chosen, inventory.GetProperty("rows")[0].GetProperty("trait").GetInt32());
        var equipment = result.RootElement.GetProperty("results")[1].GetProperty("rows")[0].GetProperty("item");
        Assert.Equal(chosen, equipment.GetProperty("trait").GetInt32());
        Assert.Equal(900001, equipment.GetProperty("itemId").GetInt64());
        var saved = store.Accounts().Single();
        Assert.Equal(chosen, saved.Inventory.Single().Trait);
        saved.LootHistory = new() { Events = [new("EU", DateTimeOffset.UnixEpoch, link, 1,
            "@Friend", "Friend", 15, 900001, false, true, true)] };
        store.SaveAccounts([saved]);
        using var loot = JsonDocument.Parse(tools.Inspect(queries:
            [new() { Section = "lootHistory", Traits = [chosen] }], offline: true));
        var drop = loot.RootElement.GetProperty("results")[0].GetProperty("rows")[0];
        Assert.Equal(chosen, drop.GetProperty("trait").GetInt32());
    }
}
