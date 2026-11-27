namespace Ashenfall.Api.Characters;

public sealed record EffectiveCharacterStats(int Level, int MaxHealth, int Damage,
    int Strength, int Dexterity, int Vitality, int Armor);
public sealed record EffectiveCombatStats(int MinDamage, int MaxDamage,
    decimal CriticalChancePercent, decimal CriticalDamageBonusPercent);

public static class CharacterRules
{
    /// <summary>Critical modifiers are additive percentage points, not relative percentages.</summary>
    public static EffectiveCombatStats CalculateCombatStats(int minimum, int maximum,
        decimal criticalChancePercent, decimal criticalBonusPercent, int levelDamageBonus,
        decimal levelCriticalChanceBonus, decimal levelCriticalDamageBonus,
        IReadOnlyList<ItemModifierRow> modifiers)
    {
        if (minimum < 0 || maximum < minimum || criticalChancePercent < 0 || criticalChancePercent > 100 ||
            criticalBonusPercent < 0 || levelDamageBonus < 0)
            throw new ArgumentOutOfRangeException(nameof(minimum), "Invalid base combat statistics.");
        decimal Modifier(string stat, string kind) => modifiers.Where(x => x.StatCode == stat && x.ModifierKind == kind).Sum(x => x.Amount);
        if (modifiers.Any(x => (x.StatCode is "fixed_damage" or "min_damage" or "max_damage") && x.ModifierKind is not ("flat" or "percent")))
            throw new ArgumentException("Unsupported damage modifier kind.");
        if (modifiers.Any(x => (x.StatCode is "critical_chance" or "critical_multiplier") && x.ModifierKind != "flat"))
            throw new ArgumentException("Critical modifiers must be flat percentage points.");
        decimal Damage(int value, string stat)
        {
            decimal flat = Modifier("fixed_damage", "flat") + Modifier(stat, "flat");
            decimal percent = Modifier("fixed_damage", "percent") + Modifier(stat, "percent");
            decimal result = Math.Round((value + (decimal)levelDamageBonus + flat) * (1 + percent / 100), 0, MidpointRounding.AwayFromZero);
            if (result < 0 || result >= int.MaxValue) throw new ArgumentOutOfRangeException(nameof(modifiers), "Damage is outside the supported range.");
            return result;
        }
        int min = (int)Damage(minimum, "min_damage");
        int max = (int)Damage(maximum, "max_damage");
        decimal chance = criticalChancePercent + levelCriticalChanceBonus + Modifier("critical_chance", "flat");
        decimal bonus = criticalBonusPercent + levelCriticalDamageBonus + Modifier("critical_multiplier", "flat");
        if (chance < 0 || bonus < 0 || min > max) throw new ArgumentOutOfRangeException(nameof(modifiers), "Invalid effective combat statistics.");
        return new EffectiveCombatStats(min, max, Math.Min(chance, 100), bonus);
    }
    public static void ValidateProgression(IReadOnlyList<LevelRuleRow> rules)
    {
        if (rules.Count is < 1 or > 1000) throw new ArgumentException("Progression needs 1 to 1000 levels.");
        long previousXp = -1;
        for (int index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];
            if (rule.Level != index + 1 || rule.RequiredTotalXp < 0 ||
                rule.RequiredTotalXp <= previousXp ||
                (index == 0 && rule.RequiredTotalXp != 0) ||
                rule.AttributePoints is < 0 or > 1000 ||
                rule.HealthBonus is < 0 or > 100000 ||
                rule.DamageBonus is < 0 or > 100000)
                throw new ArgumentException("Invalid or incomplete class progression.");
            previousXp = rule.RequiredTotalXp;
        }
    }

    public static void ValidateCatalogProgression(IReadOnlyCollection<Guid> classIds,
        IReadOnlyCollection<LevelRuleRow> allRules)
    {
        if (classIds.Count == 0 || classIds.Count != classIds.Distinct().Count())
            throw new ArgumentException("Catalog classes must be unique and nonempty.");
        if (allRules.Any(x => !classIds.Contains(x.ClassDefinitionId)))
            throw new ArgumentException("Progression references an unknown class.");
        foreach (var classId in classIds)
            ValidateProgression(allRules.Where(x => x.ClassDefinitionId == classId)
                .OrderBy(x => x.Level).ToList());
    }

    public static int DeriveLevel(long experience, IReadOnlyList<LevelRuleRow> rules)
    {
        if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
        ValidateProgression(rules);
        int derived = 1;
        foreach (var rule in rules)
        {
            if (experience >= rule.RequiredTotalXp) derived = rule.Level;
        }
        return derived;
    }

    public static void ValidateAllocation(long experience, int strength, int dexterity,
        int vitality, IReadOnlyList<LevelRuleRow> rules)
    {
        if (strength < 0 || dexterity < 0 || vitality < 0)
            throw new ArgumentException("Allocated attributes cannot be negative.");
        int level = DeriveLevel(experience, rules);
        long budget = rules.Where(x => x.Level <= level).Sum(x => (long)x.AttributePoints);
        if ((long)strength + dexterity + vitality > budget)
            throw new ArgumentException("Allocated attributes exceed earned points.");
    }

    public static void ValidateEquipment(string slot, string category, string? authoredSlot,
        short? hands, bool stackable, int quantity, bool mainHandIsTwoHanded, bool offHandOccupied)
    {
        if (category is not ("weapon" or "armor") || authoredSlot != slot)
            throw new ArgumentException("Item is incompatible with the requested slot.");
        if (stackable || quantity != 1)
            throw new ArgumentException("Equipped items must be single nonstackable instances.");
        if (slot == "off_hand" && mainHandIsTwoHanded)
            throw new ArgumentException("A two-handed weapon occupies both hands.");
        if (slot == "main_hand" && hands == 2 && offHandOccupied)
            throw new ArgumentException("Unequip the off-hand item first.");
    }

    public static EffectiveCharacterStats CalculateStats(int baseHealth, int baseDamage,
        int baseStrength, int baseDexterity, int baseVitality, int baseArmor,
        CharacterRow character, IReadOnlyList<LevelRuleRow> progression,
        IReadOnlyList<ItemModifierRow> equipment, IReadOnlyDictionary<string, decimal>? temporary = null,
        bool rejectOutOfRange = false)
    {
        int level = DeriveLevel(character.Experience, progression);
        var levelRule = progression.Single(x => x.Level == level);
        decimal Stat(string key, decimal authored)
        {
            decimal flat = equipment.Where(x => x.StatCode == key && x.ModifierKind == "flat").Sum(x => x.Amount);
            decimal percent = equipment.Where(x => x.StatCode == key && x.ModifierKind == "percent").Sum(x => x.Amount);
            decimal runtime = temporary is not null && temporary.TryGetValue(key, out var value) ? value : 0;
            decimal computed = (authored + flat) * (1 + percent / 100) + runtime;
            if (rejectOutOfRange && (computed < 0 || computed > int.MaxValue))
                throw new ArgumentOutOfRangeException(nameof(equipment), "Effective statistic is outside the supported range.");
            decimal rounded = Math.Round(computed, 0, MidpointRounding.AwayFromZero);
            return Math.Clamp(rounded, 0, int.MaxValue);
        }
        return new EffectiveCharacterStats(level,
            (int)Stat("max_health", (decimal)baseHealth + levelRule.HealthBonus),
            (int)Stat("fixed_damage", (decimal)baseDamage + levelRule.DamageBonus),
            (int)Stat("strength", (decimal)baseStrength + character.AllocatedStrength),
            (int)Stat("dexterity", (decimal)baseDexterity + character.AllocatedDexterity),
            (int)Stat("vitality", (decimal)baseVitality + character.AllocatedVitality),
            (int)Stat("armor", baseArmor));
    }
}
