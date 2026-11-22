using Ashenfall.World;
using Godot;

namespace Ashenfall.UI;

/// <summary>Objective and terminal overlay. It observes the session and only requests restart.</summary>
public partial class AdventureHud : CanvasLayer
{
    [Export] public AdventureObjective Objective { get; set; }
    [Export] public AdventureSession Session { get; set; }
    [Export] public EncounterResetController Reset { get; set; }
    [Export(PropertyHint.Range, "0,5,0.05")] public float DefeatPresentationDelay { get; set; } = 2.0f;
    public Control Overlay { get; private set; }
    public Label ObjectiveLabel { get; private set; }
    public Label ResultLabel { get; private set; }
    public Button RestartButton { get; private set; }
    public bool DefeatPresentationPending { get; private set; }
    private float _defeatRemaining;

    public override void _Ready()
    {
        Overlay = GetNode<Control>("Overlay");
        ObjectiveLabel = GetNode<Label>("ObjectiveLabel");
        ResultLabel = GetNode<Label>("Overlay/Panel/Contents/Result");
        RestartButton = GetNode<Button>("Overlay/Panel/Contents/RestartButton");
        if (Objective == null || Session == null || Reset == null)
        {
            GD.PushError("Adventure HUD requires objective, session and reset controller.");
            return;
        }
        Overlay.Hide();
        SetProcess(false);
        Objective.ProgressChanged += OnProgress;
        Session.OutcomeChanged += OnOutcome;
        RestartButton.Pressed += OnRestart;
        OnProgress(Objective.DefeatedCount, Objective.GuardianCount);
    }

    public override void _ExitTree()
    {
        DefeatPresentationPending = false;
        if (GodotObject.IsInstanceValid(Objective)) Objective.ProgressChanged -= OnProgress;
        if (GodotObject.IsInstanceValid(Session)) Session.OutcomeChanged -= OnOutcome;
        if (GodotObject.IsInstanceValid(RestartButton)) RestartButton.Pressed -= OnRestart;
    }

    private void OnProgress(int defeated, int required)
    {
        ObjectiveLabel.Text = defeated == required && required > 0
            ? $"GUARDIANS {defeated}/{required}  /  REACH THE EASTERN RUIN"
            : $"DEFEAT THE GUARDIANS  {defeated}/{required}\nTHEN REACH THE EASTERN RUIN";
    }

    private void OnOutcome(int outcome, int defeated)
    {
        bool won = outcome == (int)AdventureSession.AdventureOutcome.Victory;
        ResultLabel.Text = won
            ? $"ADVENTURE COMPLETE\n{defeated} GUARDIANS DEFEATED"
            : "THE DWARF HAS FALLEN\nTRY THE ADVENTURE AGAIN";
        if (!won && DefeatPresentationDelay > 0)
        {
            // Gameplay defeat was already committed by AdventureSession and
            // PlayerDefeatController. Only the covering UI waits for the fall.
            DefeatPresentationPending = true;
            _defeatRemaining = DefeatPresentationDelay;
            SetProcess(true);
            return;
        }
        ShowOutcome();
    }

    public override void _Process(double delta)
    {
        if (!DefeatPresentationPending) return;
        _defeatRemaining -= (float)delta;
        if (_defeatRemaining > 0) return;
        DefeatPresentationPending = false;
        SetProcess(false);
        if (Session.Outcome == AdventureSession.AdventureOutcome.Defeat) ShowOutcome();
    }

    private void ShowOutcome()
    {
        Overlay.Show();
        RestartButton.GrabFocus();
    }

    private void OnRestart() => Reset.Restart();
}
