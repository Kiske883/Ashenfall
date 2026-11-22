using System.Data;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Persistence;
using Ashenfall.Api.Catalog.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Ashenfall.CatalogPublisher;

public enum PublicationResult { Published, AlreadyPublished }

/// <summary>Trusted, offline publisher. All writes are in one database transaction.</summary>
public static class CatalogPublication
{
    public const string ApprovedHash = "a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17";
    // Two stable signed int32 keys identify the Ashenfall catalog publication lock.
    public const int LockNamespace = 0x41534846; // ASHF
    public const int LockResource = 0x43415431; // CAT1

    public static async Task<PublicationResult> PublishInitialAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var seed = InitialCatalogSeed.CreateDraft();
        ValidateApprovedSeed(seed);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await PreflightAsync(connection, cancellationToken);

        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options;
        await using var db = new CatalogDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await ExecuteAsync(connection, transaction, "SELECT pg_advisory_xact_lock(@namespace, @resource)", cancellationToken,
                new NpgsqlParameter("namespace", LockNamespace), new NpgsqlParameter("resource", LockResource));

            var release = await db.Releases.SingleOrDefaultAsync(x => x.VersionKey == seed.Release.VersionKey, cancellationToken);
            if (release is not null && release.Status != CatalogStates.Draft)
            {
                var existing = await LoadSnapshotAsync(db, release, cancellationToken);
                if (release.Status != CatalogStates.Published ||
                    release.ContentSha256 != ApprovedHash || CatalogHash.Compute(existing) != ApprovedHash ||
                    CatalogValidator.Validate(existing).Count != 0)
                    throw new InvalidOperationException("The version key is occupied by different or invalid content.");
                await transaction.CommitAsync(cancellationToken);
                return PublicationResult.AlreadyPublished;
            }

            if (release is null)
            {
                release = seed.Release;
                release.CreatedAt = DateTimeOffset.UtcNow;
                db.Releases.Add(release);
                await db.SaveChangesAsync(cancellationToken);
            }
            else if (release.Id != seed.Release.Id || release.SchemaMajor != seed.Release.SchemaMajor ||
                     release.ContentSha256 is not null || release.PublishedAt is not null)
                throw new InvalidOperationException("An incompatible draft already uses the version key.");

            var partial = await LoadSnapshotAsync(db, release, cancellationToken);
            EnsureMatchingSubset(partial, seed);
            db.Classes.AddRange(seed.Classes.Where(x => partial.Classes.All(y => y.Code != x.Code)));
            db.Enemies.AddRange(seed.Enemies.Where(x => partial.Enemies.All(y => y.Code != x.Code)));
            db.Items.AddRange(seed.Items.Where(x => partial.Items.All(y => y.Code != x.Code)));
            await db.SaveChangesAsync(cancellationToken);

            var complete = await LoadSnapshotAsync(db, release, cancellationToken);
            var errors = CatalogValidator.Validate(complete);
            if (errors.Count != 0 || CatalogHash.Compute(complete) != ApprovedHash)
                throw new InvalidOperationException("Draft validation or approved hash comparison failed.");

            await ExecuteAsync(connection, transaction, "SELECT id FROM catalog_releases WHERE id=@id FOR UPDATE",
                cancellationToken, new NpgsqlParameter("id", release.Id));
            var previous = await db.Releases.SingleOrDefaultAsync(x => x.Status == CatalogStates.Published, cancellationToken);
            if (previous is not null)
            {
                previous.Status = CatalogStates.Retired;
                await db.SaveChangesAsync(cancellationToken);
            }
            release.ContentSha256 = ApprovedHash;
            release.PublishedAt = DateTimeOffset.UtcNow;
            release.Status = CatalogStates.Published;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PublicationResult.Published;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public static void ValidateApprovedSeed(CatalogSnapshot seed)
    {
        if (seed.Release.VersionKey != InitialCatalogSeed.VersionKey ||
            CatalogValidator.Validate(seed).Count != 0 || CatalogHash.Compute(seed) != ApprovedHash)
            throw new InvalidOperationException("Initial seed differs from the reviewed catalog contract.");
    }

