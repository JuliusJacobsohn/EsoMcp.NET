using EsoMcp.Core;
using Microsoft.Data.Sqlite;

namespace EsoMcp.Tests;

public sealed class DatabaseTests
{
    [Fact]
    public void ReplacementRemovesOldRowsButNotOtherSources()
    {
        using var work = new TestWorkspace();
        var first = work.Batch(); first.Characters.Add(new(new("EU", "@A", "9007199254740993", "Old")));
        var other = work.Batch("other"); other.Characters.Add(new(new("EU", "@B", "9007199254740993", "Other")));
        work.Database.Replace(first); work.Database.Replace(other);
        Assert.Equal(2, work.Database.Characters().Rows.Count);
        first.Characters.Clear(); first.Characters.Add(new(new("EU", "@A", "9007199254740993", "New")));
        work.Database.Replace(first);
        Assert.Equal(2, work.Database.Characters().Rows.Count);
        Assert.Empty(work.Database.Characters(name: "Old").Rows);
        Assert.Equal("9007199254740993", Assert.Single(work.Database.Characters(name: "New").Rows)["game_id"]);
    }

    [Fact]
    public void FailedReplacementRollsBackWholeSource()
    {
        using var work = new TestWorkspace();
        var batch = work.Batch(); batch.Characters.Add(new(new("EU", "@A", "1", "Kept")));
        work.Database.Replace(batch);
        batch.Characters.Clear();
        batch.Sets.Add(new(10, "Bad", "{}")); batch.Sets.Add(new(10, "Duplicate", "{}"));
        Assert.Throws<SqliteException>(() => work.Database.Replace(batch));
        Assert.Equal("Kept", Assert.Single(work.Database.Characters().Rows)["name"]);
        Assert.Empty(work.Database.Sets().Rows);
    }

    [Fact]
    public void InventoryDefaultDoesNotDoubleCountAlternateSourcesOrMixAccounts()
    {
        using var work = new TestWorkspace();
        var first = work.Batch(); first.Inventory.Add(new("EU", "@A", null, "Bank", 1, 3, "link"));
        var other = work.Batch("other", 50);
        other.Inventory.Add(new("EU", "@a", null, "Bank", 1, 3, "link"));
        other.Inventory.Add(new("EU", "@B", null, "Bank", 1, 7, "link"));
        work.Database.Replace(first); work.Database.Replace(other);
        Assert.Equal(2, work.Database.Inventory().Rows.Count);
        Assert.Equal(3, work.Database.Inventory(includeAlternateSources: true).Rows.Count);
        Assert.Equal(3L, Assert.Single(work.Database.Inventory(account: "@A").Rows)["count"]);
    }

    [Fact]
    public void NamesSurviveUnnamedObservationsAndPagesAreBounded()
    {
        using var work = new TestWorkspace();
        var batch = work.Batch();
        batch.Characters.Add(new(new("EU", "@A", "1", "Éowyn")));
        batch.Characters.Add(new(new("EU", "@A", "1"), DateTimeOffset.UtcNow));
        batch.Characters.Add(new(new("EU", "@A", "2", "Second")));
        work.Database.Replace(batch);
        Assert.Equal("Éowyn", Assert.Single(work.Database.Characters(name: "owyn").Rows)["name"]);
        Assert.True(work.Database.Characters(limit: 1).HasMore);
        Assert.False(work.Database.Characters(offset: 1, limit: 1).HasMore);
        Assert.Throws<ArgumentOutOfRangeException>(() => work.Database.Characters(limit: 201));
        Assert.Empty(work.Database.Characters(name: "' OR 1=1 --").Rows);
    }
}
