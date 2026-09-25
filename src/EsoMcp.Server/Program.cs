using EsoMcp.Core;
using EsoMcp.Import;
using EsoMcp.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

try
{
    var (settings, mode) = ServerSettings.Parse(args);
    if (mode == "help") { Console.WriteLine(ServerSettings.Help); return 0; }
    var database = new Database(settings.DatabasePath);
    var options = new ImportOptions { Locations = settings.Locations, CatalogPaths = settings.CatalogPaths };
    var store = new WorkspaceStore(settings.DatabasePath);
    var workspace = new AccountWorkspace(store, database, options, !settings.AutoRefresh || settings.Locations.Count == 0);
    if (mode == "status") { Console.WriteLine(DataJson.Write(new { Accounts = store.Accounts().Select(a => new { a.Key, Characters = a.Characters.Count }), Plans = store.Plans() })); return 0; }
    if (mode == "refresh")
    {
        var result = workspace.Read();
        Console.WriteLine(DataJson.Write(new { Accounts = result.Data.Accounts.Select(a => new { a.Key, Characters = a.Characters.Count }), result.Data.Diagnostics }));
        return 0;
    }
    var builder = Host.CreateApplicationBuilder();
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Services.AddSingleton(database);
    builder.Services.AddSingleton(store);
    builder.Services.AddSingleton(workspace);
    builder.Services.AddSingleton(options);
    builder.Services.AddSingleton<ICraftingCatalog, CraftingCatalogService>();
    builder.Services.AddSingleton<ISkillCatalog, SkillCatalogService>();
    builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<AccountTools>().WithTools<BuildTools>().WithTools<CatalogTools>();
    await builder.Build().RunAsync();
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
