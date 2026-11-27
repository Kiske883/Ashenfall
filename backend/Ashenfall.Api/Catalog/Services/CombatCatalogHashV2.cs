using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ashenfall.Api.Catalog.Domain;

namespace Ashenfall.Api.Catalog.Services;

public sealed record CatalogCombatClassV2(Guid ClassId, int MinDamage, int MaxDamage,
    decimal CriticalChancePercent, decimal CriticalDamageBonusPercent);
public sealed record CatalogProgressionV2(Guid ClassId, int Level, long RequiredTotalXp,
    int HealthBonus, int DamageBonus, int AttributePoints, int MinDamageBonus, int MaxDamageBonus,
    decimal CriticalChanceBonusPercent, decimal CriticalDamageBonusPercent);
public sealed record CatalogModifierV2(Guid ItemId, string StatCode, string ModifierKind, decimal Amount);

/// <summary>Versioned canonical integrity for V1 catalog content plus V2 combat extensions.</summary>
public static class CombatCatalogHashV2
{
    public const string Format = "ASHENFALL-CATALOG-CONTENT-V2";

    public static string Compute(CatalogSnapshot catalog, IReadOnlyCollection<CatalogCombatClassV2> classes,
        IReadOnlyCollection<CatalogProgressionV2> levels, IReadOnlyCollection<CatalogModifierV2> modifiers,
        IReadOnlyCollection<(Guid SourceId, Guid TargetId)>? classLinks = null,
        IReadOnlyCollection<(Guid SourceId, Guid TargetId)>? itemLinks = null)
    {
        classLinks ??= Array.Empty<(Guid SourceId, Guid TargetId)>();
        itemLinks ??= Array.Empty<(Guid SourceId, Guid TargetId)>();
        if (CatalogValidator.Validate(catalog).Count != 0) throw new ArgumentException("Invalid catalog.");
        var classIds = catalog.Classes.Select(x => x.Id).ToHashSet();
        var itemIds = catalog.Items.Select(x => x.Id).ToHashSet();
        if (classes.Count != classIds.Count || classes.Select(x => x.ClassId).Distinct().Count() != classes.Count ||
            classes.Any(x => !classIds.Contains(x.ClassId) || x.MinDamage < 0 || x.MaxDamage < x.MinDamage ||
                x.MaxDamage == int.MaxValue || x.CriticalChancePercent is < 0 or > 100 || x.CriticalDamageBonusPercent < 0) ||
            levels.Any(x => !classIds.Contains(x.ClassId) || x.Level < 1 || x.RequiredTotalXp < 0 || x.HealthBonus < 0 || x.DamageBonus < 0 ||
                x.AttributePoints < 0 || x.MinDamageBonus < 0 || x.MaxDamageBonus < 0 || x.CriticalChanceBonusPercent < 0 || x.CriticalDamageBonusPercent < 0) ||
            levels.GroupBy(x => (x.ClassId, x.Level)).Any(x => x.Count() != 1) ||
            modifiers.Any(x => !itemIds.Contains(x.ItemId) || string.IsNullOrWhiteSpace(x.StatCode) || x.ModifierKind is not ("flat" or "percent")) ||
            modifiers.GroupBy(x => (x.ItemId, x.StatCode, x.ModifierKind)).Any(x => x.Count() != 1) ||
            classLinks.Any(x => x.SourceId == Guid.Empty || x.SourceId == x.TargetId || !classIds.Contains(x.TargetId)) ||
            classLinks.GroupBy(x => x.SourceId).Any(x => x.Count() != 1) ||
            itemLinks.Any(x => x.SourceId == Guid.Empty || x.SourceId == x.TargetId || !itemIds.Contains(x.TargetId)) ||
            itemLinks.GroupBy(x => x.SourceId).Any(x => x.Count() != 1))
            throw new ArgumentException("Invalid V2 catalog relationships or values.");
        foreach (var id in classIds)
        {
            var rules = levels.Where(x => x.ClassId == id).OrderBy(x => x.Level).ToArray();
            if (rules.Length == 0 || rules[0].RequiredTotalXp != 0 ||
                rules.Where((x, i) => x.Level != i + 1 || (i > 0 && x.RequiredTotalXp <= rules[i - 1].RequiredTotalXp)).Any())
                throw new ArgumentException("Invalid V2 progression.");
        }
        using var data = new MemoryStream();
        using var writer = new BinaryWriter(data, new UTF8Encoding(false, true), leaveOpen: true);
        void Text(string value) { var bytes = Encoding.UTF8.GetBytes(value.Normalize(NormalizationForm.FormC)); writer.Write(bytes.Length); writer.Write(bytes); }
        void Number(decimal value) => Text(value.ToString("G29", CultureInfo.InvariantCulture));
        Text(Format); Text(catalog.Release.Id.ToString("D")); Text(CatalogHash.Compute(catalog));
        writer.Write(classes.Count);
        foreach (var x in classes.OrderBy(x => x.ClassId.ToString("D"), StringComparer.Ordinal))
        { Text(x.ClassId.ToString("D")); writer.Write(x.MinDamage); writer.Write(x.MaxDamage); Number(x.CriticalChancePercent); Number(x.CriticalDamageBonusPercent); }
        writer.Write(levels.Count);
        foreach (var x in levels.OrderBy(x => x.ClassId.ToString("D"), StringComparer.Ordinal).ThenBy(x => x.Level))
        { Text(x.ClassId.ToString("D")); writer.Write(x.Level); writer.Write(x.RequiredTotalXp); writer.Write(x.HealthBonus); writer.Write(x.DamageBonus); writer.Write(x.AttributePoints); writer.Write(x.MinDamageBonus); writer.Write(x.MaxDamageBonus); Number(x.CriticalChanceBonusPercent); Number(x.CriticalDamageBonusPercent); }
        writer.Write(modifiers.Count);
        foreach (var x in modifiers.OrderBy(x => x.ItemId.ToString("D"), StringComparer.Ordinal).ThenBy(x => x.StatCode, StringComparer.Ordinal).ThenBy(x => x.ModifierKind, StringComparer.Ordinal))
        { Text(x.ItemId.ToString("D")); Text(x.StatCode); Text(x.ModifierKind); Number(x.Amount); }
        writer.Write(classLinks.Count);
        foreach (var link in classLinks.OrderBy(x => x.SourceId.ToString("D"), StringComparer.Ordinal).ThenBy(x => x.TargetId.ToString("D"), StringComparer.Ordinal))
        { Text(link.SourceId.ToString("D")); Text(link.TargetId.ToString("D")); }
        writer.Write(itemLinks.Count);
        foreach (var link in itemLinks.OrderBy(x => x.SourceId.ToString("D"), StringComparer.Ordinal).ThenBy(x => x.TargetId.ToString("D"), StringComparer.Ordinal))
        { Text(link.SourceId.ToString("D")); Text(link.TargetId.ToString("D")); }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(data.ToArray())).ToLowerInvariant();
    }
}
