#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ashenfall.Catalog;

/// <summary>Strict API decoding and client compatibility checks, independent of Godot Nodes.</summary>
public static partial class CatalogCodec
{
    [GeneratedRegex("^[a-z][a-z0-9_]{1,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HashPattern();

    public static CatalogVersion ParseVersion(string json) => Decode(json, root =>
        new CatalogVersion(Short(root, "schemaMajor"), String(root, "catalogVersion"),
            String(root, "contentSha256"), DateTimeOffset.Parse(String(root, "publishedAt"),
                System.Globalization.CultureInfo.InvariantCulture)));

    public static CatalogSnapshot ParseSnapshot(string json) => Decode(json, root =>
        new CatalogSnapshot(Short(root, "schemaMajor"), String(root, "catalogVersion"),
            String(root, "contentSha256"),
            Array(root, "classes").Select(ParseClass).ToImmutableArray(),
            Array(root, "enemies").Select(ParseEnemy).ToImmutableArray(),
            Array(root, "items").Select(ParseItem).ToImmutableArray()));

    public static void Verify(CatalogVersion version, CatalogSnapshot snapshot)
    {
        if (version.SchemaMajor != 1 || snapshot.SchemaMajor != 1)
            throw new CatalogLoadException("Unsupported catalog schema version.");
        if (version.VersionKey.Length is < 1 or > 64 || version.VersionKey != snapshot.VersionKey ||
            !HashPattern().IsMatch(version.ContentSha256) ||
            !string.Equals(version.ContentSha256, snapshot.ContentSha256, StringComparison.Ordinal))
            throw new CatalogLoadException("Catalog version or hash metadata differs between responses.");
        if (version.PublishedAt == default)
            throw new CatalogLoadException("Published catalog timestamp is invalid.");
        ValidateDefinitions(snapshot);
        if (!string.Equals(CatalogContentHash.Compute(snapshot), snapshot.ContentSha256, StringComparison.Ordinal))
            throw new CatalogLoadException("Catalog content hash verification failed.");
    }

    private static void ValidateDefinitions(CatalogSnapshot s)
    {
        if (s.Classes.IsDefault || s.Enemies.IsDefault || s.Items.IsDefault)
            throw new CatalogLoadException("Catalog definition arrays are missing.");
        var ids = new HashSet<Guid>();
        foreach (var group in new[] { s.Classes.Select(x => (x.Id, x.Code)),
            s.Enemies.Select(x => (x.Id, x.Code)), s.Items.Select(x => (x.Id, x.Code)) })
        {
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, code) in group)
                if (id == Guid.Empty || !ids.Add(id) || !CodePattern().IsMatch(code) || !codes.Add(code))
                    throw new CatalogLoadException("Catalog contains invalid or duplicate IDs/codes.");
        }
        foreach (var x in s.Classes)
            if (!Name(x.DisplayName) || x.VisualKey != "dwarf_berserker" || x.BaseMaxHealth <= 0 ||
                x.FixedDamage < 0 || !Attributes(x.BaseStrength, x.BaseDexterity, x.BaseVitality, x.BaseArmor) ||
                !Decimal3(x.MoveSpeedMps, true) || !Decimal3(x.MeleeRangeM, true) ||
                x.AttackCycleMs < 100 || x.ImpactMs < 0 || x.ImpactMs > x.AttackCycleMs)
                throw new CatalogLoadException("Catalog class definition is invalid or uses an unknown visual key.");
        foreach (var x in s.Enemies)
            if (!Name(x.DisplayName) || x.VisualKey != "fallen_guardian_v3" || x.MaxHealth <= 0 ||
                x.FixedDamage < 0 || !Attributes(x.BaseStrength, x.BaseDexterity, x.BaseVitality, x.BaseArmor) ||
                !Decimal3(x.AttackRangeM, true) || x.WindupMs < 50 || x.RecoveryMs < 50 || x.ImpactAfterExecuteMs < 0 ||
                !Decimal3(x.ChaseSpeedMps) || !Decimal3(x.ReturnSpeedMps) ||
                !Decimal3(x.DetectionRadiusM, true) || !Decimal3(x.DisengagementRadiusM, true) ||
                x.DisengagementRadiusM <= x.DetectionRadiusM || !Decimal3(x.MaxSpawnDistanceM, true) ||
                !Decimal3(x.StoppingDistanceM, true) || !Decimal3(x.ResumeDistanceM, true) ||
                x.ResumeDistanceM <= x.StoppingDistanceM)
                throw new CatalogLoadException("Catalog enemy definition is invalid or uses an unknown visual key.");
        foreach (var x in s.Items)
        {
            bool weapon = x.Category == "weapon", equippable = weapon || x.Category == "armor";
            bool slotKnown = x.EquipSlot is "main_hand" or "off_hand" or "head" or "chest" or "hands" or "legs" or "feet";
            if (!Name(x.DisplayName) || x.Category is not ("weapon" or "armor" or "consumable" or "quest") ||
                x.Rarity is not ("common" or "uncommon" or "rare" or "epic" or "legendary") ||
                x.MaxStack < 1 || (x.Stackable ? x.MaxStack <= 1 : x.MaxStack != 1) ||
                (equippable ? !slotKnown : x.EquipSlot is not null) ||
                (weapon ? x.HandRequirement is not (1 or 2) || x.EquipSlot is not ("main_hand" or "off_hand") ||
                    (x.HandRequirement == 2 && x.EquipSlot != "main_hand") : x.HandRequirement is not null) ||
                (x.VisualKey is not null && x.VisualKey is not ("dwarf_battle_axe" or "guardian_sword")))
                throw new CatalogLoadException("Catalog item definition is invalid or uses an unknown visual key.");
        }
        if (!s.Classes.Any(x => x.Code == "dwarf_warrior" && x.VisualKey == "dwarf_berserker") ||
            !s.Enemies.Any(x => x.Code == "fallen_guardian" && x.VisualKey == "fallen_guardian_v3") ||
            !s.Items.Any(x => x.Code == "training_battle_axe" && x.VisualKey == "dwarf_battle_axe") ||
            !s.Items.Any(x => x.Code == "guardian_sword" && x.VisualKey == "guardian_sword"))
            throw new CatalogLoadException("Required prototype definitions are missing.");
    }

