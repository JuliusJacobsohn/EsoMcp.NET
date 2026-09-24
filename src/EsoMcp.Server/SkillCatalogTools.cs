using System.ComponentModel;
using EsoMcp.Core;
using ModelContextProtocol.Server;

namespace EsoMcp.Server;

[McpServerToolType]
public sealed class SkillCatalogTools(ISkillCatalog catalog)
{
    [McpServerTool(Name = "refresh_skill_metadata", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Explicitly fetch current UESP skill-line IDs and up to 100 selected ability definitions into local SQLite. Pass an empty abilityIds array to refresh only the skill lines. Ordinary MCP queries remain local and do not download metadata.")]
    public async Task<string> RefreshSkillMetadata(long[] abilityIds, CancellationToken cancellationToken = default) =>
        DataJson.Write(await catalog.RefreshAsync(abilityIds, cancellationToken));
}
