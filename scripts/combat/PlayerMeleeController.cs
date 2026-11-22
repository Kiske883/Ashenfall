using Ashenfall.Characters;
using Godot;

namespace Ashenfall.Combat;

/// <summary>One target approach and one logical attack per command; visual playback is an observer.</summary>
public partial class PlayerMeleeController : Node
{
    public enum MeleeState { Idle, Approach, Attack }
    [Signal] public delegate void AttackStartedEventHandler(Node3D target, int sequence);
    [Signal] public delegate void AttackImpactEventHandler(MeleeTarget target, int sequence);
    [Signal] public delegate void AttackInterruptedEventHandler(int sequence, bool impactIssued);

    [Export] public PlayerController Actor { get; set; }
    [Export] public bool Enabled { get; set; } = true;
    [Export(PropertyHint.Range, "0.7,5,0.05")] public float MeleeRange { get; set; } = 1.45f;
    [Export(PropertyHint.Range, "0.1,5,0.05")] public float AttackDuration { get; set; } = 1.70f;
    // 0.94 s into the approved 1.70 s clip; scales with a configured cycle duration.
    [Export(PropertyHint.Range, "0,1,0.001")] public float ImpactFraction { get; set; } = 0.94f / 1.70f;
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float TargetUpdateInterval { get; set; } = 0.15f;
    [Export(PropertyHint.Range, "1,30,0.5")] public float ApproachTimeout { get; set; } = 12f;
    [Export] public Key AttackKey { get; set; } = Key.Space;

    public MeleeState State { get; private set; }
    public MeleeTarget SelectedTarget { get; private set; }
    public Node3D FacingTarget { get; private set; }
    public float AttackRemaining { get; private set; }
    public int AttackSequence { get; private set; }
    public int ImpactSequence { get; private set; }
    public MeleeTarget CommittedTarget { get; private set; }
    private NavigationAgent3D _agent;
    private float _repathTimer, _approachTimer;
    private float _attackElapsed, _activeAttackDuration, _activeImpactTime;
    private bool _impactIssued;
    public bool Defeated { get; private set; }

    public override void _Ready()
    {
        if (!Enabled)
        {
            SetPhysicsProcess(false); SetProcessUnhandledInput(false); return;
        }
        if (Actor == null)
        {
            GD.PushError("Player melee controller requires a movement actor.");
            SetPhysicsProcess(false); SetProcessUnhandledInput(false); return;
        }
        _agent = Actor.GetNode<NavigationAgent3D>("NavigationAgent3D");
        // The movement actor owns the only click decision, inside its safe physics tick.
        Actor.TryConsumeWorldClick = TryConsumeWorldClick;
        ProcessPhysicsPriority = Actor.ProcessPhysicsPriority - 1;
    }

    public override void _ExitTree()
    {
        if (!GodotObject.IsInstanceValid(Actor)) return;
        if (Actor.TryConsumeWorldClick?.Target == this) Actor.TryConsumeWorldClick = null;
        if (!Defeated) Actor.SetMovementLocked(false);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (Defeated) return;
        if (inputEvent is not InputEventKey key || !key.Pressed || key.Echo ||
            (key.PhysicalKeycode != AttackKey && key.Keycode != AttackKey)) return;
        if (IssueAttack(IsValid(SelectedTarget) ? SelectedTarget : null))
            GetViewport().SetInputAsHandled();
    }

    /// <summary>Input-independent command entry point for a future authority/transport adapter.</summary>
    public bool IssueAttack(MeleeTarget target)
    {
        if (Defeated || Actor == null || !Actor.InputEnabled || State == MeleeState.Attack ||
            (target != null && !IsValid(target))) return false;
        SelectedTarget = target;
        Actor.CancelDestination();
        FacingTarget = null;
        if (target == null) BeginAttack();
        else
        {
            State = MeleeState.Approach;
            _repathTimer = _approachTimer = 0;
        }
        return true;
    }

