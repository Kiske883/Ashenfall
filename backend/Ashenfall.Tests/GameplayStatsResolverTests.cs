using System.Collections.Immutable;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Catalog;
using Xunit;

namespace Ashenfall.Tests;

public sealed class GameplayStatsResolverTests
{
    private static CatalogSnapshot ValidSnapshot()
    {
        var seed = InitialCatalogSeed.CreateDraft();
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        string json = System.Text.Json.JsonSerializer.Serialize(new
        {
            schemaMajor = 1, catalogVersion = seed.Release.VersionKey,
            contentSha256 = CatalogHash.Compute(seed), classes = seed.Classes,
            enemies = seed.Enemies, items = seed.Items
        }, options);
        var snapshot = CatalogCodec.ParseSnapshot(json);
        CatalogCodec.Verify(new CatalogVersion(1, snapshot.VersionKey, snapshot.ContentSha256,
            DateTimeOffset.UtcNow), snapshot);
        return snapshot;
    }

    [Fact]
    public void ResolvesApprovedClassAndEnemyByStableCode()
    {
        var result = GameplayStatsResolver.Resolve(ValidSnapshot());
        Assert.Equal("prototype-1", result.VersionKey);
        Assert.Equal(100, result.Dwarf.MaximumHealth);
        Assert.Equal(25, result.Dwarf.FixedDamage);
        Assert.Equal(2.5f, result.Dwarf.MoveSpeed);
        Assert.Equal(1.45f, result.Dwarf.MeleeRange);
        Assert.Equal(1.7f, result.Dwarf.AttackDuration);
        Assert.Equal(0.94f, result.Dwarf.AttackDuration * result.Dwarf.ImpactFraction, 4);
        Assert.Equal(100, result.Guardian.MaximumHealth);
        Assert.Equal(10, result.Guardian.FixedDamage);
        Assert.Equal(0.6f, result.Guardian.Windup);
        Assert.Equal(1.0f, result.Guardian.Recovery);
        Assert.Equal(0.1f, result.Guardian.ImpactDelay);
        Assert.Equal(1.0f, result.Guardian.ChaseSpeed);
        Assert.Equal(0.7f, result.Guardian.ReturnSpeed);
        Assert.Equal(6f, result.Guardian.DetectionRadius);
        Assert.Equal(10f, result.Guardian.DisengagementRadius);
        Assert.Equal(12f, result.Guardian.MaximumDistanceFromSpawn);
        Assert.Equal(1.3f, result.Guardian.StoppingDistance);
        Assert.Equal(1.7f, result.Guardian.ResumeChaseDistance);
    }

    [Fact]
    public void IgnoresArrayOrderAndDoesNotConsumeItemBonuses()
    {
        var snapshot = ValidSnapshot();
        var reordered = snapshot with { Items = snapshot.Items.Reverse().ToImmutableArray() };
        Assert.Equal(GameplayStatsResolver.Resolve(snapshot), GameplayStatsResolver.Resolve(reordered));
    }

    [Fact]
    public void RejectsMissingOrIncompatibleRequiredDefinitions()
    {
        var snapshot = ValidSnapshot();
        Assert.Throws<CatalogLoadException>(() => GameplayStatsResolver.Resolve(
            snapshot with { Classes = ImmutableArray<CatalogClass>.Empty }));
        Assert.Throws<CatalogLoadException>(() => GameplayStatsResolver.Resolve(
            snapshot with { Enemies = ImmutableArray<CatalogEnemy>.Empty }));
        Assert.Throws<CatalogLoadException>(() => GameplayStatsResolver.Resolve(
            snapshot with { SchemaMajor = 2 }));
        Assert.Throws<CatalogLoadException>(() => GameplayStatsResolver.Resolve(
            snapshot with { Classes = snapshot.Classes.SetItem(0,
                snapshot.Classes[0] with { VisualKey = "unknown" }) }));
        Assert.Throws<CatalogLoadException>(() => GameplayStatsResolver.Resolve(
            snapshot with { Enemies = snapshot.Enemies.SetItem(0,
                snapshot.Enemies[0] with { ImpactAfterExecuteMs = 2000 }) }));
    }

    [Fact]
    public void PinnedEncounterStatsDoNotFollowAReplacementSnapshot()
    {
        var first = ValidSnapshot();
        var pinned = GameplayStatsResolver.Resolve(first);
        var later = first with
        {
            VersionKey = "future-release",
            Classes = first.Classes.SetItem(0, first.Classes[0] with { FixedDamage = 30 })
        };
        var nextEncounter = GameplayStatsResolver.Resolve(later);
        Assert.Equal("prototype-1", pinned.VersionKey);
        Assert.Equal(25, pinned.Dwarf.FixedDamage);
        Assert.Equal("future-release", nextEncounter.VersionKey);
        Assert.Equal(30, nextEncounter.Dwarf.FixedDamage);
    }
}
