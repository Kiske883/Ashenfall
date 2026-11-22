# Milestone 06C - Definitive playable Dwarf Berserker

> Historical 06C configuration: Milestone 06D now adds Guard B combat locomotion and the visual Space axe attack to this same scene. See `docs/MILESTONE_06D_PLAYABLE_COMBAT_INTEGRATION.md` for the current playable configuration; the 06C locomotion experiments remain available.

The user manually approved the grounded 2.5 m/s locomotion from 06C.1. This finalization promotes that exact configuration to **`res://scenes/world/AshenfallDwarfPlayableDemo.tscn`**. Consolidation is ready for final review; nothing is staged or committed.

## Launch

1. Open the existing project in Godot 4.7.2 Mono and build the C# solution.
2. Open `scenes/world/AshenfallDwarfPlayableDemo.tscn` and press **F6**.
3. Use WASD or left-click the ground. Holding left mouse updates destinations; release retains the last destination. WASD cancels mouse navigation.
4. Approach the Guardian on the road, observe pursuit and attacks, retreat past the starting area, then repeat without restarting. There is no damage or health.

**F5 remains `res://scenes/world/World.tscn`.** The original Milestone 05C `AshenfallPlayableDemo.tscn`, player prefab, camera, world, navigation and Guardian are unchanged.

## Definitive configuration

| Setting | Final value |
| --- | --- |
| Actor | Existing PlayerController / CharacterBody3D |
| MovementSpeedOverride | **2.5 m/s**, scoped to the dwarf scene |
| PresentationScene | `res://scenes/characters/DwarfBerserkerGroundedExperiment.tscn` |
| AnimationLibrary | `res://resources/animations/DwarfGroundedExperiment.tres` |
| Idle | Unchanged approved Idle curves, 3.2 s, playback 1x |
| Walk | Approved grounded stride, native 0.6 s |
| Support travel / support fraction | 0.50 m / 55% |
| WalkReferenceSpeed | `0.50 / (0.60 * 0.55)` = **1.5151515 m/s** |
| Playback at actual 2.5 m/s | **1.65x**, below the retained 2x limit |
| Actual cycle / cadence at full movement | 0.363636 s / 5.5 footfalls per second |
| Rate smoothing | 0, matching the actor's immediate horizontal speed changes |
| Pose blending | Approved 0.12 s Idle/Walk transition |
| Camera | Existing follow target/orientation, orthographic size 22 |
| Guardian | Chase 1.0, Return 0.7 m/s; windup 0.6, recovery 1.0 s |

The approved resources retain their existing `Experiment` filenames to preserve paths and references. Their animation curves, model, mesh attributes, materials, shaders, beard and axe were **not changed or duplicated** during finalization. The original 06B `DwarfBerserkerLocomotion.tres` and `DwarfBerserkerAnimated.tscn` remain intact.

## Shared architecture

```text
AshenfallDwarfPlayableDemo - inherits original AshenfallPlayableDemo
  World - unchanged environment, navigation, camera, original player actor
    FutureActors/Player
      Visual
        original placeholder meshes - hidden only in this instance
        Dwarf - approved grounded presentation at unit scale
  FallenGuardian - unchanged scene, AI and animation adapter
  DwarfPresentation - shared DwarfPlayerPresentation
```

`DwarfPlayerPresentation` now accepts an optional exported `MovementSpeedOverride`. Zero, its default, leaves the actor's original speed unchanged. The definitive scene selects 2.5; the original player prefab still defaults to 5. Configuration is applied once during binding, rather than adding another movement implementation or changing PlayerController.

The retained experiment subclass delegates its exported speed to that same property. It no longer performs a separate actor-speed assignment. Movement, collision, gravity, path following and input remain entirely in the existing PlayerController.

After player physics, the shared adapter measures actual XZ displacement / physics delta, chooses Idle/Walk with its existing hysteresis, and faces the visual toward actual horizontal velocity. Wall-slide orientation follows actual movement; blocked input selects Idle. It advances the approved presentation once per physics tick, retaining both-hand axe constraints and ground compensation. The actor root, collider and navigation agent are not rotated by animation.

```text
requested_rate = actual_horizontal_speed / WalkReferenceSpeed
applied_rate = clamp(requested_rate, minimum_rate, maximum_rate)
```

The final scene uses zero rate-smoothing delay; pose transitions retain the approved blend. Idle plays at 1x and repeated state selection does not restart a clip. `AttackExecuted` remains exclusively AI-owned; no player attack, damage, health or new gameplay signal was introduced.

## Preserved scenes and experiments

- **Definitive:** `res://scenes/world/AshenfallDwarfPlayableDemo.tscn` - approved grounded 2.5 m/s.
- **Original 05C:** `res://scenes/world/AshenfallPlayableDemo.tscn` - unchanged placeholder player.
- **Retained 06C.1 entry point:** `res://scenes/world/AshenfallDwarfLocomotionExperiment.tscn` - still available for exported-speed tuning, currently 2.5.
- **New historical comparison:** `res://scenes/world/AshenfallDwarfFiveMeterComparison.tscn` - retains the earlier 5 m/s dwarf behavior, original 06B Walk, 0.166667 reference, 2x capped playback and 0.1 s rate smoothing.
- **Art preview:** `res://scenes/art/DwarfBerserkerPreview.tscn` - unchanged approved 06B resources and inspection views.

