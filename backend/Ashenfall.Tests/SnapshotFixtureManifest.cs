using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Characters;

namespace Ashenfall.Tests;

// In-memory fixture specification only: it does not connect to PostgreSQL.
internal static class SnapshotFixtureManifest
{
    internal const string Version = "snapshot-09c2a1-test";
    internal static (CatalogSnapshot Catalog, LevelRuleRow[] Rules, ItemModifierRow[] Modifiers) Create()
    {
        var original = InitialCatalogSeed.CreateDraft();
        var releaseId = Guid.Parse("90000000-0000-0000-0000-000000000001");
        var classId = Guid.Parse("90000000-0000-0000-0000-000000000002");
        original.Release.Id = releaseId;
        original.Release.VersionKey = Version;
        foreach (var value in original.Classes) { value.Id = classId; value.CatalogReleaseId = releaseId; }
        foreach (var value in original.Enemies) value.CatalogReleaseId = releaseId;
        foreach (var value in original.Items) value.CatalogReleaseId = releaseId;
        var weapon = original.Items.Single(x => x.Code == "guardian_sword");
        weapon.Id = Guid.Parse("90000000-0000-0000-0000-000000000011");
        var axe = original.Items.Single(x => x.Code == "training_battle_axe");
        axe.Id = Guid.Parse("90000000-0000-0000-0000-000000000012");
        var items = original.Items.Concat(new[] {
            new ItemDefinition { Id = Guid.Parse("90000000-0000-0000-0000-000000000013"), CatalogReleaseId = releaseId, Code = "test_shield", DisplayName = "Test shield", Category = "armor", Rarity = "common", EquipSlot = "off_hand" },
            new ItemDefinition { Id = Guid.Parse("90000000-0000-0000-0000-000000000015"), CatalogReleaseId = releaseId, Code = "test_stack", DisplayName = "Test stack", Category = "consumable", Rarity = "common", Stackable = true, MaxStack = 5 }
        }).ToArray();
        var rules = new[] {
            new LevelRuleRow { ClassDefinitionId = classId, Level = 1, RequiredTotalXp = 0 },
            new LevelRuleRow { ClassDefinitionId = classId, Level = 2, RequiredTotalXp = 100, HealthBonus = 5, DamageBonus = 2 }
        };
        var modifiers = new[] {
            new ItemModifierRow { ItemDefinitionId = weapon.Id, StatCode = "max_health", ModifierKind = "flat", Amount = 5 },
            new ItemModifierRow { ItemDefinitionId = weapon.Id, StatCode = "max_health", ModifierKind = "percent", Amount = 10 },
            new ItemModifierRow { ItemDefinitionId = weapon.Id, StatCode = "fixed_damage", ModifierKind = "flat", Amount = 3 }
        };
        return (original with { Items = items }, rules, modifiers);
    }
}
