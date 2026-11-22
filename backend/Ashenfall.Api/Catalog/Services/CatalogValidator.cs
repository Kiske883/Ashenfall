using System.Text.RegularExpressions;
using Ashenfall.Api.Catalog.Domain;

namespace Ashenfall.Api.Catalog.Services;

public static partial class CatalogValidator
{
    [GeneratedRegex("^[a-z][a-z0-9_]{1,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    public static IReadOnlyList<string> Validate(CatalogSnapshot snapshot, bool requireSeedDefinitions = true)
    {
        var errors = new List<string>();
        if (snapshot.Release.Id == Guid.Empty || snapshot.Release.SchemaMajor != 1 ||
            snapshot.Release.VersionKey.Length is < 1 or > 64) errors.Add("Invalid release identity or schema.");
        if (snapshot.Release.Status is not (CatalogStates.Draft or CatalogStates.Published or CatalogStates.Retired))
            errors.Add("Invalid release state.");
        ValidateCodes(snapshot.Classes.Select(x => (x.Id, x.Code)), "class", errors);
        ValidateCodes(snapshot.Enemies.Select(x => (x.Id, x.Code)), "enemy", errors);
        ValidateCodes(snapshot.Items.Select(x => (x.Id, x.Code)), "item", errors);

        foreach (var x in snapshot.Classes)
        {
            if (x.CatalogReleaseId != snapshot.Release.Id || !GoodName(x.DisplayName) || !GoodName(x.VisualKey) ||
                x.BaseMaxHealth <= 0 || x.FixedDamage < 0 || !Attributes(x.BaseStrength, x.BaseDexterity, x.BaseVitality, x.BaseArmor) ||
                !Decimal3(x.MoveSpeedMps, positive: true) || !Decimal3(x.MeleeRangeM, positive: true) ||
                x.AttackCycleMs < 100 || x.ImpactMs < 0 || x.ImpactMs > x.AttackCycleMs)
                errors.Add($"Invalid class definition: {x.Code}.");
        }
        foreach (var x in snapshot.Enemies)
        {
            if (x.CatalogReleaseId != snapshot.Release.Id || !GoodName(x.DisplayName) || !GoodName(x.VisualKey) ||
                x.MaxHealth <= 0 || x.FixedDamage < 0 || !Attributes(x.BaseStrength, x.BaseDexterity, x.BaseVitality, x.BaseArmor) ||
                !Decimal3(x.AttackRangeM, true) || x.WindupMs < 50 || x.RecoveryMs < 50 || x.ImpactAfterExecuteMs < 0 ||
                !Decimal3(x.ChaseSpeedMps) || !Decimal3(x.ReturnSpeedMps) ||
                !Decimal3(x.DetectionRadiusM, true) || !Decimal3(x.DisengagementRadiusM, true) ||
                x.DisengagementRadiusM <= x.DetectionRadiusM || !Decimal3(x.MaxSpawnDistanceM, true) ||
                !Decimal3(x.StoppingDistanceM, true) || !Decimal3(x.ResumeDistanceM, true) ||
                x.ResumeDistanceM <= x.StoppingDistanceM)
                errors.Add($"Invalid enemy definition: {x.Code}.");
        }
        foreach (var x in snapshot.Items)
        {
            bool weapon = x.Category == "weapon";
            bool equippable = weapon || x.Category == "armor";
            bool slotKnown = x.EquipSlot is "main_hand" or "off_hand" or "head" or "chest" or "hands" or "legs" or "feet";
            if (x.CatalogReleaseId != snapshot.Release.Id || !GoodName(x.DisplayName) ||
                x.Category is not ("weapon" or "armor" or "consumable" or "quest") ||
                x.Rarity is not ("common" or "uncommon" or "rare" or "epic" or "legendary") ||
                x.MaxStack < 1 || (x.Stackable ? x.MaxStack <= 1 : x.MaxStack != 1) ||
                (equippable ? !slotKnown : x.EquipSlot is not null) ||
                (weapon ? x.HandRequirement is not (1 or 2) || x.EquipSlot is not ("main_hand" or "off_hand") ||
                    (x.HandRequirement == 2 && x.EquipSlot != "main_hand") : x.HandRequirement is not null) ||
                (x.VisualKey is not null && !GoodName(x.VisualKey)))
                errors.Add($"Invalid item definition: {x.Code}.");
        }
        if (requireSeedDefinitions)
        {
            if (!snapshot.Classes.Any(x => x.Code == "dwarf_warrior" && x.VisualKey == "dwarf_berserker"))
                errors.Add("Dwarf Warrior definition missing or has unknown visual key.");
            if (!snapshot.Enemies.Any(x => x.Code == "fallen_guardian" && x.VisualKey == "fallen_guardian_v3"))
                errors.Add("Fallen Guardian definition missing or has unknown visual key.");
            if (!snapshot.Items.Any(x => x.Code == "training_battle_axe" && x.VisualKey == "dwarf_battle_axe"))
                errors.Add("Training axe definition missing or has unknown visual key.");
            if (!snapshot.Items.Any(x => x.Code == "guardian_sword" && x.VisualKey == "guardian_sword"))
                errors.Add("Guardian sword definition missing or has unknown visual key.");
        }
        return errors;
    }

    private static void ValidateCodes(IEnumerable<(Guid Id, string Code)> entries, string type, List<string> errors)
    {
        var ids = new HashSet<Guid>(); var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, code) in entries)
            if (id == Guid.Empty || !ids.Add(id) || code is null || !CodePattern().IsMatch(code) || !codes.Add(code))
                errors.Add($"Invalid or duplicate {type} ID/code.");
    }
    private static bool GoodName(string? value) => value is { Length: >= 1 and <= 100 };
    private static bool Attributes(params int?[] values) => values.All(x => x is null or >= 0);
    private static bool Decimal3(decimal value, bool positive = false) =>
        value >= (positive ? 0.001m : 0m) && value <= 9999.999m && decimal.Truncate(value * 1000m) == value * 1000m;
}
