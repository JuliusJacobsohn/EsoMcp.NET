using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EsoMcp.Core;

public static class DataJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
}

public sealed record CharacterIdentity(string Server, string Account, string Id, string? Name = null)
{
    public string Key => Identity.Key(Server.ToUpperInvariant(), Account.ToUpperInvariant(), Id);
}
public static class Identity
{
    public static string Key(params string[] parts) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(DataJson.Write(parts))));
}
public sealed record SourceDocument(string Key, string Label, string Path, string Hash, DateTimeOffset ModifiedAt,
    string RawJson, int Priority = 0, IReadOnlyList<string>? Diagnostics = null);
public sealed record CharacterObservation(CharacterIdentity Character, DateTimeOffset? ObservedAt = null, string? DetailsJson = null);
public sealed record InventoryItem(string Server, string Account, string? CharacterKey, string Location, long ItemId,
    long Count, string Link, string? Name = null, int? Quality = null, int? BagId = null, long? Slot = null,
    DateTimeOffset? ObservedAt = null);
public sealed record KnowledgeItem(string CharacterKey, string Category, int Index, long ItemId, bool? Known, DateTimeOffset? ObservedAt);
public sealed record DataRecord(string Kind, string LocalId, string? CharacterKey, string Server, string Account,
    string? Name, DateTimeOffset? ObservedAt, string Json);
public sealed record CatalogSet(long Id, string? Name, string NamesJson);
public sealed record CatalogItem(long Id, long? SetId, string? Name, int? EquipType, int? Trait, string Json);
public sealed record CatalogSkill(long Id, string? Name, string Json);
/// <summary>One concrete result in a crafting import, including its own optional glyph and style.</summary>
public sealed record CraftingImportItem(long ItemId, long EnchantmentItemId = 0, int? EnchantmentQuality = null,
    int? StyleId = null);
/// <summary>One item to resolve from refreshable set metadata before creating a crafting import.</summary>
public sealed record CraftingPlanItem(long SetId, int? EquipType = null, int? ArmorType = null,
    int? WeaponType = null, int? Trait = null, long EnchantmentItemId = 0,
    int? EnchantmentQuality = null, int? StyleId = null);
/// <summary>One native CSPS equipment slot. Type is armor type for body slots and weapon type for hand slots.</summary>
public sealed record CspsEquipmentItem(int EquipSlot, long SetId, int Type, int Trait, int Quality,
    long EnchantmentEffectId);
/// <summary>A complete native CSPS respec plan using resolved game IDs, with gear left untouched.</summary>
public sealed record CspsRespecPlan(CspsActivePurchase[] Active, CspsPassivePurchase[] Passive,
    long[] FrontBar, long[] BackBar, CspsChampionAllocation[] ChampionPoints, long?[] ChampionSlots,
    int Health, int Magicka, int Stamina);
public sealed record CspsActivePurchase(long AbilityId, int Morph);
public sealed record CspsPassivePurchase(long AbilityId, int Rank);
public sealed record CspsChampionAllocation(long SkillId, int Points);
/// <summary>Optional ESO-Hub build-editor changes to a supplied addondata template.</summary>
public sealed record HubBuildPatch(string Template, HubAbilitySlot?[]? FrontBar = null,
    HubAbilitySlot?[]? BackBar = null, HubChampionPoint?[]? SlottedChampionPoints = null,
    HubChampionPoint[]? OtherChampionPoints = null);
public sealed record HubAbilitySlot(long AbilityId, long[]? Scripts = null);
public sealed record HubChampionPoint(long SkillId, int Points);
public sealed class ImportBatch(SourceDocument source)
{
    public SourceDocument Source { get; } = source;
    public List<CharacterObservation> Characters { get; } = [];
    public List<InventoryItem> Inventory { get; } = [];
    public List<KnowledgeItem> Knowledge { get; } = [];
    public List<DataRecord> Records { get; } = [];
    public List<CatalogSet> Sets { get; } = [];
    public List<CatalogItem> Items { get; } = [];
    public List<CatalogSkill> Skills { get; } = [];
}
public sealed record RefreshEntry(string Source, string Status, string? Message = null);
public sealed record RefreshResult(DateTimeOffset CompletedAt, IReadOnlyList<RefreshEntry> Sources);
public interface IRefreshService
{
    Task<RefreshResult> RefreshAsync(bool force = false, CancellationToken cancellationToken = default);
}
public interface IGameExports
{
    string CraftingImport(IReadOnlyList<long> itemIds, int level, int quality, int championPoints = 0,
        int styleId = 1, long enchantmentItemId = 0);
    string CraftingImport(IReadOnlyList<CraftingImportItem> items, int level, int quality, int championPoints = 0,
        int styleId = 1);
    string CspsEquipmentImport(IReadOnlyList<CspsEquipmentItem> items);
    (string Text, string Url) HubBuildImport(HubBuildPatch patch);
}
public interface ICraftingCatalog
{
    Task<IReadOnlyList<RefreshEntry>> RefreshItemMetadataAsync(IReadOnlyList<long> setIds,
        CancellationToken cancellationToken = default);
    IReadOnlyList<long> Resolve(IReadOnlyList<CraftingPlanItem> items);
}
