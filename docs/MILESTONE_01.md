# Milestone 01 - First playable 3D environment

Implemented and validated on 2026-11-10. This is an environment prototype with no player or gameplay controls.

## Files

Modified: `project.godot` (main scene and 1280 x 800 viewport).

Created:
- `scenes/world/World.tscn`
- `scenes/environment/AshPine.tscn`
- `scenes/environment/Rock.tscn`
- `scenes/environment/AncientGate.tscn`
- `scripts/environment/EnvironmentGenerator.cs`
- `scripts/world/IsometricCamera.cs`
- `resources/AshenAtmosphere.tres`
- `docs/MILESTONE_01.md`
- `docs/capture_preview.gd` (render validation utility only, not game logic)
- `docs/milestone_01_preview.png` (rendered and inspected)
- `agents/PROJECT_RULES.md`

Godot also generates `.uid` sidecars for the scripts, a `.png.import` sidecar for the preview, and ignored `.godot/` caches. `assets/` is reserved for future assets and currently empty. Existing solution, project, icon, and configuration files were preserved apart from the explicit project settings above.

## Architecture

`World` composes independent environment generation, a WorldEnvironment resource, shadow-casting moonlight, a fixed CameraRig, two future spawn markers, and a small title overlay. The generator builds a 40 x 40 ground slab with a StaticBody3D and BoxShape3D. The ground surface is y=0. Primitive mesh scenes provide reusable trees, rocks, and ancient stone gates with warm lights. All materials are native Godot resources; no external assets or dependencies are needed.

The camera uses orthographic projection, 45-degree downward inclination and 45-degree azimuth. Select CameraRig in the Inspector to change Inclination, Azimuth, Distance, ZoomSize, and Target. Orthographic ZoomSize controls framing; Distance controls position and clipping, not apparent perspective size. Camera settings apply at startup. The camera has no mouse movement or tracking behavior.

`FutureActors` contains only two Marker3D nodes. No player, networking, or singleton dependencies are implemented.

## Procedural generation

Select EnvironmentGenerator in World to configure Seed (731), TreeCount (10), RockCount (16), TerrainSize (40), and the three PackedScene references. Parameters apply at startup. A local Godot RandomNumberGenerator with a fixed seed controls path jitter, placement, rotation, and scale. The same configuration and engine version produce the same arrangement; changing counts can also change later random placements.

Trees and rocks alternate between side bands outside the central road and gate courtyard. Foreground trees are reduced in size to preserve visibility. Placement remains inside the terrain but does not perform overlap rejection. The road is built from three columns of slightly irregular stone slabs; its length follows TerrainSize. Two reusable gates frame the road near its far end. Generation is performed once in _Ready, so run the scene to see generated geometry; it is not an editor tool.

## Launch and manual checks

1. Open Godot 4.7.2 Stable .NET and import/open `C:\Propio\Proyectos\Ashenfall\project.godot`.
2. Build the C# solution if prompted, then press F6 with World.tscn open or F5 to run the configured main scene.
3. Confirm the complete terrain, paved road, pine trees, rocks, and two glowing stone gates are visible from a fixed isometric angle. There are no movement controls.
4. Stop, change Seed or a count on EnvironmentGenerator, and run again. Repeating the same configuration should reproduce the arrangement.
5. Adjust CameraRig values and rerun to check framing. Ground collision can be inspected under the Remote scene tree during execution.

PowerShell from the project directory:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path .
```

For a renderer known to work on the current GPU, append `--rendering-driver vulkan`. Forward+ remains enabled.

## Validation results

- Inspected existing files before edits; no existing scenes or scripts were present.
- `git status --short`: unavailable because this directory is not a Git repository. No repository was created and no Git mutations were performed.
- Engine executable: `4.7.2.stable.mono.official.ed1daf0bf`; project SDK `Godot.NET.Sdk/4.7.2`, target net8.0, installed .NET SDK `8.0.428`.
- `dotnet build`: passed, zero warnings and zero errors.
- Headless import/editor: passed with `--headless --path . --editor --import --quit-after 30`, exit 0 and no errors on the final run. An initial unbounded import stalled; only the processes launched for that check were stopped before the successful bounded retry.
- Scene startup: `--headless --path . --quit-after 5`, exit 0, no errors; logged seed 731, 10 trees, 16 rocks and terrain size 40.
- Actual windowed Forward+ rendering and screenshot capture: passed, exit 0. `docs/milestone_01_preview.png` was opened and visually inspected; terrain framing, distinct props, road and gates are visible.
- Current GTX 650 rejected the configured Direct3D 12 driver (Typed UAV capability); Godot automatically fell back to Vulkan 1.2.142 and rendered successfully. The existing project driver setting was preserved. Use the Vulkan launch flag if needed; no system changes are required.
- No automated player collision or gameplay tests were performed because there is no player in this milestone.

To regenerate the inspected screenshot:

```powershell
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan --script res://docs/capture_preview.gd
```

## Limitations and next milestone

Flat primitive terrain and stylized placeholder meshes; no textures, navigation, terrain height variation, prop collision, boundary walls, LOD, camera occlusion handling, or overlap prevention. Only the ground has physics collision. Very large count or camera changes may reduce clarity. There is no runtime regeneration, interaction, character, combat, inventory, multiplayer, or backend.

Suggested next milestone: introduce one local player with click-to-move or directional movement, verify ground and prop collision, and tune the fixed camera against movement. Keep player logic independent of environment generation; defer networking until local movement is stable.
