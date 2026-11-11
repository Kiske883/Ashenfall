using Godot;

namespace Ashenfall.Characters;

/// <summary>Retained 06C.1 tuning entry point, using the shared presentation configuration.</summary>
public partial class DwarfLocomotionExperiment : DwarfPlayerPresentation
{
    [Export(PropertyHint.Range, "0.1,5,0.1")] public float ExperimentalMovementSpeed { get; set; } = 2.5f;

    public override void _Ready()
    {
        MovementSpeedOverride = ExperimentalMovementSpeed;
        base._Ready();
    }
}
