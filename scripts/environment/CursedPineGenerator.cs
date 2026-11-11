using Godot;
using System.Collections.Generic;

namespace Ashenfall.Environment;

/// <summary>Visual-only art study. Two opaque mesh batches; deterministic, with no gameplay dependencies.</summary>
[Tool]
public partial class CursedPineGenerator : Node3D
{
    [Export] public int Seed { get; set; } = 1703;
    [Export(PropertyHint.Range, "4,7,0.1")] public float Height { get; set; } = 5.8f;
    [Export] public Material BarkMaterial { get; set; }
    [Export] public Material FoliageMaterial { get; set; }

    private readonly RandomNumberGenerator _random = new();
    private SurfaceTool _wood;
    private SurfaceTool _needles;
    private int _triangles;

    public override void _Ready()
    {
        // Generated children have no Owner: editor previews never serialize generated geometry.
        if (GetNodeOrNull("ProceduralBark") != null) return;
        _random.Seed = unchecked((ulong)Seed);
        _wood = new SurfaceTool();
        _needles = new SurfaceTool();
        _wood.Begin(Mesh.PrimitiveType.Triangles);
        _needles.Begin(Mesh.PrimitiveType.Triangles);

        var spine = new List<Vector3>();
        var radii = new List<float>();
        for (int i = 0; i <= 16; i++)
        {
            float t = i / 16f;
            spine.Add(Trunk(t));
            radii.Add(Mathf.Lerp(0.34f, 0.014f, Mathf.Pow(t, 0.72f)) + 0.1f * Mathf.Exp(-t * 25));
        }
        Tube(spine, radii, 16);

        // Buttress roots disappear gradually below the ground rather than ending in cut cylinders.
        for (int root = 0; root < 5; root++)
        {
            float angle = root * 2.399f + 0.4f + R(-0.25f, 0.25f);
            var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var side = direction.Cross(Vector3.Up);
            float length = R(0.65f, 1.12f);
            float thickness = R(0.11f, 0.16f);
            Tube(new List<Vector3>
            {
                direction * 0.12f + Vector3.Up * 0.18f,
                direction * 0.33f + Vector3.Up * R(0.06f, 0.1f),
                direction * length * 0.7f + side * 0.08f + Vector3.Up * 0.01f,
                direction * length + side * R(-0.2f, 0.2f) - Vector3.Up * 0.06f
            }, new List<float> { thickness, thickness * 0.7f, 0.038f, 0.003f }, 8);
        }

        // Open lower branches leave the weathered trunk readable at the isometric angle.
        for (int i = 0; i < 5; i++)
        {
            float angle = i * 2.4f + 0.7f;
            var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var start = Trunk(R(0.18f, 0.31f));
            var elbow = start + direction * R(0.45f, 0.7f) - Vector3.Up * 0.2f;
            Tube(new List<Vector3> { start, elbow, elbow + direction * 0.23f + Vector3.Up * 0.15f },
                new List<float> { 0.07f, 0.035f, 0.008f }, 7);
            Tube(new List<Vector3> { elbow, elbow + direction.Rotated(Vector3.Up, 0.65f) * 0.26f },
                new List<float> { 0.026f, 0.004f }, 6);
        }

        for (int layer = 0; layer < 7; layer++)
        {
            float t = 0.36f + layer * 0.088f;
            int branchCount = layer < 4 ? 7 : 5;
            for (int branch = 0; branch < branchCount; branch++)
            {
                float angle = branch * Mathf.Tau / branchCount + layer * 2.39f + R(-0.2f, 0.2f);
                float length = (1.8f - layer * 0.215f) * R(0.78f, 1.2f);
                var start = Trunk(t) + Vector3.Up * R(-0.12f, 0.12f);
                if ((layer == 0 && branch == 2) || (layer == 2 && branch == 5))
                {
                    var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    Tube(new List<Vector3> { start, start + direction * length * 0.4f - Vector3.Up * 0.2f,
                        start + direction * length * 0.65f - Vector3.Up * 0.12f },
                        new List<float> { 0.055f, 0.035f, 0.015f }, 7);
                }
                else Bough(start, angle, length, layer);
            }
        }
        // Small ascending crown sprays break the smooth apex silhouette.
        for (int i = 0; i < 5; i++)
            Bough(Trunk(0.93f + i * 0.01f), i * 2.4f, R(0.25f, 0.45f), 7);

        CommitBatch("ProceduralBark", _wood, BarkMaterial);
        CommitBatch("ProceduralNeedles", _needles, FoliageMaterial);
        _wood.Dispose();
        _needles.Dispose();
        SetMeta("triangle_count", _triangles);
        SetMeta("opaque_mesh_batches", 2);
        GD.Print($"CursedPine art prototype: seed={Seed}, triangles={_triangles}, opaque batches=2");
    }

