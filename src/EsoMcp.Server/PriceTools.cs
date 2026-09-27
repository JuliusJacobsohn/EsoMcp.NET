using System.ComponentModel;
using EsoMcp.Import;
using ModelContextProtocol.Server;

namespace EsoMcp.Server;

[McpServerToolType]
public sealed class PriceTools(AccountWorkspace workspace)
{
    [ModelContextProtocol.Server.McpServerTool(Name = "query_prices", ReadOnly = true, OpenWorld = false)]
    [Description("Search the complete local TTC market catalog, including items not owned by any account. No website/network requests. region=EU/NA. ttcItemIds are TTC IDs, NOT ESO IDs. quality uses ESO quality 1..5; marketLevel is required level or 50+CP (210 for CP160). ttcTrait and extra filter TTC variant keys (armor category, potion effects, writ requirements). Returns listing/sale statistics and snapshot timestamp. Unknown is not zero; prices do not establish tradability or live availability. offline=true reads the persisted catalog. Bounded pagination, limit 1..100.")]
    public string Query(string region = "EU", string? text = null, long[]? ttcItemIds = null, int? quality = null,
        int? marketLevel = null, int? ttcTrait = null, string[]? extra = null, int limit = 20, int offset = 0, bool offline = false) => ToolResult.Json(() =>
    {
        if (limit is < 1 or > 100 || offset < 0) throw new ArgumentException("Use limit 1..100 and offset >= 0.");
        if (quality is < 1 or > 5) throw new ArgumentException("quality must be 1..5.");
        region = region.ToUpperInvariant();
        if (region is not ("EU" or "NA")) throw new ArgumentException("region must be EU or NA.");
        var market = workspace.ReadPrices(offline).SingleOrDefault(p => p.Source.Region == region);
        if (market is null) return (object)new { Available = false, Region = region, Reason = "No local TTC price catalog for this region." };
        var names = market.Items.ToLookup(i => i.TtcItemId);
        var rows = market.Entries.Where(e => (ttcItemIds is null || ttcItemIds.Contains(e.Key.TtcItemId))
            && (text is null || names[e.Key.TtcItemId].Any(i => i.Name.Contains(text, StringComparison.OrdinalIgnoreCase)))
            && (quality is null || e.Key.Quality == quality - 1) && (marketLevel is null || e.Key.Level == marketLevel)
            && (ttcTrait is null || e.Key.Trait == ttcTrait) && (extra is null || e.Key.Extra.SequenceEqual(extra))).ToArray();
        return new { Available = true, market.Source, CatalogItems = market.Items.Select(i => i.TtcItemId).Distinct().Count(),
            CatalogVariants = market.Entries.Count, Total = rows.Length, Offset = offset, HasMore = offset + limit < rows.Length,
            Rows = rows.Skip(offset).Take(limit).Select(e => new { Names = names[e.Key.TtcItemId].Select(i => i.Name).Distinct(), e.Key, e.Statistics }) };
    });
}
