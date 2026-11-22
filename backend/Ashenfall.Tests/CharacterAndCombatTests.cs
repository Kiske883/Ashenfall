using Ashenfall.Api.Characters;
using Ashenfall.Combat;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ashenfall.Tests;

public sealed class CharacterAndCombatTests
{
    private sealed class FixedRandom(int roll, double chance) : ICombatRandom
    {
        public int NextIntInclusive(int minimum, int maximum) => roll;
        public double NextUnit() => chance;
    }

    [Fact] public void DamageRangeIsInclusiveAndCriticalBoundaryIsStrict()
    {
        var profile = new CombatDamageProfile(20, 30, 0.1, 1.5);
        Assert.Equal(20, CombatDamageCalculator.Calculate(profile, new FixedRandom(20, 0.1)).FinalDamage);
        var critical = CombatDamageCalculator.Calculate(profile, new FixedRandom(30, 0.099999));
        Assert.True(critical.Critical);
        Assert.Equal(45, critical.FinalDamage);
    }

    [Fact] public void DamageRoundingIsAwayFromZeroAndZeroChanceNeverCrits()
    {
        Assert.Equal(2, CombatDamageCalculator.Calculate(
            new CombatDamageProfile(1, 1, 1, 1.5), new FixedRandom(1, 0)).FinalDamage);
        Assert.False(CombatDamageCalculator.Calculate(
            new CombatDamageProfile(8, 12, 0, 1.5), new FixedRandom(8, 0)).Critical);
    }

    [Fact] public void InvalidRandomAndProfilesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatDamageCalculator.Calculate(
            new CombatDamageProfile(30, 20, 0.1, 1.5), new FixedRandom(20, 0)));
        Assert.Throws<InvalidOperationException>(() => CombatDamageCalculator.Calculate(
            new CombatDamageProfile(20, 30, 0.1, 1.5), new FixedRandom(31, 0)));
    }

    [Fact] public void ProgressionIsDerivedFromPinnedClassRules()
    {
        var rules = new[] { new LevelRuleRow { Level = 1, RequiredTotalXp = 0 },
            new LevelRuleRow { Level = 2, RequiredTotalXp = 100 },
            new LevelRuleRow { Level = 3, RequiredTotalXp = 300 } };
        Assert.Equal(1, CharacterRules.DeriveLevel(99, rules));
        Assert.Equal(2, CharacterRules.DeriveLevel(100, rules));
        Assert.Equal(3, CharacterRules.DeriveLevel(1000, rules));
        Assert.Throws<ArgumentException>(() => CharacterRules.DeriveLevel(100,
            new[] { rules[0], new LevelRuleRow { Level = 3, RequiredTotalXp = 100 } }));
    }

    [Fact] public void TwoHandedWeaponBlocksOffHandInBothDirections()
    {
        Assert.Throws<ArgumentException>(() => CharacterRules.ValidateEquipment(
            "main_hand", "weapon", "main_hand", 2, false, 1, false, true));
        Assert.Throws<ArgumentException>(() => CharacterRules.ValidateEquipment(
            "off_hand", "armor", "off_hand", null, false, 1, true, false));
        CharacterRules.ValidateEquipment("main_hand", "weapon", "main_hand", 2, false, 1, false, false);
        Assert.Throws<ArgumentException>(() => CharacterRules.ValidateEquipment(
            "head", "weapon", "main_hand", 1, false, 1, false, false));
        Assert.Throws<ArgumentException>(() => CharacterRules.ValidateEquipment(
            "main_hand", "weapon", "main_hand", 1, true, 1, false, false));
        Assert.Throws<ArgumentException>(() => CharacterRules.ValidateEquipment(
            "main_hand", "weapon", "main_hand", 1, false, 2, false, false));
    }

    [Fact] public void AttributeAllocationCannotExceedEarnedLevelBudget()
    {
        var rules = new[] { new LevelRuleRow { Level = 1, RequiredTotalXp = 0, AttributePoints = 0 },
            new LevelRuleRow { Level = 2, RequiredTotalXp = 100, AttributePoints = 3 } };
        Assert.Throws<ArgumentException>(() => CharacterRules.ValidateAllocation(99, 1, 0, 0, rules));
        CharacterRules.ValidateAllocation(100, 2, 1, 0, rules);
        Assert.Throws<ArgumentException>(() => CharacterRules.ValidateAllocation(100, 2, 2, 0, rules));
    }

    [Fact] public void ProgressionRejectsGapsDuplicateThresholdsNegativeAwardsAndOverflowBounds()
    {
        var first = new LevelRuleRow { Level = 1, RequiredTotalXp = 0 };
        CharacterRules.ValidateProgression([first, new LevelRuleRow { Level = 2, RequiredTotalXp = long.MaxValue }]);
        Assert.Throws<ArgumentException>(() => CharacterRules.ValidateProgression(
            [first, new LevelRuleRow { Level = 3, RequiredTotalXp = 100 }]));
        Assert.Throws<ArgumentException>(() => CharacterRules.ValidateProgression(
            [first, new LevelRuleRow { Level = 2, RequiredTotalXp = 0 }]));
        Assert.Throws<ArgumentException>(() => CharacterRules.ValidateProgression(
            [first, new LevelRuleRow { Level = 2, RequiredTotalXp = 100, AttributePoints = -1 }]));
        Assert.Throws<ArgumentException>(() => CharacterRules.ValidateProgression(
            [first, new LevelRuleRow { Level = 2, RequiredTotalXp = 100, HealthBonus = int.MaxValue }]));
    }

    [Fact] public void EveryClassRequiresItsOwnCompleteProgressionForFuturePublication()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var rules = new[] { new LevelRuleRow { ClassDefinitionId = a, Level = 1, RequiredTotalXp = 0 },
            new LevelRuleRow { ClassDefinitionId = b, Level = 1, RequiredTotalXp = 0 } };
        CharacterRules.ValidateCatalogProgression([a,b], rules);
        Assert.Throws<ArgumentException>(() => CharacterRules.ValidateCatalogProgression([a,b], [rules[0]]));
        Assert.Throws<ArgumentException>(() => CharacterRules.ValidateCatalogProgression([a], rules));
    }

    [Fact] public void CharacterMappingUsesReviewedPostgresqlColumnNamesWithoutConnecting()
    {
        var options = new DbContextOptionsBuilder<CharacterDbContext>()
            .UseNpgsql("Host=invalid.local;Database=not_used").Options;
        using var db = new CharacterDbContext(options);
        string sql = db.Characters.Where(x => x.DeletedAt == null).ToQueryString();
        Assert.Contains("player_characters", sql);
        Assert.Contains("character_name", sql);
        Assert.Contains("row_revision", sql);
        Assert.Contains("deleted_at", sql);
        Assert.Contains("id", sql);
    }

    [Fact] public void StatsApplyLevelAllocationEquipmentAndRuntimeInOrder()
    {
        var character = new CharacterRow { Experience = 100, AllocatedStrength = 2 };
        var rules = new[] { new LevelRuleRow { Level = 1, RequiredTotalXp = 0 },
            new LevelRuleRow { Level = 2, RequiredTotalXp = 100, HealthBonus = 5 } };
        var mods = new[] { new ItemModifierRow { StatCode = "max_health", ModifierKind = "flat", Amount = 5 },
            new ItemModifierRow { StatCode = "max_health", ModifierKind = "percent", Amount = 10 } };
        var stats = CharacterRules.CalculateStats(100, 25, 10, 0, 0, 0, character, rules,
            mods, new Dictionary<string, decimal> { ["max_health"] = 2 });
        Assert.Equal(123, stats.MaxHealth);
        Assert.Equal(12, stats.Strength);
        Assert.Equal(25, stats.Damage);
    }
}
