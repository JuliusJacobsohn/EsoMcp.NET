using System.Text.Json;
using EsoData.Accounts;
using EsoData.Catalogs;
using EsoMcp.Core;
using EsoMcp.Import;
using EsoMcp.Server;

namespace EsoMcp.Tests;

public class KnowledgeQueryTests
{
    [Fact]
    public void ChampionLookupFallsBackToInstalledCspsNames()
    {
        using var w = new TestWorkspace();
        var addon = Path.Combine(w.Folder, "CarosSkillPointSaver", "data");
        Directory.CreateDirectory(addon);
        File.WriteAllText(Path.Combine(addon, "cpinfo.lua"), """
            [66] = GS(SI_RIDINGTRAINTYPE1) --Steed's Blessing (Speed)

            [265] = string.format("", GS()) --Ironclad

            [46] = string.format("", GS()) --Bastion
            """);
        var options = new ImportOptions { Locations = [new(w.Folder, w.Folder)] };
        var workspace = new AccountWorkspace(new(w.Database.Path), w.Database, options, offlineDefault: true);
        var tools = new AccountTools(workspace, options);

        using var response = JsonDocument.Parse(tools.Resolve("champion", names: ["Ironclad"]));
        var row = response.RootElement.GetProperty("rows")[0];
        Assert.Equal(265, row.GetProperty("id").GetInt64());
        Assert.Equal("Warfare", row.GetProperty("discipline").GetString());
        Assert.Equal("installed-csps", row.GetProperty("source").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("maximumPoints").ValueKind);
    }

    [Fact]
    public void RefreshedNamesCanBeQueriedWithoutChangingLearnedState()
    {
        using var w = new TestWorkspace();
        var store = new WorkspaceStore(w.Database.Path);
        var character = new EsoCharacter { Id = "1", Name = "Example" };
        character.Progress.Knowledge["scripts"] = new() { [101] = true, [102] = false, [103] = true };
        store.SaveAccounts([new() { Name = "@Example", Server = "EU", Characters = [character] }]);
        var catalog = new GameCatalog();
        catalog.Items[101] = new(101, "Focus Script: Example");
        catalog.Items[102] = new(102, "Affix Script: Example");
        var source = new SourceDocument("names", "uesp-item-metadata", "test", "1", DateTimeOffset.UtcNow, catalog.ToJson(), 20);
        w.Database.Replace(CatalogProjection.Project(catalog, source));
        var workspace = new AccountWorkspace(store, w.Database, new(), offlineDefault: true);
        var tools = new AccountTools(workspace);
        var response = JsonDocument.Parse(tools.Inspect(queries:
            [new() { Section = "knowledge", Character = "Example", Category = "scripts", Text = "Affix", Known = false }]));
        var row = response.RootElement.GetProperty("results")[0].GetProperty("rows")[0];
        Assert.Equal(102, row.GetProperty("itemId").GetInt64());
        Assert.False(row.GetProperty("known").GetBoolean());
        Assert.Equal("Affix Script: Example", row.GetProperty("name").GetString());
        var all = JsonDocument.Parse(tools.Inspect(queries:
            [new() { Section = "knowledge", Character = "Example", Category = "scripts" }]));
        var rows = all.RootElement.GetProperty("results")[0].GetProperty("rows");
        Assert.Equal(3, rows.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, rows[2].GetProperty("name").ValueKind);
        Assert.True(rows[2].GetProperty("known").GetBoolean());
    }
}
