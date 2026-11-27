using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Characters;

namespace Ashenfall.CatalogPublisher.Balance;

public sealed record BalanceLink(Guid SourceId, Guid TargetId);
public sealed record BalanceEdit(string Parameter, decimal Value);
public sealed record BalanceDifference(string Parameter, decimal? Before, decimal? After);
public sealed record BalancePlan(int FormatVersion, Guid OperationId, string Version,
    Guid ExpectedActiveId, string ExpectedActiveHash, Guid ConfigurationId, string ConfigurationHash,
    IReadOnlyList<BalanceEdit> Changes, Guid? CharacterId, string TargetHash,
    bool NoOp, IReadOnlyList<BalanceDifference> Differences, int ClassMappings, int ItemMappings,
    CharacterGameplaySnapshotV2? ExpectedSnapshot);
public sealed record BalanceApplyResult(string Result, Guid ReleaseId, string Hash, CharacterGameplaySnapshotV2? Snapshot);

/// <summary>Detached catalog data. No connections or writes; reusable by CLI and future administration clients.</summary>
public sealed class BalanceCatalog
{
    public required CatalogSnapshot Catalog { get; init; }
    public List<CatalogCombatClassV2> Combat { get; init; } = [];
    public List<CatalogProgressionV2> Levels { get; init; } = [];
    public List<CatalogModifierV2> Modifiers { get; init; } = [];
    public List<BalanceLink> ClassLinks { get; init; } = [];
    public List<BalanceLink> ItemLinks { get; init; } = [];
    public string Hash { get; set; } = "";
    public string ComputeHash() => CombatCatalogHashV2.Compute(Catalog, Combat, Levels, Modifiers,
        ClassLinks.Select(x => (x.SourceId, x.TargetId)).ToArray(), ItemLinks.Select(x => (x.SourceId, x.TargetId)).ToArray());
    public void Verify()
    {
        if (Catalog.Release.ContentSha256 != CatalogHash.Compute(Catalog) || Hash != ComputeHash())
            throw new InvalidOperationException("Catalog content or V2 manifest does not match canonical data.");
    }
}

public sealed class BalanceState
{
    public List<BalanceCatalog> Catalogs { get; init; } = [];
    public List<CharacterRow> Characters { get; init; } = [];
    public List<OwnedItemRow> OwnedItems { get; init; } = [];
    public List<EquipmentRow> Equipment { get; init; } = [];
    public BalanceCatalog Active => Catalogs.Single(x => x.Catalog.Release.Status == CatalogStates.Published);
}

public static class BalanceJson
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = false };
    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    public static Guid Identity(Guid operation, Guid source) => new(SHA256.HashData(Encoding.UTF8.GetBytes(operation.ToString("D") + ":" + source.ToString("D"))).AsSpan(0, 16));
}

/// <summary>Typed numeric registry. Reflection touches only registered properties, never user-provided SQL or member names.</summary>
public sealed class BalanceParameter
{
    public required string Key { get; init; }
    public required string Category { get; init; }
    public required Guid OwnerId { get; init; }
    public required string Unit { get; init; }
    public required decimal Minimum { get; init; }
    public required decimal Maximum { get; init; }
    public required int Scale { get; init; }
    internal object Owner { get; init; } = null!;
    internal System.Reflection.PropertyInfo Property { get; init; } = null!;
    public decimal? Value => Property.GetValue(Owner) is { } value ? Convert.ToDecimal(value, CultureInfo.InvariantCulture) : null;
    public void Set(decimal value)
    {
        if (value < Minimum || value > Maximum || decimal.Round(value, Scale) != value)
            throw new ArgumentException($"Invalid value for {Key}: expected {Minimum}..{Maximum}, scale {Scale}, unit {Unit}.");
        var type = Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType;
        Property.SetValue(Owner, Convert.ChangeType(value, type, CultureInfo.InvariantCulture));
    }
    public object Describe() => new { Parameter = Key, Category, OwnerId, Value, Unit, Minimum, Maximum, Scale };
}

