using Godot;

namespace Ashenfall.Enemies;

/// <summary>Additive visual remnants only. The original V1 recipe and geometry are never edited.</summary>
[Tool]
public partial class FallenGuardianV2Generator : Node3D
{
    [Export] public Material IronMaterial { get; set; }
    [Export] public Material ClothMaterial { get; set; }
    [Export] public Material EyeMaterial { get; set; }

    private SurfaceTool _tool;
    private Node3D _details;
    private int _triangles;

    public override void _Ready()
    {
        if (GetNodeOrNull("CursedDetails") != null) return;
        _details = new Node3D { Name = "CursedDetails" };
        AddChild(_details);
        Begin();
        Shoulder(-1, 1f);
        Shoulder(1, 0.7f);
        // Small fragments rather than a breastplate: sternum, most ribs and neck stay visible.
        Plate(new[] { new Vector3(0.07f, 1.445f, -0.145f), new Vector3(0.19f, 1.426f, -0.14f),
            new Vector3(0.232f, 1.377f, -0.12f), new Vector3(0.19f, 1.35f, -0.14f),
            new Vector3(0.175f, 1.308f, -0.15f), new Vector3(0.092f, 1.333f, -0.154f) }, 0.009f);
        Plate(new[] { new Vector3(-0.21f, 1.507f, -0.06f), new Vector3(-0.12f, 1.522f, -0.10f),
            new Vector3(-0.075f, 1.482f, -0.136f), new Vector3(-0.145f, 1.458f, -0.13f) }, 0.008f);
        Commit("CorrodedArmor", IronMaterial);

        Begin();
        // Folded fabric with asymmetric torn hems. All cloth is opaque, not alpha cutouts.
        ClothStrip(-0.19f, 0.12f, 0.998f, -0.09f, new[] { 0.73f, 0.80f, 0.68f, 0.76f }, 0.0f);
        ClothStrip(0.085f, 0.115f, 0.975f, -0.09f, new[] { 0.69f, 0.74f, 0.64f, 0.70f }, 1.3f);
        ClothStrip(-0.065f, 0.12f, 0.885f, -0.12f, new[] { 0.64f, 0.69f, 0.59f, 0.66f }, 0.6f);
        ClothStrip(-0.16f, 0.27f, 1.005f, 0.145f, new[] { 0.77f, 0.63f, 0.73f, 0.65f }, 2.2f);
        Commit("TatteredCloth", ClothMaterial);

        Begin();
        // Match V1's existing head pivot/tilt. The red shapes remain inside the orbital bowls.
        var anatomy = GetNode<Node3D>("Anatomy");
        var visual = anatomy.GetNode<Node3D>("Visual");
        var head = visual.GetNode<Node3D>("Head");
        var headTransform = anatomy.Transform * visual.Transform * head.Transform;
        foreach (float side in new[] { -1f, 1f })
        {
            var center = new Vector3(side * 0.051f, 0.032f, -0.081f);
            for (int sector = 0; sector < 8; sector++)
            {
                float a = sector * Mathf.Tau / 8;
                float b = (sector + 1) * Mathf.Tau / 8;
                var rimA = center + new Vector3(Mathf.Cos(a) * 0.01f, Mathf.Sin(a) * 0.0065f, 0);
                var rimB = center + new Vector3(Mathf.Cos(b) * 0.01f, Mathf.Sin(b) * 0.0065f, 0);
                Tri(headTransform * (center + Vector3.Forward * 0.002f), headTransform * rimA, headTransform * rimB);
                Tri(headTransform * (center + Vector3.Back * 0.002f), headTransform * rimB, headTransform * rimA);
            }
        }
        Commit("CursedEyes", EyeMaterial);
        SetMeta("addition_triangle_count", _triangles);
        GD.Print($"Fallen Guardian V2: {_triangles} added triangles, 3 opaque added surfaces, no real-time lights");
    }

