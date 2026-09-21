using EsoMcp.Core;
using System.Text.Json;
using EsoData.Formats;
using EsoMcp.Import;
using EsoMcp.Server;

namespace EsoMcp.Tests;

public sealed class RefreshTests
{
    private const string Profiles = """
        CSPSSavedVariables={['EU Megaserver']={['@Example']={['$AccountWide']={charData={['123']={
          ['$lastCharacterName']='Example',unknownFutureField={[1]='numeric',['1']='text',[7]=true},profiles={
          [1]={name='Tank',lastSaved=100,werte={prog={part1='900100:0'},pass={part1='900200:2'}},
          comp1='64;0;0#900100,-,-,-,-,-#100-20#100,-,-,-;-,-,-,-;-,-,-,-#-#2#0',comp2='-#8798292069151066#-'}
        }},['456']={['$lastCharacterName']='No Profile'}}}}}}
        """;
    private const string Knowledge = """
        LibCharacterKnowledgeData={masterList={fieldSize=3,recipes='01b01c01d'},characters={EU={
          ['123']={name='Example',account='@Example',timestamp=100,recipes='e00000'},
          ['789']={name='Not Scanned',account='@Example'}
        }}}
        """;

    [Fact]
    public async Task RefreshMergesWorldAliasesAndIncludesCharactersWithoutProfiles()
    {
        using var work = new TestWorkspace();
        work.Write("CarosSkillPointSaver.lua", Profiles); work.Write("LibCharacterKnowledge.lua", Knowledge);
        var result = await work.Refresh.RefreshAsync();
        Assert.DoesNotContain(result.Sources, x => x.Status == "failed");
        Assert.Equal(3, work.Database.Characters().Rows.Count);
        var character = Assert.Single(work.Database.Characters(name: "Example").Rows);
        Assert.Equal("EU", character["server"]);
        var key = (string)character["character_key"]!;
        Assert.Single(work.Database.Records(key, "build").Rows);
        Assert.Equal(3, work.Database.Knowledge(key).Rows.Count);
        Assert.Single(work.Database.Characters(name: "No Profile").Rows);
        Assert.Equal(2, work.Database.Knowledge(key, known: true).Rows.Count);
        var raw = (string)work.Scalar("SELECT raw_json FROM sources WHERE label='builds'")!;
        Assert.Contains("unknownFutureField", raw);
        Assert.Contains("\"numericKey\":true", raw);
        Assert.Contains("\"numericKey\":false", raw);
    }

    [Fact]
    public async Task UnchangedMissingAndMalformedInputsKeepLastGoodDataAndQueriesWorkOffline()
    {
        using var work = new TestWorkspace();
        work.Write("CarosSkillPointSaver.lua", Profiles);
        await work.Refresh.RefreshAsync();
        Assert.Contains((await work.Refresh.RefreshAsync()).Sources, x => x.Source == "builds" && x.Status == "unchanged");
        work.Write("CarosSkillPointSaver.lua", "CSPSSavedVariables = {");
        Assert.Contains((await work.Refresh.RefreshAsync()).Sources, x => x.Source == "builds" && x.Status == "failed");
        Assert.Equal(2, work.Database.Characters().Rows.Count);
        File.Delete(Path.Combine(work.Folder, "CarosSkillPointSaver.lua"));
        Assert.Contains((await work.Refresh.RefreshAsync()).Sources, x => x.Source == "builds" && x.Status == "missing");
        var tools = new DatabaseTools(work.Database, work.Refresh);
        Assert.Contains("Example", tools.Characters());
        var build = Assert.Single(work.Database.Records(kind: "build").Rows);
        var export = new ExportTools(work.Database, new GameExports()).Build((string)build["record_key"]!);
        Assert.Contains("900200:2", export); // purchased passive allocation survives export
    }

