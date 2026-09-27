using EsoData.Accounts;
using EsoData.Catalogs;
using EsoMcp.Core;
using EsoData.Pricing;

namespace EsoMcp.Import;

public sealed record WorkspaceRead(AccountLoadResult Data, GameCatalog Catalog, IReadOnlyList<PriceCatalog>? Prices = null);
public sealed class AccountWorkspace(WorkspaceStore store, Database definitions, ImportOptions options, bool offlineDefault = false)
{
    public WorkspaceRead Read(bool offline = false)
    {
        var catalogs = new List<GameCatalog>();
        foreach (var location in options.Locations)
            if (location.AddonsPath is string addons && Directory.Exists(Path.Combine(addons, "LibSets")))
                catalogs.Add(LibSetsCatalog.Read(Path.Combine(addons, "LibSets")));
        catalogs.Add(definitions.ReadCatalog());
        catalogs.AddRange(options.CatalogPaths.Select(GameCatalog.Read));
        var catalog = GameCatalog.Merge(catalogs);
        var prices = ReadPrices(offline);
        if (offline || offlineDefault)
        {
            var accounts = store.Accounts().ToList();
            foreach (var market in prices)
                foreach (var account in accounts.Where(a => AccountLoader.NormalizeServer(a.Server) == market.Source.Region)) market.Associate(account);
            return new(new() { Accounts = accounts, Diagnostics = ["Explicit offline mode: using last stored account documents."] }, catalog, prices);
        }
        if (options.Locations.Count == 0) throw new InvalidOperationException("No source locations configured. Use explicit offline mode to query stored snapshots.");
        var data = AccountLoader.Load(options.Locations.Select(l => new AccountInput(l.SavedVariablesPath, l.DefaultServer)), catalog, prices);
        store.SaveAccounts(data.Accounts);
        return new(data, catalog, prices);
    }
    public IReadOnlyList<PriceCatalog> ReadPrices(bool offline = false)
    {
        if (offline || offlineDefault) return store.Prices();
        var result = new Dictionary<string, PriceCatalog>();
        foreach (var directory in options.Locations.Where(l => l.AddonsPath is not null)
            .Select(l => Path.Combine(l.AddonsPath!, "TamrielTradeCentre")).Distinct(StringComparer.OrdinalIgnoreCase))
        foreach (var region in new[] { "EU", "NA" })
        {
            if (!File.Exists(Path.Combine(directory, $"PriceTable{region}.lua"))) continue;
            var market = TtcPriceReader.Read(directory, region, options.PriceLanguage);
            if (!result.TryGetValue(region, out var old) || market.Source.UpdatedAt > old.Source.UpdatedAt)
                result[region] = market;
        }
        var prices = result.Values.ToArray(); store.SavePrices(prices); return prices;
    }
    public static EsoAccount Select(WorkspaceRead read, string? selector)
    {
        var accounts = read.Data.Accounts.Where(a => selector is null || string.Equals(a.Key, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.Name, selector, StringComparison.OrdinalIgnoreCase)).ToArray();
        return accounts.Length == 1 ? accounts[0] : throw new ArgumentException(accounts.Length == 0
            ? "No matching account. Inspect accounts and source diagnostics." : "Multiple accounts match. Supply the account key including server.");
    }
}
