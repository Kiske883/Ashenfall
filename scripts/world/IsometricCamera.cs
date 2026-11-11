using Godot;

namespace Ashenfall.World;

/// <summary>Fixed isometric orientation with optional smooth actor tracking.</summary>
public partial class IsometricCamera : Node3D
{
    [Export(PropertyHint.Range, "15,75,1")] public float Inclination { get; set; } = 45f;
    [Export] public float Azimuth { get; set; } = 45f;
    [Export(PropertyHint.Range, "10,100,1")] public float Distance { get; set; } = 48f;
    [Export(PropertyHint.Range, "10,80,1")] public float ZoomSize { get; set; } = 46f;
    [Export] public Vector3 Target { get; set; } = Vector3.Zero;
    [Export] public Node3D FollowTarget { get; set; }
    [Export] public Vector3 FollowOffset { get; set; } = new(0, 0.9f, 0);
    [Export(PropertyHint.Range, "0,30,0.1")] public float FollowSpeed { get; set; } = 8f;

    public override void _Ready()
    {
        // Follow after CharacterBody3D movement, then interpolate both on the same physics clock.
        ProcessPhysicsPriority = 10;
        if (GodotObject.IsInstanceValid(FollowTarget))
            GlobalPosition = FollowTarget.GlobalPosition + FollowOffset - Target;
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

    public override void _PhysicsProcess(double delta)
    {
        if (!GodotObject.IsInstanceValid(FollowTarget)) return;
        var desired = FollowTarget.GlobalPosition + FollowOffset - Target;
        GlobalPosition = GlobalPosition.Lerp(desired,
            1f - Mathf.Exp(-FollowSpeed * (float)delta));
    }
}
