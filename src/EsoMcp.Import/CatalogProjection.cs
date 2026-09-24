using EsoData.Catalogs;
using EsoMcp.Core;

namespace EsoMcp.Import;

public static class CatalogProjection
{
    public static ImportBatch Project(GameCatalog catalog, SourceDocument source)
    {
        var batch = new ImportBatch(source);
        foreach (var set in catalog.Sets.Values)
            batch.Sets.Add(new(set.Id, set.Names.GetValueOrDefault("en") ?? set.Names.Values.FirstOrDefault(), DataJson.Write(set.Names)));
        foreach (var item in catalog.Items.Values)
            batch.Items.Add(new(item.Id, item.SetId, item.Name, item.EquipType, item.Trait, DataJson.Write(item)));
        foreach (var skill in catalog.Skills.Values) batch.Skills.Add(new(skill.Id, skill.Name, DataJson.Write(skill)));
        foreach (var line in catalog.SkillLines.Values)
            batch.SkillLines.Add(new(line.Id, line.Name, line.ClassType, DataJson.Write(line)));
        batch.Records.Add(new("catalog_metadata", "metadata", null, "", "", "Catalog provenance and optional mappings", source.ModifiedAt,
            DataJson.Write(new { catalog.Sources, catalog.CollectionPieces, catalog.ResearchTraits, catalog.ResearchSignature, catalog.Extensions })));
        return batch;
    }
}