    private Vector3 Trunk(float t) => new(
        0.17f * Mathf.Sin(t * 6.5f) + 0.15f * t,
        Height * t,
        0.13f * Mathf.Sin(t * 8f + 0.4f) - 0.05f);

    private void Bough(Vector3 start, float angle, float length, int layer)
    {
        var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
        var lateral = direction.Cross(Vector3.Up);
        float droop = layer < 5 ? 0.27f : 0.1f;
        var middle = start + direction * length * 0.5f - Vector3.Up * droop;
        var tip = start + direction * length - Vector3.Up * droop * 0.25f + lateral * R(-0.12f, 0.12f);
        Tube(new List<Vector3> { start, middle, tip },
            new List<float> { 0.065f * length, 0.028f * length, 0.007f }, 7);

        int twigs = layer < 5 ? 7 : 5;
        for (int i = 0; i < twigs; i++)
        {
            float t = 0.2f + i * 0.78f / twigs;
            var center = Quadratic(start, middle, tip, t);
            for (int side = -1; side <= 1; side += 2)
            {
                float spread = length * (1 - t) * R(0.28f, 0.5f) + 0.08f;
                var end = center + direction * 0.2f + lateral * side * spread - Vector3.Up * R(0.02f, 0.15f);
                Spray(center, end, layer);
                if (i % 2 == 0)
                    Spray(center.Lerp(end, 0.55f), end + direction * R(0.14f, 0.25f), layer);
            }
        }
        Spray(middle, tip + direction * 0.12f, layer);
    }

