using System.Text.Json;
using EsoMcp.Core;
using EsoMcp.Import;

namespace EsoMcp.Server;

public sealed class ServerSettings
{
    public string DatabasePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EsoMcp", "data.db");
    public List<ImportLocation> Locations { get; set; } = [];
    public List<string> CatalogPaths { get; set; } = [];
    public bool AutoRefresh { get; set; } = true;

    public static (ServerSettings Settings, string Mode) Parse(string[] args)
    {
        var flags = new Dictionary<string, string?>();
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (key is "--help" or "--refresh" or "--status" or "--database-only" or "--no-auto-refresh") flags.Add(key, null);
            else if (key is "--config" or "--database" or "--saved-variables" or "--addons" or "--server")
            {
                if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"{key} requires a value.");
                flags.Add(key, args[i]);
            }
            else throw new ArgumentException($"Unknown option: {key}. Use --help.");
        }
        if (flags.ContainsKey("--help")) return (new(), "help");
        if (flags.ContainsKey("--refresh") && flags.ContainsKey("--status"))
            throw new ArgumentException("Choose either --refresh or --status.");
        var settings = flags.TryGetValue("--config", out var config)
            ? JsonSerializer.Deserialize<ServerSettings>(File.ReadAllText(config!), DataJson.Options)
                ?? throw new ArgumentException("Configuration must be a JSON object.")
            : new ServerSettings();
        if (flags.TryGetValue("--database", out var database)) settings.DatabasePath = database!;
        if (flags.TryGetValue("--saved-variables", out var saved))
            settings.Locations = [new(saved!, flags.GetValueOrDefault("--addons"), flags.GetValueOrDefault("--server"))];
        else if (flags.ContainsKey("--addons") || flags.ContainsKey("--server"))
            throw new ArgumentException("--addons and --server require --saved-variables; use --config for multiple locations.");
        else if (!flags.ContainsKey("--config") && OperatingSystem.IsWindows())
        {
            var live = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Elder Scrolls Online", "live");
            settings.Locations = [new(Path.Combine(live, "SavedVariables"), Path.Combine(live, "AddOns"))];
        }
        if (flags.ContainsKey("--database-only")) { settings.Locations = []; settings.CatalogPaths = []; }
        if (flags.ContainsKey("--no-auto-refresh")) settings.AutoRefresh = false;
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.DatabasePath);
        if (settings.Locations is null || settings.CatalogPaths is null)
            throw new ArgumentException("locations and catalogPaths must be arrays, not null.");
        return (settings, flags.ContainsKey("--refresh") ? "refresh" : flags.ContainsKey("--status") ? "status" : "stdio");
    }

    public const string Help = """
        EsoMcp.NET — local ESO database and MCP server (.NET 10)
        No arguments: serve MCP over stdio; refresh changed sources before database-backed tools.
          --config FILE          JSON with databasePath, locations, catalogPaths
          --database FILE        SQLite file (default: local application data/EsoMcp/data.db)
          --saved-variables DIR  One SavedVariables directory; overrides configured locations
          --addons DIR           Optional AddOns directory for its installed LibSets catalog
          --server NAME          Optional world for observations without a world identifier
          --database-only        Disable all refresh inputs; keep existing database queryable
          --no-auto-refresh      Refresh only on explicit requests (config: autoRefresh=false)
          --refresh              Import configured sources, print JSON, exit (1 if any failed)
          --status               Print database status as JSON, exit
          --help                 Print this help
        On Windows, standard ESO live folders are defaults unless --config is supplied.
        Use absolute paths in MCP client configuration. No files in game folders are modified.
        """;
}
