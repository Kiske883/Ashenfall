using Ashenfall.Characters;
using Godot;

namespace Ashenfall.Art;

/// <summary>Held impact alternatives for inspecting blade contact; no attack playback.</summary>
public partial class DwarfAxeBladePoseDemo : Node3D
{
    private DwarfAxeBladePosePresentation _dwarf;
    private Camera3D _camera;
    private Label _status;

    public override void _Ready()
    {
        _dwarf = GetNode<DwarfAxeBladePosePresentation>("Dwarf");
        _camera = GetNode<Camera3D>("Camera3D");
        _dwarf.SetProcess(false);
        AddCuttingEdgeIndicator();

        var canvas = new CanvasLayer(); AddChild(canvas);
        var title = new Label
        {
            Text = "ASHENFALL / AXE IMPACT COMPARISON\nHeld poses only - green arrow points along cutting edge",
            Position = new Vector2(26, 20)
        };
        title.AddThemeFontSizeOverride("font_size", 22); canvas.AddChild(title);
        var poses = new HBoxContainer { Position = new Vector2(26, 93) }; canvas.AddChild(poses);
        foreach (string option in new[] { "GUARD", "WINDUP", "IMPACT A", "IMPACT B", "GUARD B" })
        {
            var button = new Button { Text = option, CustomMinimumSize = new Vector2(125, 38) };
            string selected = option;
            button.Pressed += () => ShowOption(selected);
            poses.AddChild(button);
        }
        var views = new HBoxContainer { Position = new Vector2(26, 145) }; canvas.AddChild(views);
        foreach (string view in new[] { "Front", "Side", "Isometric" })
        {
            var button = new Button { Text = view, CustomMinimumSize = new Vector2(100, 34) };
            string selected = view; button.Pressed += () => SetView(selected); views.AddChild(button);
        }
        _status = new Label { Position = new Vector2(26, 730) }; canvas.AddChild(_status);
        SetView("Isometric");
        ShowOption("GUARD");
    }

    // Retained for the previous focused blade-orientation comparison test.
    public void ShowPose(string name, double time)
    {
        _dwarf.SampleAttack(time);
        _dwarf.SetBladeRollDegrees(90);
        _status.Text = $"{name} ({time:0.00} s)  |  Green arrow = cutting edge toward target  |  Static pose preview";
    }

    public void ShowOption(string option)
    {
        switch (option)
        {
            case "GUARD": ShowPose("GUARD", 0); return;
            case "WINDUP": ShowPose("WINDUP", 0.70); return;
            case "IMPACT A":
                // Start with the approved neutral grip and blade plane. Move
                // the whole held axe lower/forward as the planted body crouches.
                _dwarf.SampleAttack(0);
                _dwarf.BodyOffset = new Vector3(0, -0.07f, -0.008f);
                _dwarf.TorsoRotation = new Vector3(-0.07f, 0, 0.01f);
                _dwarf.HeadRotation = new Vector3(0.025f, 0, 0);
                _dwarf.BeardRotation = new Vector3(0.015f, 0, 0);
                _dwarf.WeaponOffset = new Vector3(0.01f, -0.03f, -0.035f);
                _dwarf.WeaponRotation = Vector3.Zero;
                _dwarf.ForearmTwistWeight = 0;
                _dwarf.ApplyPose();
                _dwarf.SetBladeRollDegrees(90);
                _status.Text = "IMPACT A - lowered guard, forward reach, planted crouch | Static";
                return;
            case "IMPACT B":
                // The user's literal guard-as-impact proposal.
                ShowPose("IMPACT B / original guard", 0);
                return;
            case "GUARD B":
                // The previous impact stance becomes the alternate guard.
                _dwarf.SampleAttack(0.96);
                // A small static handle-axis roll removes broadside exposure
                // caused by the old impact stance's torso/weapon inclination.
                _dwarf.SetBladeRollDegrees(109.5f);
                _status.Text = "GUARD B - previous impact stance, blade plane corrected | Static";
                return;
        }
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

    private void AddCuttingEdgeIndicator()
    {
        var axe = (Node3D)_dwarf.GetNode<Node3D>("Model").FindChild("BattleAxe", true, false);
        var geometry = axe.GetNode<MeshInstance3D>("Geometry");
        var butt = new Vector3(-0.46f, 0.45f, -0.43f);
        var end = new Vector3(0.46f, 1.50f, -0.43f);
        var axis = (end - butt).Normalized();
        var cutting = new Vector3(axis.Y, -axis.X, 0);
        var socket = butt.Lerp(end, 0.87f);
        var tip = socket + cutting * 0.46f + axis * 0.04f - butt;
        var arrow = new Node3D { Name = "TemporaryCuttingEdgeIndicator", Position = tip };
        geometry.AddChild(arrow);
        var color = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.30f, 1.0f, 0.15f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        var basis = new Basis(new Quaternion(Vector3.Up, cutting));
        var stem = new MeshInstance3D
        {
            Name = "Stem", Mesh = new CylinderMesh { TopRadius = 0.010f, BottomRadius = 0.010f,
                Height = 0.22f, Material = color },
            Transform = new Transform3D(basis, cutting * 0.11f)
        };
        arrow.AddChild(stem);
        var head = new MeshInstance3D
        {
            Name = "Head", Mesh = new CylinderMesh { TopRadius = 0,
                BottomRadius = 0.04f, Height = 0.08f, Material = color },
            Transform = new Transform3D(basis, cutting * 0.26f)
        };
        arrow.AddChild(head);
    }
}
