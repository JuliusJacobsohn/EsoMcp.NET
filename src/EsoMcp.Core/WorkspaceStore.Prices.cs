using EsoData.Accounts;
using EsoData.Pricing;

namespace EsoMcp.Core;

public sealed partial class WorkspaceStore
{
    public void SavePrices(IReadOnlyList<PriceCatalog> catalogs)
    {
        using var c = Open(); using var transaction = c.BeginTransaction();
        using var command = c.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "DELETE FROM price_catalogs"; command.ExecuteNonQuery();
        foreach (var catalog in catalogs)
        {
            command.CommandText = "INSERT INTO price_catalogs(region,document) VALUES($region,$json)";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$region", catalog.Source.Region);
            command.Parameters.AddWithValue("$json", AccountJson.Write(catalog));
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    public IReadOnlyList<PriceCatalog> Prices()
    {
        using var c = Open(); using var command = c.CreateCommand();
        command.CommandText = "SELECT document FROM price_catalogs ORDER BY region";
        using var reader = command.ExecuteReader(); var result = new List<PriceCatalog>();
        while (reader.Read()) result.Add(AccountJson.Read<PriceCatalog>(reader.GetString(0)));
        return result;
    }
}
