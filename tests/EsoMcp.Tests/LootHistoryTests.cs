using System.Text.Json;
using EsoData.Catalogs;
using EsoMcp.Core;
using EsoMcp.Import;
using EsoMcp.Server;

namespace EsoMcp.Tests;

public class LootHistoryTests
{
    [Fact]
    public void HistoryPersistsAndFiltersRecipientsAndCatalogSetsWithoutInventingOwnership()
    {
        using var w = new TestWorkspace();
        w.Write("uespLog.lua", """
            uespLogSavedVars={data={CharName="Self",CharId="1",AccountName="@Self",Server="EU"}}
            """);
        w.Write("LootLog.lua", """
            LootLogHistory={EU={[100]={
              "360001;|H0:item:123:362:50:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0|h|h;1;@Friend;Friend;15"
            }},NA={[100]={"360002;9876;1;@Other;Other;2"}}}
            """);
        var workspace = new AccountWorkspace(new WorkspaceStore(w.Database.Path), w.Database,
            new() { Locations = [new(w.Folder)] });
        var account = Assert.Single(workspace.Read().Data.Accounts);
        Assert.Single(account.LootHistory!.Events);
        Assert.Empty(account.Inventory);
        var catalog = new GameCatalog { Items = { [123] = new(123, "Example Ice Staff", 456, WeaponType: 13, Trait: 4) },
            Sets = { [456] = new(456, new Dictionary<string, string> { ["en"] = "Example Set" }, [123]) } };
        var catalogPath = Path.Combine(w.Folder, "catalog.json"); catalog.Write(catalogPath);
        var tools = new AccountTools(new AccountWorkspace(new WorkspaceStore(w.Database.Path), w.Database,
            new() { Locations = [new(w.Folder)], CatalogPaths = [catalogPath] }));
        var result = JsonDocument.Parse(tools.Inspect(queries: [new() { Section = "lootHistory", SetIds = [456] }])).RootElement.GetProperty("results")[0];
        Assert.Equal(1, result.GetProperty("total").GetInt32());
        Assert.Equal("@Friend", result.GetProperty("rows")[0].GetProperty("recipientAccount").GetString());
        var whisper = result.GetProperty("rows")[0].GetProperty("whisper");
        Assert.StartsWith("/w Friend, ", whisper.GetProperty("command").GetString());
        Assert.Contains("|hExample Ice Staff|h", whisper.GetProperty("itemLink").GetString());
        Assert.Single(Assert.Single(workspace.Read(offline: true).Data.Accounts).LootHistory!.Events);
        var grouped = JsonDocument.Parse(tools.Inspect(queries: [new() { Section = "lootHistory", Group = true }])).RootElement.GetProperty("results")[0];
        Assert.Equal("@Friend", grouped.GetProperty("rows")[0].GetProperty("recipientAccount").GetString());
        Assert.StartsWith("/w Friend, ", grouped.GetProperty("rows")[0].GetProperty("whispers")[0].GetProperty("command").GetString());
        var own = JsonDocument.Parse(tools.Inspect(queries: [new() { Section = "lootHistory", Character = "Self" }])).RootElement.GetProperty("results")[0];
        Assert.Equal(0, own.GetProperty("total").GetInt32());
    }

    [Fact]
    public void MissingOptionalHistoryRemainsUnavailable()
    {
        using var w = new TestWorkspace();
        var store = new WorkspaceStore(w.Database.Path);
        store.SaveAccounts([new() { Name = "@Self", Server = "EU" }]);
        var tools = new AccountTools(new AccountWorkspace(store, w.Database, new()));
        var result = JsonDocument.Parse(tools.Inspect(queries: [new() { Section = "lootHistory" }], offline: true)).RootElement.GetProperty("results")[0];
        Assert.False(result.GetProperty("available").GetBoolean());
    }

    [Fact]
    public void OwnAccountDropsDoNotGenerateRequests()
    {
        using var w = new TestWorkspace();
        var store = new WorkspaceStore(w.Database.Path);
        store.SaveAccounts([new() { Name = "@Self", Server = "EU", LootHistory = new()
        {
            Events = [new("EU", DateTimeOffset.UnixEpoch,
                "|H0:item:123:362:50:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0|h|h", 1,
                "@self", "Self", 30, 123, true, true, true)]
        } }]);
        var tools = new AccountTools(new AccountWorkspace(store, w.Database, new()));
        var result = JsonDocument.Parse(tools.Inspect(queries: [new() { Section = "lootHistory", Fields = ["whisper"] }], offline: true));
        Assert.Equal(JsonValueKind.Null, result.RootElement.GetProperty("results")[0].GetProperty("rows")[0].GetProperty("whisper").ValueKind);
    }

    [Fact]
    public void GroupingKeepsPlayersSeparateAndIncludesAllMatchingDrops()
    {
        using var w = new TestWorkspace();
        var store = new WorkspaceStore(w.Database.Path);
        EsoData.Addons.LootEvent Drop(string account, string character, int item) => new("EU", DateTimeOffset.UnixEpoch,
            $"|H0:item:{item}:362:50:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0|h|h", 1, account, character, 15, item, false, true, true);
        store.SaveAccounts([new() { Name = "@Self", Server = "EU", LootHistory = new()
        {
            Events = [Drop("@Friend", "Friend", 123), Drop("@friend", "Friend", 124), Drop("@Other", "Other", 125)]
        } }]);
        var tools = new AccountTools(new AccountWorkspace(store, w.Database, new()));
        var result = JsonDocument.Parse(tools.Inspect(queries: [new() { Section = "lootHistory", Group = true }], offline: true)).RootElement.GetProperty("results")[0];
        Assert.Equal(2, result.GetProperty("total").GetInt32());
        Assert.Equal(2, result.GetProperty("rows")[0].GetProperty("count").GetInt32());
        Assert.Equal(2, result.GetProperty("rows")[0].GetProperty("whispers")[0].GetProperty("itemLinks").GetArrayLength());
        Assert.Equal("Other", result.GetProperty("rows")[1].GetProperty("whispers")[0].GetProperty("recipient").GetString());
    }
}
