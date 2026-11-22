using Ashenfall.Combat;
using Ashenfall.Enemies;
using Godot;

namespace Ashenfall.World;

/// <summary>Radial activation with hysteresis; the enemy retains its own pursuit and leash rules.</summary>
public partial class EncounterZone : Node3D
{
    [Export] public EnemyController Enemy { get; set; }
    [Export] public Node3D Player { get; set; }
    [Export(PropertyHint.Range, "1,30,0.1")] public float ActivationRadius { get; set; } = 5.5f;
    [Export(PropertyHint.Range, "1,40,0.1")] public float ReleaseRadius { get; set; } = 8f;
    public bool Active { get; private set; }

    public override void _Ready()
    {
        if (Enemy == null || Player == null)
        {
            GD.PushError("Encounter zone requires one enemy and one player.");
            SetPhysicsProcess(false);
            return;
        }
        Enemy.PlayerTarget = null;
        ProcessPhysicsPriority = Enemy.ProcessPhysicsPriority - 1;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Enemy.State == EnemyController.EnemyState.Defeated)
        {
            Deactivate();
            SetPhysicsProcess(false);
            return;
        }
        if (!GodotObject.IsInstanceValid(Player) || !Player.IsInsideTree())
        {
            Deactivate();
            return;
        }
        var health = Player.GetNodeOrNull<HealthComponent>("Health");
        if (health is { IsAlive: false })
        {
            Deactivate();
            return;
        }
        var offset = Player.GlobalPosition - GlobalPosition;
        float distance = new Vector2(offset.X, offset.Z).Length();
        if (!Active && distance <= Mathf.Max(0.5f, ActivationRadius))
        {
            Active = true;
            Enemy.PlayerTarget = Player;
        }
        else if (Active && distance >= Mathf.Max(ActivationRadius + 0.1f, ReleaseRadius))
            Deactivate();
    }

    private void Deactivate()
    {
        if (!Active && Enemy.PlayerTarget == null) return;
        Active = false;
        if (GodotObject.IsInstanceValid(Enemy)) Enemy.PlayerTarget = null;
    }
}
