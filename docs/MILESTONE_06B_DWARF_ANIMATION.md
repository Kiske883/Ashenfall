# Milestone 06B — Dwarf Berserker articulation, Idle and Walk

Implemented locally on 2026-11-11. Visual-only prototype, awaiting the user's visual approval.

## Preview

Open `res://scenes/art/DwarfBerserkerPreview.tscn` in Godot 4.7.2 Mono and press **F6**.

- **IDLE** / **WALK** select the two looping animations. Starts in Idle; transitions blend over 0.20 s. Selecting the current state does not restart its clock.
- **Front**, **Side**, **Rear**, **Isometric** retain the 06A inspection views. Side is particularly useful for judging knee flexion and both hands holding the shaft.
- **Inspection** selects orthographic size 3.5. **Gameplay / size 22** retains the existing gameplay-scale inspection option.
- Dwarf left, unchanged static Guardian reference right. No player input, movement or gameplay is connected to this scene.

The reusable animated asset is `res://scenes/characters/DwarfBerserkerAnimated.tscn`. The original `DwarfBerserker.tscn` remains the independent static 06A asset. Use F6 for the animated preview; the constraint adapter runs at runtime, not as an editor animation-timeline tool.

## Preservation and joint structure

The original procedural generator, static scene, shader, seven materials, 06A documentation and screenshots are unchanged. The new presentation instances the original packed scene. It does not regenerate different geometry, split triangles, scale limbs, replace materials or redesign the dwarf. Tests compare every mesh's vertices, normals, vertex colors, indices and material references against an independently instantiated 06A model.

The existing articulated hierarchy is reused:

```text
DwarfBerserkerAnimated — visual adapter / animated pose controls
  AnimationPlayer — DwarfBerserkerLocomotion.tres
  Model — original DwarfBerserker.tscn
    PoseRoot — never animated
      PelvisPivot
        HipsAndBelt
        Left/RightHipPivot
          Thigh
          KneePivot / Shin and knee armor
            AnklePivot / Boot
        TorsoPivot
          LeatherCuirass
          HeadPivot
            BaldHead
            Beard — independent secondary motion
          Left/RightShoulderPivot
            UpperArm and shoulder armor
            ElbowPivot / Forearm and bracer
              WristPivot / Gauntlet / GripPoint
          BattleAxe / LeftGrip / RightGrip — separate weapon component
```

The only binding change is runtime reparenting of BattleAxe from PoseRoot to TorsoPivot, preserving its exact resting global transform. Zero pose controls reconstruct all 17 original mesh transforms, including the weapon, within floating-point tolerance. The asymmetric 06A joint locations and segment lengths are captured from the actual generated hierarchy; they are not replaced with a symmetric rig or estimated from appearance.

The two arms use a small analytical two-bone solver. The torso attachment defines the axe transform; the weapon's two existing grip markers define the wrist targets using each hand's original grip-to-wrist offset. Elbows use their original bend direction; wrists follow the weapon basis. Both hands therefore stay coordinated with the same shaft during Idle, Walk and blends, rather than receiving unrelated rotation curves. The solver only rotates existing pivots and preserves segment lengths. Reach is clamped without stretching; the authored clips stay within reach. New weapon poses would require renewed grip validation.

Legs use the same rigid two-bone construction, targeting animated foot positions. Ankle compensation cancels shin rotation and adds a small authored foot pitch. Ground clearance is computed from the actual rotated boot vertices, including its collar, so support soles stay on the flat preview floor. This is visual flat-floor compensation, not a physics or terrain IK system.

## Animation clips

`resources/animations/DwarfBerserkerLocomotion.tres` is a native reusable AnimationLibrary containing ten value tracks per clip: BodyOffset, TorsoRotation, HeadRotation, WeaponOffset, WeaponRotation, BeardRotation, left/right FootOffset and left/right FootPitch. Rotations are radians and positions are Godot units. No scale, gameplay method, signal, collision, navigation or root-motion tracks exist.

