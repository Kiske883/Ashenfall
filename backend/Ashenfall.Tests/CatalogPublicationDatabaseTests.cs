using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Persistence;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.CatalogPublisher;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Ashenfall.Tests;

public sealed class PublisherFactAttribute : FactAttribute
{
    public PublisherFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ASHENFALL_RUN_DB_TESTS") != "1")
            Skip = "Run only after the approved seed is published with ASHENFALL_RUN_DB_TESTS=1.";
    }
}

/// <summary>Opt-in checks against an already published development catalog. Test writes roll back.</summary>
public sealed class CatalogPublicationDatabaseTests
{
    private static string ConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("ASHENFALL_PUBLISHER_CONNECTION");
        var settings = string.IsNullOrWhiteSpace(configured)
            ? new NpgsqlConnectionStringBuilder
            {
                Host = "localhost", Port = 5432, Database = "ashenfall_dev", Username = "ashenfall_publisher"
            }
            : new NpgsqlConnectionStringBuilder(configured);
        if (settings.Database != "ashenfall_dev" || settings.Username != "ashenfall_publisher" ||
            !string.IsNullOrEmpty(settings.Password))
            throw new InvalidOperationException("Database test identity must be the private publisher role without an inline password.");
        return settings.ConnectionString;
    }

    [PublisherFact]
    public async Task PublishedSnapshotIsConsistentAndRepeatIsNoOp()
    {
        string cs = ConnectionString();
        await using var db = NewContext(cs);
        var release = await db.Releases.AsNoTracking().SingleAsync(x => x.VersionKey == InitialCatalogSeed.VersionKey);
        Assert.Equal(CatalogStates.Published, release.Status);
        var snapshot = await SnapshotAsync(db, release);
        Assert.Empty(CatalogValidator.Validate(snapshot));
        Assert.Equal(CatalogPublication.ApprovedHash, release.ContentSha256);
        Assert.Equal(CatalogPublication.ApprovedHash, CatalogHash.Compute(snapshot));
        Assert.Single(snapshot.Classes);
        Assert.Single(snapshot.Enemies);
        Assert.Equal(2, snapshot.Items.Count);
        Assert.Equal(PublicationResult.AlreadyPublished, await CatalogPublication.PublishInitialAsync(cs));
        Assert.Single(await db.Releases.AsNoTracking().Where(x => x.Status == CatalogStates.Published).ToListAsync());
    }

    [PublisherFact]
    public async Task ConcurrentReplaysAreSerializedAndIdempotent()
    {
        string cs = ConnectionString();
        var results = await Task.WhenAll(
            CatalogPublication.PublishInitialAsync(cs), CatalogPublication.PublishInitialAsync(cs));
        Assert.All(results, x => Assert.Equal(PublicationResult.AlreadyPublished, x));
        await using var db = NewContext(cs);
        Assert.Single(await db.Releases.AsNoTracking().Where(x => x.Status == CatalogStates.Published).ToListAsync());
    }

    [PublisherFact]
    public async Task InvalidDraftWriteIsRolledBack()
    {
        string cs = ConnectionString();
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        var id = Guid.NewGuid();
        var version = "rollback-" + Guid.NewGuid().ToString("N");
        await using (var command = new NpgsqlCommand(
            "INSERT INTO catalog_releases(id,version_key,schema_major,status,created_at) VALUES (@id,@version,1,'draft',now())",
            connection, tx))
        {
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("version", version);
            await command.ExecuteNonQueryAsync();
        }
        await using (var bad = new NpgsqlCommand(
            "INSERT INTO character_classes(id,catalog_release_id,code,display_name,visual_key,base_max_health,fixed_damage,move_speed_mps,melee_range_m,attack_cycle_ms,impact_ms) " +
            "VALUES (@id,@release,'bad_class','Bad Class','dwarf_berserker',-1,25,2.5,1.45,1700,940)", connection, tx))
        {
            bad.Parameters.AddWithValue("id", Guid.NewGuid());
            bad.Parameters.AddWithValue("release", id);
            await Assert.ThrowsAsync<PostgresException>(() => bad.ExecuteNonQueryAsync());
        }
        await tx.RollbackAsync();
        await using var db = NewContext(cs);
        Assert.False(await db.Releases.AnyAsync(x => x.VersionKey == version));
    }

    [PublisherFact]
    public async Task PublishedReleaseCannotBeReopened()
    {
        string cs = ConnectionString();
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE catalog_releases SET status='draft' WHERE version_key='prototype-1'", connection, tx);
        await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        await tx.RollbackAsync();
    }

    private static CatalogDbContext NewContext(string cs) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(cs).Options);

    private static async Task<CatalogSnapshot> SnapshotAsync(CatalogDbContext db, CatalogRelease release) =>
        new(release,
            await db.Classes.AsNoTracking().Where(x => x.CatalogReleaseId == release.Id).ToListAsync(),
            await db.Enemies.AsNoTracking().Where(x => x.CatalogReleaseId == release.Id).ToListAsync(),
            await db.Items.AsNoTracking().Where(x => x.CatalogReleaseId == release.Id).ToListAsync());
}
