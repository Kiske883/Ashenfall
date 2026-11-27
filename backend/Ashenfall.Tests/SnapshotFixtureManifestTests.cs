using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Characters;
using Xunit;

namespace Ashenfall.Tests;

public sealed class SnapshotFixtureManifestTests
{
    [Fact]
    public void FixtureHasValidCatalogProgressionAndExpectedEffectiveStats()
    {
        var (catalog, rules, modifiers) = SnapshotFixtureManifest.Create();
        Assert.Empty(CatalogValidator.Validate(catalog));
        CharacterRules.ValidateCatalogProgression(catalog.Classes.Select(x => x.Id).ToArray(), rules);
        Assert.Matches("^[0-9a-f]{64}$", CatalogHash.Compute(catalog));
        var dwarf = Assert.Single(catalog.Classes);
        var character = new CharacterRow { ClassDefinitionId = dwarf.Id, Experience = 100 };
        var without = CharacterRules.CalculateStats(dwarf.BaseMaxHealth, dwarf.FixedDamage, 0, 0, 0, 0, character, rules, []);
        var with = CharacterRules.CalculateStats(dwarf.BaseMaxHealth, dwarf.FixedDamage, 0, 0, 0, 0, character, rules, modifiers);
        Assert.Equal((2, 105, 27), (without.Level, without.MaxHealth, without.Damage));
        Assert.Equal((2, 121, 30), (with.Level, with.MaxHealth, with.Damage));
    }
}
