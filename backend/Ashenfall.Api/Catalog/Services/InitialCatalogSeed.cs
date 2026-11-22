using Ashenfall.Api.Catalog.Domain;

namespace Ashenfall.Api.Catalog.Services;

/// <summary>Prepared catalog content only; this class performs no database writes.</summary>
public static class InitialCatalogSeed
{
    public const string VersionKey = "prototype-1";

    public static CatalogSnapshot CreateDraft()
    {
        var releaseId = Guid.Parse("9dcb9c59-56ae-4e81-bf1c-6ac7021649a1");
        var release = new CatalogRelease
        {
            Id = releaseId, VersionKey = VersionKey, SchemaMajor = 1,
            Status = CatalogStates.Draft, CreatedAt = DateTimeOffset.UnixEpoch
        };
        var classes = new[]
        {
            new CharacterClass
            {
                Id = Guid.Parse("614ef7a1-a2f1-4876-9f80-e1653fd4a311"), CatalogReleaseId = releaseId,
                Code = "dwarf_warrior", DisplayName = "Dwarf Warrior", VisualKey = "dwarf_berserker",
                BaseMaxHealth = 100, FixedDamage = 25, MoveSpeedMps = 2.5m, MeleeRangeM = 1.45m,
                AttackCycleMs = 1700, ImpactMs = 940
            }
        };
        var enemies = new[]
        {
            new EnemyDefinition
            {
                Id = Guid.Parse("5c92fdc4-860a-4d7e-a2f8-363d0789cad7"), CatalogReleaseId = releaseId,
                Code = "fallen_guardian", DisplayName = "Fallen Guardian", VisualKey = "fallen_guardian_v3",
                MaxHealth = 100, FixedDamage = 10, AttackRangeM = 1.45m,
                WindupMs = 600, RecoveryMs = 1000, ImpactAfterExecuteMs = 100,
                ChaseSpeedMps = 1.0m, ReturnSpeedMps = 0.7m,
                DetectionRadiusM = 6m, DisengagementRadiusM = 10m, MaxSpawnDistanceM = 12m,
                StoppingDistanceM = 1.3m, ResumeDistanceM = 1.7m
            }
        };
        var items = new[]
        {
            new ItemDefinition
            {
                Id = Guid.Parse("270e4972-d576-479b-a2c5-51452bf2702e"), CatalogReleaseId = releaseId,
                Code = "training_battle_axe", DisplayName = "Training Battle Axe",
                Category = "weapon", Rarity = "common", Stackable = false, MaxStack = 1,
                EquipSlot = "main_hand", HandRequirement = 2, VisualKey = "dwarf_battle_axe"
            },
            new ItemDefinition
            {
                Id = Guid.Parse("4a82d635-8d6a-4fc3-a7e8-ef0a344d20f1"), CatalogReleaseId = releaseId,
                Code = "guardian_sword", DisplayName = "Guardian Sword",
                Category = "weapon", Rarity = "common", Stackable = false, MaxStack = 1,
                EquipSlot = "main_hand", HandRequirement = 1, VisualKey = "guardian_sword"
            }
        };
        return new CatalogSnapshot(release, classes, enemies, items);
    }

    public static SeedDisposition CompareExisting(CatalogSnapshot? existing, CatalogSnapshot candidate)
    {
        if (existing is null) return SeedDisposition.CreateDraft;
        if (existing.Release.VersionKey != candidate.Release.VersionKey)
            throw new ArgumentException("Seed comparison requires the same catalog version.");
        return CatalogHash.Compute(existing) == CatalogHash.Compute(candidate)
            ? SeedDisposition.AlreadyPresent : SeedDisposition.VersionConflict;
    }
}

public enum SeedDisposition { CreateDraft, AlreadyPresent, VersionConflict }