    private void Spray(Vector3 start, Vector3 end, int layer)
    {
        if (start.DistanceTo(end) > 0.45f)
            Tube(new List<Vector3> { start, end }, new List<float> { 0.007f, 0.002f }, 3);
        var forward = (end - start).Normalized();
        var side = forward.Cross(Vector3.Up).Normalized();
        // Shared spray colors form branch-scale masses instead of alternating bright needles.
        float shade = R(0.88f, 1.08f);
        var baseColor = new Color(0.052f, 0.105f, 0.063f).Lerp(
            new Color(0.085f, 0.105f, 0.055f), R(0, 0.35f)) * shade;
        var tipColor = baseColor.Lerp(new Color(0.105f, 0.16f, 0.085f), 0.45f);
        // A shallow irregular six-sided cushion connects needle bases into one tuft.
        // Fine points remain at its edges, rather than exposing every individual wedge.
        var tuftCenter = start.Lerp(end, 0.55f) + Vector3.Up * 0.015f;
        float halfLength = start.DistanceTo(end) * 0.46f + 0.055f;
        float halfWidth = R(0.09f, 0.12f);
        for (int sector = 0; sector < 6; sector++)
        {
            float a = sector * Mathf.Tau / 6;
            float b = (sector + 1) * Mathf.Tau / 6;
            var rimA = tuftCenter + forward * Mathf.Cos(a) * halfLength + side * Mathf.Sin(a) * halfWidth;
            var rimB = tuftCenter + forward * Mathf.Cos(b) * halfLength + side * Mathf.Sin(b) * halfWidth;
            _needles.SetNormal(Vector3.Up);
            _needles.SetColor(baseColor * 1.04f);
            _needles.AddVertex(tuftCenter + Vector3.Up * 0.035f);
            foreach (var rim in new[] { rimA, rimB })
            {
                _needles.SetNormal((Vector3.Up * 0.75f + (rim - tuftCenter).Normalized() * 0.45f).Normalized());
                _needles.SetColor(baseColor * 0.94f);
                _needles.AddVertex(rim);
            }
            _triangles++;
        }
        int pairs = 4;
        for (int i = 0; i < pairs; i++)
        {
            float t = (i + 0.3f) / pairs;
            var center = start.Lerp(end, t);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float length = R(0.15f, 0.24f) * (1 - t * 0.3f);
                var needleTip = center + (side * sign * 0.9f + forward * 0.45f + Vector3.Up * R(-0.2f, 0.4f)).Normalized() * length;
                // Opaque three-dimensional needle wedges: no alpha cards or transparency sorting.
                var across = forward * R(0.025f, 0.04f);
                var ridge = center.Lerp(needleTip, 0.4f) + Vector3.Up * 0.018f;
                Triangle(_needles, center - across, ridge, needleTip, baseColor, baseColor, tipColor);
                Triangle(_needles, ridge, center + across, needleTip, baseColor, baseColor, tipColor);
            }
        }
    }

    private void Tube(List<Vector3> centers, List<float> radii, int sides)
    {
        for (int ring = 0; ring < centers.Count - 1; ring++)
        {
            var tangent = (centers[ring + 1] - centers[ring]).Normalized();
            var u = tangent.Cross(Vector3.Up);
            if (u.LengthSquared() < 0.01f) u = Vector3.Right;
            u = u.Normalized();
            var v = tangent.Cross(u).Normalized();
            for (int side = 0; side < sides; side++)
            {
                float a = side * Mathf.Tau / sides;
                float b = (side + 1) * Mathf.Tau / sides;
                var n0 = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                var n1 = u * Mathf.Cos(b) + v * Mathf.Sin(b);
                float variation0 = 1 + 0.08f * Mathf.Sin(side * 2.7f + ring * 0.8f);
                float variation1 = 1 + 0.08f * Mathf.Sin((side + 1) * 2.7f + ring * 0.8f);
                var a0 = centers[ring] + n0 * radii[ring] * variation0;
                var a1 = centers[ring] + n1 * radii[ring] * variation1;
                var b0 = centers[ring + 1] + n0 * radii[ring + 1] * variation0;
                var b1 = centers[ring + 1] + n1 * radii[ring + 1] * variation1;
                var color = new Color(R(0.84f, 1.06f), R(0.84f, 1.04f), R(0.82f, 1.02f));
                WoodVertex(a0, n0, color); WoodVertex(b0, n0, color); WoodVertex(a1, n1, color);
                WoodVertex(a1, n1, color); WoodVertex(b0, n0, color); WoodVertex(b1, n1, color);
                _triangles += 2;
            }
        }
    }

    private void WoodVertex(Vector3 point, Vector3 normal, Color color)
    {
        _wood.SetNormal(normal); _wood.SetColor(color); _wood.AddVertex(point);
    }

    private void Triangle(SurfaceTool mesh, Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
    {
        var normal = (b - a).Cross(c - a).Normalized();
        if (normal.Y < 0) normal = -normal;
        normal = (normal * 0.35f + Vector3.Up * 0.65f).Normalized();
        mesh.SetNormal(normal);
        mesh.SetColor(ca); mesh.AddVertex(a);
        mesh.SetColor(cb); mesh.AddVertex(b);
        mesh.SetColor(cc); mesh.AddVertex(c);
        _triangles++;
    }

    private void CommitBatch(string name, SurfaceTool tool, Material material)
    {
        tool.Index();
        AddChild(new MeshInstance3D { Name = name, Mesh = tool.Commit(), MaterialOverride = material });
    }

    private float R(float min, float max) => _random.RandfRange(min, max);
    private static Vector3 Quadratic(Vector3 a, Vector3 b, Vector3 c, float t) =>
        a * (1 - t) * (1 - t) + b * 2 * t * (1 - t) + c * t * t;
}