The presentation explicitly advances AnimationPlayer in manual callback mode, then applies the constraints in the same `_Process` call. This prevents a one-frame lag between the axe and hands. AnimationPlayer blends the authored controls; the constraints remain active while blending. The adapter has no reference to PlayerController, EnemyController or any AI/gameplay state.

### Idle — 3.20 seconds, looping

Subtle breathing-like vertical motion (0–0.002), lateral weight shift (±0.003), small torso/head rotations, slight weapon movement and restrained beard lag. Arm and shoulder movement comes from solving the coordinated grip targets. Both feet stay planted. There is no chest scaling or exaggerated bouncing. The native curve has 81 samples, including matching endpoints.

### Walk — 1.40 seconds, looping

Short, deliberate steps with opposite legs offset by half a cycle:

- **Support, 60% of each foot's cycle:** level foot, zero sole clearance, Z moves linearly from -0.070 to +0.070 relative to its resting ankle. Both legs share a short double-support interval.
- **Swing, remaining 40%:** a Hermite fore/aft curve returns the foot forward with matching endpoint velocity. Sole lift follows a smooth `0.055 * sin(PI * swing_phase)^2` arch. Its endpoint vertical velocity is zero.
- Foot pitch is approximately ±4.85 degrees in swing, combined with ankle compensation. Knees rotate relative to their thighs; measured knee-rotation range is approximately 53.69 degrees.
- Pelvis remains only 4.8–6.2 mm below rest, with 6 mm lateral weight transfer. Small torso counter-rotation and head stabilization preserve the upper silhouette. Axe movement is restrained, arms remain coordinated, and the separate beard has a small delayed sway.
- 161 native samples, including matching endpoints. Root and PoseRoot stay fixed: this is walking **in place**.

The support travel corresponds to `0.140 / (1.40 * 0.60) = 0.166667` units/s at 1x if integrated later. This is a reference derived from this short prototype stride, not a new gameplay speed. In the stationary preview, backward foot travel during support represents forward locomotion; it is not a claim of world-space foot locking. No existing player or enemy movement speed is changed.

## Height and measured constraints

The original neutral sole-to-bald-crown height remains **1.460**. The Guardian's actual neutral sole-to-cranium standing height, measured in 06A from mesh vertices, is **1.93091047**. Its 75% minimum is **1.44818285**. The source model and its unit scale remain unchanged.

Focused checks sampled 241 points across each complete cycle, plus repeated transitions and translated/rotated instances:

| Measurement | Final observed result |
| --- | ---: |
| Lowest animated crown above floor | **1.45379972** |
| Lowest crown / Guardian standing height | **75.2909%** |
| Maximum sole lift | **0.05499604** |
| Knee rotation range | **53.6861 degrees** |
| Maximum hand-to-shaft-anchor error | **0.00000417** |
| Lowest computed boot vertex | **-0.00000708**, floating-point tolerance |
| Support trajectory error | **0.00000063** |
| Maximum shoulder change from authored rest | **0.9237 degrees** |
| Maximum wrist change from authored rest | **1.2979 degrees** |
| Maximum ankle displacement per 60 Hz step during tested transitions | **0.00864** |

Height and floor-clearance tolerances also pass during transitions. Original bone lengths, mesh attributes, material references and unit scales remain preserved. No mesh indices or materials are rewritten during playback.

## Validation performed

| Validation | Result |
| --- | --- |
| `dotnet build` | Passed, 0 warnings / 0 errors |
| Godot 4.7.2 headless editor import | Passed, exit 0 |
| Direct preview startup, bounded headless run | Passed |
| `docs/validate_dwarf_animation.gd` | Passed, 0 failures: exact neutral reconstruction, unchanged geometry/materials, grip constraints, rigid lengths, no root motion/scaling, height/floor limits, real knee flexion/lift, support trajectory, closed loop seams, repeated blended transitions, orientation independence, preview controls |
| Same focused script with Vulkan Forward+ and `-- --capture` | Passed; seven images captured and inspected |
| `docs/validate_dwarf_art.gd` | Passed, 0 failures; 06A static model and extended preview remain valid |
| `docs/validate_playable_demo.gd` | Passed, 0 failures; player, camera, navigation, repeated encounters, pursuit/attack/return |
| `docs/validate_dual_speeds.gd` | Passed, 0 failures |
| `docs/validate_mouse_navigation.gd` | Passed, 0 failures at 60 Hz |
| `docs/validate_v3_walk_demo.gd` | Passed, 0 failures |
| `docs/validate_v3_attack_animation.gd` | Passed, 0 failures |

