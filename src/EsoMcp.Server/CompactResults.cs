using System.Text.Json;
using EsoMcp.Core;

namespace EsoMcp.Server;

internal static class CompactResults
{
    public static object Page(Page page, bool includeDetails, params string[] fields)
    {
        if (includeDetails) return page;
        return new
        {
            page.Offset,
            page.Limit,
            page.HasMore,
            Rows = page.Rows.Select(row => Pick(row, fields)).ToArray()
        };
    }

    public static Dictionary<string, object?> Pick(IReadOnlyDictionary<string, object?> row, params string[] fields)
    {
        var result = new Dictionary<string, object?>();
        foreach (var field in fields)
            if (row.TryGetValue(field, out var value) && value is not null)
                result[field] = value;
        return result;
    }

    public static object ItemDefinitions(Page page, bool includeDetails)
    {
        if (includeDetails) return page;
        var rows = page.Rows.Select(row =>
        {
            var item = Pick(row, "item_id", "set_id", "name", "equip_type", "trait");
            if (row.TryGetValue("data_json", out var raw) && raw is JsonElement data && data.ValueKind == JsonValueKind.Object)
            {
                foreach (var (jsonName, outputName) in new[] { ("armorType", "armor_type"), ("weaponType", "weapon_type") })
                    if (data.TryGetProperty(jsonName, out var value) && value.ValueKind != JsonValueKind.Null)
                        item[outputName] = value;
            }
            return item;
        }).ToArray();
        return new { page.Offset, page.Limit, page.HasMore, Rows = rows };
    }

    public static object Record(JsonElement record, bool includeDetails)
    {
        if (includeDetails || record.ValueKind != JsonValueKind.Object || !record.TryGetProperty("details", out _))
            return record;
        var result = record.EnumerateObject().Where(property => property.Name != "details")
            .ToDictionary(property => property.Name, property => property.Value);
        result["_omittedFields"] = JsonSerializer.SerializeToElement(new[] { "details" });
        return result;
    }

    public static object CharacterState(CharacterStateView state, bool includeDetails)
    {
        if (includeDetails || state.Observation is null) return state;
        return state with
        {
            Observation = Pick(state.Observation, "server", "account", "name", "observed_at",
                "source_label", "modified_at", "refresh_status", "refresh_message")
        };
    }

    public static object Status(object status, bool includeDetails)
    {
        if (includeDetails) return status;
        using var document = JsonDocument.Parse(DataJson.Write(status));
        var root = document.RootElement;
        var sources = root.GetProperty("sources").EnumerateArray().Select(source =>
        {
            var result = new Dictionary<string, object?>();
            foreach (var field in new[] { "label", "modified_at", "imported_at" })
                if (source.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.Null)
                    result[field] = value.Clone();
            if (source.TryGetProperty("diagnostics_json", out var diagnostics) &&
                diagnostics.ValueKind == JsonValueKind.Array && diagnostics.GetArrayLength() > 0)
                result["diagnostics_json"] = diagnostics.Clone();
            return result;
        }).ToArray();
        var refresh = root.GetProperty("lastRefresh").EnumerateArray().Select(attempt =>
        {
            var result = new Dictionary<string, object?>();
            foreach (var field in new[] { "label", "attempted_at", "status", "message" })
                if (attempt.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.Null)
                    result[field] = value.Clone();
            return result;
        }).ToArray();
        return new { Sources = sources, LastRefresh = refresh, Counts = root.GetProperty("counts").Clone() };
    }
}
