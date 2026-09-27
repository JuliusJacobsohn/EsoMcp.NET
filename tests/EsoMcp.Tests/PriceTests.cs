using System.Text.Json;
using EsoData.Accounts;
using EsoData.Pricing;
using EsoMcp.Core;
using EsoMcp.Import;
using EsoMcp.Server;

namespace EsoMcp.Tests;

public class PriceTests
{
    [Fact]
    public void InventoryRankingFiltersBeforeSortingAndPaginatesAfterSorting()
    {
        using var w = new TestWorkspace(); var store = new WorkspaceStore(w.Database.Path);
        ItemPrice Price(decimal unit) => new(PriceMatchStatus.Matched,
            new(new(7, 0, 1, -1, []), new(unit, unit, unit, 1, 1, unit, null, null, null)));
        store.SaveAccounts([new() { Name = "@Example", Server = "EU", SharedStorage = [new() { Items =
        [
            new() { Name = "Single expensive", Location = "Bank", Count = 1, Price = Price(100) },
            new() { Name = "Bulk cheaper", Location = "Bank", Count = 20, Price = Price(10) },
            new() { Name = "Unknown", Location = "Bank", Count = 1000 },
            new() { Name = "Outside bank", Location = "CraftBag", Count = 1, Price = Price(1000) }
        ] }] }]);
        var tool = new AccountTools(new(store, w.Database, new(), true));
        JsonElement Query(string sort, int offset = 0) => JsonDocument.Parse(tool.Inspect(queries:
            [new() { Section = "inventory", Location = "bank", PriceStatus = "Matched", Sort = sort,
                Offset = offset, Limit = 1, Fields = ["name", "estimatedStackPrice"] }]))
            .RootElement.GetProperty("results")[0];
        var stack = Query("stackPriceDesc");
        Assert.Equal(2, stack.GetProperty("total").GetInt32());
        Assert.True(stack.GetProperty("hasMore").GetBoolean());
        Assert.Equal("Bulk cheaper", stack.GetProperty("rows")[0].GetProperty("name").GetString());
        Assert.Equal(200m, stack.GetProperty("rows")[0].GetProperty("estimatedStackPrice").GetDecimal());
        Assert.Equal("Single expensive", Query("unitPriceDesc").GetProperty("rows")[0].GetProperty("name").GetString());
        var second = Query("stackPriceDesc", 1);
        Assert.False(second.GetProperty("hasMore").GetBoolean());
        Assert.Equal("Single expensive", second.GetProperty("rows")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void CatalogIsIndependentOfAccountsRefreshesAndSurvivesOfflineRestart()
    {
        using var w = new TestWorkspace();
        var directory = Path.Combine(w.Folder, "TamrielTradeCentre"); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "ItemLookUpTable_EN.lua"), "self.ItemLookUpTable={['test reagent']={[1600]=7}}");
        var file = Path.Combine(directory, "PriceTableEU.lua");
        void Write(int value) => File.WriteAllText(file, "self.PriceTable={Data={[7]={[0]={[1]={[-1]={A=VALUE,S=VALUE}}}}}}".Replace("VALUE", value.ToString()));
        Write(10);
        var store = new WorkspaceStore(w.Database.Path);
        var workspace = new AccountWorkspace(store, w.Database, new() { Locations = [new(w.Folder, w.Folder)] });
        var tool = new PriceTools(workspace);
        var response = JsonDocument.Parse(tool.Query(text: "reagent", limit: 1));
        Assert.Equal(1, response.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(10m, response.RootElement.GetProperty("rows")[0].GetProperty("statistics").GetProperty("average").GetDecimal());
        Assert.Empty(store.Accounts());
        Write(25); tool.Query();
        Assert.Equal(25m, store.Prices()[0].Entries[0].Statistics.Average);
        var offline = new PriceTools(new(new(w.Database.Path), w.Database, new(), true));
        File.Delete(file);
        Assert.Contains("25", offline.Query());
        Assert.Contains("\"available\":false", tool.Query());
        Assert.Empty(store.Prices());
        Assert.Throws<ModelContextProtocol.McpException>(() => tool.Query(limit: 101));
        Assert.Throws<ModelContextProtocol.McpException>(() => tool.Query(region: "unknown"));
    }
    [Fact]
    public void InventoryAssociationRoundTripsAndDoesNotClaimUnknownPricesAreZero()
    {
        using var w = new TestWorkspace(); var store = new WorkspaceStore(w.Database.Path);
        var price = new ItemPrice(PriceMatchStatus.Matched, new(new(7, 0, 1, -1, []), new(20, 10, 30, 5, 20, 15, null, null, null)));
        store.SaveAccounts([new() { Name = "@Example", Server = "EU", SharedStorage = [new() { Items =
            [new() { Name = "Known", Count = 2, Price = price }, new() { Name = "Unknown", Count = 9 }] }] }]);
        var tool = new AccountTools(new(store, w.Database, new(), true));
        var response = JsonDocument.Parse(tool.Inspect(queries: [new() { Section = "inventory" }]));
        var rows = response.RootElement.GetProperty("results")[0].GetProperty("rows");
        Assert.Equal(30m, rows[0].GetProperty("estimatedStackPrice").GetDecimal());
        Assert.Equal("Matched", rows[0].GetProperty("price").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("estimatedStackPrice").ValueKind);
    }
}
