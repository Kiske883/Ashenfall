using Ashenfall.Api.Catalog.Domain;

namespace Ashenfall.Api.Catalog.Services;

/// <summary>Domain-side mirror of the reviewed PostgreSQL release guard.</summary>
public static class CatalogLifecycle
{
    public static bool CanInsert(CatalogRelease release) =>
        release.Id != Guid.Empty && release.Status == CatalogStates.Draft &&
        release.PublishedAt is null;

    public static bool CanTransition(CatalogRelease old, CatalogRelease next)
    {
        if (old.Id != next.Id || old.VersionKey != next.VersionKey ||
            old.SchemaMajor != next.SchemaMajor || old.CreatedAt != next.CreatedAt)
            return false;
        if (old.Status == CatalogStates.Draft && next.Status == CatalogStates.Draft)
            return next.PublishedAt is null;
        if (old.Status == CatalogStates.Draft && next.Status == CatalogStates.Published)
            return next.PublishedAt is not null && IsHash(next.ContentSha256);
        if (old.Status == CatalogStates.Published && next.Status == CatalogStates.Retired)
            return old.PublishedAt == next.PublishedAt &&
                old.ContentSha256 == next.ContentSha256;
        return false;
    }

    public static bool IsHash(string? value) => value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
