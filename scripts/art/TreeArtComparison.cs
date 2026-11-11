using Godot;

namespace Ashenfall.Art;

/// <summary>Local presentation quality, restored if the scene exits into another scene.</summary>
public partial class TreeArtComparison : Node3D
{
    private Viewport.Msaa _previousMsaa;

    public override void _Ready()
    {
        _previousMsaa = GetViewport().Msaa3D;
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
    }

    public override void _ExitTree() => GetViewport().Msaa3D = _previousMsaa;
}
