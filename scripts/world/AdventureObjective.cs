using Ashenfall.Combat;
using Ashenfall.Enemies;
using Godot;
using System.Collections.Generic;

namespace Ashenfall.World;

/// <summary>Counts required scene guardians and completes only when a living player reaches the goal.</summary>
public partial class AdventureObjective : Node
{
    [Signal] public delegate void ProgressChangedEventHandler(int defeated, int required);
    [Signal] public delegate void CompletedEventHandler(int defeated);
    [Export] public Node3D Player { get; set; }
    [Export] public HealthComponent PlayerHealth { get; set; }
    [Export] public Node3D Goal { get; set; }
    [Export(PropertyHint.Range, "0.5,10,0.1")] public float GoalRadius { get; set; } = 1.8f;
    public int GuardianCount => _guardians.Count;
    public int DefeatedCount => _defeated.Count;
    public bool IsComplete { get; private set; }
    public bool GoalReached { get; private set; }
    private readonly List<HealthComponent> _guardians = new();
    private readonly HashSet<HealthComponent> _defeated = new();
    private readonly Dictionary<HealthComponent, HealthComponent.DiedEventHandler> _subscriptions = new();

    public override void _Ready()
    {
        if (Player == null || PlayerHealth == null || Goal == null)
        {
            GD.PushError("Adventure objective requires player, player health and goal.");
            SetPhysicsProcess(false);
            return;
        }
        // Scene composition defines the required encounter. Adding/removing a root
        // Guardian instance changes the objective without modifying combat code.
        foreach (var child in GetParent().GetChildren())
        {
            if (child is not EnemyController enemy) continue;
            var health = enemy.GetNodeOrNull<HealthComponent>("Health");
            if (health == null) continue;
            _guardians.Add(health);
            if (!health.IsAlive) _defeated.Add(health);
            HealthComponent.DiedEventHandler handler = () => OnGuardianDied(health);
            _subscriptions.Add(health, handler);
            health.Died += handler;
        }
        if (_guardians.Count == 0) GD.PushError("Adventure objective found no root Guardian actors.");
        EmitSignal(SignalName.ProgressChanged, DefeatedCount, GuardianCount);
    }

    public override void _ExitTree()
    {
        foreach (var pair in _subscriptions)
            if (GodotObject.IsInstanceValid(pair.Key)) pair.Key.Died -= pair.Value;
        _subscriptions.Clear();
    }

    private void OnGuardianDied(HealthComponent health)
    {
        if (!_defeated.Add(health)) return;
        EmitSignal(SignalName.ProgressChanged, DefeatedCount, GuardianCount);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsComplete || !PlayerHealth.IsAlive || GuardianCount == 0) return;
        var offset = Player.GlobalPosition - Goal.GlobalPosition;
        GoalReached = new Vector2(offset.X, offset.Z).Length() <= Mathf.Max(0.5f, GoalRadius);
        if (!GoalReached || DefeatedCount != GuardianCount) return;
        IsComplete = true;
        SetPhysicsProcess(false);
        EmitSignal(SignalName.Completed, DefeatedCount);
    }
}
