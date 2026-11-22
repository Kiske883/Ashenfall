using Godot;
using System.Collections.Generic;

namespace Ashenfall.Characters;

/// <summary>Isolated visual attack clock and forearm twist distribution; reuses the approved rig solver.</summary>
public partial class DwarfAxeAttackPresentation : DwarfBerserkerPresentation
{
    [Export(PropertyHint.Range, "0,1,0.01")] public float ForearmTwistWeight { get; set; }
    public StringName PreviewClip { get; private set; } = "Idle";
    public bool IsAttacking { get; private set; }
    private AnimationPlayer _player;
    private readonly List<(Node3D Elbow, Node3D Wrist, Vector3 Axis)> _forearms = new();

    public override void _Ready()
    {
        base._Ready();
        _player = GetNode<AnimationPlayer>("AnimationPlayer");
        foreach (string side in new[] { "Left", "Right" })
        {
            var elbow = (Node3D)FindChild(side + "ElbowPivot", true, false);
            var wrist = (Node3D)FindChild(side + "WristPivot", true, false);
            _forearms.Add((elbow, wrist, elbow.ToLocal(wrist.GlobalPosition).Normalized()));
        }
        _player.AnimationFinished += clip =>
        {
            if (clip != "Combat/AxeAttack") return;
            IsAttacking = false; PlayClip("Idle");
        };
    }

    public void PlayClip(string state)
    {
        if (IsAttacking || (state != "Idle" && state != "Walk" && state != "Attack")) return;
        string clip = state == "Attack" ? "Combat/AxeAttack" : state;
        if (_player.CurrentAnimation == clip && _player.IsPlaying()) return;
        IsAttacking = state == "Attack"; PreviewClip = state;
        if (!IsAttacking) ForearmTwistWeight = 0;
        _player.SpeedScale = 1;
        _player.Play(clip, 0.12); _player.Advance(0); ApplyAttackPose();
    }

    /// <summary>Stops an interrupted preview attack without changing any authored pose or clip.</summary>
    public void InterruptAttack()
    {
        if (!IsAttacking) return;
        IsAttacking = false;
        PreviewClip = "Idle";
        ForearmTwistWeight = 0;
        _player.Stop(false);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        DistributeTwist();
    }

    public void SampleAttack(double seconds)
    {
        _player.Stop(); IsAttacking = true; PreviewClip = "Attack";
        _player.Play("Combat/AxeAttack", 0); _player.Seek(seconds, true); ApplyAttackPose();
    }

    private void ApplyAttackPose() { ApplyPose(); DistributeTwist(); }

    /// <summary>Re-solves a sampled visual pose while preserving the approved forearm twist.</summary>
    public void RefreshPose() => ApplyAttackPose();

    private void DistributeTwist()
    {
        float weight = Mathf.Clamp(ForearmTwistWeight, 0, 1);
        if (weight <= 0) return;
        foreach (var arm in _forearms)
        {
            var handTransform = arm.Wrist.GlobalTransform;
            var relative = arm.Elbow.GlobalBasis.GetRotationQuaternion().Inverse()
                * arm.Wrist.GlobalBasis.GetRotationQuaternion();
            if (relative.W < 0) relative = -relative;
            var vector = new Vector3(relative.X, relative.Y, relative.Z);
            var projected = arm.Axis * vector.Dot(arm.Axis);
            var twist = new Quaternion(projected.X, projected.Y, projected.Z, relative.W);
            if (twist.LengthSquared() < 0.000001f) continue;
            var rotation = Quaternion.Identity.Slerp(twist.Normalized(), weight);
            // Rotation about the original forearm axis preserves bone length and the wrist origin.
            arm.Elbow.Basis *= new Basis(rotation);
            arm.Wrist.GlobalTransform = handTransform;
        }
    }
}
