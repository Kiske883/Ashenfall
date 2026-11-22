using Godot;
using Ashenfall.Combat;

namespace Ashenfall.Characters;

/// <summary>Variant-only visual binding; reads movement after physics and never commands the actor.</summary>
public partial class DwarfPlayerPresentation : Node
{
    [Export] public PlayerController Actor { get; set; }
    [Export] public PackedScene PresentationScene { get; set; }
    [Export(PropertyHint.File, "*.tres")] public string DefeatAnimationPath { get; set; } = "";
    // Optional per-scene actor configuration. Zero preserves the original player default.
    [Export(PropertyHint.Range, "0,20,0.1")] public float MovementSpeedOverride { get; set; }
    // Approved 06B support travel / (cycle duration * support fraction).
    [Export(PropertyHint.Range, "0.01,10,0.01")] public float WalkReferenceSpeed { get; set; } = 0.14f / (1.4f * 0.6f);
    [Export(PropertyHint.Range, "0.01,1,0.01")] public float MinimumWalkPlaybackRate { get; set; } = 0.1f;
    [Export(PropertyHint.Range, "0.1,30,0.1")] public float MaximumWalkPlaybackRate { get; set; } = 2f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float RateSmoothingSeconds { get; set; } = 0.1f;
    // Enabled only by the playable combat variant. Other dwarf demos keep their approved clips.
    [Export] public bool CombatMode { get; set; }
    [Export] public Key AttackKey { get; set; } = Key.Space;
    [Export] public PlayerMeleeController MeleeController { get; set; }
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float CombatLocomotionBlendSeconds { get; set; } = 0.18f;
    // 08G: visual pose offsets only; the approved clips and actor physics stay untouched.
    [Export] public bool NaturalWalkMotion { get; set; }
    [Export(PropertyHint.Range, "0,0.1,0.001")] public float WalkTorsoYaw { get; set; } = 0.035f;
    [Export(PropertyHint.Range, "0,0.1,0.001")] public float WalkTorsoRoll { get; set; } = 0.012f;
    [Export(PropertyHint.Range, "0,0.1,0.001")] public float WalkAxeYaw { get; set; } = 0.022f;
    [Export(PropertyHint.Range, "0,0.05,0.001")] public float WalkAxeSway { get; set; } = 0.012f;
    [Export(PropertyHint.Range, "0.01,1,0.01")] public float WalkMotionBlendSeconds { get; set; } = 0.18f;
    public Vector3 ActualHorizontalVelocity { get; private set; }
    public float ActualHorizontalSpeed => ActualHorizontalVelocity.Length();
    public float RequestedWalkPlaybackRate { get; private set; }
    public float WalkPlaybackRate { get; private set; }
    public bool WalkRateLimited { get; private set; }
    public DwarfBerserkerPresentation Presentation { get; private set; }
    private Node3D _visual;
    private AnimationPlayer _player;
    private DwarfApprovedAxeAttackPresentation _combatPresentation;
    private bool _combatWalking;
    private bool _refreshAfterInterrupt;
    private Vector3 _previousPosition;
    private float _facing;
    private float _walkMotionWeight;
    private float _lastWalkPhase;
    private float _defeatClock;
    public float WalkMotionWeight => _walkMotionWeight;
    public bool Defeated { get; private set; }

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
        if (!string.IsNullOrEmpty(DefeatAnimationPath))
            _player.AddAnimationLibrary("Death", GD.Load<AnimationLibrary>(DefeatAnimationPath));
        if (CombatMode)
        {
            _combatPresentation = Presentation as DwarfApprovedAxeAttackPresentation;
            if (_combatPresentation == null || !_player.HasAnimation("Combat/CombatWalk") ||
                !_player.HasAnimation("Combat/AxeAttack") || !_player.HasAnimation("Combat/GuardB"))
            {
                GD.PushError("Combat dwarf requires the approved Guard B / CombatWalk / Impact C presentation.");
                SetPhysicsProcess(false); SetProcessUnhandledInput(false); return;
            }
            var hints = new CanvasLayer();
            AddChild(hints);
            var attackHint = new Label { Text = "SPACE  /  AXE ATTACK", Position = new Vector2(31, 91),
                MouseFilter = Control.MouseFilterEnum.Ignore };
            attackHint.AddThemeFontSizeOverride("font_size", 16);
            hints.AddChild(attackHint);
            if (MeleeController != null)
            {
                MeleeController.AttackStarted += OnAttackStarted;
                MeleeController.AttackInterrupted += OnAttackInterrupted;
            }
        }
        ProcessPhysicsPriority = Actor.ProcessPhysicsPriority + 1;
        ResetMotionTracking();
    }

    public override void _ExitTree()
    {
        if (!GodotObject.IsInstanceValid(MeleeController)) return;
        MeleeController.AttackStarted -= OnAttackStarted;
        MeleeController.AttackInterrupted -= OnAttackInterrupted;
    }

    private void OnAttackStarted(Node3D target, int sequence) =>
        RequestAttack(_player.GetAnimation("Combat/AxeAttack").Length /
            Mathf.Max(0.1f, MeleeController.AttackDuration));

    private void OnAttackInterrupted(int sequence, bool impactIssued)
    {
        _combatPresentation.InterruptApprovedAttack();
        _refreshAfterInterrupt = true;
    }

    public void ResetMotionTracking()
    {
        _previousPosition = Actor.GlobalPosition; _facing = _visual.Rotation.Y;
        ActualHorizontalVelocity = Vector3.Zero;
        WalkPlaybackRate = RequestedWalkPlaybackRate = 0; WalkRateLimited = false;
        _combatWalking = false;
        _refreshAfterInterrupt = false;
        _walkMotionWeight = 0;
        _lastWalkPhase = 0;
        if (CombatMode) _combatPresentation.ShowGuardB();
        else Presentation.PlayState("Idle");
        _player.SpeedScale = 1;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (Defeated) return;
        if (!CombatMode || MeleeController != null || inputEvent is not InputEventKey key || !key.Pressed || key.Echo ||
            (key.PhysicalKeycode != AttackKey && key.Keycode != AttackKey)) return;
        if (RequestAttack()) GetViewport().SetInputAsHandled();
    }

    /// <summary>Starts the approved visual attack once. Movement and gameplay events remain actor-owned.</summary>
    public bool RequestAttack() => RequestAttack(1);

    public bool RequestAttack(double playbackRate)
    {
        if (Defeated || !CombatMode || _combatPresentation == null || _combatPresentation.IsAttacking) return false;
        _combatWalking = false;
        _combatPresentation.PlayApprovedAttack();
        _player.SpeedScale = Mathf.Clamp((float)playbackRate, 0.1f, 10f);
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Defeated)
        {
            _defeatClock += (float)delta;
            if (_player.HasAnimation("Death/Fall"))
            {
                // The adventure-only clip drives joints and both axe grips.
                Presentation._Process(delta);
            }
            else
            {
                // Earlier standalone demos retain their existing visual fallback.
                float weight = 1 - Mathf.Exp(-5 * (float)delta);
                Presentation.Rotation = new Vector3(0, 0,
                    Mathf.LerpAngle(Presentation.Rotation.Z, -1.10f, weight));
                Presentation.Position = Presentation.Position.Lerp(new Vector3(0.43f, 0.32f, 0), weight);
            }
            VisualGrounding.Settle(Presentation, Actor);
            float duration = _player.HasAnimation("Death/Fall")
                ? (float)_player.GetAnimation("Death/Fall").Length : 1.5f;
            if (_defeatClock >= duration) SetPhysicsProcess(false);
            return;
        }
        var offset = Actor.GlobalPosition - _previousPosition;
        _previousPosition = Actor.GlobalPosition; offset.Y = 0;
        // External spawn/teleport repositioning is not a locomotion sample.
        bool teleport = offset.Length() > Mathf.Max(0.5f, Actor.MovementSpeed * (float)delta * 4);
        ActualHorizontalVelocity = teleport ? Vector3.Zero : offset / (float)delta;
        float threshold = (CombatMode ? _combatWalking : Presentation.ActiveClip == "Walk") ? 0.02f : 0.08f;
        bool walking = ActualHorizontalSpeed > threshold;
        if (CombatMode)
        {
            // AnimationPlayer owns the one-shot clock; locomotion cannot replace it.
            if (!_combatPresentation.IsAttacking && (walking != _combatWalking || _refreshAfterInterrupt))
            {
                _refreshAfterInterrupt = false;
                _combatWalking = walking;
                if (walking)
                {
                    _player.Play("Combat/CombatWalk", CombatLocomotionBlendSeconds);
                    // Enter at a near-planted support pose instead of the split-stride first key.
                    _player.Seek(0.165, false);
                }
                else _player.Play("Combat/GuardB", CombatLocomotionBlendSeconds);
            }
        }
        else Presentation.PlayState(walking ? "Walk" : "Idle");
        if (walking)
        {
            RequestedWalkPlaybackRate = ActualHorizontalSpeed / Mathf.Max(0.001f, WalkReferenceSpeed);
            float minimum = Mathf.Max(0.01f, MinimumWalkPlaybackRate);
            float maximum = Mathf.Max(minimum, MaximumWalkPlaybackRate);
            float target = Mathf.Clamp(RequestedWalkPlaybackRate, minimum, maximum);
            WalkRateLimited = !Mathf.IsEqualApprox(target, RequestedWalkPlaybackRate);
            float weight = RateSmoothingSeconds <= 0 ? 1 : 1 - Mathf.Exp(-(float)delta / RateSmoothingSeconds);
            WalkPlaybackRate = Mathf.Lerp(WalkPlaybackRate, target, weight);
            if (!CombatMode || !_combatPresentation.IsAttacking) _player.SpeedScale = WalkPlaybackRate;
            // PlayerController's desired-direction turn is overridden only in this variant's visual node.
            float angle = Mathf.Atan2(-ActualHorizontalVelocity.X, -ActualHorizontalVelocity.Z);
            _facing = Mathf.LerpAngle(_facing, angle, 1 - Mathf.Exp(-Actor.RotationSpeed * (float)delta));
        }
        else
        {
            WalkPlaybackRate = RequestedWalkPlaybackRate = 0; WalkRateLimited = false;
            if (!CombatMode || !_combatPresentation.IsAttacking) _player.SpeedScale = 1;
        }
        if (CombatMode && MeleeController != null && GodotObject.IsInstanceValid(MeleeController.FacingTarget))
        {
            var toward = MeleeController.FacingTarget.GlobalPosition - Actor.GlobalPosition;
            toward.Y = 0;
            if (toward.LengthSquared() > 0.0001f)
                _facing = Mathf.LerpAngle(_facing, Mathf.Atan2(-toward.X, -toward.Z),
                    1 - Mathf.Exp(-Actor.RotationSpeed * (float)delta));
        }
        // Stay oriented along the last actual movement even when desired input pushes against a wall.
        _visual.Rotation = new Vector3(0, _facing, 0);
        Presentation._Process(delta);
        ApplyNaturalWalkMotion(delta, walking);
    }

    private void ApplyNaturalWalkMotion(double delta, bool walking)
    {
        if (!NaturalWalkMotion || !CombatMode) return;
        // The attack owns its complete authored pose. Fade out on locomotion changes,
        // but never layer locomotion offsets over an attack or a defeated pose.
        bool attack = _combatPresentation.IsAttacking;
        float target = walking && !attack && _player.CurrentAnimation == "Combat/CombatWalk" ? 1f : 0f;
        float blend = 1f - Mathf.Exp(-(float)delta / Mathf.Max(0.01f, WalkMotionBlendSeconds));
        _walkMotionWeight = Mathf.Lerp(_walkMotionWeight, target, blend);
        if (attack || _walkMotionWeight < 0.0001f) return;

        float cycle = (float)_player.GetAnimation("Combat/CombatWalk").Length;
        // Hold the last step phase while fading to Guard B; resetting phase at
        // the clip switch would visibly snap the shoulders and the shared axe.
        if (_player.CurrentAnimation == "Combat/CombatWalk")
            _lastWalkPhase = Mathf.Tau * (float)_player.CurrentAnimationPosition / cycle;
        float phase = _lastWalkPhase;
        float step = Mathf.Sin(phase);
        float rise = Mathf.Sin(2f * phase);
        // Save sampled AnimationPlayer controls, solve one offset pose, then restore
        // them. No delta is fed into the next frame, so extended walking cannot drift.
        Vector3 torso = Presentation.TorsoRotation;
        Vector3 weaponRotation = Presentation.WeaponRotation;
        Vector3 weaponOffset = Presentation.WeaponOffset;
        try
        {
            float weight = _walkMotionWeight;
            // Counter-turn the chest and the shared two-hand axe attachment. The
            // existing IK solver then moves both shoulders/arms to the same grips.
            Presentation.TorsoRotation = (Basis.FromEuler(torso).GetRotationQuaternion() *
                new Quaternion(Vector3.Up, WalkTorsoYaw * step * weight) *
                new Quaternion(Vector3.Forward, WalkTorsoRoll * rise * weight)).GetEuler();
            Presentation.WeaponRotation = (Basis.FromEuler(weaponRotation).GetRotationQuaternion() *
                new Quaternion(Vector3.Up, -WalkAxeYaw * step * weight)).GetEuler();
            Presentation.WeaponOffset = weaponOffset + new Vector3(WalkAxeSway * step * weight, 0, 0);
            _combatPresentation.RefreshPose();
        }
        finally
        {
            Presentation.TorsoRotation = torso;
            Presentation.WeaponRotation = weaponRotation;
            Presentation.WeaponOffset = weaponOffset;
        }
    }

    public void ShowDefeat()
    {
        if (Defeated || !GodotObject.IsInstanceValid(Presentation)) return;
        Defeated = true;
        _walkMotionWeight = 0;
        _defeatClock = 0;
        _player.SpeedScale = 1;
        if (_player.HasAnimation("Death/Fall"))
        {
            _player.Play("Death/Fall", 0.12);
            _player.Advance(0);
        }
        else _player.Stop(false);
        SetProcessUnhandledInput(false);
    }
}
