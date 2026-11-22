using Ashenfall.Combat;
using Godot;

namespace Ashenfall.Characters;

/// <summary>Connects model-independent health to terminal player input and command cancellation.</summary>
public partial class PlayerDefeatController : Node
{
    [Export] public HealthComponent Health { get; set; }
    [Export] public PlayerController Actor { get; set; }
    [Export] public PlayerMeleeController Melee { get; set; }
    [Export] public DwarfPlayerPresentation Presentation { get; set; }
    public bool IsDefeated { get; private set; }

    public override void _Ready()
    {
        if (Health == null || Actor == null || Melee == null || Presentation == null)
        {
            GD.PushError("Player defeat controller requires health, actor, melee and presentation.");
            return;
        }
        Health.Died += OnDied;
        if (!Health.IsAlive) OnDied();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Health)) Health.Died -= OnDied;
    }

    private void OnDied()
    {
        if (IsDefeated) return;
        IsDefeated = true;
        Melee.Defeat();
        Actor.InputEnabled = false;
        Actor.CancelDestination();
        Actor.SetMovementLocked(true);
        Presentation.ShowDefeat();
    }
}
