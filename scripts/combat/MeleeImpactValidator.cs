using Godot;

namespace Ashenfall.Combat;

/// <summary>Center-based melee reach and first-collider visibility, checked at impact time.</summary>
public static class MeleeImpactValidator
{
    public static bool CanHit(CollisionObject3D attacker, CollisionObject3D target, float range)
    {
        if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(target) ||
            !attacker.IsInsideTree() || !target.IsInsideTree()) return false;
        var offset = target.GlobalPosition - attacker.GlobalPosition;
        if (new Vector2(offset.X, offset.Z).Length() > range || Mathf.Abs(offset.Y) > 0.5f)
            return false;
        var query = PhysicsRayQueryParameters3D.Create(attacker.GlobalPosition + Vector3.Up,
            target.GlobalPosition + Vector3.Up, 3);
        query.Exclude = new Godot.Collections.Array<Rid> { attacker.GetRid() };
        var hit = attacker.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count > 0 && hit["collider"].AsGodotObject() == target;
    }
}
