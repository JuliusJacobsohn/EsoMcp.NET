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
    var importer = new RefreshService(database, new() { Locations = settings.Locations, CatalogPaths = settings.CatalogPaths });
    if (mode == "status") { Console.WriteLine(DataJson.Write(database.Status())); return 0; }
    if (mode == "refresh")
    {
        var result = await importer.RefreshAsync();
        Console.WriteLine(DataJson.Write(result));
        return result.Sources.Any(x => x.Status == "failed") ? 1 : 0;
    }
    var builder = Host.CreateApplicationBuilder();
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Services.AddSingleton(database);
    builder.Services.AddSingleton<IRefreshService>(importer);
    builder.Services.AddSingleton<IGameExports, GameExports>();
    builder.Services.AddSingleton<ICraftingCatalog, CraftingCatalogService>();
    builder.Services.AddSingleton<ISkillCatalog, SkillCatalogService>();
    builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<DatabaseTools>().WithTools<ExportTools>()
        .WithTools<CraftingPlanTools>().WithTools<SkillCatalogTools>()
        .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (request, cancellationToken) =>
        {
            // Refresh once before database-backed tools; tool implementations remain database-only.
            if (settings.AutoRefresh && request.Params?.Name is not ("refresh_database" or "create_crafting_import"))
                await request.Services!.GetRequiredService<IRefreshService>().RefreshAsync(cancellationToken: cancellationToken);
            return await next(request, cancellationToken);
        }));
    await builder.Build().RunAsync();
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
