using Godot;
using System.Collections.Generic;

namespace Ashenfall.World;

/// <summary>One startup bake from generated physics geometry, independent of players.</summary>
public partial class WorldNavigation : NavigationRegion3D
{
    [Export] public Node3D GeometryRoot { get; set; }
    [Export] public CollisionShape3D AgentCollider { get; set; }
    public float SafeEdgeMargin { get; private set; }
    private float _extraObstacleClearance;

    public override void _Ready() => Callable.From(BakeGeneratedWorld).CallDeferred();

    private void BakeGeneratedWorld()
    {
        if (GeometryRoot == null || NavigationMesh == null || AgentCollider?.Shape is not CapsuleShape3D capsule ||
            AgentCollider.GetParent() is not CharacterBody3D actor)
        {
            GD.PushError("World navigation requires geometry, a NavigationMesh, and an actor capsule collider.");
            return;
        }
        GlobalTransform = GeometryRoot.GlobalTransform;
        NavigationMesh = (NavigationMesh)NavigationMesh.Duplicate();
        var scale = (GeometryRoot.GlobalTransform.AffineInverse() * AgentCollider.GlobalTransform).Basis.Scale.Abs();
        SafeEdgeMargin = capsule.Radius * Mathf.Max(scale.X, scale.Z) + actor.SafeMargin;
        // Keep the resource's conservative obstacle clearance, but erode the outer
        // floor edge only by the actual actor radius plus its physical safety margin.
        float rasterRadius = Mathf.Ceil(SafeEdgeMargin / NavigationMesh.CellSize) * NavigationMesh.CellSize;
        _extraObstacleClearance = Mathf.Max(0, NavigationMesh.AgentRadius - rasterRadius);
        NavigationMesh.AgentRadius = rasterRadius;
        using var source = new NavigationMeshSourceGeometryData3D();
        NavigationServer3D.ParseSourceGeometryData(NavigationMesh, source, GeometryRoot);
        // Project actual collider footprints through the bake so even rock/gate tops
        // cannot become isolated walkable islands. Radius erosion is applied by the baker.
        AddFootprints(GeometryRoot, source);
        NavigationServer3D.BakeFromSourceGeometryData(NavigationMesh, source);
        GD.Print($"World navigation baked once: {NavigationMesh.GetPolygonCount()} polygons, " +
            $"safe edge radius={SafeEdgeMargin:F3}, raster radius=" +
            $"{Mathf.Ceil(SafeEdgeMargin / NavigationMesh.CellSize) * NavigationMesh.CellSize:F3}");
    }

    private void AddFootprints(Node node, NavigationMeshSourceGeometryData3D source)
    {
        if (node is CollisionShape3D collider && !collider.Disabled &&
            collider.GetParent() is StaticBody3D body && !body.IsInGroup("navigable_ground") &&
            (body.CollisionLayer & NavigationMesh.GeometryCollisionMask) != 0)
        {
            var points = new List<Vector2>();
            var transform = GeometryRoot.GlobalTransform.AffineInverse() * collider.GlobalTransform;
            if (collider.Shape is CylinderShape3D cylinder)
            {
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.Tau / 16;
                    var p = transform * new Vector3(Mathf.Cos(angle) * cylinder.Radius, 0,
                        Mathf.Sin(angle) * cylinder.Radius);
                    points.Add(new Vector2(p.X, p.Z));
                }
            }
            else if (collider.Shape is BoxShape3D box)
            {
                float bottom = float.PositiveInfinity;
                foreach (float x in new[] { -0.5f, 0.5f })
                foreach (float y in new[] { -0.5f, 0.5f })
                foreach (float z in new[] { -0.5f, 0.5f })
                {
                    var p = transform * (box.Size * new Vector3(x, y, z));
                    bottom = Mathf.Min(bottom, p.Y);
                    points.Add(new Vector2(p.X, p.Z));
                }
                // Leave sufficient overhead clearance beneath gate lintels.
                if (bottom > NavigationMesh.AgentHeight + 0.1f) points.Clear();
            }
            if (points.Count > 2)
            {
                var hull = Geometry2D.ConvexHull(points.ToArray());
                var outlines = _extraObstacleClearance > 0
                    ? Geometry2D.OffsetPolygon(hull, _extraObstacleClearance)
                    : new Godot.Collections.Array<Vector2[]> { hull };
                foreach (var outline in outlines)
                {
                    // OffsetPolygon does not repeat the first point, unlike ConvexHull.
                    int count = outline.Length;
                    if (outline[0].IsEqualApprox(outline[count - 1])) count--;
                    var vertices = new Vector3[count];
                    for (int i = 0; i < count; i++)
                        vertices[i] = new Vector3(outline[i].X, 0, outline[i].Y);
                    source.AddProjectedObstruction(vertices, -1, 12, false);
                }
            }
        }
        foreach (Node child in node.GetChildren()) AddFootprints(child, source);
    }
}
