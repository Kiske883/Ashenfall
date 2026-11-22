using Ashenfall.Characters;
using Godot;

namespace Ashenfall.Art;

/// <summary>Visual-only approved-pose axe attack player; no gameplay events.</summary>
public partial class DwarfApprovedAxeAttackDemo : Node3D
{
    private DwarfApprovedAxeAttackPresentation _dwarf;
    private Camera3D _camera;
    private Label _status;

    public override void _Ready()
    {
        _dwarf = GetNode<DwarfApprovedAxeAttackPresentation>("Dwarf");
        _camera = GetNode<Camera3D>("Camera3D");
        var canvas = new CanvasLayer(); AddChild(canvas);
        var title = new Label { Text = "ASHENFALL / APPROVED POSE AXE ATTACK\nGUARD B  >  WINDUP  >  IMPACT A  >  GUARD B",
            Position = new Vector2(26, 20) };
        title.AddThemeFontSizeOverride("font_size", 22); canvas.AddChild(title);
        var states = new HBoxContainer { Position = new Vector2(26, 93) }; canvas.AddChild(states);
        AddButton(states, "GUARD B", () => _dwarf.ShowGuardB());
        AddButton(states, "WALK", () => _dwarf.PlayClip("Walk"));
        AddButton(states, "ATTACK", () => _dwarf.PlayApprovedAttack());
        var views = new HBoxContainer { Position = new Vector2(26, 145) }; canvas.AddChild(views);
        foreach (string view in new[] { "Front", "Side", "Isometric" })
        {
            string selected = view;
            AddButton(views, view, () => SetView(selected), 105);
        }
        AddButton(views, "Inspection", () => _camera.Size = 4, 115);
        AddButton(views, "Gameplay / 22", () => _camera.Size = 22, 155);
        _status = new Label { Position = new Vector2(26, 730) }; canvas.AddChild(_status);
        SetView("Isometric");
    }

    private static void AddButton(HBoxContainer row, string text, System.Action action, float width = 125)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, 38) };
        button.Pressed += action; row.AddChild(button);
    }

    public override void _Process(double delta)
    {
        var player = _dwarf.GetNode<AnimationPlayer>("AnimationPlayer");
        _status.Text = _dwarf.IsAttacking
            ? $"ATTACK  {player.CurrentAnimationPosition:0.00} / 1.70 s  |  one shot"
            : $"{player.CurrentAnimation}  |  preview only, no damage or gameplay events";
    }

    public void SetView(string view)
    {
        var direction = view switch
        {
            "Front" => new Vector3(0, 0.12f, -1),
            "Side" => new Vector3(1, 0.12f, 0),
            _ => new Vector3(1, 1.414214f, 1)
        };
        var focus = new Vector3(0, 1, 0);
        _camera.Position = focus + direction.Normalized() * 8;
        _camera.LookAt(focus);
        _dwarf.Rotation = new Vector3(0, view == "Isometric" ? Mathf.DegToRad(-135) : 0, 0);
    }
}
