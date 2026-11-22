using System.Data;
using System.Data.Common;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Net.Sockets;

namespace Ashenfall.Api.Catalog.Services;

public interface ICatalogReader
{
    Task<CatalogRelease?> GetActiveReleaseAsync(CancellationToken cancellationToken);
    Task<CatalogSnapshot?> GetSnapshotAsync(string versionKey, CancellationToken cancellationToken);
}

public sealed class CatalogUnavailableException : Exception
{
    public CatalogUnavailableException() : base("Catalog is unavailable.") { }
}

public sealed class UnconfiguredCatalogReader : ICatalogReader
{
    public Task<CatalogRelease?> GetActiveReleaseAsync(CancellationToken cancellationToken) => throw new CatalogUnavailableException();
    public Task<CatalogSnapshot?> GetSnapshotAsync(string versionKey, CancellationToken cancellationToken) => throw new CatalogUnavailableException();
}

/// <summary>Read-only catalog query with one PostgreSQL snapshot per response.</summary>
public sealed class CatalogReader(CatalogDbContext db, ILogger<CatalogReader> logger) : ICatalogReader
{
    public async Task<CatalogRelease?> GetActiveReleaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await db.Releases.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Status == CatalogStates.Published, cancellationToken);
        }
        catch (Exception error) when (FindDatabaseException(error) is not null)
        {
            LogDatabaseFailure(FindDatabaseException(error)!);
            throw new CatalogUnavailableException();
        }
    }

    public async Task<CatalogSnapshot?> GetSnapshotAsync(string versionKey, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
            var release = await db.Releases.AsNoTracking().SingleOrDefaultAsync(
                x => x.VersionKey == versionKey &&
                     (x.Status == CatalogStates.Published || x.Status == CatalogStates.Retired), cancellationToken);
            if (release is null) return null;
            var classes = await db.Classes.AsNoTracking().Where(x => x.CatalogReleaseId == release.Id).ToListAsync(cancellationToken);
            var enemies = await db.Enemies.AsNoTracking().Where(x => x.CatalogReleaseId == release.Id).ToListAsync(cancellationToken);
            var items = await db.Items.AsNoTracking().Where(x => x.CatalogReleaseId == release.Id).ToListAsync(cancellationToken);
            var snapshot = new CatalogSnapshot(release, classes, enemies, items);
            if (CatalogValidator.Validate(snapshot).Count != 0 ||
                !string.Equals(CatalogHash.Compute(snapshot), release.ContentSha256, StringComparison.Ordinal))
                throw new CatalogUnavailableException();
            await transaction.CommitAsync(cancellationToken);
            return snapshot;
        }
        catch (CatalogUnavailableException)
        {
            logger.LogError("Published catalog snapshot failed validation or hash verification.");
            throw;
        }
        catch (Exception error) when (FindDatabaseException(error) is not null)
        {
            LogDatabaseFailure(FindDatabaseException(error)!);
            throw new CatalogUnavailableException();
        }
    }

    private static DbException? FindDatabaseException(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is DbException database) return database;
        return null;
    }

    private void LogDatabaseFailure(DbException error)
    {
        // Never log the exception itself: provider messages may include connection details.
        string category = error switch
        {
            PostgresException { SqlState: "28P01" } => "authentication failed",
            PostgresException { SqlState: "42501" } => "insufficient catalog permissions",
            PostgresException { SqlState: "3D000" } => "database does not exist",
            NpgsqlException { InnerException: SocketException { SocketErrorCode: SocketError.ConnectionRefused } }
                => "PostgreSQL connection refused",
            NpgsqlException { InnerException: TimeoutException } => "PostgreSQL connection timed out",
            NpgsqlException => "PostgreSQL connection or query unavailable",
            _ => "catalog database unavailable"
        };
        logger.LogWarning("Catalog database request failed: {Category}.", category);
    }
}
