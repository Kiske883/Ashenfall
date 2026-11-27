using System.Text.Json;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Characters;
using Xunit;

namespace Ashenfall.Tests;

public sealed class CharacterGameplaySnapshotTests
{
    private static (CharacterRow character, CharacterClass definition, CatalogRelease release, LevelRuleRow[] rules) Fixture()
    {
        var release = new CatalogRelease { Id = Guid.NewGuid(), VersionKey = "synthetic", SchemaMajor = 1,
            Status = "published", ContentSha256 = new string('a', 64), PublishedAt = DateTimeOffset.UnixEpoch };
        var definition = new CharacterClass { Id = Guid.NewGuid(), CatalogReleaseId = release.Id, BaseMaxHealth = 100, FixedDamage = 25 };
        var character = new CharacterRow { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), ClassDefinitionId = definition.Id, Experience = 100, RowRevision = 3 };
        var rules = new[] { new LevelRuleRow { ClassDefinitionId = definition.Id, Level = 1, RequiredTotalXp = 0 },
            new LevelRuleRow { ClassDefinitionId = definition.Id, Level = 2, RequiredTotalXp = 100, HealthBonus = 5, DamageBonus = 2 } };
        return (character, definition, release, rules);
    }

    [Fact]
    public void ContractUsesExistingStatsAndSerializesIdentityAndProvenance()
    {
        var (character, definition, release, rules) = Fixture();
        var mods = new[] { new ItemModifierRow { StatCode = "max_health", ModifierKind = "flat", Amount = 5 },
            new ItemModifierRow { StatCode = "max_health", ModifierKind = "percent", Amount = 10 },
            new ItemModifierRow { StatCode = "fixed_damage", ModifierKind = "flat", Amount = 3 },
            new ItemModifierRow { StatCode = "critical_chance", ModifierKind = "flat", Amount = 100 } };
        var result = GameplaySnapshotFactory.Create(character, definition, release, rules, mods);
        Assert.Equal(2, result.EffectiveStats.Level); Assert.Equal(121, result.EffectiveStats.MaxHealth);
        Assert.Equal(30, result.EffectiveStats.FixedDamage); Assert.Equal(3, result.CharacterRevision);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(character.Id, json.RootElement.GetProperty("characterId").GetGuid());
        Assert.Equal(release.Id, json.RootElement.GetProperty("catalogReleaseId").GetGuid());
        Assert.Equal(release.ContentSha256, json.RootElement.GetProperty("catalogHash").GetString());
        Assert.False(json.RootElement.GetProperty("effectiveStats").TryGetProperty("criticalChance", out _));
    }

    [Fact]
    public void RejectsMissingRulesInvalidProvenanceAndUnsafeStats()
    {
        var (c, d, r, rules) = Fixture();
        Assert.Throws<InvalidGameplaySnapshotException>(() => GameplaySnapshotFactory.Create(c, d, r, [], []));
        r.Status = "draft";
        Assert.Throws<InvalidGameplaySnapshotException>(() => GameplaySnapshotFactory.Create(c, d, r, rules, []));
        r.Status = "published"; c.RowRevision = 0;
        Assert.Throws<InvalidGameplaySnapshotException>(() => GameplaySnapshotFactory.Create(c, d, r, rules, []));
        c.RowRevision = 1; d.BaseMaxHealth = 10001;
        Assert.Throws<InvalidGameplaySnapshotException>(() => GameplaySnapshotFactory.Create(c, d, r, rules, []));
        d.BaseMaxHealth = 100; d.FixedDamage = 1001;
        Assert.Throws<InvalidGameplaySnapshotException>(() => GameplaySnapshotFactory.Create(c, d, r, rules, []));
        d.FixedDamage = 0; c.Experience = 0;
        Assert.Equal(0, GameplaySnapshotFactory.Create(c, d, r, rules, []).EffectiveStats.FixedDamage);
        var negative = new[] { new ItemModifierRow { StatCode = "max_health", ModifierKind = "flat", Amount = -200 } };
        Assert.Throws<InvalidGameplaySnapshotException>(() => GameplaySnapshotFactory.Create(c, d, r, rules, negative));
        var zero = new[] { new ItemModifierRow { StatCode = "max_health", ModifierKind = "flat", Amount = -100 } };
        Assert.Throws<InvalidGameplaySnapshotException>(() => GameplaySnapshotFactory.Create(c, d, r, rules, zero));
        c.DeletedAt = DateTimeOffset.UtcNow;
        Assert.Throws<InvalidGameplaySnapshotException>(() => GameplaySnapshotFactory.Create(c, d, r, rules, []));
        c.DeletedAt = null; c.ClassDefinitionId = Guid.NewGuid();
        Assert.Throws<InvalidGameplaySnapshotException>(() => GameplaySnapshotFactory.Create(c, d, r, rules, []));
    }

    [Fact]
    public void AcceptedLimitsAndOverflowAreExplicit()
    {
        var (c, d, r, rules) = Fixture();
        c.Experience = 0; d.BaseMaxHealth = GameplaySnapshotFactory.MaxHealth;
        d.FixedDamage = GameplaySnapshotFactory.MaxDamage;
        var snapshot = GameplaySnapshotFactory.Create(c, d, r, rules, []);
        Assert.Equal(10000, snapshot.EffectiveStats.MaxHealth);
        Assert.Equal(1000, snapshot.EffectiveStats.FixedDamage);
        var tooLow = new[] { new ItemModifierRow { StatCode = "fixed_damage", ModifierKind = "flat", Amount = -1000.1m } };
        Assert.Throws<InvalidGameplaySnapshotException>(() => GameplaySnapshotFactory.Create(c, d, r, rules, tooLow));
    }
}
