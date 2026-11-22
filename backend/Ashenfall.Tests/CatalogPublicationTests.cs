using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.CatalogPublisher;
using Xunit;

namespace Ashenfall.Tests;

public sealed class CatalogPublicationTests
{
    [Fact]
    public void ApprovedSeedMatchesReviewedHash()
    {
        var seed = InitialCatalogSeed.CreateDraft();
        CatalogPublication.ValidateApprovedSeed(seed);
        Assert.Equal(CatalogPublication.ApprovedHash, CatalogHash.Compute(seed));
    }

    [Fact]
    public void InvalidSeedCannotBePublished()
    {
        var seed = InitialCatalogSeed.CreateDraft();
        seed.Enemies.Single().FixedDamage = -1;
        Assert.Throws<InvalidOperationException>(() => CatalogPublication.ValidateApprovedSeed(seed));
    }

    [Fact]
    public void MatchingPartialDraftCanBeCompleted()
    {
        var seed = InitialCatalogSeed.CreateDraft();
        var partial = new CatalogSnapshot(seed.Release, seed.Classes, Array.Empty<EnemyDefinition>(),
            seed.Items.Where(x => x.Code == "guardian_sword").ToArray());
        CatalogPublication.EnsureMatchingSubset(partial, seed);
    }

    [Fact]
    public void ChangedOrUnexpectedDraftIsRejected()
    {
        var seed = InitialCatalogSeed.CreateDraft();
        var partial = new CatalogSnapshot(seed.Release, seed.Classes, seed.Enemies, seed.Items);
        seed.Classes.Single().FixedDamage = 26;
        Assert.Throws<InvalidOperationException>(() => CatalogPublication.EnsureMatchingSubset(partial,
            InitialCatalogSeed.CreateDraft()));
        var extra = InitialCatalogSeed.CreateDraft();
        extra.Items.First().Code = "unreviewed_sword";
        Assert.Throws<InvalidOperationException>(() => CatalogPublication.EnsureMatchingSubset(
            new CatalogSnapshot(extra.Release, extra.Classes, extra.Enemies, extra.Items),
            InitialCatalogSeed.CreateDraft()));
    }
}
