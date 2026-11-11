using Godot;

namespace Ashenfall.Art;

/// <summary>Presentation-only attack preview. Never emits gameplay attack events.</summary>
public partial class FallenGuardianAttackDemo : Control
{
    private AnimationPlayer _player;
    public override void _Ready()
    {
        _player = GetNode<AnimationPlayer>("Stage/Guardian/AnimationPlayer");
        GetNode<TextureRect>("Portrait").Texture = new AtlasTexture
        {
            Atlas = GetNode<SubViewport>("Stage").GetTexture(), Region = new Rect2(832, 832, 384, 384), FilterClip = true
        };
        foreach (string state in new[] { "Idle", "Walk", "Attack" })
        {
            string choice = state;
            GetNode<Button>("Controls/" + choice).Pressed += () => PlayState(choice);
        }
        _player.AnimationFinished += name => { if (name == "Combat/Attack") PlayState("Idle"); };
        PlayState("Idle");
    }
    public void PlayState(string state)
    {
        if (state != "Idle" && state != "Walk" && state != "Attack") return;
        if (state == "Attack" && _player.CurrentAnimation == "Combat/Attack" && _player.IsPlaying())
        {
            GetNode<Button>("Controls/Attack").SetPressedNoSignal(true);
            return;
        }
        _player.Play(state == "Attack" ? "Combat/Attack" : state, 0.12);
        foreach (string button in new[] { "Idle", "Walk", "Attack" })
            GetNode<Button>("Controls/" + button).SetPressedNoSignal(button == state);
    }
}
