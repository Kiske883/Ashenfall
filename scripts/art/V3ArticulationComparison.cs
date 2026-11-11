using Godot;

namespace Ashenfall.Art;

/// <summary>Slow reversible joint inspection only; no clips or gameplay.</summary>
public partial class V3ArticulationComparison : EnemyArtPreview
{
    private OptionButton _choice;
    private CheckButton _repeat;
    private Label _status;
    private Node3D _joint;
    private Transform3D _rest;
    private bool _automatic;
    private float _phase, _amplitude;
    private Vector3 _axis;
    private Transform3D _cameraRest;
    public override void _Ready()
    {
        base._Ready();
        _cameraRest = GetNode<Camera3D>("Camera3D").Transform;
        var box = new VBoxContainer { Position = new Vector2(36, 150) };
        GetNode("Presentation").AddChild(box);
        var row = new HBoxContainer(); box.AddChild(row);
        _choice = new OptionButton();
        foreach (string joint in new[] { "Elbow", "Knee", "Wrist", "Ankle", "Shoulder", "Hip" })
        foreach (string side in new[] { "Left", "Right" }) _choice.AddItem(side + joint + "Pivot");
        row.AddChild(_choice);
        void Button(string text, System.Action action)
        {
            var button = new Button { Text = text }; button.Pressed += action; row.AddChild(button);
        }
        Button("Show joint", () => ShowJoint(_choice.GetItemText(_choice.Selected)));
        Button("Reset pose", ResetPose);
        _repeat = new CheckButton { Text = "Slow repeat (6 s)" };
        _repeat.Toggled += value => { _automatic = value; _phase = 0; };
        row.AddChild(_repeat);
        Button("Camera -15", () => Orbit(-15));
        Button("Camera +15", () => Orbit(15));
        Button("Camera reset", () => GetNode<Camera3D>("Camera3D").Transform = _cameraRest);
        _status = new Label { Text = "Select an independent joint. Show: static pose. Repeat: rest -> pose -> rest. Reset restores geometry." };
        box.AddChild(_status);
    }
    public void ResetPose()
    {
        if (GodotObject.IsInstanceValid(_joint)) _joint.Transform = _rest;
        _joint = null; _automatic = false;
        _repeat?.SetPressedNoSignal(false);
        if (_status != null) _status.Text = "Rest pose restored. Select a joint, Show joint, optionally Slow repeat. Camera controls orbit both models.";
    }
    public void ShowJoint(string name)
    {
        bool repeat = _automatic;
        ResetPose();
        _automatic = repeat; _repeat.SetPressedNoSignal(repeat);
        _joint = GetNode("Articulated/PoseRoot").FindChild(name, true, false) as Node3D;
        if (_joint == null) return;
        for (int i = 0; i < _choice.ItemCount; i++)
            if (_choice.GetItemText(i) == name) _choice.Select(i);
        _rest = _joint.Transform; _phase = 0;
        _axis = Vector3.Right;
        _amplitude = name.Contains("Elbow") ? 35 : name.Contains("Knee") ? -25 : name.Contains("Ankle") ? 12 : 15;
        if (name.Contains("Shoulder")) { _axis = Vector3.Back; _amplitude = name.StartsWith("Left") ? -4 : 4; }
        if (name.Contains("Hip")) _amplitude = 8;
        SetJointAmount(_automatic ? 0 : 1);
        _status.Text = name + " / " + _amplitude + " deg / rigid joint: inspect gaps, equipment and reset. No clips.";
    }
    public void SetJointAmount(float amount)
    {
        if (!GodotObject.IsInstanceValid(_joint)) return;
        _joint.Transform = _rest;
        _joint.RotateObjectLocal(_axis, Mathf.DegToRad(_amplitude) * Mathf.Clamp(amount, 0, 1));
    }
    public void Orbit(float degrees)
    {
        var camera = GetNode<Camera3D>("Camera3D");
        var focus = new Vector3(0, 1.05f, 0);
        camera.Position = focus + (camera.Position - focus).Rotated(Vector3.Up, Mathf.DegToRad(degrees));
        camera.LookAt(focus);
    }
    public override void _Process(double delta)
    {
        if (!_automatic || _joint == null) return;
        _phase = Mathf.PosMod(_phase + (float)delta, 6);
        SetJointAmount(0.5f - 0.5f * Mathf.Cos(_phase * Mathf.Tau / 6));
    }
}
