namespace Ashenfall.Api.Catalog.Domain;

public static class CatalogStates
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Retired = "retired";
}

public sealed class CatalogRelease
{
    public Guid Id { get; set; }
    public string VersionKey { get; set; } = "";
    public short SchemaMajor { get; set; }
    public string? ContentSha256 { get; set; }
    public string Status { get; set; } = CatalogStates.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class CharacterClass
{
    public Guid Id { get; set; }
    public Guid CatalogReleaseId { get; set; }
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string VisualKey { get; set; } = "";
    public int BaseMaxHealth { get; set; }
    public int FixedDamage { get; set; }
    public int? BaseStrength { get; set; }
    public int? BaseDexterity { get; set; }
    public int? BaseVitality { get; set; }
    public int? BaseArmor { get; set; }
    public decimal MoveSpeedMps { get; set; }
    public decimal MeleeRangeM { get; set; }
    public int AttackCycleMs { get; set; }
    public int ImpactMs { get; set; }
}

public sealed class EnemyDefinition
{
    public Guid Id { get; set; }
    public Guid CatalogReleaseId { get; set; }
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string VisualKey { get; set; } = "";
    public int MaxHealth { get; set; }
    public int FixedDamage { get; set; }
    public int? BaseStrength { get; set; }
    public int? BaseDexterity { get; set; }
    public int? BaseVitality { get; set; }
    public int? BaseArmor { get; set; }
    public decimal AttackRangeM { get; set; }
    public int WindupMs { get; set; }
    public int RecoveryMs { get; set; }
    public int ImpactAfterExecuteMs { get; set; }
    public decimal ChaseSpeedMps { get; set; }
    public decimal ReturnSpeedMps { get; set; }
    public decimal DetectionRadiusM { get; set; }
    public decimal DisengagementRadiusM { get; set; }
    public decimal MaxSpawnDistanceM { get; set; }
    public decimal StoppingDistanceM { get; set; }
    public decimal ResumeDistanceM { get; set; }
}

public sealed class ItemDefinition
{
    public Guid Id { get; set; }
    public Guid CatalogReleaseId { get; set; }
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Rarity { get; set; } = "";
    public bool Stackable { get; set; }
    public int MaxStack { get; set; } = 1;
    public string? EquipSlot { get; set; }
    public short? HandRequirement { get; set; }
    public string? VisualKey { get; set; }
}

public sealed record CatalogSnapshot(
    CatalogRelease Release,
    IReadOnlyList<CharacterClass> Classes,
    IReadOnlyList<EnemyDefinition> Enemies,
    IReadOnlyList<ItemDefinition> Items);