    public static void EnsureMatchingSubset(CatalogSnapshot partial, CatalogSnapshot seed)
    {
        if (partial.Release.VersionKey != seed.Release.VersionKey ||
            partial.Classes.Any(x => seed.Classes.All(y => y.Code != x.Code)) ||
            partial.Enemies.Any(x => seed.Enemies.All(y => y.Code != x.Code)) ||
            partial.Items.Any(x => seed.Items.All(y => y.Code != x.Code)))
            throw new InvalidOperationException("The draft contains unexpected definitions.");
        var expected = new CatalogSnapshot(seed.Release,
            seed.Classes.Where(x => partial.Classes.Any(y => y.Code == x.Code)).ToArray(),
            seed.Enemies.Where(x => partial.Enemies.Any(y => y.Code == x.Code)).ToArray(),
            seed.Items.Where(x => partial.Items.Any(y => y.Code == x.Code)).ToArray());
        if (CatalogHash.Compute(partial) != CatalogHash.Compute(expected))
            throw new InvalidOperationException("The existing draft differs from the approved seed.");
    }

    private static async Task<CatalogSnapshot> LoadSnapshotAsync(CatalogDbContext db, CatalogRelease release, CancellationToken ct) =>
        new(release,
            await db.Classes.AsNoTracking().Where(x => x.CatalogReleaseId == release.Id).ToListAsync(ct),
            await db.Enemies.AsNoTracking().Where(x => x.CatalogReleaseId == release.Id).ToListAsync(ct),
            await db.Items.AsNoTracking().Where(x => x.CatalogReleaseId == release.Id).ToListAsync(ct));

    private static async Task PreflightAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        const string sql = """
            SELECT current_database() = 'ashenfall_dev' AND current_user = 'ashenfall_publisher'
              AND EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115111756_InitialRpgCatalog')
              AND (SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename IN
                   ('catalog_releases','character_classes','enemy_definitions','item_definitions')) = 4
              AND (SELECT count(*) FROM pg_proc WHERE pronamespace='public'::regnamespace AND proname IN
                   ('catalog_guard_release','catalog_guard_definition')) = 2
              AND (SELECT count(*) FROM pg_trigger WHERE NOT tgisinternal AND tgenabled='O' AND tgname IN
                   ('catalog_release_guard','character_class_catalog_guard','enemy_definition_catalog_guard','item_definition_catalog_guard')) = 4
              AND EXISTS (SELECT 1 FROM pg_roles WHERE rolname=current_user AND rolcanlogin
                   AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls)
              AND has_database_privilege(current_user, 'ashenfall_dev', 'CONNECT')
              AND NOT has_database_privilege(current_user, 'ashenfall_dev', 'CREATE')
              AND NOT has_database_privilege(current_user, 'ashenfall_dev', 'TEMPORARY')
              AND has_schema_privilege(current_user, 'public', 'USAGE')
              AND NOT has_schema_privilege(current_user, 'public', 'CREATE')
              AND has_table_privilege(current_user, 'catalog_releases', 'SELECT')
              AND has_table_privilege(current_user, 'catalog_releases', 'INSERT')
              AND has_table_privilege(current_user, 'catalog_releases', 'UPDATE')
              AND NOT has_table_privilege(current_user, 'catalog_releases', 'DELETE,TRUNCATE')
              AND has_table_privilege(current_user, 'character_classes', 'SELECT')
              AND has_table_privilege(current_user, 'character_classes', 'INSERT')
              AND has_table_privilege(current_user, 'enemy_definitions', 'SELECT')
              AND has_table_privilege(current_user, 'enemy_definitions', 'INSERT')
              AND has_table_privilege(current_user, 'item_definitions', 'SELECT')
              AND has_table_privilege(current_user, 'item_definitions', 'INSERT')
              AND NOT has_table_privilege(current_user, 'character_classes', 'UPDATE,DELETE,TRUNCATE')
              AND NOT has_table_privilege(current_user, 'enemy_definitions', 'UPDATE,DELETE,TRUNCATE')
              AND NOT has_table_privilege(current_user, 'item_definitions', 'UPDATE,DELETE,TRUNCATE')
              AND (SELECT count(*) FROM catalog_releases WHERE version_key='prototype-1') <= 1;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        if (await command.ExecuteScalarAsync(ct) is not bool passed || !passed)
            throw new InvalidOperationException("Catalog schema, identity or privilege preflight failed.");
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, IDbContextTransaction transaction,
        string sql, CancellationToken ct, params NpgsqlParameter[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, (NpgsqlTransaction)transaction.GetDbTransaction());
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(ct);
    }
}
