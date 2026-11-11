using Godot;

namespace Ashenfall.Characters;

/// <summary>Variant-only visual binding; reads movement after physics and never commands the actor.</summary>
public partial class DwarfPlayerPresentation : Node
{
    [Export] public PlayerController Actor { get; set; }
    [Export] public PackedScene PresentationScene { get; set; }
    // Optional per-scene actor configuration. Zero preserves the original player default.
    [Export(PropertyHint.Range, "0,20,0.1")] public float MovementSpeedOverride { get; set; }
    // Approved 06B support travel / (cycle duration * support fraction).
    [Export(PropertyHint.Range, "0.01,10,0.01")] public float WalkReferenceSpeed { get; set; } = 0.14f / (1.4f * 0.6f);
    [Export(PropertyHint.Range, "0.01,1,0.01")] public float MinimumWalkPlaybackRate { get; set; } = 0.1f;
    [Export(PropertyHint.Range, "0.1,30,0.1")] public float MaximumWalkPlaybackRate { get; set; } = 2f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float RateSmoothingSeconds { get; set; } = 0.1f;
    public Vector3 ActualHorizontalVelocity { get; private set; }
    public float ActualHorizontalSpeed => ActualHorizontalVelocity.Length();
    public float RequestedWalkPlaybackRate { get; private set; }
    public float WalkPlaybackRate { get; private set; }
    public bool WalkRateLimited { get; private set; }
    public DwarfBerserkerPresentation Presentation { get; private set; }
    private Node3D _visual;
    private AnimationPlayer _player;
    private Vector3 _previousPosition;
    private float _facing;

    public override void _Ready()
    {
        if (Actor == null || PresentationScene == null)
        {
            GD.PushError("Dwarf player presentation requires an Actor and PresentationScene.");
            SetPhysicsProcess(false); return;
        }
        if (MovementSpeedOverride > 0) Actor.MovementSpeed = MovementSpeedOverride;
        _visual = Actor.GetNode<Node3D>("Visual");
        // Keep the original nodes/resources available; visibility changes are local to this scene instance.
        foreach (var child in _visual.GetChildren())
            if (child is Node3D original) original.Visible = false;
        Presentation = PresentationScene.Instantiate<DwarfBerserkerPresentation>();
        Presentation.Name = "Dwarf";
        _visual.AddChild(Presentation);
        Presentation.SetProcess(false); // This adapter owns a single post-movement physics clock.
        _player = Presentation.GetNode<AnimationPlayer>("AnimationPlayer");
        ProcessPhysicsPriority = Actor.ProcessPhysicsPriority + 1;
        ResetMotionTracking();
    }

    public void ResetMotionTracking()
    {
        _previousPosition = Actor.GlobalPosition; _facing = _visual.Rotation.Y;
        ActualHorizontalVelocity = Vector3.Zero;
        WalkPlaybackRate = RequestedWalkPlaybackRate = 0; WalkRateLimited = false;
        Presentation.PlayState("Idle"); _player.SpeedScale = 1;
    }

    public override void _PhysicsProcess(double delta)
    {
        var offset = Actor.GlobalPosition - _previousPosition;
        _previousPosition = Actor.GlobalPosition; offset.Y = 0;
        // External spawn/teleport repositioning is not a locomotion sample.
        bool teleport = offset.Length() > Mathf.Max(0.5f, Actor.MovementSpeed * (float)delta * 4);
        ActualHorizontalVelocity = teleport ? Vector3.Zero : offset / (float)delta;
        float threshold = Presentation.ActiveClip == "Walk" ? 0.02f : 0.08f;
        bool walking = ActualHorizontalSpeed > threshold;
        Presentation.PlayState(walking ? "Walk" : "Idle");
        if (walking)
        {
            RequestedWalkPlaybackRate = ActualHorizontalSpeed / Mathf.Max(0.001f, WalkReferenceSpeed);
            float minimum = Mathf.Max(0.01f, MinimumWalkPlaybackRate);
            float maximum = Mathf.Max(minimum, MaximumWalkPlaybackRate);
            float target = Mathf.Clamp(RequestedWalkPlaybackRate, minimum, maximum);
            WalkRateLimited = !Mathf.IsEqualApprox(target, RequestedWalkPlaybackRate);
            float weight = RateSmoothingSeconds <= 0 ? 1 : 1 - Mathf.Exp(-(float)delta / RateSmoothingSeconds);
            WalkPlaybackRate = Mathf.Lerp(WalkPlaybackRate, target, weight);
            _player.SpeedScale = WalkPlaybackRate;
            // PlayerController's desired-direction turn is overridden only in this variant's visual node.
            float angle = Mathf.Atan2(-ActualHorizontalVelocity.X, -ActualHorizontalVelocity.Z);
            _facing = Mathf.LerpAngle(_facing, angle, 1 - Mathf.Exp(-Actor.RotationSpeed * (float)delta));
        }
        else
        {
            WalkPlaybackRate = RequestedWalkPlaybackRate = 0; WalkRateLimited = false;
            _player.SpeedScale = 1;
        }
        // Stay oriented along the last actual movement even when desired input pushes against a wall.
        _visual.Rotation = new Vector3(0, _facing, 0);
        Presentation._Process(delta);
    }
}
