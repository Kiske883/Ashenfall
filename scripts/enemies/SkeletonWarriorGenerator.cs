using Godot;
using System.Collections.Generic;

namespace Ashenfall.Enemies;

/// <summary>Static art only: anatomical mesh parts and worn equipment, with no actor systems.</summary>
[Tool]
public partial class SkeletonWarriorGenerator : Node3D
{
    [Export] public int Seed { get; set; } = 401;
    [Export] public Material BoneMaterial { get; set; }
    [Export] public Material RecessMaterial { get; set; }
    [Export] public Material IronMaterial { get; set; }
    [Export] public Material WoodMaterial { get; set; }
    [Export] public Material LeatherMaterial { get; set; }

    private enum Finish { Bone, Recess, Iron, Wood, Leather }
    private readonly RandomNumberGenerator _random = new();
    private readonly Dictionary<Finish, SurfaceTool> _surfaces = new();
    private Node3D _visual;
    private Node3D _part;
    private Vector3 _pivot;
    private int _triangles;
    private int _surfaceCount;

    public override void _Ready()
    {
        _visual = GetNode<Node3D>("Visual");
        if (_visual.GetChildCount() > 0) return;
        _random.Seed = unchecked((ulong)Seed);
        Head();
        Torso();
        Pelvis();
        Arm(-1);
        Arm(1);
        Leg(-1);
        Leg(1);
        Sword();
        Shield();
        Remnants();
        SetMeta("triangle_count", _triangles);
        SetMeta("surface_count", _surfaceCount);
        SetMeta("material_count", 5);
        GD.Print($"Fallen Guardian art: {_triangles} triangles, {_surfaceCount} surfaces, 5 shared materials");
    }

