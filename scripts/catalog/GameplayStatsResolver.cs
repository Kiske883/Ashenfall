#nullable enable
using System;
using System.Linq;

namespace Ashenfall.Catalog;

/// <summary>One immutable, version-consistent set of gameplay definitions for an encounter.</summary>
public sealed record DwarfGameplayStats(int MaximumHealth, int FixedDamage, float MoveSpeed,
    float MeleeRange, float AttackDuration, float ImpactFraction);

public sealed record GuardianGameplayStats(int MaximumHealth, int FixedDamage, float AttackRange,
    float Windup, float Recovery, float ImpactDelay, float ChaseSpeed, float ReturnSpeed,
    float DetectionRadius, float DisengagementRadius, float MaximumDistanceFromSpawn,
    float StoppingDistance, float ResumeChaseDistance);

public sealed record AdventureGameplayStats(string VersionKey, string ContentSha256,
    DwarfGameplayStats Dwarf, GuardianGameplayStats Guardian);

public static class GameplayStatsResolver
{
    public static AdventureGameplayStats Resolve(CatalogSnapshot snapshot)
    {
        if (snapshot.SchemaMajor != 1 || string.IsNullOrEmpty(snapshot.VersionKey) ||
            string.IsNullOrEmpty(snapshot.ContentSha256))
            throw new CatalogLoadException("Unsupported or incomplete gameplay catalog snapshot.");
        var classes = snapshot.Classes.Where(x => x.Code == "dwarf_warrior").ToArray();
        var enemies = snapshot.Enemies.Where(x => x.Code == "fallen_guardian").ToArray();
        if (classes.Length != 1 || enemies.Length != 1 ||
            classes[0].VisualKey != "dwarf_berserker" || enemies[0].VisualKey != "fallen_guardian_v3")
            throw new CatalogLoadException("Required gameplay definitions are missing or incompatible.");
        var dwarf = classes[0];
        var guardian = enemies[0];
        if (dwarf.BaseMaxHealth < 1 || dwarf.FixedDamage < 0 || dwarf.MoveSpeedMps <= 0 ||
            dwarf.MeleeRangeM <= 0 || dwarf.AttackCycleMs < 100 || dwarf.ImpactMs < 0 ||
            dwarf.ImpactMs > dwarf.AttackCycleMs || guardian.MaxHealth < 1 ||
            guardian.FixedDamage < 0 || guardian.AttackRangeM <= 0 || guardian.WindupMs < 50 ||
            guardian.RecoveryMs < 50 || guardian.ImpactAfterExecuteMs < 0 ||
            guardian.ImpactAfterExecuteMs > guardian.RecoveryMs ||
            guardian.ChaseSpeedMps < 0 || guardian.ReturnSpeedMps < 0 ||
            guardian.DetectionRadiusM <= 0 || guardian.DisengagementRadiusM <= guardian.DetectionRadiusM ||
            guardian.MaxSpawnDistanceM <= 0 || guardian.StoppingDistanceM <= 0 ||
            guardian.ResumeDistanceM <= guardian.StoppingDistanceM)
            throw new CatalogLoadException("Gameplay catalog statistics are invalid.");
        return new AdventureGameplayStats(snapshot.VersionKey, snapshot.ContentSha256,
            new DwarfGameplayStats(dwarf.BaseMaxHealth, dwarf.FixedDamage, (float)dwarf.MoveSpeedMps,
                (float)dwarf.MeleeRangeM, dwarf.AttackCycleMs / 1000f,
                (float)dwarf.ImpactMs / dwarf.AttackCycleMs),
            new GuardianGameplayStats(guardian.MaxHealth, guardian.FixedDamage, (float)guardian.AttackRangeM,
                guardian.WindupMs / 1000f, guardian.RecoveryMs / 1000f,
                guardian.ImpactAfterExecuteMs / 1000f, (float)guardian.ChaseSpeedMps,
                (float)guardian.ReturnSpeedMps, (float)guardian.DetectionRadiusM,
                (float)guardian.DisengagementRadiusM, (float)guardian.MaxSpawnDistanceM,
                (float)guardian.StoppingDistanceM, (float)guardian.ResumeDistanceM));
    }
}
