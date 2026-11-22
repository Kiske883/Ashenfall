using System.Security.Cryptography;
using System.Text.Json;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Services;

namespace Ashenfall.Api.Catalog.Endpoints;

public static class CatalogEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/catalog/version", GetVersion);
        routes.MapGet("/api/v1/catalog/snapshot", GetSnapshot);
        return routes;
    }

    private static async Task<IResult> GetVersion(ICatalogReader reader, ILoggerFactory logs,
        CancellationToken cancellationToken)
    {
        var log = logs.CreateLogger("CatalogAvailability");
        try
        {
            var release = await reader.GetActiveReleaseAsync(cancellationToken);
            if (release is null)
            {
                log.LogWarning("Catalog unavailable: no published release.");
                return Unavailable();
            }
            return Results.Json(new
            {
                schemaMajor = release.SchemaMajor,
                catalogVersion = release.VersionKey,
                contentSha256 = release.ContentSha256,
                publishedAt = release.PublishedAt
            });
        }
        catch (CatalogUnavailableException)
        {
            log.LogWarning("Catalog unavailable: configuration or database read failed; see startup/database category logs.");
            return Unavailable();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            log.LogError("Catalog unavailable: unexpected backend failure.");
            return Unavailable();
        }
    }

    private static async Task<IResult> GetSnapshot(
        HttpContext context, ICatalogReader reader, ILoggerFactory logs, string? version,
        CancellationToken cancellationToken)
    {
        var log = logs.CreateLogger("CatalogAvailability");
        if (string.IsNullOrWhiteSpace(version) || version.Length > 64)
            return Results.Problem(statusCode: 400, title: "A valid version query is required.", type: "catalog/invalid-version");
        try
        {
            var snapshot = await reader.GetSnapshotAsync(version, cancellationToken);
            if (snapshot is null)
                return Results.Problem(statusCode: 404, title: "Catalog version not found.", type: "catalog/not-found");
            byte[] bytes = SerializeSnapshot(snapshot);
            var etag = $"\"{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}\"";
            context.Response.Headers.ETag = etag;
            context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            if (context.Request.Headers.IfNoneMatch.Any(x => x == etag))
                return Results.StatusCode(304);
            return Results.Bytes(bytes, "application/json; charset=utf-8");
        }
        catch (CatalogUnavailableException)
        {
            log.LogWarning("Catalog unavailable: configuration or database read failed; see startup/database category logs.");
            return Unavailable();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            log.LogError("Catalog unavailable: unexpected backend failure.");
            return Unavailable();
        }
    }

    private static byte[] SerializeSnapshot(CatalogSnapshot snapshot) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schemaMajor = snapshot.Release.SchemaMajor,
        catalogVersion = snapshot.Release.VersionKey,
        contentSha256 = snapshot.Release.ContentSha256,
        classes = snapshot.Classes.OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => new
        {
            x.Id, x.Code, x.DisplayName, x.VisualKey, x.BaseMaxHealth, x.FixedDamage,
            x.BaseStrength, x.BaseDexterity, x.BaseVitality, x.BaseArmor,
            x.MoveSpeedMps, x.MeleeRangeM, x.AttackCycleMs, x.ImpactMs
        }),
        enemies = snapshot.Enemies.OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => new
        {
            x.Id, x.Code, x.DisplayName, x.VisualKey, x.MaxHealth, x.FixedDamage,
            x.BaseStrength, x.BaseDexterity, x.BaseVitality, x.BaseArmor,
            x.AttackRangeM, x.WindupMs, x.RecoveryMs, x.ImpactAfterExecuteMs,
            x.ChaseSpeedMps, x.ReturnSpeedMps, x.DetectionRadiusM,
            x.DisengagementRadiusM, x.MaxSpawnDistanceM, x.StoppingDistanceM, x.ResumeDistanceM
        }),
        items = snapshot.Items.OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => new
        {
            x.Id, x.Code, x.DisplayName, x.Category, x.Rarity, x.Stackable,
            x.MaxStack, x.EquipSlot, x.HandRequirement, x.VisualKey
        })
    }, JsonOptions);

    private static IResult Unavailable() => Results.Problem(
        statusCode: 503, title: "Catalog unavailable.", type: "catalog/unavailable");
}
