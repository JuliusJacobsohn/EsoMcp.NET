using System.ComponentModel;
using EsoData.Catalogs;
using EsoMcp.Core;
using EsoMcp.Import;
using ModelContextProtocol.Server;

namespace EsoMcp.Server;

[McpServerToolType]
public sealed class CatalogTools(Database database, AccountWorkspace workspace, ImportOptions options,
    ISkillCatalog skills, ICraftingCatalog items)
{
    [McpServerTool(Name = "refresh_catalog", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Explicitly download selected UESP skill definitions or set item metadata. With no selectors, refresh names for Grimoire/script item IDs present in local knowledge coverage. Only game definition IDs are sent, never account names or learned flags. Supply abilityIds and/or setIds; each at most 100. Ordinary account/build tools read local data only.")]
    public async Task<string> Refresh(long[]? abilityIds = null, long[]? setIds = null, CancellationToken cancellationToken = default)
    {
        var read = workspace.Read();
        foreach (var location in options.Locations)
        {
            if (location.AddonsPath is not string addons || !Directory.Exists(Path.Combine(addons, "LibSets"))) continue;
            var folder = Path.Combine(addons, "LibSets"); var catalog = LibSetsCatalog.Read(folder);
            var source = new SourceDocument(Identity.Key(folder, "set-catalog"), "set-catalog", folder, "explicit-refresh",
                DateTimeOffset.UtcNow, "{}", 10);
            database.Replace(CatalogProjection.Project(catalog, source), cancellationToken);
        }
        var results = new List<RefreshEntry>();
        if (abilityIds is null && setIds is null)
        {
            var knowledgeIds = read.Data.Accounts.SelectMany(a => a.Characters)
                .SelectMany(c => c.Progress.Knowledge)
                .Where(k => k.Key is "grimoires" or "scripts")
                .SelectMany(k => k.Value.Keys).Distinct().ToArray();
            results.AddRange(await items.RefreshDefinitionsAsync(knowledgeIds, cancellationToken));
        }
        if (abilityIds is not null) results.AddRange(await skills.RefreshAsync(abilityIds, cancellationToken));
        if (setIds is { Length: > 0 }) results.AddRange(await items.RefreshItemMetadataAsync(setIds, cancellationToken));
        return DataJson.Write(new { Results = results });
    }
}
