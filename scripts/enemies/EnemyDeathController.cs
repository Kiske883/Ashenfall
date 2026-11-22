using Ashenfall.Combat;
using Godot;

namespace Ashenfall.Enemies;

/// <summary>Connects a reusable health component to this enemy's terminal gameplay state.</summary>
public partial class EnemyDeathController : Node
{
    [Export] public HealthComponent Health { get; set; }
    [Export] public EnemyController Actor { get; set; }
    [Export] public MeleeTarget Target { get; set; }
    [Export] public CollisionShape3D BodyShape { get; set; }

    public override void _Ready()
    {
        if (Health == null || Actor == null || Target == null || BodyShape == null)
        {
            GD.PushError("Enemy death controller requires health, actor, target and body shape.");
            return;
        }
        Health.Died += OnDied;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Health)) Health.Died -= OnDied;
    }

    private void OnDied()
    {
        Target.Targetable = false;
        Actor.Defeat();
        // Deferred physics changes are safe even when death occurs during a physics tick.
        BodyShape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        Actor.SetDeferred(CollisionObject3D.PropertyName.CollisionLayer, 0u);
        Actor.SetDeferred(CollisionObject3D.PropertyName.CollisionMask, 0u);
    }
}
