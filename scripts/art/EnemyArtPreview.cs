using Godot;

namespace Ashenfall.Art;

/// <summary>Presentation-only zoom choices. No project input actions or gameplay cameras are changed.</summary>
public partial class EnemyArtPreview : Node3D
{
    private Viewport.Msaa _previousMsaa;

    public override void _Ready()
    {
        _previousMsaa = GetViewport().Msaa3D;
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
        GetNode<Button>("Presentation/Inspection").Pressed += () => SetGameplayView(false);
        GetNode<Button>("Presentation/Gameplay").Pressed += () => SetGameplayView(true);
        SetGameplayView(false);
    }

    public void SetGameplayView(bool gameplay)
    {
        GetNode<Camera3D>("Camera3D").Size = gameplay ? 22f : 3.6f;
        GetNode<Label>("Presentation/Mode").Text = gameplay
            ? "GAMEPLAY SCALE  /  Orthographic size 22, matching World"
            : "INSPECTION  /  Same 45-degree isometric angle, closer framing";
    }

    public override void _ExitTree() => GetViewport().Msaa3D = _previousMsaa;
}