No experimental resource was removed or renamed. Both comparison scenes inherit existing composition and share the same adapter; no animation or movement implementation was copied. Existing `milestone_06c_*.png` screenshots record the historical 5 m/s version; `milestone_06c1_*.png` record calibration and the approved grounded candidate. These images remain unchanged and are not presented as new finalization captures.

## Validation executed for finalization

All headless tests use fixed 60 FPS. Final results:

| Check | Result |
| --- | --- |
| `dotnet build` | Passed, 0 errors / 0 warnings |
| Godot headless editor import | Passed |
| Bounded definitive-scene startup | Passed |
| `validate_dwarf_playable.gd` | Passed, 0 failures on the definitive 2.5 m/s scene |
| Same test with `--legacy-five-meter` | Passed, 0 failures on the preserved 5 m/s comparison |
| `validate_dwarf_calibration.gd --definitive` | Passed, 0 failures; actual grounded movement on the definitive scene |
| Same calibration test without that flag | Passed, 0 failures on the retained experimental entry point |
| `validate_playable_demo.gd` | Passed, 0 failures; original Milestone 05C preserved |
| `validate_dwarf_animation.gd` | Passed, 0 failures; approved 06B art/animations preserved |
| `validate_mouse_navigation.gd` | Passed, 0 failures at 60 Hz |
| `validate_gate_navigation.gd` | Passed, 0 failures |
| `validate_dual_speeds.gd` | Passed, 0 failures |
| `validate_enemy_attack.gd` | Passed, 0 failures |

The definitive integration test verifies all four WASD directions, actual 2.5 speed, Idle/Walk transitions, calibrated playback and a single animation tick, low-speed response, model facing, real boundary collision and wall sliding, held mouse, release persistence, keyboard cancellation, camera tracking/orientation, single AI events and repeated pursuit/attack/retreat/return encounters. Original collision resources/layers/masks and navigation radius/height are retained.

Grounded calibration on the definitive scene measures approximately **0.0000407 m maximum steady support drift** and **0.0000425 m maximum integrated hand-to-axe anchor error**. Geometry/material hashes and unit scale checks pass. SHA-256 comparison against the starting uncommitted work confirms all animation/presentation resources and existing screenshots are unchanged. Finalization updates are limited to the listed configuration, adapter initialization, validation and documentation files.

No new live human gameplay test or visual inspection is claimed during this consolidation. The user approved the same grounded animation/configuration before finalization; final review of this definitive entry point is still pending. No existing screenshot was overwritten.

Example commands from the project directory:

```powershell
dotnet build
& C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --editor --import --quit
& C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --fixed-fps 60 --script docs/validate_dwarf_playable.gd
& C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --fixed-fps 60 --script docs/validate_dwarf_playable.gd -- --legacy-five-meter
& C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --fixed-fps 60 --script docs/validate_dwarf_calibration.gd -- --definitive
```

Optional future integration-test captures use distinct `milestone_06c_final_` / `milestone_06c_legacy_` prefixes. Calibration capture mode retains its historical output names; no capture command was run during finalization.

## Remaining limitations

The approved gait is fast and deliberately crouched: native standing height is 1.460, while the walking crown reaches about 1.363 through knee/pelvis articulation, not model scaling. Rigid limb/armor seams remain visible in close views. Ground compensation assumes a flat floor; there is no general terrain or world-space planted-foot constraint. Starts and sharp turns can still slip, with approximately 0.286 m maximum support-ankle travel in the calibration's combined transient exercise. These are retained limitations of the manually approved candidate, not newly hidden fixes.

The original 1.9-high, 0.32-radius player capsule remains unchanged for collision/navigation consistency. Axe, beard and armor have no separate gameplay collision and can intersect nearby props. No root motion, Run, player attack, health, damage or combat integration was added.

## Exact finalization changes and Git

Modified existing uncommitted files:

- `scenes/world/AshenfallDwarfPlayableDemo.tscn`
- `scripts/characters/DwarfPlayerPresentation.cs`
- `scripts/characters/DwarfLocomotionExperiment.cs`
- `docs/validate_dwarf_playable.gd`
- `docs/validate_dwarf_calibration.gd`
- `docs/MILESTONE_06C_DWARF_PLAYABLE_INTEGRATION.md`
- `docs/MILESTONE_06C_1_DWARF_LOCOMOTION_CALIBRATION.md` - finalization/status and comparison-path note only; measurements retained.

Created:

- `scenes/world/AshenfallDwarfFiveMeterComparison.tscn`

All other pending files, UIDs, resource paths and evidence are preserved. No tracked file from HEAD changed. Branch and HEAD remain `feature/milestone-05-animation` / `0752fd9`. There are 25 untracked files: the 24 pre-existing 06C/06C.1 files plus the comparison scene. Nothing staged or committed; no push, merge, reset, stash, clean or branch switch. Stop for final approval before committing.
