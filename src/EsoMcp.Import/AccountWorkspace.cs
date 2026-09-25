using EsoData.Accounts;
using EsoData.Catalogs;
using EsoMcp.Core;

namespace EsoMcp.Import;

public sealed record WorkspaceRead(AccountLoadResult Data, GameCatalog Catalog);
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
        if (offline || offlineDefault) return new(new() { Accounts = store.Accounts().ToList(), Diagnostics = ["Explicit offline mode: using last stored account documents."] }, catalog);
        if (options.Locations.Count == 0) throw new InvalidOperationException("No source locations configured. Use explicit offline mode to query stored snapshots.");
        var data = AccountLoader.Load(options.Locations.Select(l => new AccountInput(l.SavedVariablesPath, l.DefaultServer)), catalog);
        store.SaveAccounts(data.Accounts);
        return new(data, catalog);
    }
    public static EsoAccount Select(WorkspaceRead read, string? selector)
    {
        var accounts = read.Data.Accounts.Where(a => selector is null || string.Equals(a.Key, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.Name, selector, StringComparison.OrdinalIgnoreCase)).ToArray();
        return accounts.Length == 1 ? accounts[0] : throw new ArgumentException(accounts.Length == 0
            ? "No matching account. Inspect accounts and source diagnostics." : "Multiple accounts match. Supply the account key including server.");
    }
}