The first strict grip test exposed loss of precision in tiny-angle quaternion alignment; a normalized cross/dot construction corrected it. Initial capture tooling also needed cached mesh arrays and explicit image-format conversion; the final captures were regenerated successfully. Final checks above report no errors or warnings. No claim of live human visual approval is made.

Reproduce from the project directory:

```powershell
dotnet build
& C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --editor --import --quit
& C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --script docs/validate_dwarf_animation.gd
& C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe --path . --rendering-driver vulkan --resolution 1280x800 --script docs/validate_dwarf_animation.gd -- --capture
```

## Visual inspection and limitations

Rendered with the existing Vulkan Forward+ renderer on GTX 650, without changing project settings. Inspected an Idle comparison image, a four-phase Idle contact sheet, eight equally spaced Walk phases from front, side, rear and isometric views, and a Walk image at actual orthographic size 22. Contact sheets read left-to-right, then top-to-bottom. Walk frames are at 0, .175, .350, .525, .700, .875, 1.050 and 1.225 seconds; Idle sheet phases are 0, .8, 1.6 and 2.4 seconds.

Knee bending and alternating foot lift are visible, especially in the side view. The bald head, beard volume, armor silhouette and two-handed axe guard remain recognizable. Idle is intentionally subtle. Arms move minimally because both hands carry the heavy weapon, rather than swinging independently.

Rigid geometry does not deform like skin: shin/boot collars, armor transitions and elbow creases remain visible in close rear/side inspection, especially during swing. Some overlapping armor volumes shade sharply. No filler geometry or redesign was added to hide those limitations. The beard is one rigid secondary-motion component, not strand physics or independently simulated braids. The broad beard and axe can partly obscure the upper legs from the isometric view. Small details remain difficult to read at orthographic size 22, as in 06A.

Foot compensation assumes a horizontal floor. There is no terrain adaptation, movement-speed synchronization, collision response or gameplay integration. The clips and constraints are designed for the current restrained poses, not arbitrary extreme authoring targets. Real-time human review of the animation's weight, cadence and close-up rigid joints in F6 is still required. Stop here for approval before attacks or any gameplay integration.

## Exact files and Git status

Modified:

- `scenes/art/DwarfBerserkerPreview.tscn` — instances the new animated wrapper.
- `scripts/art/DwarfBerserkerPreview.cs` — Idle/Walk buttons and updated explanatory text; existing inspection views retained.

Created:

- `scenes/characters/DwarfBerserkerAnimated.tscn`
- `scripts/characters/DwarfBerserkerPresentation.cs`
- `scripts/characters/DwarfBerserkerPresentation.cs.uid`
- `resources/animations/DwarfBerserkerLocomotion.tres`
- `docs/validate_dwarf_animation.gd`
- `docs/validate_dwarf_animation.gd.uid`
- `docs/MILESTONE_06B_DWARF_ANIMATION.md`
- `docs/milestone_06b_idle_isometric.png`
- `docs/milestone_06b_idle_cycle.png`
- `docs/milestone_06b_walk_front.png`
- `docs/milestone_06b_walk_side.png`
- `docs/milestone_06b_walk_rear.png`
- `docs/milestone_06b_walk_isometric.png`
- `docs/milestone_06b_walk_gameplay.png`

Godot additionally creates ignored PNG import sidecars and cache/build files. Initial branch `feature/milestone-05-animation`, HEAD `f0ae082`, working tree clean. Final branch/HEAD unchanged; two modified tracked files and fourteen new untracked files, nothing staged. No commit, push, merge, reset, stash, clean or branch switch. All original dwarf art, Guardian assets/animations, player/AI controllers, camera/navigation/collision systems, main world and playable demo remain unchanged.
