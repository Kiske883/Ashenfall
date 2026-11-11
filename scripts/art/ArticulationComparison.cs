using Godot;

namespace Ashenfall.Art;

/// <summary>Comparison-only reversible demonstrations. No gameplay animation clips.</summary>
public partial class ArticulationComparison : EnemyArtPreview
{
    private Node3D _selected;
    private Transform3D _rest;
    private Vector3 _axis;
    private float _degrees;
    private float _angle = -135;
    private float _phase;
    private bool _automatic;
    private CheckButton _repeat;
    private Label _description;

    public override void _Ready()
    {
        base._Ready();
        var box = new VBoxContainer { Position = new Vector2(36, 150) };
        GetNode("Presentation").AddChild(box);
        var row = new HBoxContainer();
        box.AddChild(row);
        void Button(string text, System.Action action)
        {
            var button = new Button { Text = text };
            button.Pressed += action;
            row.AddChild(button);
        }
        Button("Pelvis left", () => ShowDemo("PelvisLeft"));
        Button("Pelvis right", () => ShowDemo("PelvisRight"));
        Button("Shield arm", () => ShowDemo("Shield"));
        Button("Sword arm", () => ShowDemo("Sword"));
        Button("Head left", () => ShowDemo("HeadLeft"));
        Button("Head right", () => ShowDemo("HeadRight"));
        Button("Reset pose", ResetPose);
        row = new HBoxContainer();
        box.AddChild(row);
        Button("Prototype view -45", () => Turn(-45));
        Button("Prototype view +45", () => Turn(45));
        Button("Restore front view", () => Turn(-135 - _angle));
        _repeat = new CheckButton { Text = "Slow repeat (selected joint)" };
        _repeat.Toggled += SetAutomatic;
        row.AddChild(_repeat);
        _description = new Label { Text = "Choose a joint above. Reset restores rest pose. Only the prototype moves." };
        box.AddChild(_description);
    }

    // Reference model deliberately remains stationary, including during turntable inspection.
    public void Turn(float degrees)
    {
        _angle += degrees;
        GetNode<Node3D>("Articulated").RotationDegrees = new Vector3(0, _angle, 0);
    }

    private void RestoreJoint()
    {
        if (GodotObject.IsInstanceValid(_selected)) _selected.Transform = _rest;
        _selected = null;
    }

    public void ResetPose()
    {
        RestoreJoint();
        SetAutomatic(false);
        _repeat?.SetPressedNoSignal(false);
        _angle = -135;
        GetNode<Node3D>("Articulated").RotationDegrees = new Vector3(0, _angle, 0);
        if (_description != null) _description.Text = "Rest pose restored. Original remains stationary.";
    }

    public void ShowDemo(string demo)
    {
        RestoreJoint();
        string pivot;
        switch (demo)
        {
            case "PelvisLeft": pivot = "PelvisPivot"; _axis = Vector3.Up; _degrees = 25; break;
            case "PelvisRight": pivot = "PelvisPivot"; _axis = Vector3.Up; _degrees = -25; break;
            case "Shield": pivot = "LeftArmPivot"; _axis = Vector3.Back; _degrees = -18; break;
            case "Sword": pivot = "RightArmPivot"; _axis = Vector3.Back; _degrees = 18; break;
            case "HeadRight": pivot = "HeadPivot"; _axis = Vector3.Up; _degrees = -30; break;
            default: pivot = "HeadPivot"; _axis = Vector3.Up; _degrees = 30; break;
        }
        _selected = GetNode("Articulated/PoseRoot").FindChild(pivot, true, false) as Node3D;
        if (_selected == null) return;
        _rest = _selected.Transform;
        _phase = 0;
        SetPreviewAmount(_automatic ? 0 : 1);
        _description.Text = $"{demo}: {Mathf.Abs(_degrees)} deg about local {(_axis == Vector3.Up ? "Y" : "Z")}. Repeat: rest -> pose -> rest (6 s).";
    }

    // Retained for the existing foundation validation entry point.
    public void InspectPivot() => ShowDemo("HeadLeft");

    public void SetPreviewAmount(float amount)
    {
        if (!GodotObject.IsInstanceValid(_selected)) return;
        _selected.Transform = _rest;
        _selected.RotateObjectLocal(_axis, Mathf.DegToRad(_degrees) * Mathf.Clamp(amount, 0, 1));
    }

    public void SetAutomatic(bool enabled)
    {
        _automatic = enabled;
        _phase = 0;
    }

    public override void _Process(double delta)
    {
        if (!_automatic || _selected == null) return;
        _phase = Mathf.PosMod(_phase + (float)delta, 6f);
        SetPreviewAmount(0.5f - 0.5f * Mathf.Cos(_phase * Mathf.Tau / 6f));
    }
}