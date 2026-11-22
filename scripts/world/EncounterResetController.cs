using Godot;

namespace Ashenfall.World;

/// <summary>Recreates the current encounter scene inside the running game.</summary>
public partial class EncounterResetController : Node
{
    public int RestartRequests { get; private set; }

    public bool Restart()
    {
        var scene = GetTree().CurrentScene;
        if (!GodotObject.IsInstanceValid(scene) || string.IsNullOrEmpty(scene.SceneFilePath)) return false;
        RestartRequests++;
        // Scene replacement releases every actor, timer and signal subscription together.
        GetTree().CallDeferred("change_scene_to_file", scene.SceneFilePath);
        return true;
    }
}
