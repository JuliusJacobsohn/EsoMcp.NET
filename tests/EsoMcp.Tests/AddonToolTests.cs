using System.Text.Json;
using EsoMcp.Import;
using EsoMcp.Server;

namespace EsoMcp.Tests;

public sealed class AddonToolTests
{
    [Fact]
    public void ChecksConfiguredLocationsWithoutLoadingInvalidAccountData()
    {
        using var test = new TestWorkspace();
        var addons = Path.Combine(test.Folder, "AddOns");
        var saves = Path.Combine(test.Folder, "SavedVariables");
        Directory.CreateDirectory(Path.Combine(addons, "IIfA"));
        Directory.CreateDirectory(saves);
        File.WriteAllText(Path.Combine(addons, "IIfA", "IIfA.txt"), "## Version: test");
        File.WriteAllText(Path.Combine(saves, "IIfA.lua"), "not valid Lua");
        var tool = new AddonTools(new ImportOptions { Locations = [new(saves, addons), new(Path.Combine(test.Folder, "other"))] });
        using var json = JsonDocument.Parse(tool.Inspect());
        Assert.Equal("configuration", json.RootElement.GetProperty("source").GetString());
        var reports = json.RootElement.GetProperty("locations");
        Assert.Equal(2, reports.GetArrayLength());
        var inventory = reports[0].GetProperty("addons").EnumerateArray().Single(a => a.GetProperty("id").GetString() == "IIfA");
        Assert.True(inventory.GetProperty("installed").GetBoolean());
        Assert.True(inventory.GetProperty("savedFiles")[0].GetProperty("exists").GetBoolean());
        Assert.False(reports[1].GetProperty("savedVariablesDirectoryFound").GetBoolean());
    }

    [Fact]
    public void ExplicitPathsOverrideConfigurationAndInferSiblingFolders()
    {
        using var test = new TestWorkspace();
        var saves = Path.Combine(test.Folder, "SavedVariables");
        var addons = Path.Combine(test.Folder, "AddOns");
        var tool = new AddonTools(new ImportOptions { Locations = [new("ignored")] });
        using var json = JsonDocument.Parse(tool.Inspect(savedVariablesPath: saves));
        Assert.Equal("arguments", json.RootElement.GetProperty("source").GetString());
        var report = Assert.Single(json.RootElement.GetProperty("locations").EnumerateArray());
        Assert.Equal(addons, report.GetProperty("addonsPath").GetString());
        Assert.False(report.GetProperty("addonsDirectoryFound").GetBoolean());
    }
}
