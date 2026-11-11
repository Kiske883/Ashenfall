using Godot;

namespace Ashenfall.Art;

/// <summary>Isolated visual clip preview; no movement or AI integration.</summary>
public partial class LocomotionComparison : EnemyArtPreview
{
    private AnimationPlayer _player;
    private HSlider _scrub;
    private Label _status;
    private float _angle = -135;
    private string _clip = "Idle";
    public override void _Ready()
    {
        base._Ready();
        _player = GetNode<AnimationPlayer>("Articulated/AnimationPlayer");
        var box = new VBoxContainer { Position = new Vector2(36, 150) };
        GetNode("Presentation").AddChild(box);
        var row = new HBoxContainer();
        box.AddChild(row);
        void Button(string label, System.Action action)
        {
            var button = new Button { Text = label };
            button.Pressed += action;
            row.AddChild(button);
        }
        Button("Idle", () => PlayClip("Idle"));
        Button("Walk", () => PlayClip("Walk"));
        Button("Pause", () => _player.Pause());
        Button("Resume", () => _player.Play(_clip));
        Button("Rest pose", ResetPose);
        Button("Half speed", () => _player.SpeedScale = 0.5f);
        Button("Normal speed", () => _player.SpeedScale = 1);
        Button("View -45", () => Turn(-45));
        Button("View +45", () => Turn(45));
        _scrub = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.001, CustomMinimumSize = new Vector2(700, 20) };
        _scrub.ValueChanged += value => SampleClip(_clip, (float)value);
        box.AddChild(_scrub);
        _status = new Label { Text = "Choose Idle / Walk. Slider pauses and samples one cycle. Original stays still." };
        box.AddChild(_status);
        ResetPose();
    }
    public void PlayClip(string name)
    {
        if (name != "Idle" && name != "Walk") return;
        _clip = name;
        _player.Play(name);
        _status.Text = name + " / looping in place / no root motion. Slider: pause and inspect.";
    }
    public void SampleClip(string name, float phase)
    {
        PlayClip(name);
        _player.Seek(Mathf.Clamp(phase, 0, 1) * (float)_player.GetAnimation(name).Length, true);
        _player.Pause();
        _scrub.SetValueNoSignal(Mathf.Clamp(phase, 0, 1));
    }
    public void ResetPose()
    {
        _player.Play("RESET");
        _player.Advance(0);
        _player.Stop(true);
        _status.Text = "Rest pose / original geometry. Idle 2.8 s / Walk 1.2 s. Slider pauses the selected clip.";
    }
    public void Turn(float degrees)
    {
        _angle += degrees;
        GetNode<Node3D>("Articulated").RotationDegrees = new Vector3(0, _angle, 0);
    }
    public override void _Process(double delta)
    {
        if (_player != null && _player.IsPlaying() && _player.CurrentAnimation != "RESET")
            _scrub.SetValueNoSignal(_player.CurrentAnimationPosition / _player.CurrentAnimationLength);
    }
}
