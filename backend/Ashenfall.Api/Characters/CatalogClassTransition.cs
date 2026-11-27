using Ashenfall.Api.Catalog.Domain;

namespace Ashenfall.Api.Characters;

/// <summary>Explicit successor mapping; class codes alone do not establish identity.</summary>
public static class CatalogClassTransition
{
    public static CharacterClass Resolve(CharacterClass source, CatalogRelease sourceRelease,
        CatalogRelease activeRelease, IReadOnlyList<CharacterClass> candidates,
        IReadOnlyCollection<(Guid SourceId, Guid TargetId)> links)
    {
        if (source.Id == Guid.Empty || source.CatalogReleaseId != sourceRelease.Id ||
            sourceRelease.Status is not (CatalogStates.Published or CatalogStates.Retired) ||
            activeRelease.Status != CatalogStates.Published || activeRelease.Id == Guid.Empty)
            throw new InvalidOperationException("Invalid catalog transition context.");
        if (sourceRelease.Id == activeRelease.Id) return source;
        var mappedIds = links.Where(x => x.SourceId == source.Id).Select(x => x.TargetId).ToHashSet();
        var matches = candidates.Where(x => mappedIds.Contains(x.Id) && x.CatalogReleaseId == activeRelease.Id && x.Code == source.Code).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("No unique active class successor.");
        return matches[0];
    }
}