    [Fact]
    public async Task ObservedStatePreservesSkillsCpAndLargeIdsButDoesNotExposeCredentials()
    {
        using var work = new TestWorkspace();
        work.Write("uespLog.lua", """
            uespLogSavedVars={Default={['@Example']={charData={data={CharName='Observer',CharId='9007199254740993',
              AccountName='@Example',UniqueAccountName='ServerPC@Example',WorldName='NA Megaserver',TimeStamp=100,
              APIVersion=999999,Level=32,Skills={['1:1:1']={id=900100,rank=2,type='passive'}},
              ChampionPoints2={['Star']={skillId=100,points=20}},password='SECRET',futureStat=123
            }}}}}
            """);
        await work.Refresh.RefreshAsync();
        Assert.Equal("NA", Assert.Single(work.Database.Characters().Rows)["server"]);
        var record = Assert.Single(work.Database.Records(kind: "character_state").Rows);
        var json = work.Database.Record((string)record["record_key"]!).GetRawText();
        Assert.Contains("futureStat", json); Assert.Contains("isPassive\":true", json);
        Assert.Contains("skillId\":100", json); Assert.DoesNotContain("SECRET", json);
        Assert.Contains("SECRET", (string)work.Scalar("SELECT raw_json FROM sources")!);
    }

    [Fact]
    public async Task CatalogRefreshReplacesDefinitionsAndQueriesNeedNoCatalogFile()
    {
        using var work = new TestWorkspace();
        var path = Path.Combine(work.Folder, "catalog.json");
        work.Write("catalog.json", """{"sets":{"10":{"id":10,"names":{"en":"Example Set"},"itemIds":[42]}},"items":{"42":{"id":42,"setId":10,"trait":6}},"skills":{"7":{"id":7,"name":"Skill"}}}""");
        var refresh = new RefreshService(work.Database, new() { CatalogPaths = [path] });
        await refresh.RefreshAsync();
        Assert.Single(work.Database.Sets(text: "Example").Rows);
        Assert.Single(work.Database.ItemDefinitions(setId: 10, trait: 6).Rows);
        Assert.Single(work.Database.Skills(text: "Skill").Rows);
        File.Delete(path);
        Assert.Single(work.Database.Sets().Rows);
        work.Write("catalog.json", "{}");
        await refresh.RefreshAsync();
        Assert.Empty(work.Database.Sets().Rows);
    }

    [Fact]
    public void CraftingExportKeepsGlyphsDistinctForEachResolvedItem()
    {
        var result = new GameExports().CraftingImport([
            new CraftingImportItem(900001, 68343),
            new CraftingImportItem(900002, 45870, EnchantmentQuality: 3, StyleId: 4)
        ], level: 50, quality: 4, championPoints: 160);

        Assert.Contains("item:900001:369:50:68343:369:50:", result);
        Assert.Contains("item:900002:369:50:45870:368:50:", result);
        Assert.Contains(":4:1:0:0:10000:0", result);
    }
    [Fact]
    public void CraftingExportEncodesPurpleLevel32AndRejectsUncraftableLevel()
    {
        var tools = new ExportTools(null!, new GameExports());
        using var result = JsonDocument.Parse(tools.Crafting([900001], 32, 4));
        Assert.Contains("item:900001:23:32:", result.RootElement.GetProperty("text").GetString());
        Assert.Throws<ModelContextProtocol.McpException>(() => tools.Crafting([900001], 33, 4));
    }

    [Fact]
    public void CspsEquipmentExportUsesNativeSlotOrderAndLeavesOtherSectionsEmpty()
    {
        var result = new GameExports().CspsEquipmentImport([
            new(0, 642, 2, 11, 4, 146),
            new(3, 642, 1, 11, 4, 146),
            new(4, 642, 3, 8, 4, 16),
            new(5, 642, 14, 11, 4, 146),
            new(20, 642, 13, 4, 4, 7)
        ]);

        var build = CspsBuild.Parse(result);
        Assert.Null(build.Skills);
        Assert.Equal(new CspsGearSlot(642, 2, 11, 4, 146), build.Gear![0]);
        Assert.Equal(new CspsGearSlot(642, 1, 11, 4, 146), build.Gear[1]);
        Assert.Equal(new CspsGearSlot(642, 3, 8, 4, 16), build.Gear[10]);
        Assert.Equal(new CspsGearSlot(642, 14, 11, 4, 146), build.Gear[11]);
        Assert.Equal(new CspsGearSlot(642, 13, 4, 4, 7), build.Gear[12]);
        Assert.Equal("-#-#-#-#-#642:2:11:4:146;642:1:11:4:146;0;0;0;0;0;0;0;0;642:3:8:4:16;642:14:11:4:146;642:13:4:4:7;0;0;0#-#-#-", result);
    }
}
