using System.Data;
using System.Text.RegularExpressions;
using Ashenfall.Api.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ashenfall.Api.Characters;

public sealed record GameplaySnapshotStats(int Level, int MaxHealth, int FixedDamage);
public sealed record CharacterGameplaySnapshot(int SchemaVersion, Guid CharacterId,
    long CharacterRevision, Guid ClassDefinitionId, Guid CatalogReleaseId,
    string CatalogVersion, string CatalogHash, GameplaySnapshotStats EffectiveStats);

public sealed class InvalidGameplaySnapshotException : Exception
{
    public InvalidGameplaySnapshotException() : base("Authoritative character data is unavailable or invalid.") { }
}

public static class GameplaySnapshotFactory
{
    // These are transport safety bounds aligned with current Godot export ranges,
    // not established long-term game balance limits.
    public const int MaxHealth = 10000;
    public const int MaxDamage = 1000;

    public static CharacterGameplaySnapshot Create(CharacterRow character, CharacterClass definition,
        CatalogRelease release, IReadOnlyList<LevelRuleRow> rules, IReadOnlyList<ItemModifierRow> modifiers)
    {
        if (character.Id == Guid.Empty || character.AccountId == Guid.Empty || character.ClassDefinitionId == Guid.Empty ||
            character.RowRevision < 1 || character.DeletedAt != null || definition.Id != character.ClassDefinitionId ||
            definition.CatalogReleaseId == Guid.Empty || definition.CatalogReleaseId != release.Id ||
            release.Status != CatalogStates.Published || release.SchemaMajor != 1 || release.PublishedAt is null ||
            string.IsNullOrWhiteSpace(release.VersionKey) || release.VersionKey.Length > 64 ||
            release.ContentSha256 is null || !Regex.IsMatch(release.ContentSha256, "^[0-9a-f]{64}$") ||
            definition.BaseMaxHealth <= 0 || definition.FixedDamage < 0 || rules.Count == 0 ||
            rules.Any(r => r.ClassDefinitionId != definition.Id))
            throw new InvalidGameplaySnapshotException();
        try
        {
            CharacterRules.ValidateAllocation(character.Experience, character.AllocatedStrength,
                character.AllocatedDexterity, character.AllocatedVitality, rules);
            var effective = CharacterRules.CalculateStats(definition.BaseMaxHealth, definition.FixedDamage,
                definition.BaseStrength ?? 0, definition.BaseDexterity ?? 0, definition.BaseVitality ?? 0,
                definition.BaseArmor ?? 0, character, rules, modifiers, rejectOutOfRange: true);
            if (effective.Level < 1 || effective.MaxHealth is < 1 or > MaxHealth || effective.Damage is < 0 or > MaxDamage)
                throw new InvalidGameplaySnapshotException();
            return new CharacterGameplaySnapshot(1, character.Id, character.RowRevision, definition.Id,
                release.Id, release.VersionKey, release.ContentSha256,
                new GameplaySnapshotStats(effective.Level, effective.MaxHealth, effective.Damage));
        }
        catch (Exception e) when (e is ArgumentException or OverflowException)
        {
            throw new InvalidGameplaySnapshotException();
        }
    }
}

public sealed class CharacterGameplaySnapshotReader(CharacterDbContext db)
{
    public async Task<CharacterGameplaySnapshot?> ReadAsync(string provider, string subject, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.IdentityProvider == provider && x.ProviderSubject == subject, ct);
        if (account is null) { await tx.CommitAsync(ct); return null; }
        var character = await db.Characters.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == account.Id && x.DeletedAt == null, ct);
        if (character is null) { await tx.CommitAsync(ct); return null; }
        var definition = await db.SnapshotClasses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == character.ClassDefinitionId, ct);
        if (definition is null) throw new InvalidGameplaySnapshotException();
        var release = await db.SnapshotReleases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == definition.CatalogReleaseId, ct);
        if (release is null) throw new InvalidGameplaySnapshotException();
        var rules = await db.LevelRules.AsNoTracking().Where(x => x.ClassDefinitionId == definition.Id).OrderBy(x => x.Level).ToListAsync(ct);
        var ids = await (from e in db.Equipment join i in db.OwnedItems on e.OwnedItemId equals i.Id
                         where e.CharacterId == character.Id && i.CharacterId == character.Id select i.ItemDefinitionId).ToListAsync(ct);
        var modifiers = await db.ItemModifiers.AsNoTracking().Where(x => ids.Contains(x.ItemDefinitionId)).ToListAsync(ct);
        var snapshot = GameplaySnapshotFactory.Create(character, definition, release, rules, modifiers);
        await tx.CommitAsync(ct);
        return snapshot;
    }
}
