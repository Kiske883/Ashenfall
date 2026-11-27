using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Services;

namespace Ashenfall.Api.Characters;

public sealed record GameplaySnapshotStatsV2(int Level, int MaxHealth, int FixedDamage,
    int MinDamage, int MaxDamage, decimal CriticalChancePercent, decimal CriticalDamageBonusPercent);
public sealed record CharacterGameplaySnapshotV2(int SchemaVersion, Guid CharacterId, long CharacterRevision,
    Guid ClassDefinitionId, Guid CatalogReleaseId, string CatalogVersion, string CatalogHash, GameplaySnapshotStatsV2 EffectiveStats);

/// <summary>Builds a V2 response only from a complete hash-verified published catalog view.</summary>
public static class GameplaySnapshotV2Factory
{
    public static CharacterGameplaySnapshotV2 Create(CharacterRow character, CharacterClass originalClass,
        CatalogRelease originalRelease, CatalogSnapshot active, IReadOnlyCollection<CatalogCombatClassV2> combat,
        IReadOnlyCollection<CatalogProgressionV2> progression, IReadOnlyCollection<CatalogModifierV2> modifiers,
        IReadOnlyCollection<(Guid SourceId, Guid TargetId)> classLinks, IReadOnlyCollection<Guid> equippedItemIds,
        string manifestHash, IReadOnlyCollection<(Guid SourceId, Guid TargetId)>? itemLinks = null,
        IReadOnlyCollection<ItemDefinition>? historicalItems = null,
        IReadOnlyCollection<CatalogRelease>? historicalItemReleases = null)
    {
        try
        {
            itemLinks ??= Array.Empty<(Guid SourceId, Guid TargetId)>();
            historicalItems ??= Array.Empty<ItemDefinition>();
            historicalItemReleases ??= Array.Empty<CatalogRelease>();
            if (character.Id == Guid.Empty || character.AccountId == Guid.Empty || character.RowRevision < 1 ||
                character.DeletedAt != null || character.ClassDefinitionId != originalClass.Id ||
                active.Release.Status != CatalogStates.Published || active.Release.PublishedAt is null ||
                string.IsNullOrWhiteSpace(manifestHash) ||
                !string.Equals(CombatCatalogHashV2.Compute(active, combat, progression, modifiers, classLinks, itemLinks), manifestHash, StringComparison.Ordinal))
                throw new InvalidGameplaySnapshotException();
            var resolved = CatalogClassTransition.Resolve(originalClass, originalRelease, active.Release, active.Classes, classLinks);
            var definition = combat.Single(x => x.ClassId == resolved.Id);
            var levels = progression.Where(x => x.ClassId == resolved.Id).OrderBy(x => x.Level).ToArray();
            var rules = levels.Select(x => new LevelRuleRow { ClassDefinitionId = x.ClassId, Level = x.Level,
                RequiredTotalXp = x.RequiredTotalXp, HealthBonus = x.HealthBonus, DamageBonus = x.DamageBonus, AttributePoints = x.AttributePoints }).ToArray();
            CharacterRules.ValidateAllocation(character.Experience, character.AllocatedStrength, character.AllocatedDexterity, character.AllocatedVitality, rules);
            var currentLevel = CharacterRules.DeriveLevel(character.Experience, rules);
            var level = levels.Single(x => x.Level == currentLevel);
            var resolvedItems = equippedItemIds.Select(id => {
                var direct = active.Items.SingleOrDefault(x => x.Id == id);
                if (direct is not null) return direct.Id;
                var source = historicalItems.SingleOrDefault(x => x.Id == id) ?? throw new InvalidGameplaySnapshotException();
                var historicalRelease = historicalItemReleases.SingleOrDefault(x => x.Id == source.CatalogReleaseId);
                if (historicalRelease is null || historicalRelease.Status is not (CatalogStates.Published or CatalogStates.Retired) || historicalRelease.Id == active.Release.Id)
                    throw new InvalidGameplaySnapshotException();
                var links = itemLinks.Where(x => x.SourceId == id).ToArray();
                if (links.Length != 1) throw new InvalidGameplaySnapshotException();
                var target = active.Items.SingleOrDefault(x => x.Id == links[0].TargetId) ?? throw new InvalidGameplaySnapshotException();
                if (source.Code != target.Code || source.Category != target.Category || source.EquipSlot != target.EquipSlot || source.HandRequirement != target.HandRequirement || source.Stackable != target.Stackable || source.MaxStack != target.MaxStack)
                    throw new InvalidGameplaySnapshotException();
                return target.Id;
            }).ToArray();
            var effectiveItems = resolvedItems.SelectMany(id => modifiers.Where(x => x.ItemId == id)).Select(x => new ItemModifierRow {
                ItemDefinitionId = x.ItemId, StatCode = x.StatCode, ModifierKind = x.ModifierKind, Amount = x.Amount }).ToArray();
            var stats = CharacterRules.CalculateStats(resolved.BaseMaxHealth, resolved.FixedDamage,
                resolved.BaseStrength ?? 0, resolved.BaseDexterity ?? 0, resolved.BaseVitality ?? 0, resolved.BaseArmor ?? 0,
                character, rules, effectiveItems, rejectOutOfRange: true);
            var fight = CharacterRules.CalculateCombatStats(checked(definition.MinDamage + level.MinDamageBonus),
                checked(definition.MaxDamage + level.MaxDamageBonus), definition.CriticalChancePercent,
                definition.CriticalDamageBonusPercent, level.DamageBonus, level.CriticalChanceBonusPercent,
                level.CriticalDamageBonusPercent, effectiveItems);
            if (stats.MaxHealth is < 1 or > GameplaySnapshotFactory.MaxHealth || stats.Damage is < 0 or > GameplaySnapshotFactory.MaxDamage ||
                fight.MaxDamage * (1m + fight.CriticalDamageBonusPercent / 100m) > int.MaxValue)
                throw new InvalidGameplaySnapshotException();
            return new CharacterGameplaySnapshotV2(2, character.Id, character.RowRevision, character.ClassDefinitionId,
                active.Release.Id, active.Release.VersionKey, manifestHash,
                new GameplaySnapshotStatsV2(stats.Level, stats.MaxHealth, stats.Damage, fight.MinDamage, fight.MaxDamage,
                    fight.CriticalChancePercent, fight.CriticalDamageBonusPercent));
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or OverflowException)
        { throw new InvalidGameplaySnapshotException(); }
    }
}
