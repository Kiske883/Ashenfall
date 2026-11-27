using System.Text.Json;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Characters;
using Xunit;

namespace Ashenfall.Tests;

public sealed class GameplaySnapshotV2FactoryTests
{
    [Fact]
    public void HistoricalIdentityResolvesThroughHashedLinkAndPreservesRevision()
    {
        var active = InitialCatalogSeed.CreateDraft();
        active.Release.Status = CatalogStates.Published;
        active.Release.PublishedAt = DateTimeOffset.UnixEpoch;
        var target = active.Classes.Single();
        var oldRelease = new CatalogRelease { Id = Guid.NewGuid(), Status = CatalogStates.Retired };
        var source = new CharacterClass { Id = Guid.NewGuid(), Code = target.Code, CatalogReleaseId = oldRelease.Id };
        var character = new CharacterRow { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), ClassDefinitionId = source.Id, Experience = 100, RowRevision = 1 };
        var links = new[] { (SourceId: source.Id, TargetId: target.Id) };
        var combat = new[] { new CatalogCombatClassV2(target.Id, 25, 25, 5, 100) };
        var levels = new[] { new CatalogProgressionV2(target.Id, 1, 0, 0, 0, 0, 0, 0, 0, 0),
            new CatalogProgressionV2(target.Id, 2, 100, 5, 2, 0, 0, 0, 0, 0) };
        var item = active.Items.First().Id;
        var mods = new[] { new CatalogModifierV2(item, "critical_chance", "flat", 10), new CatalogModifierV2(item, "critical_multiplier", "flat", 50) };
        var hash = CombatCatalogHashV2.Compute(active, combat, levels, mods, links);
        var snapshot = GameplaySnapshotV2Factory.Create(character, source, oldRelease, active, combat, levels, mods, links, [item], hash);
        Assert.Equal((2, character.Id, 1L, source.Id), (snapshot.SchemaVersion, snapshot.CharacterId, snapshot.CharacterRevision, snapshot.ClassDefinitionId));
        Assert.Equal((105, 27, 27, 27, 15m, 150m), (snapshot.EffectiveStats.MaxHealth, snapshot.EffectiveStats.FixedDamage, snapshot.EffectiveStats.MinDamage, snapshot.EffectiveStats.MaxDamage, snapshot.EffectiveStats.CriticalChancePercent, snapshot.EffectiveStats.CriticalDamageBonusPercent));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(27, json.RootElement.GetProperty("effectiveStats").GetProperty("minDamage").GetInt32());
        Assert.Throws<InvalidGameplaySnapshotException>(() => GameplaySnapshotV2Factory.Create(character, source, oldRelease, active, combat, levels, mods, [], [item], hash));
        Assert.Throws<InvalidGameplaySnapshotException>(() => GameplaySnapshotV2Factory.Create(character, source, oldRelease, active, combat, levels, mods, links, [Guid.NewGuid()], hash));
        var targetItem = active.Items.First();
        var historical = new ItemDefinition { Id = Guid.NewGuid(), CatalogReleaseId = oldRelease.Id, Code = targetItem.Code,
            Category = targetItem.Category, EquipSlot = targetItem.EquipSlot, HandRequirement = targetItem.HandRequirement, Stackable = targetItem.Stackable };
        var itemLinks = new[] { (SourceId: historical.Id, TargetId: targetItem.Id) };
        var linkedHash = CombatCatalogHashV2.Compute(active, combat, levels, mods, links, itemLinks);
        Assert.NotEqual(hash, linkedHash);
        var migrated = GameplaySnapshotV2Factory.Create(character, source, oldRelease, active, combat, levels, mods,
            links, [historical.Id], linkedHash, itemLinks, [historical], [oldRelease]);
        Assert.Equal(15m, migrated.EffectiveStats.CriticalChancePercent);
        Assert.Equal(150m, migrated.EffectiveStats.CriticalDamageBonusPercent);
        var duplicateDefinitions = GameplaySnapshotV2Factory.Create(character, source, oldRelease, active, combat, levels, mods,
            links, [historical.Id, historical.Id], linkedHash, itemLinks, [historical], [oldRelease]);
        Assert.Equal(25m, duplicateDefinitions.EffectiveStats.CriticalChancePercent);
        historical.Category = "quest";
        Assert.Throws<InvalidGameplaySnapshotException>(() => GameplaySnapshotV2Factory.Create(character, source, oldRelease, active, combat, levels, mods, links, [historical.Id], linkedHash, itemLinks, [historical], [oldRelease]));
    }
}
