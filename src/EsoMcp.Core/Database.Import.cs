using Microsoft.Data.Sqlite;

namespace EsoMcp.Core;

public sealed partial class Database
{
    /// <summary>Atomically replaces one successful import. Exceptions roll back to its previous complete state.</summary>
    public void Replace(ImportBatch batch, CancellationToken cancellationToken = default)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM sources WHERE source_key=$key";
        delete.Parameters.AddWithValue("$key", batch.Source.Key);
        delete.ExecuteNonQuery();
        using var writer = new BatchWriter(connection, transaction, cancellationToken);
        var source = batch.Source;
        writer.Insert("sources", "source_key,label,path,hash,modified_at,imported_at,priority,diagnostics_json,raw_json",
            source.Key, source.Label, source.Path, source.Hash, source.ModifiedAt, DateTimeOffset.UtcNow, source.Priority,
            DataJson.Write(source.Diagnostics ?? []), source.RawJson);
        foreach (var observation in batch.Characters.GroupBy(x => x.Character.Key).Select(g => g.OrderByDescending(x => x.ObservedAt).First()))
        {
            var c = observation.Character;
            writer.Insert("characters", "source_key,character_key,server,account,game_id,name,observed_at,details_json",
                source.Key, c.Key, c.Server, c.Account, c.Id, c.Name, observation.ObservedAt, observation.DetailsJson);
        }
        foreach (var i in batch.Inventory)
            writer.Insert("inventory", "source_key,server,account,character_key,location,item_id,count,link,name,quality,bag_id,slot,observed_at",
                source.Key, i.Server, i.Account, i.CharacterKey, i.Location, i.ItemId, i.Count, i.Link, i.Name, i.Quality, i.BagId, i.Slot, i.ObservedAt);
        foreach (var k in batch.Knowledge)
            writer.Insert("knowledge", "source_key,character_key,category,entry_index,item_id,known,observed_at",
                source.Key, k.CharacterKey, k.Category, k.Index, k.ItemId, k.Known, k.ObservedAt);
        foreach (var r in batch.Records)
            writer.Insert("records", "record_key,source_key,kind,local_id,character_key,server,account,name,observed_at,data_json",
                Identity.Key(source.Key, r.Kind, r.LocalId), source.Key, r.Kind, r.LocalId, r.CharacterKey, r.Server, r.Account, r.Name, r.ObservedAt, r.Json);
        foreach (var s in batch.Sets)
            writer.Insert("catalog_sets", "source_key,set_id,name,names_json", source.Key, s.Id, s.Name, s.NamesJson);
        foreach (var i in batch.Items)
            writer.Insert("catalog_items", "source_key,item_id,set_id,name,equip_type,trait,data_json",
                source.Key, i.Id, i.SetId, i.Name, i.EquipType, i.Trait, i.Json);
        foreach (var s in batch.Skills)
            writer.Insert("catalog_skills", "source_key,skill_id,name,data_json", source.Key, s.Id, s.Name, s.Json);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
    }
    private sealed class BatchWriter(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken) : IDisposable
    {
        private readonly Dictionary<string, SqliteCommand> commands = [];
        public void Insert(string table, string columns, params object?[] values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!commands.TryGetValue(table, out var command))
            {
                command = connection.CreateCommand(); command.Transaction = transaction;
                command.CommandText = $"INSERT INTO {table}({columns}) VALUES({string.Join(',', Enumerable.Range(0, values.Length).Select(i => "$p" + i))})";
                for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue("$p" + i, DBNull.Value);
                commands[table] = command;
            }
            for (var i = 0; i < values.Length; i++) command.Parameters[i].Value = values[i] switch
            {
                null => DBNull.Value, DateTimeOffset date => date.ToUniversalTime().ToString("O"), _ => values[i]!
            };
            command.ExecuteNonQuery();
        }
        public void Dispose() { foreach (var command in commands.Values) command.Dispose(); }
    }
}
