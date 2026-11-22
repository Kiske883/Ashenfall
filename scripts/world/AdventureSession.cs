using Ashenfall.Characters;
using Ashenfall.Combat;
using Godot;

namespace Ashenfall.World;

/// <summary>Coordinates the one-shot adventure outcome without owning combat or UI.</summary>
public partial class AdventureSession : Node
{
    public enum AdventureOutcome { Playing, Victory, Defeat }
    [Signal] public delegate void OutcomeChangedEventHandler(int outcome, int defeatedCount);
    [Export] public AdventureObjective Objective { get; set; }
    [Export] public HealthComponent PlayerHealth { get; set; }
    [Export] public PlayerController Player { get; set; }
    [Export] public PlayerMeleeController Melee { get; set; }
    public AdventureOutcome Outcome { get; private set; } = AdventureOutcome.Playing;

    public override void _Ready()
    {
        if (Objective == null || PlayerHealth == null || Player == null || Melee == null)
        {
            GD.PushError("Adventure session requires objective, player health, player and melee.");
            return;
        }
        Objective.Completed += OnCompleted;
        PlayerHealth.Died += OnPlayerDied;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Objective)) Objective.Completed -= OnCompleted;
        if (GodotObject.IsInstanceValid(PlayerHealth)) PlayerHealth.Died -= OnPlayerDied;
    }

    private void OnCompleted(int defeated)
    {
        if (Outcome != AdventureOutcome.Playing || !PlayerHealth.IsAlive) return;
        Outcome = AdventureOutcome.Victory;
        Player.InputEnabled = false;
        Player.CancelDestination();
        Player.SetMovementLocked(true);
        Melee.Defeat(); // Cancels any in-flight logical attack; the player remains healthy.
        EmitSignal(SignalName.OutcomeChanged, (int)Outcome, defeated);
    }

    private void OnPlayerDied()
    {
        if (Outcome != AdventureOutcome.Playing) return;
        Outcome = AdventureOutcome.Defeat;
        EmitSignal(SignalName.OutcomeChanged, (int)Outcome, Objective.DefeatedCount);
    }
}