    private void Shoulder(int side, float scale)
    {
        var center = new Vector3(side * 0.285f, 1.47f, 0.012f);
        var outline = new[] { new Vector3(-0.12f, 0.065f, -0.105f), new Vector3(0.02f, 0.1f, -0.13f),
            new Vector3(0.15f, 0.042f, -0.07f), new Vector3(0.177f, -0.03f, 0.012f),
            new Vector3(0.117f, -0.075f, 0.092f), new Vector3(0.025f, -0.025f, 0.129f),
            new Vector3(-0.11f, 0.02f, 0.08f) };
        var crest = center + new Vector3(side * 0.018f, 0.125f, 0) * scale;
        for (int i = 0; i < outline.Length; i++)
        {
            if (side < 0 && i == 3) continue; // Missing corroded sector on the larger shoulder.
            var a = center + new Vector3(outline[i].X * side, outline[i].Y, outline[i].Z) * scale;
            var next = outline[(i + 1) % outline.Length];
            var b = center + new Vector3(next.X * side, next.Y, next.Z) * scale;
            Tri(crest, a, b);
            var edge = Vector3.Down * 0.009f;
            Tri(a, a + edge, b); Tri(b, a + edge, b + edge);
        }
        // Two uneven lower lames broaden the soldier silhouette without hiding upper arm bones.
        for (int lame = 0; lame < 2; lame++)
        {
            var start = center + new Vector3(side * 0.105f, -0.045f - lame * 0.05f, -0.075f) * scale;
            Plate(new[] { start + new Vector3(-side * 0.07f, 0.025f, 0),
                start + new Vector3(side * 0.055f, 0.018f, 0.025f),
                start + new Vector3(side * 0.07f, -0.04f, 0.035f),
                start + new Vector3(side * 0.015f, -0.035f, -0.015f),
                start + new Vector3(-side * 0.04f, -0.058f, -0.015f) }, 0.007f);
        }
    }

    private void ClothStrip(float x, float width, float top, float z, float[] hem, float phase)
    {
        var points = new Vector3[4, 5];
        for (int column = 0; column < 4; column++)
        for (int row = 0; row < 5; row++)
        {
            float u = column / 3f;
            float v = row / 4f;
            points[column, row] = new Vector3(x + u * width + Mathf.Sin(v * 3 + phase) * 0.008f,
                Mathf.Lerp(top, hem[column], v), z + Mathf.Sin(u * Mathf.Tau + phase) * (0.012f + v * 0.012f));
        }
        for (int column = 0; column < 3; column++)
        for (int row = 0; row < 4; row++)
        {
            // A small hole, in addition to torn silhouettes; no transparent material.
            if (phase > 1 && column == 1 && row == 3) continue;
            Tri(points[column, row], points[column + 1, row], points[column, row + 1]);
            Tri(points[column + 1, row], points[column + 1, row + 1], points[column, row + 1]);
        }
    }

    private void Plate(Vector3[] outline, float thickness)
    {
        var flat = new Vector2[outline.Length];
        for (int i = 0; i < outline.Length; i++) flat[i] = new Vector2(outline[i].X, outline[i].Y);
        var indices = Geometry2D.TriangulatePolygon(flat);
        for (int i = 0; i < indices.Length; i += 3)
        {
            Tri(outline[indices[i]], outline[indices[i + 1]], outline[indices[i + 2]]);
            Tri(outline[indices[i]] + Vector3.Back * thickness, outline[indices[i + 2]] + Vector3.Back * thickness,
                outline[indices[i + 1]] + Vector3.Back * thickness);
        }
        for (int i = 0; i < outline.Length; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Length];
            Tri(a, b, a + Vector3.Back * thickness);
            Tri(b, b + Vector3.Back * thickness, a + Vector3.Back * thickness);
        }
    }

    private void Begin()
    {
        _tool = new SurfaceTool();
        _tool.Begin(Mesh.PrimitiveType.Triangles);
    }

    private void Tri(Vector3 a, Vector3 b, Vector3 c)
    {
        var normal = (b - a).Cross(c - a).Normalized();
        _tool.SetNormal(normal);
        _tool.SetColor(Colors.White);
        _tool.AddVertex(a); _tool.AddVertex(b); _tool.AddVertex(c);
        _triangles++;
    }

    private void Commit(string name, Material material)
    {
        _tool.SetMaterial(material);
        _tool.Index();
        _details.AddChild(new MeshInstance3D { Name = name, Mesh = _tool.Commit() });
        _tool.Dispose();
    }
}
