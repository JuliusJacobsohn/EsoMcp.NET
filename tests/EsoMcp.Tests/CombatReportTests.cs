using System.Text.Json;
using EsoMcp.Core;
using EsoMcp.Import;
using EsoMcp.Server;

namespace EsoMcp.Tests;

public sealed class CombatReportTests
{
    [Fact]
    public void RefreshReadsSavedSummariesAndOfflineRetainsThemWithoutAssigningAccountOwnership()
    {
        using var w = new TestWorkspace(); var store = new WorkspaceStore(w.Database.Path);
        w.Write("uespLog.lua", "uespLogSavedVars={CharName='Example',CharId='1',AccountName='@Example',Server='EU'}");
        var workspace = new AccountWorkspace(store, w.Database, new() { Locations = [new(w.Folder)] });
        var tools = new AccountTools(workspace);
        AccountQuery[] query = [new() { Section = "combatReports", Character = "Example", Fields = ["damagePerSecond"] }];
        void Save(int damage) => w.Write("CombatMetricsFightData.lua", $$$$"""
            CombatMetricsFightDataSV={version=22,[1]={charData={name='Example'},date=100,calculated={DPSOut={{{{damage}}}}}},
              [2]={charData={name='Other'},date=200,calculated={DPSOut=999}}}
            """);
        Save(100); Assert.Equal(100, Result(tools.Inspect(queries: query)).GetProperty("rows")[0].GetProperty("damagePerSecond").GetDouble());
        Save(200); var result = Result(tools.Inspect(queries: query));
        Assert.Equal(1, result.GetProperty("total").GetInt32());
        Assert.Contains("installation-wide", result.GetProperty("scope").GetString());
        Assert.Equal(200, result.GetProperty("rows")[0].GetProperty("damagePerSecond").GetDouble());
        Save(300);
        Assert.Equal(200, Result(tools.Inspect(queries: query, offline: true)).GetProperty("rows")[0].GetProperty("damagePerSecond").GetDouble());
        Assert.Equal(2, Assert.Single(new WorkspaceStore(w.Database.Path).CombatReports()).Fights.Count);
    }

    [Fact]
    public void NoSourceAndEmptySavedHistoryHaveDifferentAvailability()
    {
        using var w = new TestWorkspace();
        w.Write("uespLog.lua", "uespLogSavedVars={CharName='Example',CharId='1',AccountName='@Example',Server='EU'}");
        var tools = new AccountTools(new(new WorkspaceStore(w.Database.Path), w.Database, new() { Locations = [new(w.Folder)] }));
        AccountQuery[] query = [new() { Section = "combatReports" }];
        Assert.False(Result(tools.Inspect(queries: query)).GetProperty("available").GetBoolean());
        w.Write("CombatMetricsFightData.lua", "CombatMetricsFightDataSV={version=22}");
        var result = Result(tools.Inspect(queries: query));
        Assert.True(result.GetProperty("available").GetBoolean()); Assert.Equal(0, result.GetProperty("total").GetInt32());
    }

    [Fact]
    public void CharacterChecksExposeAttributesBarsStatisticsAndEffects()
    {
        using var w = new TestWorkspace();
        w.Write("uespLog.lua", """
            uespLogSavedVars={CharName='Example',CharId='1',AccountName='@Example',Server='EU',
              AttributesHealth=0,AttributesMagicka=64,Stats={Health=24000},Buffs={{id=123,name='Boon'}},ActionBar={[3]=999}}
            """);
        var tools = new AccountTools(new(new WorkspaceStore(w.Database.Path), w.Database, new() { Locations = [new(w.Folder)] }));
        var result = JsonDocument.Parse(tools.Inspect(queries: new[] { "attributes", "bars", "statistics", "effects" }
            .Select(section => new AccountQuery { Section = section, Character = "Example" }).ToArray())).RootElement.GetProperty("results");
        Assert.All(result.EnumerateArray(), row => Assert.True(row.GetProperty("available").GetBoolean()));
        Assert.Equal(64, result[0].GetProperty("rows")[0].GetProperty("magicka").GetInt32());
        Assert.Equal("Boon", result[3].GetProperty("rows")[0].GetProperty("active")[0].GetProperty("name").GetString());
    }

    private static JsonElement Result(string text) => JsonDocument.Parse(text).RootElement.GetProperty("results")[0];
}
