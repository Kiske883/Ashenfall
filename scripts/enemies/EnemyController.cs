using Godot;

namespace Ashenfall.Enemies;

/// <summary>Bounded pursuit and logical attack preparation; no damage or hit detection.</summary>
public partial class EnemyController : CharacterBody3D
{
    public enum EnemyState { Idle, Chase, Hold, Return, AttackWindup, AttackRecovery }
    [Signal] public delegate void AttackExecutedEventHandler(Node3D target, int sequence);
    [Export] public bool AttacksEnabled { get; set; } = true;
    [Export(PropertyHint.Range, "0.1,10,0.05")] public float AttackRange { get; set; } = 1.45f;
    [Export(PropertyHint.Range, "0.05,10,0.05")] public float AttackWindupDuration { get; set; } = 0.6f;
    [Export(PropertyHint.Range, "0.05,10,0.05")] public float AttackRecoveryDuration { get; set; } = 1;
    [Export(PropertyHint.Range, "0.1,30,0.1")] public float AttackFacingSpeed { get; set; } = 10;
    [Export] public bool ShowAttackDebug { get; set; }
    public int AttackCount { get; private set; }
    public float EffectiveAttackRange => Mathf.Max(Stop + 0.01f, AttackRange);
    [Export] public Node3D PlayerTarget { get; set; }
    [Export(PropertyHint.Range, "0.1,100,0.1")] public float DetectionRadius { get; set; } = 6;
    [Export(PropertyHint.Range, "0.1,100,0.1")] public float DisengagementRadius { get; set; } = 10;
    [Export(PropertyHint.Range, "0.1,100,0.1")] public float MaximumDistanceFromSpawn { get; set; } = 12;
    /// <summary>Chase speed; name retained for existing scene/Inspector references.</summary>
    [Export(PropertyHint.Range, "0,20,0.1")] public float MovementSpeed { get; set; } = 1;
    [Export(PropertyHint.Range, "0,20,0.1")] public float ReturnSpeed { get; set; } = 0.7f;
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
    // Read-only presentation clock. AI remains the sole owner of phase timing and execution.
    public ulong AttackCycleId { get; private set; }
    public float AttackPhaseDuration { get; private set; }
    public float AttackPhaseRemaining => PreparingOrRecovering ? Mathf.Max(0, _attackTimer) : 0;
    private NavigationAgent3D _agent;
    private Node3D _visual;
    private float _updateTimer, _idleTimer, _stuckTimer;
    private Vector3 _progressPosition;
    private float _attackTimer;
    private Node3D _attackTarget;
    private Label3D _debugLabel;

    public override void _Ready()
    {
        _agent = GetNode<NavigationAgent3D>("NavigationAgent3D");
        _visual = GetNode<Node3D>("Visual");
        SpawnPosition = GlobalPosition;
        _progressPosition = GlobalPosition;
        _debugLabel = new Label3D { Name = "AttackDebug", Position = new Vector3(0, 2.3f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 32, PixelSize = 0.008f };
        AddChild(_debugLabel);
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
        if (next == EnemyState.AttackWindup)
        {
            _attackTarget = PlayerTarget;
            _attackTimer = Mathf.Max(0.05f, AttackWindupDuration);
            AttackPhaseDuration = _attackTimer;
            AttackCycleId++;
        }
        else if (next == EnemyState.AttackRecovery)
        {
            _attackTimer = Mathf.Max(0.05f, AttackRecoveryDuration);
            AttackPhaseDuration = _attackTimer;
        }
        else _attackTarget = null;
    }

    private bool PreparingOrRecovering => State == EnemyState.AttackWindup || State == EnemyState.AttackRecovery;
    private void Face(Vector3 direction, float rate, float dt)
    {
        direction.Y = 0;
        if (direction.LengthSquared() < 0.0001f) return;
        _visual.Rotation = new Vector3(0, Mathf.LerpAngle(_visual.Rotation.Y,
            Mathf.Atan2(-direction.X, -direction.Z), 1 - Mathf.Exp(-Mathf.Max(0.1f, rate) * dt)), 0);
    }

    private void UpdateAttack(float dt)
    {
        if (!AttacksEnabled || (PreparingOrRecovering && _attackTarget != PlayerTarget))
        {
            if (PreparingOrRecovering) Transition(EnemyState.Hold);
            return;
        }
        if (State == EnemyState.Hold || PreparingOrRecovering)
            Face(PlayerTarget.GlobalPosition - GlobalPosition, AttackFacingSpeed, dt);
        if (State == EnemyState.Hold)
        {
            var offset = PlayerTarget.GlobalPosition - GlobalPosition;
            var horizontal = new Vector3(offset.X, 0, offset.Z);
            if (horizontal.Length() <= EffectiveAttackRange && Mathf.Abs(offset.Y) <= 0.5f &&
                (-_visual.Basis.Z).Dot(horizontal.Normalized()) >= 0.98f)
                Transition(EnemyState.AttackWindup);
            return; // The entry tick never consumes windup time.
        }
        if (!PreparingOrRecovering) return;
        _attackTimer -= dt;
        if (_attackTimer > 0.00001f) return;
        if (State == EnemyState.AttackWindup)
        {
            // Commit recovery BEFORE notifying observers: this cycle cannot execute twice.
            var target = _attackTarget;
            Transition(EnemyState.AttackRecovery);
            AttackCount++;
            EmitSignal(SignalName.AttackExecuted, target, AttackCount);
        }
        else Transition(Distance(GlobalPosition, PlayerTarget.GlobalPosition) > EffectiveAttackRange
            ? EnemyState.Chase : EnemyState.Hold);
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
            if ((State == EnemyState.Chase || State == EnemyState.Hold || PreparingOrRecovering) && LeashExceeded())
                Transition(EnemyState.Return);
            if (State == EnemyState.Chase && Distance(GlobalPosition, PlayerTarget.GlobalPosition) <= Stop + 0.01f)
                Transition(EnemyState.Hold);
            else if (State == EnemyState.Hold && Distance(GlobalPosition, PlayerTarget.GlobalPosition) >
                (AttacksEnabled ? Mathf.Min(Resume, EffectiveAttackRange + 0.15f) : Resume))
                Transition(EnemyState.Chase);
            if (State == EnemyState.Hold || PreparingOrRecovering) UpdateAttack(dt);
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
                    float movementSpeed = State == EnemyState.Return ? ReturnSpeed : MovementSpeed;
                    speed = Mathf.Min(Mathf.Max(0, movementSpeed), Mathf.Min(direction.Length(), allowance) / dt);
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
        if (speed > 0.01f) Face(direction, RotationSpeed, dt);
        _debugLabel.Visible = ShowAttackDebug;
        if (ShowAttackDebug) _debugLabel.Text = $"{State}  /  attacks: {AttackCount}";
    }
}