    private bool TryConsumeWorldClick(Vector2 screen)
    {
        if (Defeated) return false;
        var camera = Actor.MovementCamera ?? GetViewport().GetCamera3D();
        if (camera == null || !screen.IsFinite() || !GetViewport().GetVisibleRect().HasPoint(screen))
            return false;
        var from = camera.ProjectRayOrigin(screen);
        var to = from + camera.ProjectRayNormal(screen) * camera.Far;
        var query = PhysicsRayQueryParameters3D.Create(from, to, 3);
        query.Exclude = new Godot.Collections.Array<Rid> { Actor.GetRid() };
        var hit = Actor.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0 && hit["collider"].AsGodotObject() is CollisionObject3D body)
        {
            foreach (Node child in body.GetChildren())
            {
                if (child is not MeleeTarget target || !IsValid(target)) continue;
                // A target click is consumed even during an ongoing attack: it never
                // becomes a simultaneous ground order.
                IssueAttack(target);
                return true;
            }
        }
        // Ground and obstacle clicks retain the original movement acquisition.
        if (State == MeleeState.Attack) InterruptAttack();
        else CancelTargetOrder();
        return false;
    }

    private static bool IsValid(MeleeTarget target) => GodotObject.IsInstanceValid(target) &&
        target.IsInsideTree() && target.CanBeTargeted && GodotObject.IsInstanceValid(target.Actor);

    private void CancelTargetOrder()
    {
        SelectedTarget = null;
        FacingTarget = null;
        if (State == MeleeState.Approach)
        {
            State = MeleeState.Idle;
            Actor.CancelDestination();
        }
    }

    private void BeginAttack()
    {
        State = MeleeState.Attack;
        _activeAttackDuration = Mathf.Max(0.1f, AttackDuration);
        _activeImpactTime = Mathf.Clamp(ImpactFraction, 0, 1) * _activeAttackDuration;
        AttackRemaining = _activeAttackDuration;
        _attackElapsed = 0;
        _impactIssued = false;
        CommittedTarget = IsValid(SelectedTarget) ? SelectedTarget : null;
        Actor.CancelDestination();
        Actor.SetMovementLocked(true);
        AttackSequence = checked(AttackSequence + 1);
        EmitSignal(SignalName.AttackStarted, CommittedTarget?.Actor,
            AttackSequence);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (State == MeleeState.Attack)
        {
            // A deliberate WASD retreat wins before this tick can emit an impact.
            if (ManualMoveHeld()) { InterruptAttack(); return; }
            _attackElapsed += (float)delta;
            AttackRemaining = Mathf.Max(0, _activeAttackDuration - _attackElapsed);
            if (!_impactIssued && _attackElapsed >= _activeImpactTime)
            {
                _impactIssued = true;
                ImpactSequence = AttackSequence;
                EmitSignal(SignalName.AttackImpact, CommittedTarget, AttackSequence);
            }
            if (AttackRemaining <= 0)
            {
                State = MeleeState.Idle;
                FacingTarget = null;
                CommittedTarget = null;
                Actor.SetMovementLocked(false);
            }
            return;
        }
        if (State != MeleeState.Approach) return;
        if (!Actor.InputEnabled || !IsValid(SelectedTarget) || ManualMoveHeld())
        {
            CancelTargetOrder(); return;
        }
        _approachTimer += (float)delta;
        if (_approachTimer > ApproachTimeout) { CancelTargetOrder(); return; }
        var targetActor = SelectedTarget.Actor;
        var toTarget = targetActor.GlobalPosition - Actor.GlobalPosition;
        var horizontal = new Vector3(toTarget.X, 0, toTarget.Z);
        float distance = horizontal.Length();
        bool inRange = distance <= Mathf.Max(0.7f, MeleeRange) && Mathf.Abs(toTarget.Y) <= 0.5f;
        bool visible = inRange && HasLineOfSight(targetActor);
        if (visible)
        {
            Actor.CancelDestination();
            FacingTarget = targetActor;
            var forward = Actor.VisualForward;
            forward.Y = 0;
            if (distance < 0.01f || forward.Normalized().Dot(horizontal.Normalized()) >= 0.98f)
                BeginAttack();
            return;
        }
        FacingTarget = null;
        _repathTimer -= (float)delta;
        if (_repathTimer > 0) return;
        _repathTimer = Mathf.Max(0.05f, TargetUpdateInterval);
        var map = _agent.GetNavigationMap();
        if (!map.IsValid || NavigationServer3D.MapGetIterationId(map) == 0) return;
        var endpoint = NavigationServer3D.MapGetClosestPoint(map, targetActor.GlobalPosition);
        if (endpoint.DistanceTo(targetActor.GlobalPosition) > 0.5f) { CancelTargetOrder(); return; }
        var path = NavigationServer3D.MapGetPath(map, Actor.GlobalPosition, endpoint, true);
        if (path.Length < 2) { CancelTargetOrder(); return; }
        // Stop one arrival tolerance before the target's center-based melee radius.
        float retreat = Mathf.Max(0.05f, Mathf.Max(0.7f, MeleeRange) - Actor.ArrivalDistance + 0.02f);
        var waypoint = endpoint;
        for (int i = path.Length - 1; i > 0; i--)
        {
            var segment = path[i - 1] - path[i];
            segment.Y = 0;
            float length = segment.Length();
            if (length >= retreat)
            {
                waypoint = path[i] + segment / length * retreat;
                break;
            }
            retreat -= length;
            waypoint = path[i - 1];
        }
        if (!Actor.TrySetDestination(waypoint)) CancelTargetOrder();
    }

    private bool ManualMoveHeld() => Input.GetVector(Actor.MoveLeftAction, Actor.MoveRightAction,
        Actor.MoveUpAction, Actor.MoveDownAction) != Vector2.Zero;

    private bool HasLineOfSight(CollisionObject3D target)
        => MeleeImpactValidator.CanHit(Actor, target, float.PositiveInfinity);

    /// <summary>Authoritative local impact check; never trusts the earlier approach result.</summary>
    public bool CanHit(MeleeTarget target)
    {
        if (!IsValid(target) || !GodotObject.IsInstanceValid(Actor)) return false;
        return MeleeImpactValidator.CanHit(Actor, target.Actor, Mathf.Max(0.7f, MeleeRange));
    }

    /// <summary>Movement cancels the remaining attack; an issued impact is never undone.</summary>
    public bool InterruptAttack()
    {
        if (State != MeleeState.Attack || Defeated) return false;
        int sequence = AttackSequence;
        bool hitMomentPassed = _impactIssued;
        State = MeleeState.Idle;
        SelectedTarget = null;
        CommittedTarget = null;
        FacingTarget = null;
        AttackRemaining = 0;
        _impactIssued = true;
        Actor.SetMovementLocked(false);
        EmitSignal(SignalName.AttackInterrupted, sequence, hitMomentPassed);
        return true;
    }

    /// <summary>Terminal cancellation. No queued or in-flight impact can resume.</summary>
    public void Defeat()
    {
        if (Defeated) return;
        Defeated = true;
        Enabled = false;
        State = MeleeState.Idle;
        SelectedTarget = null;
        CommittedTarget = null;
        FacingTarget = null;
        AttackRemaining = 0;
        _impactIssued = true;
        if (GodotObject.IsInstanceValid(Actor))
        {
            Actor.CancelDestination();
            Actor.SetMovementLocked(true);
        }
        SetPhysicsProcess(false);
        SetProcessUnhandledInput(false);
    }
}
