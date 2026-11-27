using Ashenfall.Api.Catalog.Services;
using Xunit;

namespace Ashenfall.Tests;

public sealed class CombatCatalogHashV2Tests
{
    [Fact]
    public void HashCoversCombatProgressionModifiersAndRelationships()
    {
        var catalog = InitialCatalogSeed.CreateDraft();
        var cls = catalog.Classes.Single().Id;
        var item = catalog.Items.First().Id;
        var combat = new[] { new CatalogCombatClassV2(cls, 25, 25, 5, 100) };
        var levels = new[] { new CatalogProgressionV2(cls, 1, 0, 0, 0, 0, 0, 0, 0, 0),
            new CatalogProgressionV2(cls, 2, 100, 5, 2, 0, 0, 0, 0, 0) };
        var modifiers = new[] { new CatalogModifierV2(item, "critical_chance", "flat", 10) };
        var hash = CombatCatalogHashV2.Compute(catalog, combat, levels, modifiers);
        Assert.Equal(hash, CombatCatalogHashV2.Compute(catalog, combat, levels.Reverse().ToArray(), modifiers));
        Assert.NotEqual(hash, CombatCatalogHashV2.Compute(catalog, [combat[0] with { MinDamage = 24 }], levels, modifiers));
        Assert.NotEqual(hash, CombatCatalogHashV2.Compute(catalog, combat, [levels[0], levels[1] with { HealthBonus = 6 }], modifiers));
        Assert.NotEqual(hash, CombatCatalogHashV2.Compute(catalog, combat, levels, [modifiers[0] with { Amount = 11 }]));
        Assert.Throws<ArgumentException>(() => CombatCatalogHashV2.Compute(catalog, combat, levels, [modifiers[0] with { ItemId = Guid.NewGuid() }]));
    }
}
