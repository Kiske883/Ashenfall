using Godot;
using System.Collections.Generic;

namespace Ashenfall.Characters;

/// <summary>Original static procedural art, unit scale, soles at zero and bald crown at 1.460.</summary>
[Tool]
public partial class DwarfBerserkerGenerator : Node3D
{
    [Export] public Material Skin { get; set; }
    [Export] public Material Recess { get; set; }
    [Export] public Material Leather { get; set; }
    [Export] public Material Iron { get; set; }
    [Export] public Material Edge { get; set; }
    [Export] public Material Hair { get; set; }
    [Export] public Material Wood { get; set; }
    private enum Finish { Skin, Recess, Leather, Iron, Edge, Hair, Wood }
    private readonly Dictionary<Finish, SurfaceTool> _surfaces = new();
    private Node3D _part;
    private Vector3 _origin;
    private int _triangles, _surfaceCount;
    private readonly Vector3 _axeButt = new(-0.46f, 0.45f, -0.43f);
    private readonly Vector3 _axeEnd = new(0.46f, 1.50f, -0.43f);

    public override void _Ready()
    {
        if (HasNode("PoseRoot")) return;
        var root = Pivot(this, "PoseRoot", Vector3.Zero);
        var pelvis = Pivot(root, "PelvisPivot", new Vector3(0, 0.59f, 0));
        var torso = Pivot(pelvis, "TorsoPivot", new Vector3(0, 0.83f, 0));
        Body(pelvis, torso);
        Head(torso);
        foreach (int side in new[] { -1, 1 }) { Leg(pelvis, side); Arm(torso, side); }
        Axe(root);
        SetMeta("triangle_count", _triangles);
        SetMeta("surface_count", _surfaceCount);
        SetMeta("material_count", 7);
        GD.Print($"Dwarf Berserker: {_triangles} triangles, {_surfaceCount} surfaces, 7 materials, crown 1.460");
    }

