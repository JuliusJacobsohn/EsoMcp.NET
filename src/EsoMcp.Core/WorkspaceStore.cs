using EsoData.Accounts;
using EsoData.Builds;
using Microsoft.Data.Sqlite;

namespace EsoMcp.Core;

/// <summary>Observed account documents and authored plans have separate lifecycles in the same SQLite file.</summary>
public sealed class WorkspaceStore
{
    private readonly string connectionString;
    public WorkspaceStore(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = full, DefaultTimeout = 30 }.ToString();
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS account_documents(account_key TEXT PRIMARY KEY, refreshed_at TEXT NOT NULL, document TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS build_plans(plan_id TEXT PRIMARY KEY, account_key TEXT NOT NULL, character_id TEXT NOT NULL,
                name TEXT NOT NULL, revision INTEGER NOT NULL, document TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS plans_character ON build_plans(account_key,character_id);
            """;
        command.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var c = new SqliteConnection(connectionString); c.Open(); return c; }
    public void SaveAccounts(IReadOnlyList<EsoAccount> accounts)
    {
        using var c = Open(); using var transaction = c.BeginTransaction();
        using (var clear = c.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM account_documents";
            clear.ExecuteNonQuery();
        }
        using var command = c.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO account_documents VALUES($key,$time,$json) ON CONFLICT(account_key) DO UPDATE SET refreshed_at=excluded.refreshed_at,document=excluded.document";
        var key = command.Parameters.Add("$key", SqliteType.Text); var time = command.Parameters.Add("$time", SqliteType.Text);
        var json = command.Parameters.Add("$json", SqliteType.Text);
        time.Value = DateTimeOffset.UtcNow.ToString("O");
        foreach (var account in accounts) { key.Value = account.Key; json.Value = AccountJson.Write(account); command.ExecuteNonQuery(); }
        transaction.Commit();
    }
    public IReadOnlyList<EsoAccount> Accounts()
    {
        using var c = Open(); using var command = c.CreateCommand(); command.CommandText = "SELECT document FROM account_documents ORDER BY account_key";
        using var reader = command.ExecuteReader(); var result = new List<EsoAccount>();
        while (reader.Read()) result.Add(AccountJson.Read<EsoAccount>(reader.GetString(0))); return result;
    }
    public BuildPlan Plan(string id)
    {
        using var c = Open(); using var command = c.CreateCommand(); command.CommandText = "SELECT document FROM build_plans WHERE plan_id=$id";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() is string json ? AccountJson.Read<BuildPlan>(json) : throw new KeyNotFoundException($"Build plan {id} was not found.");
    }
    public IReadOnlyList<object> Plans(string? accountKey = null)
    {
        using var c = Open(); using var command = c.CreateCommand();
        command.CommandText = "SELECT plan_id,name,account_key,character_id,revision FROM build_plans WHERE $account IS NULL OR account_key=$account ORDER BY name,plan_id";
        command.Parameters.AddWithValue("$account", accountKey ?? (object)DBNull.Value);
        using var reader = command.ExecuteReader(); var results = new List<object>();
        while (reader.Read()) results.Add(new { Id = reader.GetString(0), Name = reader.GetString(1), Account = reader.GetString(2), Character = reader.GetString(3), Revision = reader.GetInt32(4) });
        return results;
    }
    public BuildPlan SavePlan(BuildPlan plan, int expectedRevision)
    {
        var updated = AccountJson.Clone(plan); updated.Revision = checked(expectedRevision + 1);
        using var c = Open(); using var command = c.CreateCommand();
        command.CommandText = expectedRevision == 0
            ? "INSERT INTO build_plans VALUES($id,$account,$character,$name,$revision,$json) ON CONFLICT(plan_id) DO NOTHING"
            : "UPDATE build_plans SET account_key=$account,character_id=$character,name=$name,revision=$revision,document=$json WHERE plan_id=$id AND revision=$expected";
        command.Parameters.AddWithValue("$id", updated.Id); command.Parameters.AddWithValue("$account", updated.AccountKey);
        command.Parameters.AddWithValue("$character", updated.CharacterId); command.Parameters.AddWithValue("$name", updated.Name);
        command.Parameters.AddWithValue("$revision", updated.Revision); command.Parameters.AddWithValue("$json", AccountJson.Write(updated));
        if (expectedRevision != 0) command.Parameters.AddWithValue("$expected", expectedRevision);
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Build revision changed. Read the current plan before editing again.");
        return updated;
    }
    public void DeletePlan(string id, int expectedRevision)
    {
        using var c = Open(); using var command = c.CreateCommand();
        command.CommandText = "DELETE FROM build_plans WHERE plan_id=$id AND revision=$revision";
        command.Parameters.AddWithValue("$id", id); command.Parameters.AddWithValue("$revision", expectedRevision);
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Plan missing or revision changed.");
    }
}
