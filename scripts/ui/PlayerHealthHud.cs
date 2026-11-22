using Ashenfall.Combat;
using Godot;

namespace Ashenfall.UI;

/// <summary>Screen-space observer; owns no health or combat decisions.</summary>
public partial class PlayerHealthHud : CanvasLayer
{
    [Export] public HealthComponent Health { get; set; }
    public ProgressBar Bar { get; private set; }
    public Label ValueLabel { get; private set; }

    public override void _Ready()
    {
        Bar = GetNode<ProgressBar>("Panel/Bar");
        ValueLabel = GetNode<Label>("Panel/Value");
        if (Health == null)
        {
            GD.PushError("Player HUD requires a health component.");
            return;
        }
        Health.HealthChanged += UpdateDisplay;
        UpdateDisplay(Health.CurrentHealth, Health.MaximumHealth);
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Health)) Health.HealthChanged -= UpdateDisplay;
    }

    private void UpdateDisplay(int current, int maximum)
    {
        Bar.MaxValue = maximum;
        Bar.Value = current;
        ValueLabel.Text = $"HP {current} / {maximum}";
    }
}
