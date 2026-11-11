using Godot;

namespace Ashenfall.Environment;

/// <summary>Seeded decoration independent of camera, players, and networking.</summary>
public partial class EnvironmentGenerator : Node3D
{
    [Export] public int Seed { get; set; } = 731;
    [Export(PropertyHint.Range, "0,60,1")] public int TreeCount { get; set; } = 10;
    [Export(PropertyHint.Range, "0,60,1")] public int RockCount { get; set; } = 16;
    [Export(PropertyHint.Range, "24,80,1")] public float TerrainSize { get; set; } = 40;
    [Export] public PackedScene TreeScene { get; set; }
    [Export] public PackedScene RockScene { get; set; }
    [Export] public PackedScene RuinScene { get; set; }

    public override void _Ready()
    {
        var random = new RandomNumberGenerator { Seed = unchecked((ulong)Seed) };
        var earth = Material(new Color("343b35"));
        var stone = Material(new Color("66665c"));
        AddBox("Ground", new Vector3(TerrainSize, 0.8f, TerrainSize), new Vector3(0, -0.4f, 0), earth, true);
        // Physical perimeter also contains WASD debug movement. No foliage-sized blockers.
        // Layer 4 is physics containment only: it must not occlude ground picking or
        // contribute a second obstacle footprint to terrain-edge navigation erosion.
        var boundary = new StaticBody3D { Name = "TerrainBoundary", CollisionLayer = 8, CollisionMask = 2 };
        AddChild(boundary);
        foreach (float side in new[] { -1f, 1f })
        {
            boundary.AddChild(new CollisionShape3D
            {
                Position = new Vector3(side * (TerrainSize / 2 + 0.25f), 2, 0),
                Shape = new BoxShape3D { Size = new Vector3(0.5f, 4, TerrainSize + 1) }
            });
            boundary.AddChild(new CollisionShape3D
            {
                Position = new Vector3(0, 2, side * (TerrainSize / 2 + 0.25f)),
                Shape = new BoxShape3D { Size = new Vector3(TerrainSize + 1, 4, 0.5f) }
            });
        }

        // Broken pilgrimage road. Keep its center free for future player movement.
        int rows = Mathf.FloorToInt((TerrainSize - 4) / 1.6f);
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < 3; column++)
        {
            float z = (row - (rows - 1) / 2f) * 1.6f;
            var slab = AddBox($"Road_{row}_{column}", new Vector3(1.05f, 0.10f, 1.4f),
                new Vector3((column - 1) * 1.16f + random.RandfRange(-0.08f, 0.08f), 0.025f, z), stone, false);
            slab.RotationDegrees = new Vector3(0, random.RandfRange(-5, 5), 0);
        }

        PlaceScatter(TreeScene, TreeCount, "Tree", random, 0.8f, 1.25f);
        PlaceScatter(RockScene, RockCount, "Rock", random, 0.65f, 1.5f);
        foreach (float side in new[] { -1f, 1f })
        {
            var ruin = RuinScene.Instantiate<Node3D>();
            ruin.Name = side < 0 ? "WesternRuin" : "EasternRuin";
            ruin.Position = new Vector3(side * 5.2f, 0, -7);
            AddChild(ruin);
        }
        GD.Print($"Ashenfall environment ready: seed={Seed}, trees={TreeCount}, rocks={RockCount}, terrain={TerrainSize}");
    }

    private void PlaceScatter(PackedScene scene, int count, string prefix,
        RandomNumberGenerator random, float minScale, float maxScale)
    {
        for (int i = 0; i < count; i++)
        {
            var item = scene.Instantiate<Node3D>();
            float side = i % 2 == 0 ? -1 : 1;
            // Side bands leave the road and ruin courtyard clear; shorter foreground trees preserve visibility.
            float x = side * random.RandfRange(8, TerrainSize / 2 - 2);
            float z = random.RandfRange(-TerrainSize / 2 + 3, TerrainSize / 2 - 3);
            float scale = random.RandfRange(minScale, maxScale);
            if (prefix == "Tree" && z > 4) scale *= 0.72f;
            item.Name = $"{prefix}_{i:00}";
            item.Position = new Vector3(x, 0, z);
            item.Scale = Vector3.One * scale;
            item.RotationDegrees = new Vector3(0, random.RandfRange(0, 360), 0);
            AddChild(item);
        }
    }

    private static StandardMaterial3D Material(Color color) => new()
    {
        AlbedoColor = color, Roughness = 0.95f
    };

    private MeshInstance3D AddBox(string name, Vector3 size, Vector3 position,
        Material material, bool collision)
    {
        var mesh = new MeshInstance3D
        {
            Name = name, Mesh = new BoxMesh { Size = size },
            MaterialOverride = material, Position = position
        };
        AddChild(mesh);
        if (collision)
        {
            var body = new StaticBody3D();
            mesh.AddChild(body);
            if (name == "Ground") body.AddToGroup("navigable_ground");
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        }
        return mesh;
    }
}
