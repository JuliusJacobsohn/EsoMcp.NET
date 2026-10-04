using EsoData.Accounts;
using EsoData.Addons;

namespace EsoMcp.Core;

public sealed partial class WorkspaceStore
{
    public void SaveCombatReports(IReadOnlyList<CombatMetricsReport> reports)
    {
        using var connection = Open(); using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "DELETE FROM combat_reports"; command.ExecuteNonQuery();
        foreach (var report in reports)
        {
            command.CommandText = "INSERT OR REPLACE INTO combat_reports VALUES($key,$json)";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$key", report.Source.Path ?? report.Source.Addon);
            command.Parameters.AddWithValue("$json", AccountJson.Write(report));
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    public IReadOnlyList<CombatMetricsReport> CombatReports()
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT document FROM combat_reports ORDER BY source_key";
        using var reader = command.ExecuteReader(); var reports = new List<CombatMetricsReport>();
        while (reader.Read()) reports.Add(AccountJson.Read<CombatMetricsReport>(reader.GetString(0)));
        return reports;
    }
}
