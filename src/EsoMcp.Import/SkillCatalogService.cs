using EsoData.Catalogs;
using EsoMcp.Core;

namespace EsoMcp.Import;

/// <summary>Explicitly imports current UESP skill definitions and skill-line IDs into SQLite.</summary>
public sealed class SkillCatalogService(Database database) : ISkillCatalog
{
    private const string Endpoint = "https://esolog.uesp.net/exportJson.php";
    private static readonly HttpClient Client = new();

    public async Task<IReadOnlyList<RefreshEntry>> RefreshAsync(IReadOnlyList<long> abilityIds,
        CancellationToken cancellationToken = default)
    {
        if (abilityIds.Count > 100 || abilityIds.Any(id => id <= 0))
            throw new ArgumentException("Provide 0..100 positive ability IDs.", nameof(abilityIds));
        var ids = abilityIds.Distinct().Order().ToArray();
        var catalogs = new List<GameCatalog>();
        var linesUrl = $"{Endpoint}?table=minedSkillLines";
        catalogs.Add(UespCatalog.Parse(await Client.GetStringAsync(linesUrl, cancellationToken), linesUrl));
        foreach (var chunk in ids.Chunk(20))
        {
            var url = $"{Endpoint}?table=minedSkills&ids={string.Join(',', chunk)}";
            catalogs.Add(UespCatalog.Parse(await Client.GetStringAsync(url, cancellationToken), url));
        }
        var catalog = GameCatalog.Merge(catalogs);
        var raw = catalog.ToJson();
        var source = new SourceDocument(Identity.Key("uesp-skill-metadata", string.Join(',', ids)),
            "uesp-skill-metadata", linesUrl, Identity.Key(raw), DateTimeOffset.UtcNow, raw, Priority: 20);
        database.Replace(CatalogProjection.Project(catalog, source), cancellationToken);
        database.RecordAttempt(source.Key, source.Label, source.Path, "imported",
            $"{catalog.SkillLines.Count} skill lines, {catalog.Skills.Count} abilities.");
        return [new(source.Label, "imported",
            $"{catalog.SkillLines.Count} skill lines, {catalog.Skills.Count} abilities.")];
    }
}
