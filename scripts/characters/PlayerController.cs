using Godot;

namespace Ashenfall.Characters;

/// <summary>Mouse navigation and optional camera-relative keyboard debugging for one actor.</summary>
public partial class PlayerController : CharacterBody3D
{
    [Export(PropertyHint.Range, "0,20,0.1")] public float MovementSpeed { get; set; } = 5f;
    [Export(PropertyHint.Range, "0,30,0.1")] public float RotationSpeed { get; set; } = 12f;
    [Export(PropertyHint.Range, "0.1,1,0.05")] public float ArrivalDistance { get; set; } = 0.2f;
    [Export(PropertyHint.Range, "0.1,3,0.1")] public float SlowdownDistance { get; set; } = 1f;
    [Export(PropertyHint.Range, "0.05,0.5,0.01")] public float HoldUpdateInterval { get; set; } = 0.12f;
    [Export] public bool InputEnabled { get; set; } = true;
    [Export] public Camera3D MovementCamera { get; set; }
    [Export] public Node3D TargetPlaneAnchor { get; set; }
    [Export] public float TargetPlaneHeightOffset { get; set; }
    [Export(PropertyHint.Range, "0.0001,0.1,0.0001")]
    public float MinimumPlaneRayVerticalComponent { get; set; } = 0.001f;
    [Export(PropertyHint.Range, "10,10000,10")]
    public float MaximumPlaneRayDistance { get; set; } = 1000f;
    [Export] public StringName MoveLeftAction { get; set; } = "move_left";
    [Export] public StringName MoveRightAction { get; set; } = "move_right";
    [Export] public StringName MoveUpAction { get; set; } = "move_up";
    [Export] public StringName MoveDownAction { get; set; } = "move_down";
    [Export] public StringName MouseMoveAction { get; set; } = "move_to_cursor";

    public bool HasDestination { get; private set; }
    public Vector3 Destination { get; private set; }
    private Node3D _visual;
    private NavigationAgent3D _agent;
    private bool _mouseHeld;
    private bool _pendingClick;
    private Vector2 _cursor;
    private float _holdTimer;
    private float _stuckTime;
    private Vector3 _progressPosition;

    public override void _Ready()
    {
        _visual = GetNode<Node3D>("Visual");
        _agent = GetNode<NavigationAgent3D>("NavigationAgent3D");
    }

    public override void _Input(InputEvent inputEvent)
    {
        // Always observe release, including over a UI control. Release keeps the last goal.
        if (inputEvent.IsActionReleased(MouseMoveAction)) _mouseHeld = false;
        if (inputEvent is InputEventMouseMotion motion) _cursor = motion.Position;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!InputEnabled || !inputEvent.IsActionPressed(MouseMoveAction)) return;
        _cursor = GetViewport().GetMousePosition();
        if (inputEvent is InputEventMouseButton mouse) _cursor = mouse.Position;
        _mouseHeld = true;
        _pendingClick = true;
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Camera picking is deferred to physics time for safe space queries.</summary>
    public bool TrySetScreenDestination(Vector2 screenPosition)
    {
        var camera = MovementCamera ?? GetViewport().GetCamera3D();
        if (!InputEnabled || camera == null) return false;
        float planeHeight = (GodotObject.IsInstanceValid(TargetPlaneAnchor)
            ? TargetPlaneAnchor.GlobalPosition.Y : 0) + TargetPlaneHeightOffset;
        if (!MouseTargetAcquisition.TryAcquire(camera, GetWorld3D().DirectSpaceState,
            screenPosition, planeHeight, MinimumPlaneRayVerticalComponent, MaximumPlaneRayDistance,
            out var request, out bool needsProjection)) return false;
        return needsProjection ? TrySetProjectedDestination(request) : TrySetDestination(request);
    }

    /// <summary>Reject invalid/unreachable orders without discarding the last valid one.</summary>
    public bool TrySetDestination(Vector3 point) => TrySetNavigationDestination(point, false);

    /// <summary>Projects a plane request to the mesh; the cursor is not limited by world bounds.</summary>
    public bool TrySetProjectedDestination(Vector3 point) => TrySetNavigationDestination(point, true);

