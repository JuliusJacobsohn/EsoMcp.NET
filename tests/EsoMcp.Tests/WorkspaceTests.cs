using EsoData.Accounts;
using EsoData.Builds;
using EsoMcp.Core;
using EsoMcp.Import;
using EsoMcp.Server;
using System.Text.Json;

namespace EsoMcp.Tests;

public class WorkspaceTests
{
    [Fact]
    public void GuideTargetPersistsWithoutCopyingOrExportingCurrentAllocation()
    {
        using var w = new TestWorkspace(); var store = new WorkspaceStore(w.Database.Path);
        w.Write("uespLog.lua", """
            uespLogSavedVars={data={CharName="Example",CharId="1",AccountName="@Example",Server="EU",
            SkillPointsTotal=100,SkillPointsUnused=100,AttributesHealth=64,Skills={}}}
            """);
        var workspace = new AccountWorkspace(store, w.Database, new() { Locations = [new(w.Folder)] });
        var tools = new BuildTools(workspace, store);
        var created = JsonDocument.Parse(tools.Edit("create", character: "Example", patch: new()
        {
            Target = new() { Variant = "Default", Masteries = ["Example mastery"], Attributes = new() { Health = 64 } }
        }));
        var id = created.RootElement.GetProperty("plan").GetProperty("id").GetString()!;
        Assert.Equal(BuildSections.None, store.Plan(id).Build.Sections);
        Assert.Contains("Example mastery", tools.Edit("read", id, section: "target"));
        Assert.Contains("guide-target", tools.Analyze(id));
        Assert.ThrowsAny<Exception>(() => tools.Export(id));
        Assert.ThrowsAny<Exception>(() => tools.Verify(id, 1));
        Assert.Equal(1, new WorkspaceStore(w.Database.Path).Plan(id).Revision);
    }
    [Fact]
    public void RefreshDoesNotOverwritePlansAndRestartKeepsRevisions()
    {
        using var w = new TestWorkspace(); var store = new WorkspaceStore(w.Database.Path);
        var account = new EsoAccount { Name = "@Example", Server = "EU", Characters = [new() { Id = "1", Name = "Example" }] };
        store.SaveAccounts([account]);
        var saved = store.SavePlan(new() { Id = "plan", AccountKey = account.Key, CharacterId = "1", Name = "Target" }, 0);
        account.Characters[0].Progress.TotalSkillPoints = 200; store.SaveAccounts([account]);
        var restarted = new WorkspaceStore(w.Database.Path);
        Assert.Equal("Target", restarted.Plan("plan").Name);
        Assert.Equal(200, Assert.Single(restarted.Accounts()).Characters[0].Progress.TotalSkillPoints);
        saved.Name = "Revised"; var second = restarted.SavePlan(saved, saved.Revision);
        Assert.Equal(2, second.Revision);
        Assert.Throws<InvalidOperationException>(() => store.SavePlan(saved, 1));
        Assert.Equal("Revised", store.Plan("plan").Name);
    }
    [Fact]
    public void NewSourceDataAppearsWithoutInvalidatingDraftAndReadOutputIsBounded()
    {
        using var w = new TestWorkspace(); var store = new WorkspaceStore(w.Database.Path);
        var workspace = new AccountWorkspace(store, w.Database, new() { Locations = [new(w.Folder)] });
        void Save(int points) => w.Write("uespLog.lua", $$$$"""
            uespLogSavedVars={data={CharName="Example",CharId="1",AccountName="@Example",Server="EU",
            SkillPointsTotal={{{{points}}}},SkillPointsUnused=0,Skills={}}}
            """);
        Save(100);
        var tools = new BuildTools(workspace, store);
        var created = JsonDocument.Parse(tools.Edit("create", character: "Example", name: "Target"));
        var id = created.RootElement.GetProperty("plan").GetProperty("id").GetString()!;
        Save(101);
        var account = Assert.Single(workspace.Read().Data.Accounts);
        Assert.Equal(101, account.Character("Example").Progress.TotalSkillPoints);
        Assert.Equal(1, store.Plan(id).Revision);
        var output = tools.Edit("update", id, 1, patch: new() { Attributes = new() { Magicka = 64 } });
        Assert.True(output.Length < 3000, output);
        Assert.Equal(2, store.Plan(id).Revision);
        Assert.ThrowsAny<Exception>(() => tools.Edit("update", id, 1, patch: new() { Attributes = new() { Health = 64 } }));
        Assert.Equal(64, store.Plan(id).Build.Attributes.Magicka);
    }
    [Fact]
    public void AccountQueriesRequireUnambiguousIdentity()
    {
        var data = new AccountLoadResult { Accounts = [new() { Name = "@Same", Server = "EU" }, new() { Name = "@Same", Server = "NA" }] };
        Assert.Throws<ArgumentException>(() => AccountWorkspace.Select(new(data, new()), "@Same"));
        Assert.Equal("EU", AccountWorkspace.Select(new(data, new()), "EU/@SAME").Server);
    }
}
