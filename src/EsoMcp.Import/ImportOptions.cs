namespace EsoMcp.Import;

public sealed record ImportLocation(string SavedVariablesPath, string? AddonsPath = null, string? DefaultServer = null);
public sealed class ImportOptions
{
    public List<ImportLocation> Locations { get; set; } = [];
    public List<string> CatalogPaths { get; set; } = [];
}
