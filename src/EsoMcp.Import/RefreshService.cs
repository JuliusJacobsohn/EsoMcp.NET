using System.Security.Cryptography;
using System.Text;
using EsoData.Catalogs;
using EsoData.Lua;
using EsoMcp.Core;

namespace EsoMcp.Import;

public sealed class RefreshService(Database database, ImportOptions options) : IRefreshService
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly (string File, string Provider, int Priority)[] Inputs =
    [
        ("IIfA.lua", "inventory", 100), ("LibCharacterKnowledge.lua", "knowledge", 100),
        ("CarosSkillPointSaver.lua", "builds", 100), ("LibMultiAccountSets.lua", "collections", 100),
        ("uespLog.lua", "character-observations", 50), ("DolgubonsLazySetCrafter.lua", "crafting-queue", 100)
    ];
    public async Task<RefreshResult> RefreshAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var results = new List<RefreshEntry>();
            foreach (var location in options.Locations)
            {
                foreach (var input in Inputs)
                {
                    var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(location.SavedVariablesPath, input.File));
                    Process(path, input.Provider, input.Priority, () => ReadStable(path), (text, source) =>
                        AddonProjection.Project(input.Provider, SavedVariables.Parse(text), source, location.DefaultServer));
                }
                if (location.AddonsPath is string addons)
                {
                    var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(addons, "LibSets"));
                    var names = System.IO.Path.Combine(folder, "Data", "LibSets_Data_SetNames.lua");
                    var ids = System.IO.Path.Combine(folder, "Data", "LibSets_Data_SetItemIds.lua");
                    Process(folder, "set-catalog", 10, () =>
                    {
                        var a = ReadStable(names); var b = ReadStable(ids);
                        return (DataJson.Write(new[] { a.Text, b.Text }), a.Modified > b.Modified ? a.Modified : b.Modified);
                    }, (text, source) =>
                    {
                        var parts = System.Text.Json.JsonSerializer.Deserialize<string[]>(text)!;
                        return CatalogProjection.Project(LibSetsCatalog.Parse(parts[0], parts[1], folder), source with { RawJson = text });
                    });
                }
            }
            foreach (var path in options.CatalogPaths.Select(System.IO.Path.GetFullPath))
                Process(path, "external-catalog", 100, () => ReadStable(path), (text, source) =>
                    CatalogProjection.Project(GameCatalog.FromJson(text), source with { RawJson = text }));
            return new(DateTimeOffset.UtcNow, results);

            void Process(string path, string provider, int priority, Func<(string Text, DateTimeOffset Modified)> read,
                Func<string, SourceDocument, ImportBatch> project)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = Identity.Key(OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path, provider);
                try
                {
                    var (text, modified) = read();
                    var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
                    if (!force && database.HasHash(key, hash))
                    {
                        database.RecordAttempt(key, provider, path, "unchanged"); results.Add(new(provider, "unchanged", path)); return;
                    }
                    var source = new SourceDocument(key, provider, path, hash, modified, "null", priority);
                    var batch = project(text, source);
                    database.Replace(batch, cancellationToken);
                    database.RecordAttempt(key, provider, path, "imported"); results.Add(new(provider, "imported", path));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException or
                    System.Text.Json.JsonException or OverflowException or ArgumentException or InvalidOperationException)
                {
                    var status = error is FileNotFoundException or DirectoryNotFoundException ? "missing" : "failed";
                    database.RecordAttempt(key, provider, path, status, error.Message);
                    results.Add(new(provider, status, error.Message));
                }
            }
        }
        finally { gate.Release(); }
    }
    private static (string Text, DateTimeOffset Modified) ReadStable(string path)
    {
        var before = new FileInfo(path);
        if (!before.Exists) throw new FileNotFoundException("Import file is not present; any previous import is retained.", path);
        var stamp = before.LastWriteTimeUtc; var size = before.Length;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        before.Refresh();
        if (!before.Exists || before.LastWriteTimeUtc != stamp || before.Length != size)
            throw new IOException("Source changed during refresh. Run refresh again after the game finishes saving.");
        return (text, stamp);
    }
}
