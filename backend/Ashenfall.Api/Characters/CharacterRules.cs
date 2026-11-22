namespace Ashenfall.Api.Characters;

public sealed record EffectiveCharacterStats(int Level, int MaxHealth, int Damage,
    int Strength, int Dexterity, int Vitality, int Armor);

public static class CharacterRules
{
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
        IReadOnlyList<ItemModifierRow> equipment, IReadOnlyDictionary<string, decimal>? temporary = null)
    {
        int level = DeriveLevel(character.Experience, progression);
        var levelRule = progression.Single(x => x.Level == level);
        decimal Stat(string key, decimal authored)
        {
            decimal flat = equipment.Where(x => x.StatCode == key && x.ModifierKind == "flat").Sum(x => x.Amount);
            decimal percent = equipment.Where(x => x.StatCode == key && x.ModifierKind == "percent").Sum(x => x.Amount);
            decimal runtime = temporary is not null && temporary.TryGetValue(key, out var value) ? value : 0;
            return Math.Clamp(Math.Round((authored + flat) * (1 + percent / 100) + runtime,
                0, MidpointRounding.AwayFromZero), 0, int.MaxValue);
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
