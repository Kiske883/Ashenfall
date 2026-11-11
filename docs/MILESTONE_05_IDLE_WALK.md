# Milestone 05A.2 - Idle and Walk visual prototype

Branch `feature/milestone-05-animation`, clean starting HEAD `e4ec9ab`. New isolated presentation and preview only; all existing files, including approved V1/V2 art, articulation prototype, EnemyController, gameplay enemy, player, world, navigation and camera code remain unchanged. No staging, commit, push, merge or branch change.

## Reusable resources

- `res://scenes/enemies/FallenGuardianAnimatedPresentation.tscn` composes the existing articulated presentation as `Presentation` and a sibling `AnimationPlayer`. No clip autoplays; the default is the approved resting pose.
- `res://resources/animations/FallenGuardianLocomotion.tres` is a native AnimationLibrary containing Idle, Walk and RESET. Its track paths resolve from the animated scene root into the generated articulated hierarchy. The existing child generators and adapter finish Ready before the scene is used for playback.
- `res://scenes/art/LocomotionComparison.tscn` displays unchanged original V2 at left and the new presentation at right, sharing lighting, camera and scale.

There is no per-frame procedural pose generator: AnimationPlayer evaluates reusable rotation tracks. Only the preview UI script runs presentation controls. The library must be used with the documented articulated hierarchy; it is not a generic humanoid retargeting resource.

## Clips

Idle loops over 2.8 seconds. Torso rotations peak at 0.6 degrees X / 0.8 Y, with subtle head counter-motion and asymmetrical arm motion of approximately 0.7 and 1 degree. Pelvis and both legs remain exactly at rest, so foot transforms stay stable. It has no vertical bob, scaling, or translation.

Walk loops over 1.2 seconds. Rigid legs alternate +/-9 degrees on local X. Arms swing in opposing phase at 4 and 5 degrees, with only 0.5 degrees of lateral motion. Torso counter-rotation peaks at 1.7 degrees Y; head counter-motion is 1.4 degrees Y, reducing overall head yaw. Equipment follows the existing arm attachments and eyes follow the head. Combined shoulder/chest armor remains on the torso; restrained arm amplitudes avoid the large excursions exposed by the articulation demo.

All seven controlled pivots have rotation tracks in both clips, including explicit zero rotations where appropriate. This prevents residual leg poses when switching Walk to Idle. Cubic interpolated cyclic keys provide matching beginning/end poses. RESET assigns zero pivot rotations; the original head tilt and asymmetric model stance remain on their original nodes. No AnimationTree, skeleton, skinning, IK, root motion, AI connection or attack clip is introduced.

## Preview controls

1. Open project.godot in Godot 4.7.2 .NET and build.
2. Open `res://scenes/art/LocomotionComparison.tscn`, press F6.
3. Idle / Walk select a looping clip. Pause / Resume control playback. Rest pose applies RESET immediately and stops.
4. Slider pauses and samples the selected cycle from 0 to 1. During playback it displays current phase. Half speed / Normal speed control local AnimationPlayer.SpeedScale.
5. View -45 / +45 rotate only the animated prototype. Original V2 stays fixed. Inspection uses size 3.6; Gameplay scale uses size 22. These controls do not change the game camera or InputMap.
6. Inspect forward/back stride extremes with slider positions 0.25 and 0.75, then check the cycle seam at 0/1. Compare feet in Idle and attachments in both clips.

F5 still opens the unchanged playable world; its enemy gameplay scene still uses the unanimated approved V2 visual. The previous ArticulationComparison remains available with its original controls.

## Validation performed

- dotnet build: passed, zero warnings/errors.
- Godot headless editor import: passed.
- `docs/validate_locomotion.gd`: zero failures with both real Vulkan rendering and headless execution. Checks approved rest arrays/transforms, reusable loop resources, valid rotation-only paths, loop seams, 25 samples per clip preserving mesh/material/array references and unit scales, no root motion, stable Idle legs, opposing Walk limb phase, actual playback advancement, reset and fixed reference. Gameplay size 22 also checked.
- Real Vulkan Forward+ frames rendered and saved to `docs/milestone_05_locomotion_idle.png` and `docs/milestone_05_locomotion_walk.png`. Both were opened and visually inspected for pose, silhouette and attachment placement. These are sampled frames, not a claim of manual full-cycle motion approval. Final UI phase-indicator synchronization was checked headlessly after these captures.
- Existing articulation, enemy pursuit, enemy attack and player mouse/navigation suites: zero failures in each. The latter suites also load the original world and verify physics/navigation behavior remains intact.
- No interactive manual full-cycle review, crowd-performance benchmark or gameplay animation integration was performed. User visual approval remains pending.

Reproduce:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_locomotion.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan --fixed-fps 60 --script res://docs/validate_locomotion.gd -- --capture
```

## Limitations and next review

The walk is an in-place rigid-limb approximation, with no knees, elbows, ankle articulation or planted-foot IK. Feet trace arcs and may show floating, sliding or slight floor penetration rather than a production-quality contact gait. It cannot yet match travel speed because there is no movement integration. Armor is still combined; large shoulder motion remains unsupported. Clip selection is direct rather than a production locomotion blend/state machine. No performance claim is made for enemy crowds.

Next step is visual approval of complete cycles at both camera scales, particularly stride readability and foot contact. Any subsequent integration should derive locomotion speed from actual displacement and preserve controller authority over physics, facing and AttackExecuted. No combat or attack-animation work is included here.

## Complete new file inventory

- `resources/animations/FallenGuardianLocomotion.tres`
- `scenes/enemies/FallenGuardianAnimatedPresentation.tscn`
- `scenes/art/LocomotionComparison.tscn`
- `scripts/art/LocomotionComparison.cs` and `.uid`
- `docs/validate_locomotion.gd` and `.uid`
- `docs/MILESTONE_05_IDLE_WALK.md`
- `docs/milestone_05_locomotion_idle.png`
- `docs/milestone_05_locomotion_walk.png`
- Godot-generated `.png.import` sidecars (ignored under existing project rules).

Existing files modified: none. All additions local/untracked; stop for visual review before gameplay integration.
