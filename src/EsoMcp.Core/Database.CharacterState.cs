using System.Text.Json;

namespace EsoMcp.Core;

public sealed record CharacterStateView(string CharacterKey, string Section, bool Available,
    IReadOnlyDictionary<string, object?>? Observation, object? Data);

public sealed partial class Database
{
    /// <summary>Latest recorded character state, independent of the originating addon.</summary>
    public CharacterStateView CharacterState(string characterKey, string section = "summary")
    {
        if (section is not ("summary" or "research" or "champion" or "statistics" or "skills" or "equipment" or "all"))
            throw new ArgumentException("section must be summary, research, champion, statistics, skills, equipment or all.");
        var row = Rows("""
            SELECT r.record_key,r.character_key,r.server,r.account,r.name,r.observed_at,
              s.source_key,s.label AS source_label,s.modified_at,s.imported_at,
              a.status AS refresh_status,a.message AS refresh_message,r.data_json
            FROM records r JOIN sources s USING(source_key) LEFT JOIN attempts a USING(source_key)
            WHERE r.kind='character_state' AND r.character_key=$character
            ORDER BY COALESCE(r.observed_at,s.modified_at) DESC,s.priority DESC,s.modified_at DESC,r.record_key
            LIMIT 1
            """, ("character", characterKey)).FirstOrDefault();
        if (row is null) return new(characterKey, section, false, null, null);
        var data = (JsonElement)row["data_json"]!;
        row.Remove("data_json");
        bool Has(string key) => data.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null;
        Dictionary<string, JsonElement> Select(params string[] keys) => keys.Where(Has)
            .ToDictionary(key => key, key => data.GetProperty(key));

        object? result;
        if (section == "all")
            result = data.EnumerateObject().Where(x => x.Name != "details").ToDictionary(x => x.Name, x => x.Value);
        else if (section == "summary")
        {
            var available = new[] { "research", "champion", "statistics", "skills", "equipment" }
                .Where(Has).ToArray();
            result = new
            {
                Character = Select("level", "class", "race", "apiVersion", "attributes", "unspentSkillPoints", "totalSkillPoints", "championPoints"),
                AvailableSections = available
            };
        }
        else if (section == "skills")
            result = Has("skills") || Has("skillLineRanks")
                ? Select("skills", "skillLineRanks", "unspentSkillPoints", "totalSkillPoints") : null;
        else result = Has(section) ? data.GetProperty(section) : null;
        return new(characterKey, section, result is not null, row, result);
    }
}
