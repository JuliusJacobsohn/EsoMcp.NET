using System.ComponentModel;
using EsoData.Addons;
using EsoMcp.Import;
using ModelContextProtocol.Server;

namespace EsoMcp.Server;

[McpServerToolType]
public sealed class AddonTools(ImportOptions options)
{
    [McpServerTool(Name = "inspect_addons", ReadOnly = true, OpenWorld = false)]
    [Description("Check whether supported ESO addons are installed locally and where they are found. Reads addon manifests and saved-file metadata only; does not load account data, write files, access the network or require a discovered account. Reports missing directories/addons, versions, required dependency presence/minimum versions, and saved-file paths/sizes/times separately. Installed does not prove enabled or loaded in ESO; saved files can remain after uninstalling an addon. Defaults to all configured source locations, or standard Documents ESO/live folders if none are configured. Optional addonsPath/savedVariablesPath inspect another installation; one supplied path infers its sibling directory. This is a fresh local check even in database-only mode.")]
    public string Inspect(string? addonsPath = null, string? savedVariablesPath = null) => ToolResult.Json(() =>
    {
        var explicitPaths = addonsPath is not null || savedVariablesPath is not null;
        var reports = explicitPaths ? [AddonDiscovery.Inspect(addonsPath, savedVariablesPath)]
            : options.Locations.Count == 0 ? new[] { AddonDiscovery.Inspect() }
            : options.Locations.Select(l => AddonDiscovery.Inspect(l.AddonsPath, l.SavedVariablesPath)).ToArray();
        return new { Source = explicitPaths ? "arguments" : options.Locations.Count == 0 ? "default" : "configuration", Locations = reports };
    });
}
