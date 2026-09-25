using EsoData.Catalogs;

namespace EsoMcp.Core;

public sealed partial class Database
{
    /// <summary>Read existing downloaded definitions without reimporting personal account projections.</summary>
    public GameCatalog ReadCatalog()
    {
        using var c = Open(); using var command = c.CreateCommand();
        command.CommandText = "SELECT raw_json FROM sources WHERE label IN ('uesp-item-metadata','uesp-skill-metadata','external-catalog') ORDER BY priority,modified_at,source_key";
        using var reader = command.ExecuteReader(); var catalogs = new List<GameCatalog>();
        while (reader.Read()) catalogs.Add(GameCatalog.FromJson(reader.GetString(0)));
        return GameCatalog.Merge(catalogs);
    }
}
