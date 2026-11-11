using Godot;

namespace Ashenfall.Characters;

/// <summary>Acquires an unbounded mouse request; knows nothing about actors or navigation.</summary>
public static class MouseTargetAcquisition
{
    public static bool TryAcquire(Camera3D camera, PhysicsDirectSpaceState3D space,
        Vector2 screenPosition, float planeHeight, float minimumVerticalComponent,
        float maximumPlaneRayDistance, out Vector3 point, out bool needsProjection)
    {
        point = Vector3.Zero;
        needsProjection = false;
        if (!screenPosition.IsFinite() || !camera.GetViewport().GetVisibleRect().HasPoint(screenPosition))
            return false;

        var origin = camera.ProjectRayOrigin(screenPosition);
        var direction = camera.ProjectRayNormal(screenPosition);
        if (!origin.IsFinite() || !direction.IsFinite()) return false;
        var ray = PhysicsRayQueryParameters3D.Create(origin, origin + direction * camera.Far, 1);
        var hit = space.IntersectRay(ray);
        if (hit.Count > 0 && hit["collider"].AsGodotObject() is Node body &&
            body.IsInGroup("navigable_ground") && hit["normal"].AsVector3().Y >= 0.9f)
        {
            point = hit["position"].AsVector3();
            return point.IsFinite();
        }

        // A horizontal/near-horizontal ray has no stable target on this plane.
        if (!float.IsFinite(planeHeight) || !float.IsFinite(minimumVerticalComponent) ||
            !float.IsFinite(maximumPlaneRayDistance) || maximumPlaneRayDistance <= 0 ||
            Mathf.Abs(direction.Y) <= Mathf.Max(minimumVerticalComponent, 0.000001f)) return false;
        float distance = (planeHeight - origin.Y) / direction.Y;
        if (!float.IsFinite(distance) || distance < 0 || distance > maximumPlaneRayDistance) return false;
        point = origin + direction * distance;
        needsProjection = true;
        return point.IsFinite();
    }
}
