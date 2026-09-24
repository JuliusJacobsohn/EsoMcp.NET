using System.Text.Json;
using EsoMcp.Server;

namespace EsoMcp.Tests;

public sealed class CompactResultTests
{
    [Fact]
    public void InventoryDefaultsToUsefulFieldsAndAllowsExactDetails()
    {
        using var work = new TestWorkspace();
        var batch = work.Batch();
        batch.Inventory.Add(new("EU", "@A", "character", "Bank", 123, 2, "|H1:item:123|h|h", "Item", 4, 2, 7));
        work.Database.Replace(batch);
        var tools = new DatabaseTools(work.Database, work.Refresh);

        using var compact = JsonDocument.Parse(tools.Inventory());
        var row = compact.RootElement.GetProperty("rows")[0];
        Assert.Equal(123, row.GetProperty("item_id").GetInt64());
        Assert.Equal(2, row.GetProperty("count").GetInt64());
        Assert.False(row.TryGetProperty("link", out _));
        Assert.False(row.TryGetProperty("source_key", out _));
        Assert.Equal(25, compact.RootElement.GetProperty("limit").GetInt32());

        using var detailed = JsonDocument.Parse(tools.Inventory(includeDetails: true));
        Assert.Equal("|H1:item:123|h|h", detailed.RootElement.GetProperty("rows")[0].GetProperty("link").GetString());
    }

    [Fact]
    public void RecordOmitsRawDetailsUntilExplicitlySelected()
    {
        using var work = new TestWorkspace();
        var batch = work.Batch();
        batch.Records.Add(new("character_state", "one", "character", "EU", "@A", "Name", null,
            """{"level":50,"details":{"large":"raw"}}"""));
        work.Database.Replace(batch);
        var tools = new DatabaseTools(work.Database, work.Refresh);
        var key = (string)work.Database.Records().Rows[0]["record_key"]!;

        using var compact = JsonDocument.Parse(tools.Record(key));
        Assert.Equal(50, compact.RootElement.GetProperty("level").GetInt32());
        Assert.False(compact.RootElement.TryGetProperty("details", out _));
        Assert.Equal("details", compact.RootElement.GetProperty("_omittedFields")[0].GetString());

        using var selected = JsonDocument.Parse(tools.Record(key, field: "details"));
        Assert.Equal("raw", selected.RootElement.GetProperty("large").GetString());
        using var detailed = JsonDocument.Parse(tools.Record(key, includeDetails: true));
        Assert.True(detailed.RootElement.TryGetProperty("details", out _));
    }

    [Fact]
    public void ItemDefinitionsSurfaceCraftingSelectorsWithoutFullJson()
    {
        using var work = new TestWorkspace();
        var batch = work.Batch();
        batch.Items.Add(new(123, 10, "Test item", 3, 4,
            """{"id":123,"setId":10,"equipType":3,"armorType":2,"weaponType":null,"trait":4}"""));
        work.Database.Replace(batch);
        var tools = new DatabaseTools(work.Database, work.Refresh);

        using var compact = JsonDocument.Parse(tools.Items(setId: 10));
        var item = compact.RootElement.GetProperty("rows")[0];
        Assert.Equal(2, item.GetProperty("armor_type").GetInt32());
        Assert.Equal(4, item.GetProperty("trait").GetInt32());
        Assert.False(item.TryGetProperty("data_json", out _));

        using var detailed = JsonDocument.Parse(tools.Items(setId: 10, includeDetails: true));
        Assert.Equal(2, detailed.RootElement.GetProperty("rows")[0].GetProperty("data_json").GetProperty("armorType").GetInt32());
    }
}
