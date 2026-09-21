using Microsoft.Data.Sqlite;

namespace EsoMcp.Core;

public sealed partial class Database
{
    public string Path { get; }
    public Database(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = Schema;
        command.ExecuteNonQuery();
    }
    internal SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = Path, ForeignKeys = true, DefaultTimeout = 30 }.ToString());
        connection.Open();
        return connection;
    }
    public bool HasHash(string sourceKey, string hash)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sources WHERE source_key=$key AND hash=$hash";
        command.Parameters.AddWithValue("$key", sourceKey); command.Parameters.AddWithValue("$hash", hash);
        return command.ExecuteScalar() is not null;
    }
    public void RecordAttempt(string key, string label, string path, string status, string? message = null)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO attempts(source_key,label,path,attempted_at,status,message) VALUES($key,$label,$path,$time,$status,$message)
            ON CONFLICT(source_key) DO UPDATE SET attempted_at=excluded.attempted_at,status=excluded.status,message=excluded.message
            """;
        foreach (var (name, value) in new (string, object?)[] { ("key", key), ("label", label), ("path", path),
                     ("time", DateTimeOffset.UtcNow.ToString("O")), ("status", status), ("message", message) })
            command.Parameters.AddWithValue("$" + name, value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
    private const string Schema = """
        PRAGMA journal_mode=WAL;
        CREATE TABLE IF NOT EXISTS sources(
          source_key TEXT PRIMARY KEY,label TEXT NOT NULL,path TEXT NOT NULL,hash TEXT NOT NULL,
          modified_at TEXT NOT NULL,imported_at TEXT NOT NULL,priority INTEGER NOT NULL,diagnostics_json TEXT NOT NULL,raw_json TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS attempts(
          source_key TEXT PRIMARY KEY,label TEXT NOT NULL,path TEXT NOT NULL,attempted_at TEXT NOT NULL,status TEXT NOT NULL,message TEXT);
        CREATE TABLE IF NOT EXISTS characters(
          source_key TEXT NOT NULL REFERENCES sources ON DELETE CASCADE,character_key TEXT NOT NULL,
          server TEXT NOT NULL,account TEXT NOT NULL,game_id TEXT NOT NULL,name TEXT,observed_at TEXT,details_json TEXT,
          PRIMARY KEY(source_key,character_key));
        CREATE TABLE IF NOT EXISTS inventory(
          row_id INTEGER PRIMARY KEY,source_key TEXT NOT NULL REFERENCES sources ON DELETE CASCADE,
          server TEXT NOT NULL,account TEXT NOT NULL,character_key TEXT,location TEXT NOT NULL,item_id INTEGER NOT NULL,
          count INTEGER NOT NULL,link TEXT NOT NULL,name TEXT,quality INTEGER,bag_id INTEGER,slot INTEGER,observed_at TEXT);
        CREATE INDEX IF NOT EXISTS inventory_item ON inventory(item_id);
        CREATE INDEX IF NOT EXISTS inventory_character ON inventory(character_key);
        CREATE TABLE IF NOT EXISTS knowledge(
          source_key TEXT NOT NULL REFERENCES sources ON DELETE CASCADE,character_key TEXT NOT NULL,
          category TEXT NOT NULL,entry_index INTEGER NOT NULL,item_id INTEGER NOT NULL,known INTEGER,observed_at TEXT,
          PRIMARY KEY(source_key,character_key,category,entry_index));
        CREATE INDEX IF NOT EXISTS knowledge_item ON knowledge(item_id);
        CREATE TABLE IF NOT EXISTS records(
          record_key TEXT PRIMARY KEY,source_key TEXT NOT NULL REFERENCES sources ON DELETE CASCADE,
          kind TEXT NOT NULL,local_id TEXT NOT NULL,character_key TEXT,server TEXT NOT NULL,account TEXT NOT NULL,
          name TEXT,observed_at TEXT,data_json TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS records_character ON records(character_key,kind);
        CREATE TABLE IF NOT EXISTS catalog_sets(
          source_key TEXT NOT NULL REFERENCES sources ON DELETE CASCADE,set_id INTEGER NOT NULL,name TEXT,names_json TEXT NOT NULL,
          PRIMARY KEY(source_key,set_id));
        CREATE TABLE IF NOT EXISTS catalog_items(
          source_key TEXT NOT NULL REFERENCES sources ON DELETE CASCADE,item_id INTEGER NOT NULL,set_id INTEGER,name TEXT,
          equip_type INTEGER,trait INTEGER,data_json TEXT NOT NULL,PRIMARY KEY(source_key,item_id));
        CREATE INDEX IF NOT EXISTS catalog_items_set ON catalog_items(set_id,item_id);
        CREATE TABLE IF NOT EXISTS catalog_skills(
          source_key TEXT NOT NULL REFERENCES sources ON DELETE CASCADE,skill_id INTEGER NOT NULL,name TEXT,data_json TEXT NOT NULL,
          PRIMARY KEY(source_key,skill_id));
        PRAGMA user_version=1;
        """;
}
