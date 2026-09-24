using System.Text.Json;

namespace EsoMcp.Core;

public sealed record Page(int Offset, int Limit, bool HasMore, IReadOnlyList<Dictionary<string, object?>> Rows);

public sealed partial class Database
{
    public object Status() => new
    {
        DatabasePath = Path,
        Sources = Rows("SELECT source_key,label,path,modified_at,imported_at,priority,diagnostics_json FROM sources ORDER BY label"),
        LastRefresh = Rows("SELECT source_key,label,attempted_at,status,message FROM attempts ORDER BY label"),
        Counts = Rows("""
            SELECT (SELECT COUNT(DISTINCT character_key) FROM characters) AS characters,
              (SELECT COUNT(*) FROM inventory) AS inventory_observations,(SELECT COUNT(*) FROM knowledge) AS knowledge_entries,
              (SELECT COUNT(*) FROM records) AS records,(SELECT COUNT(DISTINCT set_id) FROM catalog_sets) AS sets,
              (SELECT COUNT(DISTINCT item_id) FROM catalog_items) AS catalog_items
            """)
    };
    public Page Characters(string? name = null, string? account = null, string? server = null, int offset = 0, int limit = 50) =>
        Paged("""
            WITH ranked AS (
              SELECT c.*,s.imported_at,ROW_NUMBER() OVER(PARTITION BY character_key
                ORDER BY (name IS NULL OR name=''),COALESCE(observed_at,s.modified_at) DESC,s.priority DESC) AS rn
              FROM characters c JOIN sources s USING(source_key))
            SELECT character_key,server,account,game_id,name,observed_at,imported_at,source_key FROM ranked
            WHERE rn=1 AND ($name IS NULL OR instr(lower(COALESCE(name,'')),lower($name))>0)
              AND ($account IS NULL OR account=$account COLLATE NOCASE) AND ($server IS NULL OR server=$server COLLATE NOCASE)
            ORDER BY server,account,name,game_id
            """, offset, limit, ("name", name), ("account", account), ("server", server));

    public Page Inventory(string? text = null, long? itemId = null, long? setId = null, string? characterKey = null,
        string? account = null, string? server = null, bool includeAlternateSources = false, int offset = 0, int limit = 50) =>
        Paged("""
            SELECT i.*,s.label AS source_label,s.modified_at,s.imported_at FROM inventory i JOIN sources s USING(source_key)
            WHERE ($text IS NULL OR instr(lower(COALESCE(i.name,'')),lower($text))>0)
              AND ($item IS NULL OR i.item_id=$item)
              AND ($set IS NULL OR EXISTS(SELECT 1 FROM catalog_items c WHERE c.item_id=i.item_id AND c.set_id=$set))
              AND ($character IS NULL OR i.character_key=$character)
              AND ($account IS NULL OR i.account=$account COLLATE NOCASE) AND ($server IS NULL OR i.server=$server COLLATE NOCASE)
              AND ($alternates=1 OR NOT EXISTS(
                SELECT 1 FROM inventory other JOIN sources os ON other.source_key=os.source_key
                WHERE other.account=i.account COLLATE NOCASE AND other.server=i.server COLLATE NOCASE
                AND (os.priority>s.priority OR (os.priority=s.priority AND
                  (os.modified_at>s.modified_at OR (os.modified_at=s.modified_at AND os.source_key<s.source_key))))))
            ORDER BY i.server,i.account,i.location,i.name,i.row_id
            """, offset, limit, ("text", text), ("item", itemId), ("set", setId), ("character", characterKey),
            ("account", account), ("server", server), ("alternates", includeAlternateSources));

    public Page Knowledge(string? characterKey = null, string? category = null, long? itemId = null, bool? known = null,
        int offset = 0, int limit = 50) => Paged("""
            SELECT k.*,c.name AS character_name,c.server,c.account FROM knowledge k
              JOIN characters c USING(source_key,character_key)
            WHERE ($character IS NULL OR k.character_key=$character) AND ($category IS NULL OR k.category=$category)
              AND ($item IS NULL OR k.item_id=$item) AND ($known IS NULL OR k.known=$known)
            ORDER BY c.name,k.character_key,k.category,k.entry_index,k.source_key
            """, offset, limit, ("character", characterKey), ("category", category), ("item", itemId), ("known", known));

    public Page Records(string? characterKey = null, string? kind = null, string? account = null, string? server = null,
        int offset = 0, int limit = 50) => Paged("""
            SELECT record_key,source_key,kind,local_id,character_key,server,account,name,observed_at FROM records
            WHERE ($character IS NULL OR character_key=$character) AND ($kind IS NULL OR kind=$kind)
              AND ($account IS NULL OR account=$account COLLATE NOCASE) AND ($server IS NULL OR server=$server COLLATE NOCASE)
            ORDER BY kind,name,record_key
            """, offset, limit, ("character", characterKey), ("kind", kind), ("account", account), ("server", server));

