using Godot;

namespace Ashenfall.Art;

/// <summary>Two-button isolated clip demonstration. Magnifies the center of a size-22 camera image.</summary>
public partial class FallenGuardianWalkDemo : Control
{
    public override void _Ready()
    {
        var stage = GetNode<SubViewport>("Stage");
        GetNode<TextureRect>("Portrait").Texture = new AtlasTexture
        {
            Atlas = stage.GetTexture(), Region = new Rect2(832, 832, 384, 384), FilterClip = true
        };
        GetNode<Button>("Controls/Idle").Pressed += () => PlayState("Idle");
        GetNode<Button>("Controls/Walk").Pressed += () => PlayState("Walk");
        PlayState("Walk");
    }
    public void PlayState(string state)
    {
        if (state != "Idle" && state != "Walk") return;
        GetNode<AnimationPlayer>("Stage/Guardian/AnimationPlayer").Play(state, 0.18);
        GetNode<Button>("Controls/Idle").SetPressedNoSignal(state == "Idle");
        GetNode<Button>("Controls/Walk").SetPressedNoSignal(state == "Walk");
    }
}