public static class BalanceParameters
{
    public static IReadOnlyList<BalanceParameter> Discover(BalanceCatalog data)
    {
        var result = new List<BalanceParameter>();
        void Add(object owner, Guid id, string prefix, string property, decimal min, decimal max, int scale = 0, string unit = "points")
        {
            string key = string.Concat(property.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
            result.Add(new BalanceParameter { Key = prefix + "/" + key, Category = prefix.Split('/')[0], OwnerId = id,
                Unit = unit, Minimum = min, Maximum = max, Scale = scale, Owner = owner,
                Property = owner.GetType().GetProperty(property) ?? throw new InvalidOperationException("Registry property missing.") });
        }
        foreach (var c in data.Catalog.Classes)
        {
            var p = "class/" + c.Code;
            Add(c, c.Id, p, nameof(c.BaseMaxHealth), 1, 10000, unit: "HP");
            Add(c, c.Id, p, nameof(c.FixedDamage), 0, 1000, unit: "HP damage");
            foreach (string field in new[] { "BaseStrength", "BaseDexterity", "BaseVitality", "BaseArmor" }) Add(c, c.Id, p, field, 0, int.MaxValue);
            foreach (string field in new[] { "MoveSpeedMps", "MeleeRangeM" }) Add(c, c.Id, p, field, .001m, 9999.999m, 3, field == "MoveSpeedMps" ? "m/s" : "m");
            Add(c, c.Id, p, "AttackCycleMs", 100, int.MaxValue, unit: "ms"); Add(c, c.Id, p, "ImpactMs", 0, int.MaxValue, unit: "ms");
            var combat = data.Combat.Single(x => x.ClassId == c.Id);
            Add(combat, c.Id, p, "MinDamage", 0, 1000, unit: "HP damage"); Add(combat, c.Id, p, "MaxDamage", 0, 1000, unit: "HP damage");
            Add(combat, c.Id, p, "CriticalChancePercent", 0, 100, 4, "percent (5 means 5%)");
            Add(combat, c.Id, p, "CriticalDamageBonusPercent", 0, 100000, 4, "bonus percent (100 means +100%)");
        }
        foreach (var e in data.Catalog.Enemies)
        {
            var p = "enemy/" + e.Code;
            Add(e, e.Id, p, "MaxHealth", 1, int.MaxValue, unit: "HP"); Add(e, e.Id, p, "FixedDamage", 0, int.MaxValue, unit: "HP damage");
            foreach (string field in new[] { "BaseStrength", "BaseDexterity", "BaseVitality", "BaseArmor" }) Add(e, e.Id, p, field, 0, int.MaxValue);
            foreach (string field in new[] { "WindupMs", "RecoveryMs", "ImpactAfterExecuteMs" }) Add(e, e.Id, p, field, field == "ImpactAfterExecuteMs" ? 0 : 50, int.MaxValue, unit: "ms");
            foreach (string field in new[] { "AttackRangeM", "ChaseSpeedMps", "ReturnSpeedMps", "DetectionRadiusM", "DisengagementRadiusM", "MaxSpawnDistanceM", "StoppingDistanceM", "ResumeDistanceM" })
                Add(e, e.Id, p, field, field is "ChaseSpeedMps" or "ReturnSpeedMps" ? 0 : .001m, 9999.999m, 3, field.EndsWith("Mps") ? "m/s" : "m");
        }
        foreach (var l in data.Levels)
        {
            var p = "level/" + data.Catalog.Classes.Single(x => x.Id == l.ClassId).Code + "/" + l.Level;
            Add(l, l.ClassId, p, "RequiredTotalXp", 0, long.MaxValue, unit: "total XP threshold");
            foreach (string field in new[] { "HealthBonus", "DamageBonus", "MinDamageBonus", "MaxDamageBonus" }) Add(l, l.ClassId, p, field, 0, 100000);
            Add(l, l.ClassId, p, "AttributePoints", 0, 1000);
            Add(l, l.ClassId, p, "CriticalChanceBonusPercent", 0, 100000, 4, "percentage points");
            Add(l, l.ClassId, p, "CriticalDamageBonusPercent", 0, 100000, 4, "bonus percentage points");
        }
        foreach (var m in data.Modifiers)
        {
            var item = data.Catalog.Items.Single(x => x.Id == m.ItemId);
            Add(m, m.ItemId, "modifier/" + item.Code + "/" + m.StatCode + "/" + m.ModifierKind, "Amount", -100000, 100000, 4,
                m.StatCode is "critical_chance" or "critical_multiplier" ? "percentage points" : m.ModifierKind == "percent" ? "percent" : "flat points");
        }
        return result.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
    }
    public static void Edit(BalanceCatalog data, IReadOnlyList<BalanceEdit> edits)
    {
        if (edits.Select(x => x.Parameter).Distinct(StringComparer.Ordinal).Count() != edits.Count) throw new ArgumentException("Duplicate parameter edit.");
        var registry = Discover(data).ToDictionary(x => x.Key, StringComparer.Ordinal);
        foreach (var edit in edits)
            if (registry.TryGetValue(edit.Parameter, out var parameter)) parameter.Set(edit.Value);
            else throw new ArgumentException($"Unsupported parameter: {edit.Parameter}");
    }
}
