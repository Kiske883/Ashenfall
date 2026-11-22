using Godot;

namespace Ashenfall.Characters;

/// <summary>Isolated approved-pose attack preview; the AI and player do not reference this class.</summary>
public partial class DwarfApprovedAxeAttackPresentation : DwarfAxeBladePosePresentation
{
    private AnimationPlayer _player;
    private Quaternion _weaponOrientation = Quaternion.Identity;
    private Quaternion _torsoOrientation = Quaternion.Identity;

    // AnimationPlayer interpolates Quaternion values; these setters convert
    // them immediately before the inherited rig solves hands and legs.
    [Export] public Quaternion WeaponOrientation
    {
        get => _weaponOrientation;
        set { _weaponOrientation = value.Normalized(); WeaponRotation = new Basis(_weaponOrientation).GetEuler(); }
    }

    [Export] public Quaternion TorsoOrientation
    {
        get => _torsoOrientation;
        set { _torsoOrientation = value.Normalized(); TorsoRotation = new Basis(_torsoOrientation).GetEuler(); }
    }

    [Export] public float BladeRollDegrees { get; set; } = 109.5f;

    public override void _Ready()
    {
        base._Ready();
        _player = GetNode<AnimationPlayer>("AnimationPlayer");
        // The base presentation owns one-shot attack completion. Its callback
        // runs first; restore the selected guard in this later subscriber.
        _player.AnimationFinished += clip =>
        {
            if (clip == "Combat/AxeAttack") ShowGuardB();
        };
        ShowGuardB();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        SetBladeRollDegrees(BladeRollDegrees);
    }

    public void ShowGuardB()
    {
        if (IsAttacking) return;
        _player.SpeedScale = 1;
        _player.Play("Combat/GuardB", 0);
        base._Process(0);
        SetBladeRollDegrees(BladeRollDegrees);
    }

    public void PlayApprovedAttack()
    {
        PlayClip("Attack");
        SetBladeRollDegrees(BladeRollDegrees);
    }

    /// <summary>Releases the one-shot; the gameplay adapter blends to guard or walk.</summary>
    public void InterruptApprovedAttack()
    {
        if (!IsAttacking) return;
        InterruptAttack();
    }

    /// <summary>Deterministic sampled inspection without blending; do not use for gameplay.</summary>
    public void SampleApprovedAttack(double seconds)
    {
        SampleAttack(seconds);
        SetBladeRollDegrees(BladeRollDegrees);
    }
}
