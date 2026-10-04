using EsoData.Addons;

namespace EsoMcp.Server;

internal static class CombatReportQuery
{
    internal static object Read(IReadOnlyList<CombatMetricsReport> reports, AccountQuery query, string? characterName)
    {
        if (query.Offset < 0 || query.Limit is < 1 or > 100) throw new ArgumentException("Use offset >= 0 and limit 1..100.");
        var matches = reports.SelectMany(report => report.Fights.Select(fight => (Report: report, Fight: fight)))
            .Where(x => (characterName is null || string.Equals(x.Fight.CharacterName, characterName, StringComparison.OrdinalIgnoreCase))
                && (query.Ids is null || query.Ids.Contains(x.Fight.Id))
                && (query.Text is null || new[] { x.Fight.Label, x.Fight.CharacterName, x.Fight.Zone, x.Fight.Subzone }
                    .Any(value => value?.Contains(query.Text, StringComparison.OrdinalIgnoreCase) == true)))
            .OrderByDescending(x => x.Fight.StartedAt).ThenByDescending(x => x.Report.Source.FileWrittenAt)
            .ThenByDescending(x => x.Fight.Id).ToArray();
        var rows = matches.Skip(query.Offset).Take(query.Limit).Select(x => AccountTools.Project(new
        {
            x.Fight.Id, x.Fight.CharacterName, x.Fight.Label, x.Fight.Zone, x.Fight.Subzone,
            x.Fight.StartedAt, x.Fight.LocalTime, x.Fight.DamageDurationSeconds, x.Fight.HealingDurationSeconds,
            x.Fight.DamagePerSecond, x.Fight.IncomingDamagePerSecond, x.Fight.HealingPerSecond, x.Fight.IncomingHealingPerSecond,
            x.Fight.HasEncodedDetails, x.Fight.HasCombatLog, x.Report.Source.Path, x.Report.Source.FileWrittenAt
        }, query.Fields)).ToArray();
        return new
        {
            query.Section, Available = reports.Count > 0, Scope = "installation-wide; character name is not account ownership",
            Total = matches.Length, query.Offset, HasMore = query.Offset + query.Limit < matches.Length, Rows = rows,
            Sources = reports.Select(r => new { r.Source.Path, r.Source.FileWrittenAt, r.FormatVersion, SavedFights = r.Fights.Count, r.Diagnostics }),
            Note = "Saved CMX summaries only. Unsaved recent fights are in game memory. Encoded ability/rotation details are not decoded."
        };
    }
}
