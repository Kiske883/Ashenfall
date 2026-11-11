using Godot;

namespace Ashenfall.World;

/// <summary>Fixed camera rig; future player tracking can move the rig's target.</summary>
public partial class IsometricCamera : Node3D
{
    [Export(PropertyHint.Range, "15,75,1")] public float Inclination { get; set; } = 45f;
    [Export] public float Azimuth { get; set; } = 45f;
    [Export(PropertyHint.Range, "10,100,1")] public float Distance { get; set; } = 48f;
    [Export(PropertyHint.Range, "10,80,1")] public float ZoomSize { get; set; } = 46f;
    [Export] public Vector3 Target { get; set; } = Vector3.Zero;

    public override void _Ready()
    {
        var camera = GetNode<Camera3D>("Camera3D");
        float pitch = Mathf.DegToRad(Inclination);
        float yaw = Mathf.DegToRad(Azimuth);
        camera.Position = Target + Distance * new Vector3(
            Mathf.Cos(pitch) * Mathf.Sin(yaw), Mathf.Sin(pitch),
            Mathf.Cos(pitch) * Mathf.Cos(yaw));
        camera.Projection = Camera3D.ProjectionType.Orthogonal;
        camera.Size = ZoomSize;
        camera.LookAt(ToGlobal(Target), Vector3.Up);
        camera.Current = true;
    }
}
