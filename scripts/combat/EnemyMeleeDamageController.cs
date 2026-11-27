using Ashenfall.Enemies;
using Godot;

namespace Ashenfall.Combat;

/// <summary>One delayed, model-independent impact for each Guardian attack execution.</summary>
public partial class EnemyMeleeDamageController : Node
{
    [Signal] public delegate void DamageAppliedEventHandler(Node3D target, int amount, int sequence);
    [Export] public EnemyController Actor { get; set; }
    [Export] public HealthComponent AttackerHealth { get; set; }
    [Export] public HealthComponent TargetHealth { get; set; }
    [Export(PropertyHint.Range, "0,1000,1")] public int Damage { get; set; } = 10;
    [Export] public bool DevelopmentVariableDamage { get; set; }
    [Export] public int DevelopmentMinimumDamage { get; set; } = 8;
    [Export] public int DevelopmentMaximumDamage { get; set; } = 12;
    [Export] public float DevelopmentCriticalChance { get; set; } = 0.05f;
    [Export] public float DevelopmentCriticalMultiplier { get; set; } = 1.5f;
    public bool LastImpactCritical { get; private set; }
    // AttackExecuted occurs at 0.60 s. The default impact is 0.70 s, during the visual strike.
    [Export(PropertyHint.Range, "0,0.95,0.01")] public float ImpactDelay { get; set; } = 0.10f;
    public int LastConsumedSequence { get; private set; }
    public int SuccessfulHits { get; private set; }
    public int PendingSequence => _pendingSequence;
    private int _pendingSequence;
    private Node3D _committedTarget;
    private float _remaining;

    public override void _Ready()
    {
        if (Actor == null || AttackerHealth == null || TargetHealth == null)
        {
            GD.PushError("Enemy melee damage requires actor and both health components.");
            SetPhysicsProcess(false);
            return;
        }
        Actor.AttackExecuted += OnAttackExecuted;
        AttackerHealth.Died += CancelPending;
        TargetHealth.Died += CancelPending;
        SetPhysicsProcess(false);
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Actor)) Actor.AttackExecuted -= OnAttackExecuted;
        if (GodotObject.IsInstanceValid(AttackerHealth)) AttackerHealth.Died -= CancelPending;
        if (GodotObject.IsInstanceValid(TargetHealth)) TargetHealth.Died -= CancelPending;
    }

    private void OnAttackExecuted(Node3D target, int sequence)
    {
        if (sequence <= LastConsumedSequence || sequence != Actor.AttackCount ||
            _pendingSequence != 0 || !AttackerHealth.IsAlive || !TargetHealth.IsAlive ||
            Actor.State != EnemyController.EnemyState.AttackRecovery ||
            target != Actor.PlayerTarget) return;
        _pendingSequence = sequence;
        _committedTarget = target;
        _remaining = Mathf.Max(0, ImpactDelay);
        // Even a zero-delay hit resolves once on the next physics tick.
        SetPhysicsProcess(true);
    }

    public void CancelPending()
    {
        if (_pendingSequence != 0) LastConsumedSequence = _pendingSequence;
        _pendingSequence = 0;
        _committedTarget = null;
        SetPhysicsProcess(false);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_pendingSequence == 0) return;
        if (!AttackerHealth.IsAlive || !TargetHealth.IsAlive ||
            Actor.State != EnemyController.EnemyState.AttackRecovery ||
            Actor.AttackCount != _pendingSequence || Actor.PlayerTarget != _committedTarget)
        {
            CancelPending();
            return;
        }
        _remaining -= (float)delta;
        if (_remaining > 0) return;
        var target = _committedTarget;
        int sequence = _pendingSequence;
        LastConsumedSequence = sequence; // A miss also consumes the logical attack.
        _pendingSequence = 0;
        _committedTarget = null;
        SetPhysicsProcess(false);
        LastImpactCritical = false;
        if (target is not CollisionObject3D body ||
            body.GetNodeOrNull<HealthComponent>("Health") != TargetHealth ||
            !MeleeImpactValidator.CanHit(Actor, body, Actor.EffectiveAttackRange)) return;
        int amount = Damage;
        if (OS.IsDebugBuild() && (DevelopmentVariableDamage ||
            System.Environment.GetEnvironmentVariable("ASHENFALL_DEV_VARIABLE_DAMAGE") == "1"))
        {
            var result = CombatDamageCalculator.Calculate(new CombatDamageProfile(
                DevelopmentMinimumDamage, DevelopmentMaximumDamage,
                DevelopmentCriticalChance, DevelopmentCriticalMultiplier), new SystemCombatRandom());
            amount = result.FinalDamage;
            LastImpactCritical = result.Critical;
        }
        int before = TargetHealth.CurrentHealth;
        if (amount > 0 && TargetHealth.ApplyDamage(amount))
        {
            SuccessfulHits++;
            EmitSignal(SignalName.DamageApplied, target, before - TargetHealth.CurrentHealth, sequence);
        }
    }
}
