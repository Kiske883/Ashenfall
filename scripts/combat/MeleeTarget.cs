using Godot;

namespace Ashenfall.Combat;

/// <summary>Passive marker on a physics actor. It has no health, damage or visual dependencies.</summary>
public partial class MeleeTarget : Node
{
    [Export] public bool Targetable { get; set; } = true;
    [Export] public HealthComponent Health { get; set; }
    public bool CanBeTargeted => Targetable && (Health == null || Health.IsAlive);
    public CollisionObject3D Actor => GetParent() as CollisionObject3D;
}
