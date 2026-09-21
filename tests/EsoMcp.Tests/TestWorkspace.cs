using EsoMcp.Core;
using EsoMcp.Import;
using Microsoft.Data.Sqlite;

namespace EsoMcp.Tests;

public sealed class TestWorkspace : IDisposable
{
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "EsoMcp.Tests", Guid.NewGuid().ToString("N"));
    public Database Database { get; }
    public RefreshService Refresh { get; }
    public TestWorkspace()
    {
        Directory.CreateDirectory(Folder);
        Database = new(Path.Combine(Folder, "test.db"));
        Refresh = new(Database, new() { Locations = [new(Folder)] });
    }
    public void Write(string name, string contents) => File.WriteAllText(Path.Combine(Folder, name), contents);
    public object? Scalar(string sql)
    {
        using var connection = new SqliteConnection($"Data Source={Database.Path}");
        connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = sql;
        return command.ExecuteScalar();
    }
    public ImportBatch Batch(string source = "first", int priority = 100) => new(new(source, source, "synthetic", "hash",
        DateTimeOffset.UtcNow, "{}", priority));
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(Folder, recursive: true);
    }
}
