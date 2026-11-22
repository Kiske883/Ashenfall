using Godot;
using Ashenfall.Characters;

namespace Ashenfall.Enemies;

/// <summary>One-way presentation of AI state, actual movement and the AI's phase clock.</summary>
public partial class EnemyAnimationController : Node
{
    [Export] public EnemyController Actor { get; set; }
    [Export] public AnimationPlayer Player { get; set; }
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float BlendDuration { get; set; } = 0.12f;
    // Approved unit-scale clip: 0.28 m of stance travel / (1.4 s * 0.60 support).
    [Export(PropertyHint.Range, "0.01,10,0.01")] public float WalkReferenceSpeed { get; set; } = 0.28f / (1.4f * 0.6f);
    [Export(PropertyHint.Range, "0.01,1,0.01")] public float MinimumWalkPlaybackRate { get; set; } = 0.1f;
    [Export(PropertyHint.Range, "0.1,10,0.05")] public float MaximumWalkPlaybackRate { get; set; } = 2;
    [Export(PropertyHint.Range, "0,1,0.01")] public float WalkRateSmoothingSeconds { get; set; } = 0.1f;
    [Export(PropertyHint.Range, "0,0.2,0.005")] public float AnticipationTorsoPitch { get; set; } = 0.07f;
    [Export(PropertyHint.Range, "0,0.35,0.005")] public float AnticipationSwordShoulderLift { get; set; } = 0.20f;
    public float AnticipationWeight { get; private set; }
    public StringName ActiveClip { get; private set; } = "";
    public int AttackPlaybackStarts { get; private set; }
    public float ActualHorizontalSpeed { get; private set; }
    public float RequestedWalkPlaybackRate { get; private set; }
    public float WalkPlaybackRate { get; private set; }
    public bool WalkRateLimited { get; private set; }
    private Vector3 _previousPosition;
    private ulong _cycle;
    private Node3D _visual;
    private Node3D _torso, _swordShoulder;
    private Transform3D _defeatStart;
    private float _defeatClock;
    private bool _defeatStarted;

    public override void _Ready()
    {
        if (Actor == null || Player == null)
        {
            GD.PushError("Enemy animation requires Actor and AnimationPlayer references.");
            SetPhysicsProcess(false);
            return;
        }
        // The child runs after AI MoveAndSlide and never writes actor transforms or state.
        ProcessPhysicsPriority = Actor.ProcessPhysicsPriority + 1;
        Player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        _visual = Actor.GetNode<Node3D>("Visual");
        _torso = _visual.GetNode<Node3D>("Presentation/PoseRoot/PelvisPivot/TorsoPivot");
        _swordShoulder = _torso.GetNode<Node3D>("RightShoulderPivot");
        _previousPosition = Actor.GlobalPosition;
        Select("Idle", 0);
        Player.Advance(0);
    }

    private void Select(StringName clip, double blend)
    {
        if (ActiveClip == clip) return;
        ActiveClip = clip;
        Player.Play(clip, blend);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Actor.State == EnemyController.EnemyState.Defeated)
        {
            AnticipationWeight = 0;
            if (!_defeatStarted)
            {
                _defeatStarted = true;
                ActiveClip = "Defeated";
                Player.Stop(true);
                _defeatStart = _visual.Transform;
            }
            _defeatClock += (float)delta;
            float progress = Mathf.Clamp(_defeatClock / 0.65f, 0, 1);
            progress = progress * progress * (3 - 2 * progress);
            // Visual-only forward collapse; the body stays in the tree but loses collision.
            var tilt = new Quaternion(Vector3.Right, -1.15f * progress);
            _visual.Transform = new Transform3D(new Basis(_defeatStart.Basis.GetRotationQuaternion() * tilt),
                _defeatStart.Origin + new Vector3(0, -0.06f * progress, -0.08f * progress));
            VisualGrounding.Settle(_visual, Actor);
            if (_defeatClock >= 0.65f) SetPhysicsProcess(false);
            return;
        }
        var offset = Actor.GlobalPosition - _previousPosition;
        _previousPosition = Actor.GlobalPosition;
        ActualHorizontalSpeed = new Vector2(offset.X, offset.Z).Length() / (float)delta;
        bool attack = Actor.State == EnemyController.EnemyState.AttackWindup ||
            Actor.State == EnemyController.EnemyState.AttackRecovery;
        if (attack)
        {
            WalkPlaybackRate = RequestedWalkPlaybackRate = 0;
            WalkRateLimited = false;
            if (ActiveClip != "Combat/Attack" || _cycle != Actor.AttackCycleId)
            {
                _cycle = Actor.AttackCycleId;
                // Force a fresh attack even when two AI cycles are adjacent.
                ActiveClip = "Combat/Attack";
                Player.Play(ActiveClip, BlendDuration);
                Player.Seek(0, false);
                AttackPlaybackStarts++;
            }
            float fraction = Mathf.Clamp(1 - Actor.AttackPhaseRemaining /
                Mathf.Max(0.05f, Actor.AttackPhaseDuration), 0, 1);
            double targetTime = Actor.State == EnemyController.EnemyState.AttackWindup
                ? fraction * 0.6 : 0.6 + fraction * 1.0;
            // Advance, rather than restart/seek every frame, preserves AnimationPlayer blending.
            double advance = targetTime - Player.CurrentAnimationPosition;
            if (advance >= 0) Player.Advance(advance);
            else { Player.Seek(targetTime, true); }
            // The approved attack clip already raises the sword. This small
            // temporary pose accent makes its preparation legible at gameplay
            // scale. AnimationPlayer rewrites both tracked joints every tick,
            // so cancellation and the strike cannot inherit an old offset.
            AnticipationWeight = Actor.State == EnemyController.EnemyState.AttackWindup
                ? Mathf.Sin(Mathf.Pi * fraction) : 0;
            if (AnticipationWeight > 0)
            {
                _torso.Basis *= new Basis(Vector3.Right, -AnticipationTorsoPitch * AnticipationWeight);
                _swordShoulder.Basis *= new Basis(Vector3.Right, -AnticipationSwordShoulderLift * AnticipationWeight);
            }
            return;
        }

        AnticipationWeight = 0;
        bool locomotion = Actor.State == EnemyController.EnemyState.Chase || Actor.State == EnemyController.EnemyState.Return;
        float threshold = ActiveClip == "Walk" ? 0.02f : 0.08f;
        bool walking = locomotion && ActualHorizontalSpeed > threshold;
        Select(walking ? "Walk" : "Idle", BlendDuration);
        if (walking)
        {
            RequestedWalkPlaybackRate = ActualHorizontalSpeed / Mathf.Max(0.001f, WalkReferenceSpeed);
            float minimum = Mathf.Max(0.01f, MinimumWalkPlaybackRate);
            float maximum = Mathf.Max(minimum, MaximumWalkPlaybackRate);
            float targetRate = Mathf.Clamp(RequestedWalkPlaybackRate, minimum, maximum);
            WalkRateLimited = !Mathf.IsEqualApprox(targetRate, RequestedWalkPlaybackRate);
            float weight = WalkRateSmoothingSeconds <= 0 ? 1 :
                1 - Mathf.Exp(-(float)delta / WalkRateSmoothingSeconds);
            WalkPlaybackRate = Mathf.Lerp(WalkPlaybackRate, targetRate, weight);
            Player.Advance(delta * WalkPlaybackRate);
        }
        else
        {
            WalkPlaybackRate = RequestedWalkPlaybackRate = 0;
            WalkRateLimited = false;
            Player.Advance(delta);
        }
    }
}