    private bool TrySetNavigationDestination(Vector3 point, bool allowDistantProjection)
    {
        if (!InputEnabled || !point.IsFinite()) return false;
        var map = _agent.GetNavigationMap();
        if (!map.IsValid || NavigationServer3D.MapGetIterationId(map) == 0) return false;
        var closest = NavigationServer3D.MapGetClosestPoint(map, point);
        // Rasterization can lose one cell at the border plus a fractional cell when
        // rounding the capsule radius. Accept that bounded XZ error (including corners)
        // by projecting to a reachable safe point, never by moving outside the mesh.
        float snapTolerance = 2f * Mathf.Sqrt(2f) * NavigationServer3D.MapGetCellSize(map);
        if (Mathf.Abs(closest.Y - point.Y) > 0.3f ||
            (!allowDistantProjection && HorizontalDistance(closest, point) > snapTolerance))
            return false;
        if (HasDestination && HorizontalDistance(closest, Destination) < 0.25f) return true;
        var path = NavigationServer3D.MapGetPath(map, GlobalPosition, closest, true);
        if (path.Length == 0 || path[path.Length - 1].DistanceTo(closest) > 0.25f) return false;
        Destination = closest;
        _agent.TargetPosition = closest;
        HasDestination = true;
        _stuckTime = 0;
        _progressPosition = GlobalPosition;
        return true;
    }

    public void CancelDestination()
    {
        HasDestination = false;
        _stuckTime = 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var input = InputEnabled
            ? Input.GetVector(MoveLeftAction, MoveRightAction, MoveUpAction, MoveDownAction)
            : Vector2.Zero;
        var direction = Vector3.Zero;
        float speed = MovementSpeed;
        if (!InputEnabled)
        {
            CancelDestination();
            _mouseHeld = _pendingClick = false;
        }
        else if (input != Vector2.Zero)
        {
            CancelDestination();
            _mouseHeld = _pendingClick = false; // Keyboard wins until a new mouse press.
            var camera = MovementCamera ?? GetViewport().GetCamera3D();
            var right = camera != null ? camera.GlobalBasis.X : Vector3.Right;
            var back = camera != null ? camera.GlobalBasis.Z : Vector3.Back;
            right.Y = back.Y = 0;
            direction = (right.Normalized() * input.X + back.Normalized() * input.Y).LimitLength(1);
        }
        else
        {
            _holdTimer -= dt;
            if (_pendingClick || (_mouseHeld && _holdTimer <= 0))
            {
                TrySetScreenDestination(_cursor);
                _pendingClick = false;
                _holdTimer = HoldUpdateInterval;
            }
            if (HasDestination)
            {
                _agent.TargetDesiredDistance = ArrivalDistance;
                var next = _agent.GetNextPathPosition();
                float remaining = HorizontalDistance(GlobalPosition, Destination);
                if (remaining <= ArrivalDistance || _agent.IsNavigationFinished()) CancelDestination();
                else
                {
                    direction = next - GlobalPosition;
                    direction.Y = 0;
                    float distance = direction.Length();
                    direction = direction.Normalized();
                    // Brake only at the final segment; cap each step to avoid waypoint overshoot.
                    if (_agent.GetCurrentNavigationPathIndex() >= _agent.GetCurrentNavigationPath().Length - 1)
                        speed *= Mathf.Clamp(remaining / Mathf.Max(SlowdownDistance, ArrivalDistance), 0, 1);
                    speed = Mathf.Min(speed, distance / dt);
                    if (HorizontalDistance(GlobalPosition, _progressPosition) > 0.05f)
                    {
                        _progressPosition = GlobalPosition;
                        _stuckTime = 0;
                    }
                    else _stuckTime += dt;
                    if (_stuckTime > 1.5f)
                    {
                        CancelDestination();
                        direction = Vector3.Zero;
                    }
                }
            }
        }

        var velocity = Velocity;
        velocity.X = direction.X * speed;
        velocity.Z = direction.Z * speed;
        if (!IsOnFloor()) velocity += GetGravity() * dt;
        else velocity.Y = 0;
        Velocity = velocity;
        MoveAndSlide();

        if (direction.LengthSquared() > 0.0001f)
        {
            float angle = Mathf.Atan2(-direction.X, -direction.Z);
            _visual.Rotation = new Vector3(0, Mathf.LerpAngle(_visual.Rotation.Y, angle,
                1f - Mathf.Exp(-RotationSpeed * dt)), 0);
        }
    }

    private static float HorizontalDistance(Vector3 from, Vector3 to) =>
        new Vector2(from.X - to.X, from.Z - to.Z).Length();
}
