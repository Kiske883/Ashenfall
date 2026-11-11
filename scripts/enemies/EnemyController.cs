using Godot;

namespace Ashenfall.Enemies;

/// <summary>Static-world navigation with explicit, bounded pursuit. No combat or perception raycasts.</summary>
public partial class EnemyController : CharacterBody3D
{
    public enum EnemyState { Idle, Chase, Hold, Return }
    [Export] public Node3D PlayerTarget { get; set; }
    [Export(PropertyHint.Range, "0.1,100,0.1")] public float DetectionRadius { get; set; } = 6;
    [Export(PropertyHint.Range, "0.1,100,0.1")] public float DisengagementRadius { get; set; } = 10;
    [Export(PropertyHint.Range, "0.1,100,0.1")] public float MaximumDistanceFromSpawn { get; set; } = 12;
    [Export(PropertyHint.Range, "0,20,0.1")] public float MovementSpeed { get; set; } = 3;
    [Export(PropertyHint.Range, "0.1,10,0.1")] public float StoppingDistance { get; set; } = 1.3f;
    [Export(PropertyHint.Range, "0.1,10,0.1")] public float ResumeChaseDistance { get; set; } = 1.7f;
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float ArrivalTolerance { get; set; } = 0.2f;
    [Export(PropertyHint.Range, "0.05,2,0.05")] public float TargetUpdateInterval { get; set; } = 0.25f;
    [Export] public float RotationSpeed { get; set; } = 10;
    [Export] public float StuckTimeout { get; set; } = 2;
    [Export] public float ReacquisitionDelay { get; set; } = 1;
    public EnemyState State { get; private set; } = EnemyState.Idle;
    public Vector3 SpawnPosition { get; private set; }
    public int TargetUpdateCount { get; private set; }
    public bool HasPath { get; private set; }
    private NavigationAgent3D _agent;
    private Node3D _visual;
    private float _updateTimer, _idleTimer, _stuckTimer;
    private Vector3 _progressPosition;

    public override void _Ready()
    {
        _agent = GetNode<NavigationAgent3D>("NavigationAgent3D");
        _visual = GetNode<Node3D>("Visual");
        SpawnPosition = GlobalPosition;
        _progressPosition = GlobalPosition;
    }

    private static float Distance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
    private float Arrival => Mathf.Max(0.05f, ArrivalTolerance);
    private float Stop => Mathf.Max(Arrival, StoppingDistance);
    private float Resume => Mathf.Max(Stop + 0.1f, ResumeChaseDistance);
    private bool TargetValid => GodotObject.IsInstanceValid(PlayerTarget) && PlayerTarget.IsInsideTree();
    private bool LeashExceeded() => !TargetValid ||
        Distance(GlobalPosition, PlayerTarget.GlobalPosition) > Mathf.Max(DetectionRadius + 0.1f, DisengagementRadius) ||
        Distance(SpawnPosition, GlobalPosition) >= Mathf.Max(Arrival, MaximumDistanceFromSpawn) ||
        Distance(SpawnPosition, PlayerTarget.GlobalPosition) > Mathf.Max(Arrival, MaximumDistanceFromSpawn);

    private void Transition(EnemyState next)
    {
        State = next;
        HasPath = false;
        _updateTimer = _stuckTimer = 0;
        _progressPosition = GlobalPosition;
        if (next == EnemyState.Idle) _idleTimer = Mathf.Max(0, ReacquisitionDelay);
    }

    private bool UpdatePath(Vector3 requested)
    {
        var map = _agent.GetNavigationMap();
        var destination = NavigationServer3D.MapGetClosestPoint(map, requested);
        // A target on another floor/outside the map is not a valid pursuit endpoint.
        if (destination.DistanceTo(requested) > 0.5f) return HasPath = false;
        var path = NavigationServer3D.MapGetPath(map, GlobalPosition, destination, true, _agent.NavigationLayers);
        TargetUpdateCount++;
        if (path.Length == 0 || path[^1].DistanceTo(destination) > Arrival) return HasPath = false;
        _agent.TargetPosition = destination;
        return HasPath = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var direction = Vector3.Zero;
        float speed = 0;
        var map = _agent.GetNavigationMap();
        bool ready = map.IsValid && NavigationServer3D.MapGetIterationId(map) > 0;
        _idleTimer = Mathf.Max(0, _idleTimer - dt);
        _updateTimer -= dt;
        if (ready)
        {
            if (State == EnemyState.Idle && _idleTimer <= 0 && TargetValid &&
                Distance(GlobalPosition, PlayerTarget.GlobalPosition) <= Mathf.Max(0, DetectionRadius) && !LeashExceeded())
                Transition(EnemyState.Chase);
            if ((State == EnemyState.Chase || State == EnemyState.Hold) && LeashExceeded())
                Transition(EnemyState.Return);
            if (State == EnemyState.Chase && Distance(GlobalPosition, PlayerTarget.GlobalPosition) <= Stop + 0.01f)
                Transition(EnemyState.Hold);
            else if (State == EnemyState.Hold && Distance(GlobalPosition, PlayerTarget.GlobalPosition) > Resume)
                Transition(EnemyState.Chase);
            if (State == EnemyState.Return && Distance(GlobalPosition, SpawnPosition) <= Arrival)
                Transition(EnemyState.Idle);

            if (State == EnemyState.Chase || State == EnemyState.Return)
            {
                if (_updateTimer <= 0)
                {
                    bool found = UpdatePath(State == EnemyState.Return ? SpawnPosition : PlayerTarget.GlobalPosition);
                    _updateTimer = Mathf.Max(0.05f, TargetUpdateInterval);
                    if (!found && State == EnemyState.Chase) Transition(EnemyState.Return);
                }
                if (HasPath)
                {
                    var next = _agent.GetNextPathPosition();
                    direction = next - GlobalPosition;
                    direction.Y = 0;
                    float remaining = Distance(GlobalPosition, _agent.TargetPosition);
                    float allowance = State == EnemyState.Chase
                        ? Mathf.Max(0, Distance(GlobalPosition, PlayerTarget.GlobalPosition) - Stop)
                        : Mathf.Max(0, remaining - Arrival * 0.5f);
                    speed = Mathf.Min(Mathf.Max(0, MovementSpeed), Mathf.Min(direction.Length(), allowance) / dt);
                    direction = direction.Normalized();
                    if (Distance(GlobalPosition, _progressPosition) >= 0.05f)
                    {
                        _progressPosition = GlobalPosition;
                        _stuckTimer = 0;
                    }
                    else _stuckTimer += dt;
                    if (_stuckTimer > Mathf.Max(0.5f, StuckTimeout))
                    {
                        if (State == EnemyState.Chase) Transition(EnemyState.Return);
                        else { HasPath = false; _stuckTimer = 0; }
                        speed = 0;
                    }
                }
            }
        }
        var velocity = direction * speed;
        velocity.Y = IsOnFloor() ? 0 : Velocity.Y + GetGravity().Y * dt;
        Velocity = velocity;
        MoveAndSlide();
        if (speed > 0.01f)
            _visual.Rotation = new Vector3(0, Mathf.LerpAngle(_visual.Rotation.Y,
                Mathf.Atan2(-direction.X, -direction.Z), 1 - Mathf.Exp(-Mathf.Max(0, RotationSpeed) * dt)), 0);
    }
}
