using Godot;

namespace Ashenfall.Combat;

/// <summary>Validates and resolves one impact per logical player attack sequence.</summary>
public partial class MeleeDamageController : Node
{
    [Signal] public delegate void DamageAppliedEventHandler(Node3D target, int amount, int sequence);
    [Export] public PlayerMeleeController Melee { get; set; }
    [Export(PropertyHint.Range, "0,1000,1")] public int Damage { get; set; } = 25;
    // Explicit local experiment only. Published catalog fixed damage remains authoritative by default.
    [Export] public bool DevelopmentVariableDamage { get; set; }
    public bool UseFixedDamageOnly { get; set; }
    private CombatDamageProfile? _authoritativeProfile;
    private readonly ICombatRandom _authoritativeRandom = new SystemCombatRandom();

    public void ConfigureAuthoritativeDamage(int minimum, int maximum, double chancePercent, double bonusPercent)
    {
        if (minimum < 0 || maximum < minimum || maximum == int.MaxValue || !double.IsFinite(chancePercent) ||
            chancePercent < 0 || chancePercent > 100 || !double.IsFinite(bonusPercent) || bonusPercent < 0 ||
            maximum * (1 + bonusPercent / 100) > int.MaxValue || IsInsideTree())
            throw new System.InvalidOperationException("Invalid or late authoritative combat configuration.");
        _authoritativeProfile = new CombatDamageProfile(minimum, maximum, chancePercent / 100, 1 + bonusPercent / 100);
    }
    [Export] public int DevelopmentMinimumDamage { get; set; } = 20;
    [Export] public int DevelopmentMaximumDamage { get; set; } = 30;
    [Export] public float DevelopmentCriticalChance { get; set; } = 0.10f;
    [Export] public float DevelopmentCriticalMultiplier { get; set; } = 1.5f;
    public bool LastImpactCritical { get; private set; }
    public int LastResolvedSequence { get; private set; }
    public int SuccessfulHits { get; private set; }

    public override void _Ready()
    {
        if (Melee == null)
        {
            GD.PushError("Melee damage requires a logical melee controller.");
            return;
        }
        Melee.AttackImpact += OnAttackImpact;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Melee)) Melee.AttackImpact -= OnAttackImpact;
    }

    private void OnAttackImpact(MeleeTarget target, int sequence) => TryApplyImpact(target, sequence);

    /// <summary>A future authority can call this only for the controller's current impact sequence.</summary>
    public bool TryApplyImpact(MeleeTarget target, int sequence)
    {
        if (Melee == null || sequence <= 0 || sequence != Melee.ImpactSequence ||
            sequence == LastResolvedSequence || Melee.State != PlayerMeleeController.MeleeState.Attack ||
            target != Melee.CommittedTarget) return false;

        // Consume the sequence before validation: a missed/invalid strike cannot hit later.
        LastResolvedSequence = sequence;
        LastImpactCritical = false;
        if (!Melee.CanHit(target) || target.Health == null) return false;
        int amount = Damage;
        if (_authoritativeProfile is { } authoritative)
        {
            var result = CombatDamageCalculator.Calculate(authoritative, _authoritativeRandom);
            amount = result.FinalDamage;
            LastImpactCritical = result.Critical;
        }
        else if (!UseFixedDamageOnly && OS.IsDebugBuild() && (DevelopmentVariableDamage ||
            System.Environment.GetEnvironmentVariable("ASHENFALL_DEV_VARIABLE_DAMAGE") == "1"))
        {
            var result = CombatDamageCalculator.Calculate(new CombatDamageProfile(
                DevelopmentMinimumDamage, DevelopmentMaximumDamage,
                DevelopmentCriticalChance, DevelopmentCriticalMultiplier), new SystemCombatRandom());
            amount = result.FinalDamage;
            LastImpactCritical = result.Critical;
        }
        int before = target.Health.CurrentHealth;
        if (amount <= 0 || !target.Health.ApplyDamage(amount)) return false;
        int applied = before - target.Health.CurrentHealth;
        SuccessfulHits++;
        EmitSignal(SignalName.DamageApplied, target.Actor, applied, sequence);
        return true;
    }
}
