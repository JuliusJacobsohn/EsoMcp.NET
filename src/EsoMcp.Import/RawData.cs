using EsoData.Lua;

namespace EsoMcp.Import;

internal static class RawData
{
    // An entry list preserves Lua's distinct numeric/string keys, holes and 64-bit integers.
    public static object? Preserve(object? value) => value is LuaTable table
        ? table.Select(x => new { Key = x.Key.Value, NumericKey = x.Key.IsNumeric, Value = Preserve(x.Value) }).ToArray()
        : value;

    // Human-readable record details omit credential fields; the complete source stays private in SQLite.
    public static object? Details(object? value) => value is LuaTable table
        ? table.Where(x => !Secret(x.Key.Value)).Select(x => new
        { Key = x.Key.Value, NumericKey = x.Key.IsNumeric, Value = Details(x.Value) }).ToArray()
        : value;
    private static bool Secret(string key) => key.Contains("password", StringComparison.OrdinalIgnoreCase)
        || key.Contains("apikey", StringComparison.OrdinalIgnoreCase) || key.Contains("token", StringComparison.OrdinalIgnoreCase);
}
