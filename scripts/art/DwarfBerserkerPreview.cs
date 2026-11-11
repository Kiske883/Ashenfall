using Godot;

namespace Ashenfall.Art;

/// <summary>Isolated visual inspection; no player, gameplay, or global input changes.</summary>
public partial class DwarfBerserkerPreview : Node3D
{
    private Camera3D _camera;
    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        var canvas = new CanvasLayer(); AddChild(canvas);
        var title = new Label { Text = "ASHENFALL / DWARF BERSERKER\nVisual prototype — dwarf left / approved Guardian right", Position = new Vector2(26, 20) };
        title.AddThemeFontSizeOverride("font_size", 22); canvas.AddChild(title);
        var row = new HBoxContainer { Position = new Vector2(26, 85) }; canvas.AddChild(row);
        foreach (string view in new[] { "Front", "Side", "Rear", "Isometric" })
        {
            var button = new Button { Text = view, CustomMinimumSize = new Vector2(100, 38) };
            string selected = view; button.Pressed += () => SetView(selected); row.AddChild(button);
        }
        var detail = new Button { Text = "Inspection", CustomMinimumSize = new Vector2(110, 38) };
        detail.Pressed += () => _camera.Size = 3.5f; row.AddChild(detail);
        var gameplay = new Button { Text = "Gameplay / size 22", CustomMinimumSize = new Vector2(180, 38) };
        gameplay.Pressed += () => _camera.Size = 22; row.AddChild(gameplay);
        var note = new Label { Text = "Dwarf: 1.460 m   |   Guardian: 1.931 m (soles to crown)   |   ratio: 75.61%\nStatic art only. Bald head / separate beard and axe / no animations or gameplay.", Position = new Vector2(26, 730) };
        canvas.AddChild(note);
        SetView("Isometric");
    }
    public void SetView(string view)
    {
        var direction = view switch
        {
            "Front" => new Vector3(0, 0.12f, -1),
            "Side" => new Vector3(1, 0.12f, 0),
            "Rear" => new Vector3(0, 0.12f, 1),
            _ => new Vector3(1, 1.414214f, 1)
        };
        var focus = new Vector3(0, 0.85f, 0);
        _camera.Position = focus + direction.Normalized() * 8;
        _camera.LookAt(focus);
        var right = _camera.Basis.X;
        var dwarf = GetNode<Node3D>("Dwarf"); var guardian = GetNode<Node3D>("Guardian");
        dwarf.Position = right * -0.88f;
        guardian.Position = right * 0.88f + Vector3.Down * 0.015f;
        dwarf.Rotation = guardian.Rotation = new Vector3(0, view == "Isometric" ? Mathf.DegToRad(-135) : 0, 0);
    }
}
