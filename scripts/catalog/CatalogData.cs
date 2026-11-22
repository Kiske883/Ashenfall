#nullable enable
using System;
using System.Collections.Immutable;

namespace Ashenfall.Catalog;

// The API does not expose the database release UUID. The version key identifies the release.
public sealed record CatalogVersion(short SchemaMajor, string VersionKey, string ContentSha256, DateTimeOffset PublishedAt);

public sealed record CatalogClass(
    Guid Id, string Code, string DisplayName, string VisualKey, int BaseMaxHealth, int FixedDamage,
    int? BaseStrength, int? BaseDexterity, int? BaseVitality, int? BaseArmor,
    decimal MoveSpeedMps, decimal MeleeRangeM, int AttackCycleMs, int ImpactMs);

public sealed record CatalogEnemy(
    Guid Id, string Code, string DisplayName, string VisualKey, int MaxHealth, int FixedDamage,
    int? BaseStrength, int? BaseDexterity, int? BaseVitality, int? BaseArmor,
    decimal AttackRangeM, int WindupMs, int RecoveryMs, int ImpactAfterExecuteMs,
    decimal ChaseSpeedMps, decimal ReturnSpeedMps, decimal DetectionRadiusM,
    decimal DisengagementRadiusM, decimal MaxSpawnDistanceM, decimal StoppingDistanceM,
    decimal ResumeDistanceM);

public sealed record CatalogItem(
    Guid Id, string Code, string DisplayName, string Category, string Rarity,
    bool Stackable, int MaxStack, string? EquipSlot, short? HandRequirement, string? VisualKey);

public sealed record CatalogSnapshot(
    short SchemaMajor, string VersionKey, string ContentSha256,
    ImmutableArray<CatalogClass> Classes, ImmutableArray<CatalogEnemy> Enemies,
    ImmutableArray<CatalogItem> Items);

public sealed class CatalogLoadException(string message) : Exception(message);
