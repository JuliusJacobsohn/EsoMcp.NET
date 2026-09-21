using System.Text.Json;
using EsoMcp.Core;
using EsoMcp.Import;
using EsoMcp.Server;

namespace EsoMcp.Tests;

public sealed class CharacterStateTests
{
    [Fact]
    public async Task StructuredSectionsRemainQueryableAfterSourceDisappears()
    {
        using var work = new TestWorkspace();
        work.Write("uespLog.lua", """
            uespLogSavedVars={CharName='Crafter',CharId='123',AccountName='@Example',WorldName='EU',TimeStamp=200,
              Skills={['Craft:Clothing']=50},Research={Timestamp=100,['Clothier:Trait:Known']=126,
              ['Clothier:Trait:Total']=126,['Clothier:Open']=3},
              ChampionPoints2={Slots={[1]=66,[2]=0},['Total:Unspent']=9,['Craft:Star']={skillId=66,id=900123,points=50,slot=1}},
              Stats={Health=40000,['Bar1:Health']=39000},password='SECRET',futureField='UNMODELED'}
            """);
        await work.Refresh.RefreshAsync();
        var key = (string)Assert.Single(work.Database.Characters().Rows)["character_key"]!;
        File.Delete(Path.Combine(work.Folder, "uespLog.lua"));
        await work.Refresh.RefreshAsync();
        var tools = new DatabaseTools(work.Database, work.Refresh);
        using var research = JsonDocument.Parse(tools.CharacterState(key, "research"));
        Assert.True(research.RootElement.GetProperty("available").GetBoolean());
        Assert.Equal("missing", research.RootElement.GetProperty("observation").GetProperty("refresh_status").GetString());
        Assert.Equal(126, research.RootElement.GetProperty("data").GetProperty("crafts")[0].GetProperty("knownTraits").GetInt32());
        Assert.Contains("1970-01-01T00:01:40", research.RootElement.GetProperty("data").GetProperty("observedAt").GetString());
        using var cp = JsonDocument.Parse(tools.CharacterState(key, "champion"));
        Assert.Equal(0, cp.RootElement.GetProperty("data").GetProperty("slots").GetProperty("2").GetInt32());
        using var stats = JsonDocument.Parse(tools.CharacterState(key, "statistics"));
        Assert.Equal(39000, stats.RootElement.GetProperty("data").GetProperty("bars").GetProperty("1").GetProperty("values").GetProperty("Health").GetInt32());
        var all = tools.CharacterState(key, "all");
        Assert.Contains("skillLineRanks", all); Assert.DoesNotContain("SECRET", all); Assert.DoesNotContain("UNMODELED", all);
        Assert.False(work.Database.CharacterState(key, "equipment").Available);
        Assert.Throws<ModelContextProtocol.McpException>(() => tools.CharacterState(key, "invalid"));
    }

    [Fact]
    public void LatestObservationDoesNotMixCharactersOrFillMissingSectionsFromOlderState()
    {
        using var work = new TestWorkspace();
        var first = work.Batch("old", 100);
        first.Records.Add(new("character_state", "1", "character", "EU", "@A", "Old", DateTimeOffset.FromUnixTimeSeconds(100),
            """{"level":50,"research":{"crafts":[]}}"""));
        var latest = work.Batch("new", 50);
        latest.Records.Add(new("character_state", "2", "character", "EU", "@A", "Renamed", DateTimeOffset.FromUnixTimeSeconds(200),
            """{"level":50,"research":null} """));
        latest.Records.Add(new("character_state", "3", "other", "NA", "@B", "Renamed", DateTimeOffset.FromUnixTimeSeconds(300),
            """{"level":1,"research":{"crafts":[]}}"""));
        work.Database.Replace(first); work.Database.Replace(latest);
        var result = work.Database.CharacterState("character", "research");
        Assert.False(result.Available);
        Assert.Equal("Renamed", result.Observation!["name"]);
        Assert.Equal("@A", result.Observation["account"]);
        Assert.Equal("new", result.Observation["source_key"]);
        Assert.True(work.Database.CharacterState("character").Available);
        Assert.False(work.Database.CharacterState("unknown").Available);
    }

    [Fact]
    public async Task OldImportFingerprintAndChangedWorldMappingReprojectUnchangedSaves()
    {
        using var work = new TestWorkspace();
        work.Write("uespLog.lua", "uespLogSavedVars={CharName='Example',CharId='123',AccountName='@A'}");
        await work.Refresh.RefreshAsync();
        work.Scalar("UPDATE sources SET hash='legacy-content-only-hash'");
        Assert.Contains((await work.Refresh.RefreshAsync()).Sources, x => x.Source == "character-observations" && x.Status == "imported");
        Assert.Contains((await work.Refresh.RefreshAsync()).Sources, x => x.Source == "character-observations" && x.Status == "unchanged");
        var mapped = new RefreshService(work.Database, new() { Locations = [new(work.Folder, DefaultServer: "EU")] });
        Assert.Contains((await mapped.RefreshAsync()).Sources, x => x.Source == "character-observations" && x.Status == "imported");
        Assert.Equal("EU", Assert.Single(work.Database.Characters().Rows)["server"]);
    }
}
