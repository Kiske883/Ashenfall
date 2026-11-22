using Ashenfall.Characters;
using Godot;

namespace Ashenfall.Art;

/// <summary>Isolated visual comparison; it never changes the playable dwarf.</summary>
public partial class DwarfCombatGuardComparison : Node3D
{
    private DwarfApprovedAxeAttackPresentation _candidate, _previous, _active;
    private Camera3D _camera;
    private Label _status;
    private bool _slow;
    private string _mode = "COMBAT IDLE";

    public override void _Ready()
    {
        _previous = GetNode<DwarfApprovedAxeAttackPresentation>("Dwarf");
        _candidate = GetNode<DwarfApprovedAxeAttackPresentation>("DwarfCandidate");
        _camera = GetNode<Camera3D>("Camera3D");
        _previous.Visible = false;
        _active = _candidate;

        var canvas = new CanvasLayer(); AddChild(canvas);
        var title = new Label { Text = "ASHENFALL / COMBAT GUARD + IMPACT COMPARISON\nWalk in GUARD B | Attack A vs lower IMPACT C",
            Position = new Vector2(26, 20) };
        title.AddThemeFontSizeOverride("font_size", 22); canvas.AddChild(title);
        var actions = new HBoxContainer { Position = new Vector2(26, 93) }; canvas.AddChild(actions);
        AddButton(actions, "COMBAT IDLE", () =>
        {
            if (_candidate.IsAttacking) return;
            Activate(_candidate);
            Player(_candidate).Play("Combat/GuardB", .18);
            Player(_candidate).Advance(0);
            SetMode("COMBAT IDLE");
        });
        AddButton(actions, "COMBAT WALK", () =>
        {
            if (_candidate.IsAttacking) return;
            Activate(_candidate);
            var player = Player(_candidate);
            player.Play("Combat/CombatWalk", .18);
            // Enter near planted support instead of the clip's maximum split stride.
            player.Seek(.165, false);
            player.Advance(0);
            SetMode("COMBAT WALK / 2.5 m/s reference"); ApplySpeed();
        });
        AddButton(actions, "ATTACK A", () => Attack(_previous, "ATTACK A / IMPACT A"));
        AddButton(actions, "ATTACK C", () => Attack(_candidate, "ATTACK C / IMPACT C"));
        AddButton(actions, "IMPACT A", () => HoldImpact(_previous, "IMPACT A / held"), 120);
        AddButton(actions, "IMPACT C", () => HoldImpact(_candidate, "IMPACT C / held"), 120);
        var views = new HBoxContainer { Position = new Vector2(26, 145) }; canvas.AddChild(views);
        foreach (var view in new[] { "Front", "Side", "Isometric" })
        {
            var selected = view; AddButton(views, view, () => SetView(selected), 110);
        }
        AddButton(views, "Slow 0.35x", () => { _slow = !_slow; ApplySpeed(); }, 130);
        AddButton(views, "Inspection", () => _camera.Size = 4, 120);
        AddButton(views, "Gameplay / 22", () => _camera.Size = 22, 150);
        _status = new Label { Position = new Vector2(26, 730) }; canvas.AddChild(_status);
        SetView("Isometric");
        _candidate.ShowGuardB();
        ApplySpeed();
    }

    private static AnimationPlayer Player(DwarfApprovedAxeAttackPresentation dwarf) => dwarf.GetNode<AnimationPlayer>("AnimationPlayer");

    private static void AddButton(HBoxContainer row, string text, System.Action action, float width = 150)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, 38) };
        button.Pressed += action; row.AddChild(button);
    }

    private void Activate(DwarfApprovedAxeAttackPresentation actor)
    {
        _active = actor;
        _previous.Visible = actor == _previous;
        _candidate.Visible = actor == _candidate;
    }

    private void Attack(DwarfApprovedAxeAttackPresentation actor, string label)
    {
        Activate(actor);
        actor.PlayApprovedAttack();
        SetMode(label);
        ApplySpeed();
    }

    private void HoldImpact(DwarfApprovedAxeAttackPresentation actor, string label)
    {
        if (actor.IsAttacking) return;
        Activate(actor);
        var player = Player(actor);
        player.Play("Combat/AxeAttack", 0);
        player.Seek(.94, true);
        player.Stop(true);
        actor.ApplyPose();
        actor.SetBladeRollDegrees(actor.BladeRollDegrees);
        SetMode(label);
    }

    private void SetMode(string mode) { _mode = mode; ApplySpeed(); }

    private void ApplySpeed()
    {
        if (_active == null) return;
        // Original 0.6 s stride was calibrated at 1.51515 m/s. At the
        // approved world speed of 2.5 m/s it plays at about 1.65x.
        var factor = _mode.StartsWith("COMBAT WALK") ? 2.5f / 1.51515f : 1f;
        Player(_active).SpeedScale = factor * (_slow ? .35f : 1f);
    }

    public override void _Process(double delta)
    {
        if (_status != null)
            _status.Text = $"{_mode}  |  {Player(_active).CurrentAnimationPosition:0.00} s  |  {(_slow ? "SLOW" : "NORMAL")}  |  visual only";
    }

    public void SetView(string view)
    {
        var direction = view switch
        {
            "Front" => new Vector3(0, .12f, -1),
            "Side" => new Vector3(1, .12f, 0),
            _ => new Vector3(1, 1.414214f, 1)
        };
        var focus = new Vector3(0, 1, 0);
        _camera.Position = focus + direction.Normalized() * 8;
        _camera.LookAt(focus);
        var angle = view == "Isometric" ? Mathf.DegToRad(-135) : 0;
        _previous.Rotation = new Vector3(0, angle, 0);
        _candidate.Rotation = new Vector3(0, angle, 0);
    }
}
