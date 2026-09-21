using EsoData.Addons;
using EsoData.Lua;
using EsoData.Models;
using EsoMcp.Core;

namespace EsoMcp.Import;

internal static class AddonProjection
{
    public static ImportBatch Project(string provider, LuaTable raw, SourceDocument source, string? defaultServer)
    {
        var info = new SourceInfo(provider, source.Path, source.ModifiedAt);
        var diagnostics = new List<string>();
        var batch = new ImportBatch(source with { RawJson = DataJson.Write(RawData.Preserve(raw)), Diagnostics = diagnostics });
        CharacterIdentity Character(CharacterReference c) => new(World(c.Server, c.SourceAccountId), c.Account, c.Id, c.Name);
        string World(string server, string? qualified = null) => NormalizeWorld(!string.IsNullOrWhiteSpace(server) ? server
            : defaultServer ?? "unresolved:" + (qualified ?? source.Key));
        switch (provider)
        {
            case "inventory":
            case "character-observations":
            {
                var data = provider == "inventory" ? IifaReader.Parse(raw, info) : UespLogReader.Parse(raw, info);
                diagnostics.AddRange(data.Diagnostics);
                if (provider == "inventory" && raw.Table("IIFA_DATABASE") is null)
                    throw new FormatException("Inventory root is missing; previous import retained.");
                if (provider == "character-observations" && raw.Table("uespLogSavedVars") is null)
                    throw new FormatException("uespLog root is missing; previous import retained.");
                foreach (var c in data.Characters) batch.Characters.Add(new(Character(c)));
                foreach (var state in data.CharacterStates)
                {
                    var c = Character(state.Character);
                    batch.Characters.Add(new(c, state.ObservedAt));
                    batch.Records.Add(new("character_state", c.Key + ":" + batch.Records.Count, c.Key, c.Server, c.Account, c.Name,
                        state.ObservedAt, DataJson.Write(new
                        {
                            state.Level, state.Class, state.Race, state.UnspentSkillPoints, state.TotalSkillPoints, state.ChampionPoints,
                            state.ApiVersion, state.Attributes, Skills = state.SkillLineRanks is null ? null : state.Skills,
                            state.ChampionAllocations,
                            state.Champion, state.Research, state.Statistics, state.SkillLineRanks,
                            Equipment = state.Raw.Table("EquipSlots") is null ? null
                                : state.Equipment.ToDictionary(x => x.Key, x => x.Value.ToString()),
                            Details = RawData.Details(state.Raw)
                        })));
                }
                foreach (var inventory in data.Inventories)
                {
                    var server = World(inventory.Server, inventory.SourceAccountId);
                    var key = inventory.CharacterId is null ? null : new CharacterIdentity(server, inventory.Account, inventory.CharacterId).Key;
                    foreach (var item in inventory.Items)
                        batch.Inventory.Add(new(server, inventory.Account, key, item.Location, item.Link.ItemId, item.Count,
                            item.Link.ToString(), item.Name, item.Quality, item.BagId, item.Slot, inventory.ObservedAt));
                }
                if (provider == "inventory") AddInventoryDetails();
                break;
            }
            case "knowledge":
            {
                var data = CharacterKnowledgeReader.Parse(raw, info);
                diagnostics.AddRange(data.Diagnostics);
                foreach (var knowledge in data.Characters)
                {
                    var c = Character(knowledge.Character);
                    batch.Characters.Add(new(c, knowledge.ObservedAt));
                    foreach (var category in knowledge.Categories)
                    foreach (var entry in category.Value)
                        batch.Knowledge.Add(new(c.Key, category.Key, entry.Index, entry.ItemId, entry.Known, knowledge.ObservedAt));
                    if (knowledge.Research is not null)
                        batch.Records.Add(new("research", c.Key, c.Key, c.Server, c.Account, c.Name, knowledge.ObservedAt,
                            DataJson.Write(new { knowledge.Research, knowledge.ObservedAt, Meaning = "Trait indexes require a matching catalog; elapsed timers are not proof of learned traits." })));
                    batch.Records.Add(new("knowledge_metadata", c.Key, c.Key, c.Server, c.Account, c.Name, knowledge.ObservedAt,
                        DataJson.Write(RawData.Details(knowledge.Raw))));
                }
                break;
            }
            case "builds":
            {
                var data = CspsReader.Parse(raw, info);
                foreach (var profile in data.Profiles)
                {
                    var c = Character(profile.Character);
                    batch.Characters.Add(new(c, profile.SavedAt));
                    batch.Records.Add(new("build", c.Key + ":" + profile.ProfileId, c.Key, c.Server, c.Account, profile.Name ?? "Default",
                        profile.SavedAt, DataJson.Write(new
                        {
                            profile.ProfileId, profile.Name, profile.SavedAt, NativeText = profile.Build.ToString(),
                            profile.EquipmentUniqueIds, IsObservedState = false, Details = RawData.Details(profile.Raw)
                        })));
                }
                // Character metadata exists even when the character has never saved a profile.
                foreach (var world in raw.Table("CSPSSavedVariables")?.Tables() ?? [])
                foreach (var account in world.Value.Tables())
                foreach (var entry in account.Value.Table("$AccountWide")?.Table("charData")?.Tables() ?? [])
                {
                    var c = new CharacterIdentity(World(world.Key.Value), account.Key.Value, entry.Key.Value,
                        entry.Value.String("$lastCharacterName") ?? entry.Value.String("$LastCharacterName"));
                    batch.Characters.Add(new(c));
                    batch.Records.Add(new("character_metadata", c.Key, c.Key, c.Server, c.Account, c.Name, null, DataJson.Write(RawData.Details(entry.Value))));
                }
                break;
            }
            case "collections":
                foreach (var collection in SetCollectionReader.Parse(raw, info).Accounts)
                    batch.Records.Add(new("collection", Identity.Key(collection.Server, collection.Account), null,
                        World(collection.Server), collection.Account, "Set collection", collection.ObservedAt,
                        DataJson.Write(new { collection.SetMasks, Meaning = "36-bit collection slot masks; piece metadata is required to resolve slots." })));
                break;
            case "crafting-queue":
                foreach (var request in LazySetCrafterReader.Parse(raw, info).Requests)
                    batch.Records.Add(new("crafting_request", request.Scope + ":" + batch.Records.Count, null, "", "", request.Scope, null,
                        DataJson.Write(new { Link = request.Link.ToString(), request.Quantity, request.Reference, Details = RawData.Details(request.Raw) })));
                break;
            default: throw new ArgumentException("Unknown import provider.", nameof(provider));
        }
        return batch;

        void AddInventoryDetails()
        {
            foreach (var account in raw.Table("IIFA_DATABASE")?.Tables() ?? [])
            foreach (var world in account.Value.Table("servers")?.Tables() ?? [])
            {
                foreach (var entry in world.Value.Table("assets") ?? new LuaTable())
                {
                    var name = world.Value.Table("CharIdToName")?.Get(entry.Key) as string;
                    var isCharacter = name is not null;
                    var c = isCharacter ? new CharacterIdentity(World(world.Key.Value), account.Key.Value, entry.Key.Value, name) : null;
                    batch.Records.Add(new("storage_metadata", Identity.Key(world.Key.Value, account.Key.Value, entry.Key.Value), c?.Key,
                        World(world.Key.Value), account.Key.Value, name ?? entry.Key.Value, null, DataJson.Write(RawData.Details(entry.Value))));
                }
            }
        }
    }
    private static string NormalizeWorld(string world) => world.Trim().ToUpperInvariant() switch
    {
        "EU MEGASERVER" or "EU" => "EU",
        "NA MEGASERVER" or "NA" => "NA",
        _ => world
    };
}