    public JsonElement Record(string key)
    {
        var result = Rows("SELECT data_json FROM records WHERE record_key=$key", ("key", key));
        if (result.Count == 0) throw new KeyNotFoundException("Record not found. Use list_records to get its record_key.");
        return (JsonElement)result[0]["data_json"]!;
    }
    public Page Sets(string? text = null, long? setId = null, int offset = 0, int limit = 50) => Paged("""
        WITH ranked AS (
          SELECT c.*,ROW_NUMBER() OVER(PARTITION BY set_id ORDER BY s.priority DESC,s.modified_at DESC,s.source_key) AS rn
          FROM catalog_sets c JOIN sources s USING(source_key))
        SELECT source_key,set_id,name,names_json FROM ranked WHERE rn=1
          AND ($text IS NULL OR instr(lower(names_json),lower($text))>0) AND ($id IS NULL OR set_id=$id)
        ORDER BY name,set_id
        """, offset, limit, ("text", text), ("id", setId));

    public Page ItemDefinitions(long? setId = null, long? itemId = null, int? equipType = null, int? trait = null,
        int offset = 0, int limit = 50) => Paged("""
        WITH ranked AS (
          SELECT c.*,ROW_NUMBER() OVER(PARTITION BY item_id ORDER BY s.priority DESC,s.modified_at DESC,s.source_key) AS rn
          FROM catalog_items c JOIN sources s USING(source_key))
        SELECT item_id,set_id,name,equip_type,trait,data_json,source_key FROM ranked WHERE rn=1
          AND ($set IS NULL OR set_id=$set) AND ($item IS NULL OR item_id=$item)
          AND ($equip IS NULL OR equip_type=$equip) AND ($trait IS NULL OR trait=$trait)
        ORDER BY item_id
        """, offset, limit, ("set", setId), ("item", itemId), ("equip", equipType), ("trait", trait));

    public IReadOnlyList<long> CatalogItemIds(long setId)
    {
        if (setId <= 0) throw new ArgumentOutOfRangeException(nameof(setId));
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT item_id FROM catalog_items WHERE set_id=$set ORDER BY item_id";
        command.Parameters.AddWithValue("$set", setId);
        using var reader = command.ExecuteReader();
        var ids = new List<long>();
        while (reader.Read()) ids.Add(reader.GetInt64(0));
        return ids;
    }

    /// <summary>Returns all source definitions in ascending priority order so callers can merge their non-null fields.</summary>
    public IReadOnlyList<string> CatalogItemDocuments(long setId)
    {
        if (setId <= 0) throw new ArgumentOutOfRangeException(nameof(setId));
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.data_json FROM catalog_items c JOIN sources s USING(source_key)
            WHERE c.set_id=$set ORDER BY s.priority,s.modified_at,s.source_key,c.item_id
            """;
        command.Parameters.AddWithValue("$set", setId);
        using var reader = command.ExecuteReader();
        var documents = new List<string>();
        while (reader.Read()) documents.Add(reader.GetString(0));
        return documents;
    }

    public Page Skills(string? text = null, long? skillId = null, int offset = 0, int limit = 50) => Paged("""
        WITH ranked AS (
          SELECT c.*,ROW_NUMBER() OVER(PARTITION BY skill_id ORDER BY s.priority DESC,s.modified_at DESC,s.source_key) AS rn
          FROM catalog_skills c JOIN sources s USING(source_key))
        SELECT skill_id,name,data_json,source_key FROM ranked WHERE rn=1
          AND ($text IS NULL OR instr(lower(COALESCE(name,'')),lower($text))>0) AND ($id IS NULL OR skill_id=$id)
        ORDER BY name,skill_id
        """, offset, limit, ("text", text), ("id", skillId));

    public Page SkillLines(string? classType = null, long? skillLineId = null, int offset = 0, int limit = 50) => Paged("""
        WITH ranked AS (
          SELECT c.*,ROW_NUMBER() OVER(PARTITION BY skill_line_id ORDER BY s.priority DESC,s.modified_at DESC,s.source_key) AS rn
          FROM catalog_skill_lines c JOIN sources s USING(source_key))
        SELECT skill_line_id,name,class_type,data_json,source_key FROM ranked WHERE rn=1
          AND ($class IS NULL OR class_type=$class) AND ($id IS NULL OR skill_line_id=$id)
        ORDER BY class_type,name,skill_line_id
        """, offset, limit, ("class", classType), ("id", skillLineId));

    private Page Paged(string sql, int offset, int limit, params (string Key, object? Value)[] parameters)
    {
        if (offset < 0 || limit is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(limit), "offset >= 0; limit must be 1..200.");
        var rows = Rows(sql + " LIMIT $limit OFFSET $offset", [.. parameters, ("limit", limit + 1), ("offset", offset)]);
        return new(offset, limit, rows.Count > limit, rows.Take(limit).ToArray());
    }
    private List<Dictionary<string, object?>> Rows(string sql, params (string Key, object? Value)[] parameters)
    {
        using var connection = Open();
        using var command = connection.CreateCommand(); command.CommandText = sql;
        foreach (var p in parameters) command.Parameters.AddWithValue("$" + p.Key, p.Value ?? DBNull.Value);
        using var reader = command.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                row[name] = reader.IsDBNull(i) ? null : name.EndsWith("_json", StringComparison.Ordinal)
                    ? ParseJson(reader.GetString(i)) : reader.GetValue(i);
            }
            rows.Add(row);
        }
        return rows;
    }
    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