    private Node3D Pivot(Node3D parent, string name, Vector3 point)
    {
        var node = new Node3D { Name = name };
        parent.AddChild(node); node.GlobalPosition = ToGlobal(point);
        return node;
    }
    private void Begin(Node3D parent, string name, Vector3 pivot)
    {
        _part = Pivot(parent, name, pivot); _origin = pivot; _surfaces.Clear();
    }
    private void End()
    {
        var mesh = new ArrayMesh();
        foreach (var pair in _surfaces)
        {
            pair.Value.SetMaterial(MaterialFor(pair.Key)); pair.Value.Index();
            pair.Value.Commit(mesh); pair.Value.Dispose(); _surfaceCount++;
        }
        _part.AddChild(new MeshInstance3D { Name = "Geometry", Mesh = mesh });
    }
    private Material MaterialFor(Finish finish) => finish switch
    {
        Finish.Skin => Skin, Finish.Recess => Recess, Finish.Leather => Leather,
        Finish.Iron => Iron, Finish.Edge => Edge, Finish.Hair => Hair, _ => Wood
    };
    private void Vertex(Vector3 p, Vector3 normal, Finish finish, float tint = 1)
    {
        if (!_surfaces.TryGetValue(finish, out var surface))
        {
            surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles); _surfaces[finish] = surface;
        }
        float wear = 0.94f + 0.06f * Mathf.Sin(p.X * 37 + p.Y * 29 + p.Z * 19);
        surface.SetNormal(normal); surface.SetColor(new Color(wear * tint, wear * tint, wear * tint));
        surface.AddVertex(p - _origin);
    }
    private void Tri(Vector3 a, Vector3 b, Vector3 c, Finish finish, float tint = 1)
    {
        var n = (b - a).Cross(c - a);
        if (n.LengthSquared() < 1e-14f) return;
        n = n.Normalized(); Vertex(a, n, finish, tint); Vertex(b, n, finish, tint); Vertex(c, n, finish, tint); _triangles++;
    }
    private void Ellipsoid(Vector3 center, Vector3 radii, Finish finish, int rings = 8, int segments = 14)
    {
        for (int row = 0; row < rings; row++)
        for (int column = 0; column < segments; column++)
        {
            var p = new Vector3[4];
            for (int corner = 0; corner < 4; corner++)
            {
                float latitude = (row + corner / 2) * Mathf.Pi / rings;
                float longitude = (column + corner % 2) * Mathf.Tau / segments;
                p[corner] = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude)) * radii;
            }
            foreach (var indices in new[] { new[] { 0, 2, 1 }, new[] { 1, 2, 3 } })
            {
                if ((p[indices[1]] - p[indices[0]]).Cross(p[indices[2]] - p[indices[0]]).LengthSquared() < 1e-14f) continue;
                foreach (int i in indices) Vertex(center + p[i], (p[i] / (radii * radii)).Normalized(), finish);
                _triangles++;
            }
        }
    }
    private void Tube(Vector3[] points, float[] radii, Finish finish, int sides = 10, float grooves = 0)
    {
        var frame = new (Vector3 X, Vector3 Z)[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            var tangent = (points[Mathf.Min(points.Length - 1, i + 1)] - points[Mathf.Max(0, i - 1)]).Normalized();
            var x = tangent.Cross(Vector3.Forward);
            if (x.LengthSquared() < 0.01f) x = tangent.Cross(Vector3.Right);
            x = x.Normalized(); frame[i] = (x, tangent.Cross(x).Normalized());
        }
        for (int ring = 0; ring < points.Length - 1; ring++)
        for (int side = 0; side < sides; side++)
        {
            var p = new Vector3[4]; var n = new Vector3[4];
            for (int corner = 0; corner < 4; corner++)
            {
                int r = ring + corner / 2;
                float a = (side + corner % 2) * Mathf.Tau / sides;
                n[corner] = frame[r].X * Mathf.Cos(a) + frame[r].Z * Mathf.Sin(a);
                p[corner] = points[r] + n[corner] * radii[r] * (1 + grooves * Mathf.Cos(a * 5 + r * 0.15f));
            }
            foreach (int i in new[] { 0, 2, 1, 1, 2, 3 }) Vertex(p[i], n[i], finish);
            _triangles += 2;
        }
        foreach (int end in new[] { 0, points.Length - 1 })
        for (int side = 0; side < sides; side++)
        {
            float a = side * Mathf.Tau / sides, b = (side + 1) * Mathf.Tau / sides;
            Tri(points[end], points[end] + (frame[end].X * Mathf.Cos(a) + frame[end].Z * Mathf.Sin(a)) * radii[end],
                points[end] + (frame[end].X * Mathf.Cos(b) + frame[end].Z * Mathf.Sin(b)) * radii[end], finish);
        }
    }
    private void Link(Vector3 a, Vector3 b, float radiusA, float radiusB, Finish finish, int sides = 12) =>
        Tube(new[] { a, a.Lerp(b, 0.45f), b }, new[] { radiusA, Mathf.Max(radiusA, radiusB) * 1.08f, radiusB }, finish, sides);
    private void Band(Vector3 center, Vector3 axis, float radius, float width, Finish finish, float depthRatio = 1)
    {
        var x = axis.Cross(Vector3.Forward);
        if (x.LengthSquared() < 0.01f) x = axis.Cross(Vector3.Right);
        x = x.Normalized(); var z = axis.Cross(x).Normalized();
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.Tau / 16, b = (i + 1) * Mathf.Tau / 16;
            var radialA = (x * Mathf.Cos(a) + z * Mathf.Sin(a)) * radius;
            var radialB = (x * Mathf.Cos(b) + z * Mathf.Sin(b)) * radius;
            radialA.Z *= depthRatio; radialB.Z *= depthRatio;
            var p = center + radialA;
            var q = center + radialB;
            Tri(p - axis * width / 2, q - axis * width / 2, p + axis * width / 2, finish);
            Tri(q - axis * width / 2, q + axis * width / 2, p + axis * width / 2, finish);
        }
    }
    private void Plate(Vector2[] polygon, float z, float thickness, Finish finish)
    {
        var indices = Geometry2D.TriangulatePolygon(polygon);
        for (int i = 0; i < indices.Length; i += 3)
        {
            var a = new Vector3(polygon[indices[i]].X, polygon[indices[i]].Y, z);
            var b = new Vector3(polygon[indices[i + 1]].X, polygon[indices[i + 1]].Y, z);
            var c = new Vector3(polygon[indices[i + 2]].X, polygon[indices[i + 2]].Y, z);
            Tri(a, c, b, finish); Tri(a + Vector3.Back * thickness, b + Vector3.Back * thickness, c + Vector3.Back * thickness, finish);
        }
        for (int i = 0; i < polygon.Length; i++)
        {
            var a = new Vector3(polygon[i].X, polygon[i].Y, z);
            var b = new Vector3(polygon[(i + 1) % polygon.Length].X, polygon[(i + 1) % polygon.Length].Y, z);
            Tri(a, b, a + Vector3.Back * thickness, finish); Tri(b, b + Vector3.Back * thickness, a + Vector3.Back * thickness, finish);
        }
    }

    private void Body(Node3D pelvis, Node3D torso)
    {
        Begin(pelvis, "HipsAndBelt", new Vector3(0, 0.59f, 0));
        Ellipsoid(new Vector3(0, 0.59f, 0.035f), new Vector3(0.27f, 0.13f, 0.18f), Finish.Leather);
        Band(new Vector3(0, 0.665f, 0.025f), Vector3.Up, 0.265f, 0.075f, Finish.Leather, 0.70f);
        Plate(new[] { new Vector2(-0.065f,0.7f),new Vector2(0.065f,0.7f),new Vector2(0.06f,0.63f),new Vector2(-0.06f,0.63f) }, -0.166f, 0.02f, Finish.Iron);
        for (int side = -1; side <= 1; side++)
        {
            float x = side * 0.14f;
            Plate(new[] { new Vector2(x-0.065f,0.63f),new Vector2(x+0.065f,0.63f),new Vector2(x+0.055f,0.46f),new Vector2(x-0.05f,0.49f) }, -0.18f, 0.03f, Finish.Leather);
        }
        End();
        Begin(torso, "LeatherCuirass", new Vector3(0, 0.87f, 0));
        Ellipsoid(new Vector3(0, 0.87f, 0.035f), new Vector3(0.31f, 0.235f, 0.195f), Finish.Leather, 10, 18);
        foreach (float side in new[] { -1f, 1f })
        {
            Tube(new[] { new Vector3(side*0.23f,1.075f,-0.03f),new Vector3(side*0.21f,0.98f,-0.155f),new Vector3(side*0.17f,0.72f,-0.14f) }, new[] {0.033f,0.027f,0.025f}, Finish.Leather, 6);
            for (int rivet = 0; rivet < 4; rivet++)
                Ellipsoid(new Vector3(side*0.21f,0.94f-rivet*0.055f,-0.167f),new Vector3(0.011f,0.011f,0.006f),Finish.Iron,4,7);
            Plate(new[] {new Vector2(side*0.12f,1.02f),new Vector2(side*0.27f,0.99f),new Vector2(side*0.25f,0.86f),new Vector2(side*0.13f,0.88f)},-0.172f,0.017f,Finish.Iron);
        }
        // Rear lacing and broad back panels remain readable in the rear inspection view.
        for (int i = 0; i < 5; i++) Link(new Vector3(-0.06f,0.97f-i*0.045f,0.223f),new Vector3(0.06f,0.94f-i*0.045f,0.223f),0.006f,0.006f,Finish.Wood,5);
        End();
    }
    private void Head(Node3D torso)
    {
        var pivot = Pivot(torso, "HeadPivot", new Vector3(0, 1.19f, 0));
        Begin(pivot, "BaldHead", new Vector3(0, 1.305f, 0.015f));
        Ellipsoid(new Vector3(0, 1.305f, 0.015f),new Vector3(0.145f,0.155f,0.125f),Finish.Skin,14,24);
        Ellipsoid(new Vector3(0,1.222f,-0.005f),new Vector3(0.14f,0.07f,0.105f),Finish.Skin);
        Link(new Vector3(0,1.08f,0.025f),new Vector3(0,1.21f,0.02f),0.09f,0.095f,Finish.Skin);
        foreach (float side in new[] {-1f,1f})
        {
            Ellipsoid(new Vector3(side*0.15f,1.29f,0.02f),new Vector3(0.027f,0.046f,0.026f),Finish.Skin,6,10);
            Ellipsoid(new Vector3(side*0.16f,1.288f,-0.002f),new Vector3(0.009f,0.026f,0.009f),Finish.Recess,5,8);
            Ellipsoid(new Vector3(side*0.06f,1.312f,-0.105f),new Vector3(0.036f,0.014f,0.018f),Finish.Recess,5,10);
            Ellipsoid(new Vector3(side*0.058f,1.312f,-0.123f),new Vector3(0.012f,0.006f,0.004f),Finish.Edge,4,8);
            Ellipsoid(new Vector3(side*0.057f,1.312f,-0.127f),new Vector3(0.004f,0.005f,0.002f),Finish.Recess,4,6);
            Tube(new[] {new Vector3(side*0.021f,1.343f,-0.125f),new Vector3(side*0.064f,1.35f,-0.128f),new Vector3(side*0.11f,1.334f,-0.085f)},new[] {0.016f,0.018f,0.011f},Finish.Skin,8);
            Ellipsoid(new Vector3(side*0.095f,1.272f,-0.08f),new Vector3(0.041f,0.027f,0.033f),Finish.Skin,6,10);
            Ellipsoid(new Vector3(side*0.022f,1.287f,-0.153f),new Vector3(0.015f,0.009f,0.014f),Finish.Skin,5,8);
            Ellipsoid(new Vector3(side*0.02f,1.281f,-0.163f),new Vector3(0.006f,0.004f,0.003f),Finish.Recess,4,6);
        }
        Ellipsoid(new Vector3(0,1.315f,-0.133f),new Vector3(0.029f,0.043f,0.035f),Finish.Skin,8,12);
        Tube(new[] {new Vector3(0.10f,1.37f,-0.07f),new Vector3(0.092f,1.342f,-0.112f),new Vector3(0.082f,1.322f,-0.119f)},new[] {0.0028f,0.002f,0.0018f},Finish.Leather,5);
        End();
        Beard(pivot);
    }
    private void Beard(Node3D head)
    {
        Begin(head, "Beard", new Vector3(0,1.215f,-0.11f));
        for (int lockIndex = -2; lockIndex <= 2; lockIndex++)
        {
            float x = lockIndex*0.052f;
            float end = 0.77f+Mathf.Abs(lockIndex)*0.026f;
            Tube(new[] {new Vector3(x*1.05f,1.245f,-0.09f),new Vector3(x*1.2f,1.14f,-0.184f),new Vector3(x,0.98f,-0.245f),new Vector3(x*0.65f,end,-0.23f)},new[] {0.043f,0.062f,0.059f,0.012f},Finish.Hair,16,0.16f);
        }
        foreach (float side in new[] {-1f,1f})
        {
            Tube(new[] {new Vector3(side*0.115f,1.28f,-0.06f),new Vector3(side*0.145f,1.16f,-0.1f),new Vector3(side*0.11f,0.98f,-0.2f),new Vector3(side*0.09f,0.84f,-0.24f)},new[] {0.027f,0.046f,0.044f,0.012f},Finish.Hair,14,0.15f);
            Tube(new[] {new Vector3(side*0.008f,1.263f,-0.16f),new Vector3(side*0.06f,1.247f,-0.174f),new Vector3(side*0.11f,1.21f,-0.169f)},new[] {0.021f,0.026f,0.015f},Finish.Hair,12,0.12f);
            for (int strand = 0; strand < 3; strand++)
            {
                var points = new Vector3[17]; var radii = new float[17];
                for (int i = 0; i < points.Length; i++)
                {
                    float t = i/16f, a = strand*Mathf.Tau/3+t*Mathf.Tau*2.5f;
                    points[i] = new Vector3(side*0.065f+Mathf.Cos(a)*0.016f,Mathf.Lerp(1.02f,side<0?0.615f:0.65f,t),-0.267f+Mathf.Sin(a)*0.016f);
                    radii[i] = Mathf.Lerp(0.014f,0.008f,t);
                }
                Tube(points,radii,Finish.Hair,6);
            }
            foreach (float y in new[] {0.91f,0.76f,side<0?0.65f:0.69f})
                Band(new Vector3(side*0.065f,y,-0.267f),Vector3.Up,0.033f,0.027f,Finish.Iron);
        }
        End();
    }
    private void Leg(Node3D pelvis, int side)
    {
        string name = side<0?"Left":"Right";
        var hip = new Vector3(side*0.17f,0.57f,0.025f);
        var knee = new Vector3(side*0.19f,0.33f,-0.005f);
        var ankle = new Vector3(side*0.19f,0.13f,0.015f);
        var h = Pivot(pelvis,name+"HipPivot",hip);
        Begin(h,name+"Thigh",hip); Link(hip,knee,0.135f,0.10f,Finish.Leather); End();
        var k = Pivot(h,name+"KneePivot",knee);
        Begin(k,name+"Shin",knee); Link(knee,ankle,0.10f,0.095f,Finish.Leather);
        Ellipsoid(knee+Vector3.Forward*0.058f,new Vector3(0.09f,0.07f,0.06f),Finish.Iron,6,10);
        End();
        var a = Pivot(k,name+"AnklePivot",ankle);
        Begin(a,name+"Boot",ankle);
        // Beveled footprint, exact soles at Y=0. Horizontal top/bottom faces.
        var footprint = new[] {new Vector2(-0.10f,-0.21f),new Vector2(0.10f,-0.21f),new Vector2(0.125f,-0.16f),new Vector2(0.11f,0.09f),new Vector2(-0.11f,0.09f),new Vector2(-0.125f,-0.16f)};
        var indices = Geometry2D.TriangulatePolygon(footprint);
        Vector3 P(int i,float y) => new(ankle.X+footprint[i].X,y,footprint[i].Y);
        for(int i=0;i<indices.Length;i+=3)
        {
            Tri(P(indices[i],0),P(indices[i+1],0),P(indices[i+2],0),Finish.Iron);
            Tri(P(indices[i],0.14f),P(indices[i+2],0.14f),P(indices[i+1],0.14f),Finish.Leather);
        }
        for(int i=0;i<footprint.Length;i++)
        {
            int j=(i+1)%footprint.Length;
            Tri(P(i,0),P(j,0),P(i,0.14f),Finish.Leather); Tri(P(j,0),P(j,0.14f),P(i,0.14f),Finish.Leather);
        }
        Ellipsoid(new Vector3(ankle.X,0.14f,-0.07f),new Vector3(0.115f,0.065f,0.12f),Finish.Leather,6,12);
        Band(new Vector3(ankle.X,0.22f,0.015f),Vector3.Up,0.10f,0.035f,Finish.Iron);
        End();
    }
    private void Arm(Node3D torso, int side)
    {
        string name=side<0?"Left":"Right";
        var shoulder=new Vector3(side*0.32f,1.075f,0.015f);
        var grip=_axeButt.Lerp(_axeEnd,side<0?0.28f:0.60f);
        var wrist=grip+Vector3.Back*0.075f;
        var delta=wrist-shoulder;
        var bend=Vector3.Right*side+Vector3.Down*0.45f;
        bend=(bend-delta.Normalized()*bend.Dot(delta.Normalized())).Normalized();
        var elbow=(shoulder+wrist)*0.5f+bend*Mathf.Sqrt(Mathf.Max(0,0.30f*0.30f-delta.LengthSquared()/4));
        var s=Pivot(torso,name+"ShoulderPivot",shoulder);
        Begin(s,name+"UpperArm",shoulder);
        Ellipsoid(shoulder,new Vector3(0.135f,0.12f,0.135f),Finish.Skin);
        Link(shoulder,elbow,0.105f,0.084f,Finish.Skin,14);
        // Compact layered shoulder iron, shaped over the deltoid rather than a giant sphere.
        Ellipsoid(shoulder+Vector3.Up*0.07f+Vector3.Back*0.012f,new Vector3(0.15f,0.075f,0.145f),Finish.Iron,6,14);
        Band(shoulder+Vector3.Down*0.04f,Vector3.Up,0.13f,0.035f,Finish.Leather);
        End();
        var e=Pivot(s,name+"ElbowPivot",elbow);
        Begin(e,name+"Forearm",elbow); Link(elbow,wrist,0.095f,0.068f,Finish.Skin,14);
        Link(elbow.Lerp(wrist,0.48f),wrist,0.096f,0.078f,Finish.Leather);
        Band(elbow.Lerp(wrist,0.75f),delta.Normalized(),0.095f,0.04f,Finish.Iron);
        End();
        var w=Pivot(e,name+"WristPivot",wrist);
        Begin(w,name+"Gauntlet",wrist);
        Ellipsoid(grip+Vector3.Back*0.042f,new Vector3(0.065f,0.07f,0.035f),Finish.Leather,6,12);
        var axis=(_axeEnd-_axeButt).Normalized();
        var tangent=axis.Cross(Vector3.Forward).Normalized();
        for(int finger=0;finger<4;finger++)
        {
            var points=new Vector3[9];var radii=new float[9];
            var center=grip+axis*((finger-1.5f)*0.019f);
            for(int j=0;j<points.Length;j++)
            {
                float angle=0.25f+j/8f*Mathf.Pi*1.65f;
                points[j]=center+(Vector3.Forward*Mathf.Cos(angle)+tangent*Mathf.Sin(angle))*0.036f;
                radii[j]=0.0105f;
            }
            Tube(points,radii,Finish.Leather,6);
            Ellipsoid(center+Vector3.Forward*0.039f,new Vector3(0.014f,0.014f,0.011f),Finish.Iron,4,7);
        }
        Tube(new[] {grip+axis*0.055f+Vector3.Back*0.035f,grip+axis*0.045f-tangent*0.035f,grip+axis*0.005f+Vector3.Forward*0.03f},new[] {0.017f,0.015f,0.013f},Finish.Leather,7);
        var hand=_part;
        End();
        var marker=new Marker3D {Name="GripPoint"};hand.AddChild(marker);marker.GlobalPosition=ToGlobal(grip);
    }
    private void Axe(Node3D root)
    {
        Begin(root,"BattleAxe",_axeButt);
        var axis=(_axeEnd-_axeButt).Normalized(); var cross=new Vector3(axis.Y,-axis.X,0);
        Link(_axeButt,_axeEnd,0.025f,0.025f,Finish.Wood,12);
        foreach(float t in new[] {0.04f,0.38f,0.76f,0.83f,0.98f}) Band(_axeButt.Lerp(_axeEnd,t),axis,0.033f,0.035f,Finish.Iron);
        for(int wrap=0;wrap<12;wrap++) Band(_axeButt.Lerp(_axeEnd,0.12f+wrap*0.045f),axis,0.026f,0.017f,Finish.Leather);
        var socket=_axeButt.Lerp(_axeEnd,0.87f);
        // Broad bearded cutting head, short rear counterweight: original asymmetrical forged design.
        var outline=new[] {new Vector2(0.02f,0.12f),new Vector2(0.25f,0.25f),new Vector2(0.44f,0.20f),new Vector2(0.46f,0.04f),new Vector2(0.39f,-0.17f),new Vector2(0.22f,-0.29f),new Vector2(0.05f,-0.10f)};
        Vector3 P(Vector2 p,float z)=>socket+cross*p.X+axis*p.Y+Vector3.Back*z;
        var indices=Geometry2D.TriangulatePolygon(outline);
        for(int side=-1;side<=1;side+=2)
        {
            for(int i=0;i<indices.Length;i+=3) Tri(P(outline[indices[i]]*0.83f,side*0.034f),P(outline[indices[i+1]]*0.83f,side*0.034f),P(outline[indices[i+2]]*0.83f,side*0.034f),Finish.Iron);
            for(int i=0;i<outline.Length;i++)
            {
                int j=(i+1)%outline.Length;
                Tri(P(outline[i]*0.83f,side*0.034f),P(outline[j]*0.83f,side*0.034f),P(outline[i],side*0.006f),Finish.Edge);
                Tri(P(outline[j]*0.83f,side*0.034f),P(outline[j],side*0.006f),P(outline[i],side*0.006f),Finish.Edge);
            }
        }
        foreach(int side in new[] {-1,1})
            Tube(new[] {socket+Vector3.Back*side*0.045f, socket-cross*0.14f+Vector3.Back*side*0.045f},new[] {0.08f,0.055f},Finish.Iron,6);
        for (int i=0;i<outline.Length;i++)
        {
            int next=(i+1)%outline.Length;
            Tri(P(outline[i],-0.006f),P(outline[next],-0.006f),P(outline[i],0.006f),Finish.Edge);
            Tri(P(outline[next],-0.006f),P(outline[next],0.006f),P(outline[i],0.006f),Finish.Edge);
        }
        Link(socket-cross*0.12f,socket-cross*0.23f+axis*0.035f,0.06f,0.025f,Finish.Iron,6);
        Ellipsoid(socket,new Vector3(0.06f,0.09f,0.055f),Finish.Iron,6,10);
        var axe=_part;End();
        foreach(var pair in new[] {("LeftGrip",0.28f),("RightGrip",0.60f)})
        {
            var marker=new Marker3D {Name=pair.Item1};axe.AddChild(marker);marker.GlobalPosition=ToGlobal(_axeButt.Lerp(_axeEnd,pair.Item2));
        }
    }
}
