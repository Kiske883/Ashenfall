using Godot;
using Ashenfall.Characters;

namespace Ashenfall.Art;

public partial class DwarfAxeAttackDemo : Node3D
{
    private DwarfAxeAttackPresentation _dwarf;
    private Camera3D _camera;
    private Label _status;
    public override void _Ready()
    {
        _dwarf = GetNode<DwarfAxeAttackPresentation>("Dwarf"); _camera = GetNode<Camera3D>("Camera3D");
        var canvas = new CanvasLayer(); AddChild(canvas);
        var title = new Label { Text = "ASHENFALL / TWO-HANDED AXE\n06D - isolated visual attack prototype", Position = new Vector2(26, 20) };
        title.AddThemeFontSizeOverride("font_size", 23); canvas.AddChild(title);
        var states = new HBoxContainer { Position = new Vector2(26, 90) }; canvas.AddChild(states);
        foreach (string state in new[] { "Idle", "Walk", "Attack" })
        {
            var button = new Button { Text = state.ToUpperInvariant(), CustomMinimumSize = new Vector2(125, 38) };
            string selected = state; button.Pressed += () => _dwarf.PlayClip(selected); states.AddChild(button);
        }
        var views = new HBoxContainer { Position = new Vector2(26, 140) }; canvas.AddChild(views);
        foreach (string view in new[] { "Front", "Side", "Isometric" })
        {
            var button = new Button { Text = view, CustomMinimumSize = new Vector2(100, 34) };
            string selected = view; button.Pressed += () => SetView(selected); views.AddChild(button);
        }
        var detail = new Button { Text = "Inspection", CustomMinimumSize = new Vector2(110, 34) };
        detail.Pressed += () => _camera.Size = 4.0f; views.AddChild(detail);
        var gameplay = new Button { Text = "Gameplay / 22", CustomMinimumSize = new Vector2(150, 34) };
        gameplay.Pressed += () => _camera.Size = 22; views.AddChild(gameplay);
        _status = new Label { Position = new Vector2(26, 730) }; canvas.AddChild(_status);
        SetView("Isometric");
    }
    public override void _Process(double delta)
    {
        _status.Text = _dwarf.IsAttacking ? "ATTACK - full cycle plays once, then returns to Idle" :
            _dwarf.PreviewClip + " - ATTACK plays once / no damage, events or gameplay integration";
    }
    public void SetView(string view)
    {
        var direction = view switch { "Front" => new Vector3(0, 0.12f, -1), "Side" => new Vector3(1, 0.12f, 0), _ => new Vector3(1, 1.414214f, 1) };
        var focus = new Vector3(0, 1.0f, 0);
        _camera.Position = focus + direction.Normalized() * 8; _camera.LookAt(focus);
        _dwarf.Rotation = new Vector3(0, view == "Isometric" ? Mathf.DegToRad(-135) : 0, 0);
    }
}
