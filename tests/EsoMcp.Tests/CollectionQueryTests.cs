using System.Text.Json;
using EsoData.Accounts;
using EsoData.Catalogs;
using EsoMcp.Core;
using EsoMcp.Import;
using EsoMcp.Server;

namespace EsoMcp.Tests;

public class CollectionQueryTests
{
    [Fact]
    public void GameCapturedDefinitionsProvideCollectedNamesAndCost()
    {
        using var w = new TestWorkspace();
        w.Write("LibMultiAccountSets.lua", """
            LibMultiAccountSetsSavedVariables={EsoDataPieces={[7]={{101,8192,'Example Ring'},{102,2,'Example Sword'}}}}
            """);
        var store = new WorkspaceStore(w.Database.Path);
        store.SaveAccounts([new EsoAccount { Name = "@Example", Server = "EU", SetCollections = new() { [7] = 8192 } }]);
        var options = new ImportOptions { Locations = [new(w.Folder)] };
        var tools = new AccountTools(new(store, w.Database, options, offlineDefault: true));
        using var response = JsonDocument.Parse(tools.Inspect(queries:
            [new() { Section = "collections", SetIds = [7, 8], IncludeDetails = true }]));
        var rows = response.RootElement.GetProperty("results")[0].GetProperty("rows");
        Assert.Equal(1, rows[0].GetProperty("collectedCount").GetInt32());
        Assert.Equal(2, rows[0].GetProperty("totalCount").GetInt32());
        Assert.Equal(75, rows[0].GetProperty("reconstructionCrystals").GetInt32());
        var ring = rows[0].GetProperty("pieces").EnumerateArray().Single(p => p.GetProperty("pieceId").GetInt64() == 101);
        Assert.Equal("Example Ring", ring.GetProperty("name").GetString());
        Assert.True(ring.GetProperty("collected").GetBoolean());
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("collectedCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("reconstructionCrystals").ValueKind);
    }
}