    private void Head()
    {
        BeginPart("Head", new Vector3(0, 1.73f, 0));
        // Vault is open in the lower front: sockets are real openings, not painted eyes.
        Ellipsoid(new Vector3(0, 1.805f, 0.014f), new Vector3(0.116f, 0.145f, 0.107f), Finish.Bone, 10, 20, true);
        // Restrained old fracture across the cranial vault; not an emissive ornament.
        Tube(new[] { new Vector3(-0.015f, 1.94f, -0.024f), new Vector3(-0.012f, 1.905f, -0.064f),
            new Vector3(0.002f, 1.875f, -0.081f), new Vector3(-0.007f, 1.847f, -0.089f) },
            new[] { 0.0014f, 0.0015f, 0.0012f, 0.0008f }, Finish.Recess, 4);
        foreach (float side in new[] { -1f, 1f })
        {
            var eye = new Vector3(side * 0.051f, 1.762f, -0.096f);
            OvalRing(eye, new Vector2(0.043f, 0.036f), 0.009f, Finish.Bone, 14);
            // Deep concave occlusion bowl sits behind the opening.
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.Tau / 14;
                float b = (i + 1) * Mathf.Tau / 14;
                Tri(eye + new Vector3(0, 0, 0.036f),
                    eye + new Vector3(Mathf.Cos(a) * 0.036f, Mathf.Sin(a) * 0.03f, 0.006f),
                    eye + new Vector3(Mathf.Cos(b) * 0.036f, Mathf.Sin(b) * 0.03f, 0.006f), Finish.Recess);
            }
            Tube(new[] { new Vector3(side * 0.012f, 1.797f, -0.102f),
                new Vector3(side * 0.061f, 1.808f, -0.099f), new Vector3(side * 0.102f, 1.787f, -0.052f) },
                new[] { 0.012f, 0.014f, 0.01f }, Finish.Bone, 7);
            Tube(new[] { new Vector3(side * 0.082f, 1.738f, -0.091f),
                new Vector3(side * 0.103f, 1.717f, -0.047f), new Vector3(side * 0.087f, 1.696f, 0.002f) },
                new[] { 0.013f, 0.012f, 0.015f }, Finish.Bone, 7);
            Tube(new[] { new Vector3(side * 0.087f, 1.705f, 0.005f),
                new Vector3(side * 0.089f, 1.649f, -0.032f), new Vector3(side * 0.052f, 1.632f, -0.09f),
                new Vector3(0, 1.63f, -0.103f) }, new[] { 0.012f, 0.017f, 0.016f, 0.015f }, Finish.Bone, 8);
            Ellipsoid(new Vector3(side * 0.033f, 1.704f, -0.076f), new Vector3(0.04f, 0.021f, 0.026f), Finish.Bone, 5, 10);
        }
        Tube(new[] { new Vector3(0, 1.794f, -0.098f), new Vector3(0, 1.738f, -0.111f) },
            new[] { 0.009f, 0.006f }, Finish.Bone, 6);
        Tri(new Vector3(-0.017f, 1.732f, -0.108f), new Vector3(0.017f, 1.732f, -0.108f),
            new Vector3(0, 1.703f, -0.12f), Finish.Recess);
        Ellipsoid(new Vector3(0, 1.667f, -0.072f), new Vector3(0.065f, 0.027f, 0.013f), Finish.Recess, 5, 12);
        for (int tooth = 0; tooth < 8; tooth++)
        {
            float x = (tooth - 3.5f) * 0.012f;
            float z = -0.104f + Mathf.Abs(x) * 0.3f;
            Box(new Vector3(x, 1.682f, z), new Vector3(0.009f, tooth == 1 ? 0.013f : 0.02f, 0.015f), Finish.Bone);
            if (tooth != 6)
                Box(new Vector3(x, 1.652f, z), new Vector3(0.009f, 0.015f, 0.013f), Finish.Bone);
        }
        EndPart();
        _part.RotationDegrees = new Vector3(8, -4, 0);
    }

    private void Torso()
    {
        BeginPart("RibcageAndSpine", new Vector3(0, 1.19f, 0));
        for (int vertebra = 0; vertebra < 14; vertebra++)
        {
            float y = 0.99f + vertebra * 0.042f;
            float z = 0.055f + Mathf.Sin(vertebra * 0.34f) * 0.018f;
            Ellipsoid(new Vector3(0, y, z), new Vector3(0.028f, 0.019f, 0.026f), Finish.Bone, 4, 7);
            Tube(new[] { new Vector3(0, y, z), new Vector3(0, y + 0.006f, z + 0.052f) },
                new[] { 0.014f, 0.004f }, Finish.Bone, 5);
        }
        for (int rib = 0; rib < 8; rib++)
        {
            float y = 1.474f - rib * 0.044f;
            float width = 0.145f + Mathf.Sin((rib + 1) * Mathf.Pi / 10) * 0.065f;
            foreach (float side in new[] { -1f, 1f })
                CurvedTube(new[] { new Vector3(side * 0.025f, y, 0.065f),
                    new Vector3(side * width * 0.67f, y + 0.003f, 0.105f),
                    new Vector3(side * width, y - 0.02f, 0.01f),
                    new Vector3(side * width * 0.75f, y - 0.055f, -0.095f),
                    new Vector3(side * 0.03f, y - 0.075f, -0.122f) },
                    new[] { 0.013f, 0.014f, 0.012f, 0.011f, 0.009f }, Finish.Bone, 7);
        }
        Tube(new[] { new Vector3(0, 1.48f, -0.096f), new Vector3(0, 1.37f, -0.132f), new Vector3(0, 1.12f, -0.12f) },
            new[] { 0.022f, 0.017f, 0.009f }, Finish.Bone, 8);
        foreach (float side in new[] { -1f, 1f })
        {
            Tube(new[] { new Vector3(0, 1.485f, -0.07f), new Vector3(side * 0.15f, 1.51f, -0.03f),
                new Vector3(side * 0.277f, 1.469f, 0.012f) }, new[] { 0.018f, 0.017f, 0.023f }, Finish.Bone, 8);
            Prism(new[] { new Vector2(side * 0.04f, 1.47f), new Vector2(side * 0.23f, 1.48f),
                new Vector2(side * 0.14f, 1.28f) }, 0.105f, 0.016f, Finish.Bone);
        }
        EndPart();
    }

    private void Pelvis()
    {
        BeginPart("Pelvis", new Vector3(0, 0.94f, 0));
        foreach (float side in new[] { -1f, 1f })
        {
            Prism(new[] { new Vector2(side * 0.025f, 1.0f), new Vector2(side * 0.11f, 1.038f),
                new Vector2(side * 0.175f, 1.005f), new Vector2(side * 0.158f, 0.953f),
                new Vector2(side * 0.08f, 0.93f), new Vector2(side * 0.025f, 0.957f) }, 0.02f, 0.028f, Finish.Bone);
            OvalRing(new Vector3(side * 0.079f, 0.901f, -0.011f), new Vector2(0.048f, 0.055f), 0.014f, Finish.Bone, 12);
            Tube(new[] { new Vector3(side * 0.13f, 0.944f, 0.025f), new Vector3(side * 0.11f, 0.862f, -0.005f),
                new Vector3(0, 0.852f, -0.045f) }, new[] { 0.022f, 0.018f, 0.013f }, Finish.Bone, 8);
        }
        Prism(new[] { new Vector2(-0.037f, 1.0f), new Vector2(0.037f, 1.0f), new Vector2(0, 0.87f) }, 0.058f, 0.033f, Finish.Bone);
        EndPart();
    }

    private void Arm(int side)
    {
        var shoulder = new Vector3(side * 0.279f, 1.46f, 0.012f);
        var elbow = new Vector3(side * 0.377f, side > 0 ? 1.17f : 1.21f, -0.034f);
        var wrist = new Vector3(side * 0.426f, side > 0 ? 0.944f : 1.1f, -0.216f);
        BeginPart(side < 0 ? "LeftArm" : "RightArm", shoulder);
        LongBone(shoulder, elbow, 0.022f);
        LongBone(elbow + Vector3.Right * 0.014f, wrist + Vector3.Right * 0.012f, 0.012f);
        LongBone(elbow - Vector3.Right * 0.015f, wrist - Vector3.Right * 0.012f, 0.011f);
        var palm = wrist + new Vector3(side * 0.012f, -0.04f, -0.015f);
        for (int finger = 0; finger < 4; finger++)
        {
            var knuckle = palm + new Vector3((finger - 1.5f) * 0.014f, -0.015f, -0.012f);
            Tube(new[] { wrist, knuckle, knuckle + new Vector3(0, -0.037f, -0.016f),
                knuckle + new Vector3(-side * 0.017f, -0.052f, 0.006f) }, new[] { 0.0055f, 0.0055f, 0.0045f, 0.0035f }, Finish.Bone, 6);
        }
        Tube(new[] { wrist + Vector3.Right * side * 0.02f, palm + new Vector3(side * 0.032f, -0.014f, -0.04f),
            palm + new Vector3(side * 0.006f, -0.04f, -0.042f) }, new[] { 0.007f, 0.006f, 0.004f }, Finish.Bone, 6);
        if (side > 0)
        {
            var rim = new[] { new Vector3(-0.07f, 0.045f, -0.08f), new Vector3(0.04f, 0.06f, -0.085f),
                new Vector3(0.11f, -0.005f, -0.04f), new Vector3(0.09f, -0.06f, 0.03f),
                new Vector3(0.01f, -0.045f, 0.08f), new Vector3(-0.07f, 0.01f, 0.07f) };
            for (int i = 0; i < rim.Length; i++)
                if (i != 2) Tri(shoulder + Vector3.Up * 0.075f, shoulder + rim[i],
                    shoulder + rim[(i + 1) % rim.Length], Finish.Iron);
        }
        EndPart();
    }

    private void Leg(int side)
    {
        var hip = new Vector3(side * 0.11f, 0.878f, 0.015f);
        var knee = new Vector3(side * 0.14f, 0.492f, side > 0 ? -0.025f : 0.035f);
        var ankle = new Vector3(side * 0.16f, 0.095f, side > 0 ? -0.015f : 0.07f);
        BeginPart(side < 0 ? "LeftLeg" : "RightLeg", hip);
        LongBone(hip, knee, 0.026f);
        LongBone(knee, ankle, 0.021f);
        LongBone(knee + Vector3.Right * side * 0.027f, ankle + Vector3.Right * side * 0.025f, 0.009f);
        Ellipsoid(knee + Vector3.Forward * 0.026f, new Vector3(0.025f, 0.029f, 0.014f), Finish.Bone, 5, 8);
        var heel = ankle + new Vector3(0, -0.055f, 0.025f);
        Ellipsoid(heel, new Vector3(0.036f, 0.025f, 0.044f), Finish.Bone, 5, 8);
        for (int toe = 0; toe < 5; toe++)
        {
            float offset = (toe - 2) * 0.014f;
            var toeBase = heel + new Vector3(offset, -0.014f, -0.115f);
            Tube(new[] { heel, toeBase, toeBase + new Vector3(0, 0, -0.045f + toe * 0.004f) },
                new[] { 0.007f, 0.006f, 0.003f }, Finish.Bone, 6);
        }
        EndPart();
    }

    private void Sword()
    {
        BeginPart("Sword", new Vector3(0.438f, 0.91f, -0.23f));
        var pommel = new Vector3(0.425f, 0.966f, -0.21f);
        var guard = new Vector3(0.465f, 0.79f, -0.26f);
        Tube(new[] { pommel, guard }, new[] { 0.014f, 0.014f }, Finish.Leather, 8);
        Ellipsoid(pommel, new Vector3(0.022f, 0.024f, 0.022f), Finish.Iron, 5, 8);
        Tube(new[] { guard + Vector3.Left * 0.11f, guard + Vector3.Left * 0.04f + Vector3.Up * 0.01f,
            guard + Vector3.Right * 0.06f, guard + Vector3.Right * 0.11f + Vector3.Down * 0.02f },
            new[] { 0.011f, 0.014f, 0.014f, 0.008f }, Finish.Iron, 6);
        var tip = new Vector3(0.68f, 0.065f, -0.39f);
        var axis = (tip - guard).Normalized();
        var width = axis.Cross(Vector3.Back).Normalized() * 0.037f;
        var left = guard - width;
        var right = guard + width;
        var ridge = guard + Vector3.Forward * 0.01f;
        var bevelEnd = guard.Lerp(tip, 0.86f);
        Tri(left, bevelEnd - width * 0.55f, ridge, Finish.Iron);
        Tri(ridge, bevelEnd - width * 0.55f, bevelEnd + Vector3.Forward * 0.009f, Finish.Iron);
        Tri(ridge, bevelEnd + Vector3.Forward * 0.009f, right, Finish.Iron);
        Tri(right, bevelEnd + Vector3.Forward * 0.009f, bevelEnd + width * 0.55f, Finish.Iron);
        Tri(bevelEnd - width * 0.55f, tip, bevelEnd + Vector3.Forward * 0.009f, Finish.Iron);
        Tri(bevelEnd + Vector3.Forward * 0.009f, tip, bevelEnd + width * 0.55f, Finish.Iron);
        Tri(left, right, tip, Finish.Iron); // Back facet.
        EndPart();
    }

    private void Shield()
    {
        var center = new Vector3(-0.45f, 1.065f, -0.30f);
        BeginPart("Shield", center);
        // Separate irregular boards leave real splits; the right board has a missing edge chunk.
        Prism(Translate(new[] { new Vector2(-0.29f, 0.28f), new Vector2(-0.13f, 0.435f),
            new Vector2(-0.095f, 0.44f), new Vector2(-0.095f, -0.33f), new Vector2(-0.25f, -0.16f),
            new Vector2(-0.305f, 0.08f) }, center), center.Z - 0.06f, 0.035f, Finish.Wood);
        Prism(Translate(new[] { new Vector2(-0.083f, 0.443f), new Vector2(0.09f, 0.43f),
            new Vector2(0.09f, -0.35f), new Vector2(0.015f, -0.46f), new Vector2(-0.083f, -0.335f) }, center), center.Z - 0.067f, 0.036f, Finish.Wood);
        Prism(Translate(new[] { new Vector2(0.103f, 0.427f), new Vector2(0.22f, 0.39f),
            new Vector2(0.286f, 0.255f), new Vector2(0.23f, 0.19f), new Vector2(0.295f, 0.09f),
            new Vector2(0.245f, -0.16f), new Vector2(0.103f, -0.34f) }, center), center.Z - 0.062f, 0.034f, Finish.Wood);
        var rim = new[] { new Vector3(-0.29f, 0.28f, -0.084f), new Vector3(-0.13f, 0.435f, -0.084f),
            new Vector3(0.21f, 0.39f, -0.084f), new Vector3(0.285f, 0.255f, -0.084f),
            new Vector3(0.294f, 0.09f, -0.084f), new Vector3(0.245f, -0.16f, -0.084f),
            new Vector3(0.015f, -0.46f, -0.084f), new Vector3(-0.25f, -0.16f, -0.084f), new Vector3(-0.305f, 0.08f, -0.084f) };
        for (int i = 0; i < rim.Length; i++)
        {
            if (i == 3) continue; // Broken rim at the chipped right edge.
            Tube(new[] { center + rim[i], center + rim[(i + 1) % rim.Length] }, new[] { 0.017f, 0.017f }, Finish.Iron, 5);
        }
        Ellipsoid(center + new Vector3(0, 0.03f, -0.10f), new Vector3(0.09f, 0.09f, 0.036f), Finish.Iron, 6, 12);
        foreach (float side in new[] { -1f, 1f })
        {
            Ellipsoid(center + new Vector3(side * 0.17f, 0.24f, -0.087f), Vector3.One * 0.012f, Finish.Iron, 4, 6);
            Ellipsoid(center + new Vector3(side * 0.13f, -0.19f, -0.087f), Vector3.One * 0.012f, Finish.Iron, 4, 6);
        }
        Tube(new[] { center + new Vector3(-0.1f, 0.09f, 0.015f), center + new Vector3(0.07f, -0.06f, 0.015f) },
            new[] { 0.02f, 0.02f }, Finish.Leather, 6);
        EndPart();
    }

    private void Remnants()
    {
        BeginPart("LeatherAndRags", new Vector3(0, 1.02f, 0));
        // A narrow diagonal harness leaves most of the rib cage exposed.
        var a = new Vector3(-0.22f, 1.49f, -0.09f);
        var b = new Vector3(0.13f, 1.08f, -0.13f);
        var offset = new Vector3(0.014f, 0.012f, 0);
        Tri(a - offset, b - offset, a + offset, Finish.Leather);
        Tri(a + offset, b - offset, b + offset, Finish.Leather);
        for (int rag = 0; rag < 3; rag++)
        {
            float x = -0.16f + rag * 0.13f;
            Prism(new[] { new Vector2(x, 1.01f), new Vector2(x + 0.1f, 1.0f),
                new Vector2(x + 0.09f, 0.79f), new Vector2(x + 0.055f, 0.835f),
                new Vector2(x + 0.025f, 0.75f) }, 0.11f, 0.004f, Finish.Leather);
        }
        EndPart();
    }

    private void BeginPart(string name, Vector3 pivot)
    {
        _part = new Node3D { Name = name, Position = pivot };
        _visual.AddChild(_part);
        _pivot = pivot;
        _surfaces.Clear();
    }

    private void EndPart()
    {
        var mesh = new ArrayMesh();
        foreach (var entry in _surfaces)
        {
            entry.Value.SetMaterial(MaterialFor(entry.Key));
            entry.Value.Index();
            entry.Value.Commit(mesh);
            entry.Value.Dispose();
            _surfaceCount++;
        }
        _part.AddChild(new MeshInstance3D { Name = "Geometry", Mesh = mesh });
    }

    private Material MaterialFor(Finish finish) => finish switch
    {
        Finish.Bone => BoneMaterial, Finish.Recess => RecessMaterial,
        Finish.Iron => IronMaterial, Finish.Wood => WoodMaterial, _ => LeatherMaterial
    };

    private void Vertex(Vector3 p, Vector3 normal, Finish finish, float shade = 1)
    {
        if (!_surfaces.TryGetValue(finish, out var tool))
        {
            tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            _surfaces.Add(finish, tool);
        }
        tool.SetNormal(normal);
        tool.SetColor(new Color(shade, shade, shade));
        tool.AddVertex(p - _pivot);
    }

    private void Tri(Vector3 a, Vector3 b, Vector3 c, Finish finish)
    {
        var normal = (b - a).Cross(c - a).Normalized();
        Vertex(a, normal, finish); Vertex(b, normal, finish); Vertex(c, normal, finish);
        _triangles++;
    }

    private void Tube(Vector3[] centers, float[] radii, Finish finish, int sides = 8)
    {
        // Parallel transport approximation using per-ring tangent frames keeps curved ribs continuous.
        var frames = new (Vector3 U, Vector3 V)[centers.Length];
        for (int ring = 0; ring < centers.Length; ring++)
        {
            var tangent = (centers[Mathf.Min(ring + 1, centers.Length - 1)] - centers[Mathf.Max(0, ring - 1)]).Normalized();
            var u = tangent.Cross(Vector3.Forward);
            if (u.LengthSquared() < 0.01f) u = Vector3.Right;
            u = u.Normalized();
            frames[ring] = (u, tangent.Cross(u).Normalized());
        }
        for (int ring = 0; ring < centers.Length - 1; ring++)
        for (int side = 0; side < sides; side++)
        {
            var points = new Vector3[4];
            var normals = new Vector3[4];
            for (int corner = 0; corner < 4; corner++)
            {
                int r = ring + corner / 2;
                float angle = (side + corner % 2) * Mathf.Tau / sides;
                var n = frames[r].U * Mathf.Cos(angle) + frames[r].V * Mathf.Sin(angle);
                points[corner] = centers[r] + n * radii[r] * (1 + 0.055f * Mathf.Sin(angle * 3 + r));
                normals[corner] = n;
            }
            float shade = _random.RandfRange(0.88f, 1.02f);
            foreach (int i in new[] { 0, 2, 1, 1, 2, 3 }) Vertex(points[i], normals[i], finish, shade);
            _triangles += 2;
        }
        foreach (int end in new[] { 0, centers.Length - 1 })
        for (int side = 0; side < sides; side++)
        {
            float a = side * Mathf.Tau / sides;
            float b = (side + 1) * Mathf.Tau / sides;
            Tri(centers[end], centers[end] + (frames[end].U * Mathf.Cos(a) + frames[end].V * Mathf.Sin(a)) * radii[end],
                centers[end] + (frames[end].U * Mathf.Cos(b) + frames[end].V * Mathf.Sin(b)) * radii[end], finish);
        }
    }

    private void LongBone(Vector3 a, Vector3 b, float radius)
    {
        float[] profile = { 1.35f, 1.65f, 1, 0.77f, 0.95f, 1.6f, 1.3f };
        var points = new Vector3[profile.Length];
        var radii = new float[profile.Length];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)(points.Length - 1);
            points[i] = a.Lerp(b, t) + Vector3.Back * Mathf.Sin(t * Mathf.Pi) * 0.008f;
            radii[i] = radius * profile[i];
        }
        Tube(points, radii, Finish.Bone, 10);
    }

    private void CurvedTube(Vector3[] controls, float[] radiusControls, Finish finish, int sides)
    {
        var points = new List<Vector3>();
        var radii = new List<float>();
        for (int segment = 0; segment < controls.Length - 1; segment++)
        for (int step = 0; step < 3; step++)
        {
            float t = step / 3f;
            points.Add(controls[segment].CubicInterpolate(controls[segment + 1],
                controls[Mathf.Max(0, segment - 1)], controls[Mathf.Min(controls.Length - 1, segment + 2)], t));
            radii.Add(Mathf.Lerp(radiusControls[segment], radiusControls[segment + 1], t));
        }
        points.Add(controls[^1]);
        radii.Add(radiusControls[^1]);
        Tube(points.ToArray(), radii.ToArray(), finish, sides);
    }

    private void OvalRing(Vector3 center, Vector2 radius, float thickness, Finish finish, int segments)
    {
        var points = new Vector3[segments + 1];
        var radii = new float[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.Tau / segments;
            float irregularity = 1 + 0.06f * Mathf.Sin(angle * 3);
            points[i] = center + new Vector3(Mathf.Cos(angle) * radius.X, Mathf.Sin(angle) * radius.Y, 0) * irregularity;
            radii[i] = thickness;
        }
        Tube(points, radii, finish, 7);
    }

    private void Ellipsoid(Vector3 center, Vector3 radius, Finish finish, int rings, int segments,
        bool openFace = false, bool upperOnly = false)
    {
        for (int row = 0; row < rings; row++)
        for (int column = 0; column < segments; column++)
        {
            var p = new Vector3[4];
            for (int corner = 0; corner < 4; corner++)
            {
                float latitude = (row + corner / 2) * Mathf.Pi / rings;
                float angle = (column + corner % 2) * Mathf.Tau / segments;
                p[corner] = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(angle), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(angle)) * radius;
            }
            var average = (p[0] + p[1] + p[2] + p[3]) / 4;
            if (openFace && average.Z < -radius.Z * 0.35f && average.Y < -radius.Y * 0.05f) continue;
            if (upperOnly && average.Y < -radius.Y * 0.15f) continue;
            foreach (int i in new[] { 0, 2, 1, 1, 2, 3 })
                Vertex(center + p[i], (p[i] / (radius * radius)).Normalized(), finish);
            _triangles += 2;
        }
    }

    private void Box(Vector3 center, Vector3 size, Finish finish) => Prism(new[]
    {
        new Vector2(center.X - size.X / 2, center.Y - size.Y / 2),
        new Vector2(center.X + size.X / 2, center.Y - size.Y / 2),
        new Vector2(center.X + size.X / 2, center.Y + size.Y / 2),
        new Vector2(center.X - size.X / 2, center.Y + size.Y / 2)
    }, center.Z, size.Z, finish);

    private void Prism(Vector2[] outline, float z, float depth, Finish finish)
    {
        var indices = Geometry2D.TriangulatePolygon(outline);
        for (int triangle = 0; triangle < indices.Length; triangle += 3)
        {
            var a = outline[indices[triangle]];
            var b = outline[indices[triangle + 1]];
            var c = outline[indices[triangle + 2]];
            Tri(new Vector3(a.X, a.Y, z - depth / 2), new Vector3(c.X, c.Y, z - depth / 2), new Vector3(b.X, b.Y, z - depth / 2), finish);
            Tri(new Vector3(a.X, a.Y, z + depth / 2), new Vector3(b.X, b.Y, z + depth / 2), new Vector3(c.X, c.Y, z + depth / 2), finish);
        }
        for (int i = 0; i < outline.Length; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Length];
            var a0 = new Vector3(a.X, a.Y, z - depth / 2);
            var b0 = new Vector3(b.X, b.Y, z - depth / 2);
            Tri(a0, b0, a0 + Vector3.Back * depth, finish);
            Tri(b0, b0 + Vector3.Back * depth, a0 + Vector3.Back * depth, finish);
        }
    }

    private static Vector2[] Translate(Vector2[] points, Vector3 offset)
    {
        for (int i = 0; i < points.Length; i++) points[i] += new Vector2(offset.X, offset.Y);
        return points;
    }
}
