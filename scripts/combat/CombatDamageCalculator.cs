using System;

namespace Ashenfall.Combat;

public interface ICombatRandom
{
    // Inclusive integer bounds; floating value is in [0, 1).
    int NextIntInclusive(int minimum, int maximum);
    double NextUnit();
}

public sealed class SystemCombatRandom : ICombatRandom
{
    public int NextIntInclusive(int minimum, int maximum) => Random.Shared.Next(minimum, checked(maximum + 1));
    public double NextUnit() => Random.Shared.NextDouble();
}

public readonly record struct CombatDamageProfile(int Minimum, int Maximum,
    double CriticalChance, double CriticalMultiplier, int EffectiveArmor = 0,
    double AttackCycleMultiplier = 1);

public readonly record struct CombatDamageResult(int RolledDamage, bool Critical,
    int FinalDamage, int EffectiveArmor, double AttackCycleMultiplier);

/// <summary>Pure local impact formula. Armor is recorded but not mitigated until its rule is approved.</summary>
public static class CombatDamageCalculator
{
    public static CombatDamageResult Calculate(CombatDamageProfile profile, ICombatRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (profile.Minimum < 0 || profile.Maximum < profile.Minimum ||
            profile.Maximum == int.MaxValue || !double.IsFinite(profile.CriticalChance) ||
            profile.CriticalChance < 0 || profile.CriticalChance > 1 ||
            !double.IsFinite(profile.CriticalMultiplier) || profile.CriticalMultiplier < 1 ||
            !double.IsFinite(profile.AttackCycleMultiplier) || profile.AttackCycleMultiplier <= 0 ||
            profile.EffectiveArmor < 0)
            throw new ArgumentOutOfRangeException(nameof(profile));
        int roll = random.NextIntInclusive(profile.Minimum, profile.Maximum);
        double chance = random.NextUnit();
        if (roll < profile.Minimum || roll > profile.Maximum || !double.IsFinite(chance) ||
            chance < 0 || chance >= 1)
            throw new InvalidOperationException("Combat random source returned an out-of-range value.");
        bool critical = chance < profile.CriticalChance;
        double scaled = critical ? roll * profile.CriticalMultiplier : roll;
        if (scaled > int.MaxValue) throw new OverflowException("Damage exceeds the supported range.");
        int final = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
        return new CombatDamageResult(roll, critical, final,
            profile.EffectiveArmor, profile.AttackCycleMultiplier);
    }
}
