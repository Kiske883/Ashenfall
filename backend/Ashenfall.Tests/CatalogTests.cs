using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Persistence;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace Ashenfall.Tests;

public sealed class CatalogTests
{
    [Fact]
    public void SeedMatchesApprovedFixedGameplayAndIsComplete()
    {
        var seed = InitialCatalogSeed.CreateDraft();
        Assert.Empty(CatalogValidator.Validate(seed));
        var dwarf = Assert.Single(seed.Classes);
        Assert.Equal((100, 25, 2.5m, 1.45m, 1700, 940),
            (dwarf.BaseMaxHealth, dwarf.FixedDamage, dwarf.MoveSpeedMps, dwarf.MeleeRangeM, dwarf.AttackCycleMs, dwarf.ImpactMs));
        var guardian = Assert.Single(seed.Enemies);
        Assert.Equal((100, 10, 600, 1000, 100, 1.0m, 0.7m),
            (guardian.MaxHealth, guardian.FixedDamage, guardian.WindupMs, guardian.RecoveryMs,
                guardian.ImpactAfterExecuteMs, guardian.ChaseSpeedMps, guardian.ReturnSpeedMps));
        Assert.All(seed.Items, x => Assert.Equal("common", x.Rarity));
        Assert.Equal(new short?[] { 1, 2 }, seed.Items.Select(x => x.HandRequirement).Order().ToArray());
        Assert.All(seed.Classes, x => Assert.Null(x.BaseStrength));
        Assert.All(seed.Enemies, x => Assert.Null(x.BaseArmor));
    }

    [Fact]
    public void ValidatorRejectsInvalidHandAndTiming()
    {
        var seed = InitialCatalogSeed.CreateDraft();
        seed.Items.Single(x => x.Code == "training_battle_axe").EquipSlot = "off_hand";
        seed.Classes.Single().ImpactMs = 1800;
        Assert.Contains(CatalogValidator.Validate(seed), x => x.Contains("item"));
        Assert.Contains(CatalogValidator.Validate(seed), x => x.Contains("class"));
    }

    [Fact]
    public void HashIsStableUnderInputOrderingAndDecimalScale()
    {
        var seed = InitialCatalogSeed.CreateDraft();
        string first = CatalogHash.Compute(seed);
        Assert.Equal("a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17", first);
        var reordered = new CatalogSnapshot(seed.Release, seed.Classes.Reverse().ToArray(),
            seed.Enemies.Reverse().ToArray(), seed.Items.Reverse().ToArray());
        Assert.Equal(first, CatalogHash.Compute(reordered));
        seed.Classes.Single().MoveSpeedMps = 2.500m;
        Assert.Equal(first, CatalogHash.Compute(seed));
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Fact]
    public void HashDetectsStatsDefinitionsNullsAndVisualKeys()
    {
        var seed = InitialCatalogSeed.CreateDraft();
        string baseline = CatalogHash.Compute(seed);
        seed.Classes.Single().FixedDamage++;
        Assert.Equal("26f978ea12411b35176f7b2ab335e026de67b9465d7dcf9fe65ef5162e3bd11b", CatalogHash.Compute(seed));
        Assert.NotEqual(baseline, CatalogHash.Compute(seed));
        seed.Classes.Single().FixedDamage--;
        seed.Classes.Single().BaseStrength = 0;
        Assert.NotEqual(baseline, CatalogHash.Compute(seed));
        seed.Classes.Single().BaseStrength = null;
        seed.Items.Single(x => x.Code == "guardian_sword").VisualKey = "different_visual";
        Assert.NotEqual(baseline, CatalogHash.Compute(seed));
        seed.Items.Single(x => x.Code == "guardian_sword").VisualKey = "guardian_sword";
        Assert.NotEqual(baseline, CatalogHash.Compute(seed with { Items = seed.Items.Take(1).ToArray() }));
    }

    [Fact]
    public void SameSeedVersionIsIdempotentAndChangedVersionContentConflicts()
    {
        var candidate = InitialCatalogSeed.CreateDraft();
        Assert.Equal(SeedDisposition.CreateDraft, InitialCatalogSeed.CompareExisting(null, candidate));
        Assert.Equal(SeedDisposition.AlreadyPresent,
            InitialCatalogSeed.CompareExisting(InitialCatalogSeed.CreateDraft(), candidate));
        candidate.Enemies.Single().FixedDamage = 11;
        Assert.Equal(SeedDisposition.VersionConflict,
            InitialCatalogSeed.CompareExisting(InitialCatalogSeed.CreateDraft(), candidate));
    }

    [Fact]
    public void PublicationLifecycleRejectsSkippingAndMutation()
    {
        var draft = InitialCatalogSeed.CreateDraft().Release;
        Assert.True(CatalogLifecycle.CanInsert(draft));
        var published = new CatalogRelease
        {
            Id = draft.Id, VersionKey = draft.VersionKey, SchemaMajor = draft.SchemaMajor,
            CreatedAt = draft.CreatedAt, Status = CatalogStates.Published,
            PublishedAt = DateTimeOffset.UnixEpoch,
            ContentSha256 = CatalogHash.Compute(InitialCatalogSeed.CreateDraft())
        };
        Assert.True(CatalogLifecycle.CanTransition(draft, published));
        var retired = new CatalogRelease
        {
            Id = published.Id, VersionKey = published.VersionKey, SchemaMajor = published.SchemaMajor,
            CreatedAt = published.CreatedAt, Status = CatalogStates.Retired,
            PublishedAt = published.PublishedAt, ContentSha256 = published.ContentSha256
        };
        Assert.True(CatalogLifecycle.CanTransition(published, retired));
        Assert.False(CatalogLifecycle.CanTransition(draft, retired));
        Assert.False(CatalogLifecycle.CanTransition(retired, published));
        retired.ContentSha256 = new string('a', 64);
        Assert.False(CatalogLifecycle.CanTransition(published, retired));
    }

    [Fact]
    public void EfModelAndMigrationContainOnlyFourCatalogTablesAndGuards()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql().Options;
        using var db = new CatalogDbContext(options);
        Assert.Equal(4, db.Model.GetEntityTypes().Count());
        var release = db.Model.FindEntityType(typeof(CatalogRelease))!;
        var publishedIndex = Assert.Single(release.GetIndexes(), x => x.GetDatabaseName() == "catalog_one_published");
        Assert.True(publishedIndex.IsUnique);
        Assert.Equal("status = 'published'", publishedIndex.GetFilter());
        var item = db.Model.FindEntityType(typeof(ItemDefinition))!;
        Assert.Equal("smallint", item.FindProperty(nameof(ItemDefinition.HandRequirement))!.GetColumnType());

        var migration = new InitialRpgCatalog();
        var names = migration.UpOperations.OfType<CreateTableOperation>().Select(x => x.Name).Order().ToArray();
        Assert.Equal(new[] { "catalog_releases", "character_classes", "enemy_definitions", "item_definitions" }, names);
        string guardSql = string.Join('\n', migration.UpOperations.OfType<SqlOperation>().Select(x => x.Sql));
        Assert.Contains("catalog_guard_release", guardSql);
        Assert.Contains("catalog_guard_definition", guardSql);
        Assert.Equal(4, guardSql.Split("CREATE TRIGGER").Length - 1);
        Assert.Equal(4, migration.DownOperations.OfType<DropTableOperation>().Count());
    }
}