    private static bool Name(string value) => value.Length is >= 1 and <= 100;
    private static bool Attributes(params int?[] values) => values.All(x => x is null or >= 0);
    private static bool Decimal3(decimal value, bool positive = false) =>
        value >= (positive ? 0.001m : 0m) && value <= 9999.999m && decimal.Truncate(value * 1000m) == value * 1000m;

    private static CatalogClass ParseClass(JsonElement x) => new(
        GuidValue(x, "id"), String(x, "code"), String(x, "displayName"), String(x, "visualKey"),
        Int(x, "baseMaxHealth"), Int(x, "fixedDamage"), NullableInt(x, "baseStrength"),
        NullableInt(x, "baseDexterity"), NullableInt(x, "baseVitality"), NullableInt(x, "baseArmor"),
        Decimal(x, "moveSpeedMps"), Decimal(x, "meleeRangeM"), Int(x, "attackCycleMs"), Int(x, "impactMs"));

    private static CatalogEnemy ParseEnemy(JsonElement x) => new(
        GuidValue(x, "id"), String(x, "code"), String(x, "displayName"), String(x, "visualKey"),
        Int(x, "maxHealth"), Int(x, "fixedDamage"), NullableInt(x, "baseStrength"),
        NullableInt(x, "baseDexterity"), NullableInt(x, "baseVitality"), NullableInt(x, "baseArmor"),
        Decimal(x, "attackRangeM"), Int(x, "windupMs"), Int(x, "recoveryMs"), Int(x, "impactAfterExecuteMs"),
        Decimal(x, "chaseSpeedMps"), Decimal(x, "returnSpeedMps"), Decimal(x, "detectionRadiusM"),
        Decimal(x, "disengagementRadiusM"), Decimal(x, "maxSpawnDistanceM"), Decimal(x, "stoppingDistanceM"),
        Decimal(x, "resumeDistanceM"));

    private static CatalogItem ParseItem(JsonElement x) => new(
        GuidValue(x, "id"), String(x, "code"), String(x, "displayName"), String(x, "category"),
        String(x, "rarity"), Bool(x, "stackable"), Int(x, "maxStack"), NullableString(x, "equipSlot"),
        NullableShort(x, "handRequirement"), NullableString(x, "visualKey"));

    private static T Decode<T>(string json, Func<JsonElement, T> parse)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new CatalogLoadException("Catalog response must be an object.");
            return parse(doc.RootElement);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException
                                  or FormatException or OverflowException or ArgumentOutOfRangeException)
        { throw new CatalogLoadException("Catalog response is malformed or missing required fields."); }
    }
    private static JsonElement Field(JsonElement x, string name) => x.GetProperty(name);
    private static string String(JsonElement x, string name) => Field(x, name).GetString()
        ?? throw new CatalogLoadException($"Missing {name}.");
    private static string? NullableString(JsonElement x, string name) => Field(x, name).GetString();
    private static int Int(JsonElement x, string name) => Field(x, name).GetInt32();
    private static short Short(JsonElement x, string name) => Field(x, name).GetInt16();
    private static int? NullableInt(JsonElement x, string name) => Field(x, name).ValueKind == JsonValueKind.Null
        ? null : Int(x, name);
    private static short? NullableShort(JsonElement x, string name) => Field(x, name).ValueKind == JsonValueKind.Null
        ? null : Short(x, name);
    private static decimal Decimal(JsonElement x, string name) => Field(x, name).GetDecimal();
    private static bool Bool(JsonElement x, string name) => Field(x, name).GetBoolean();
    private static Guid GuidValue(JsonElement x, string name) => Field(x, name).GetGuid();
    private static IEnumerable<JsonElement> Array(JsonElement x, string name) => Field(x, name).EnumerateArray();
}
