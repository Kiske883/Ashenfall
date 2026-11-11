# Milestone 06C.1 — Grounded dwarf locomotion calibration

**Finalization note:** The user has manually approved this 2.5 m/s candidate. Its unchanged resources are now used by `res://scenes/world/AshenfallDwarfPlayableDemo.tscn`; final configuration and review status are documented in `MILESTONE_06C_DWARF_PLAYABLE_INTEGRATION.md`. The earlier 5 m/s behavior is retained at `res://scenes/world/AshenfallDwarfFiveMeterComparison.tscn`. The experimental entry point and all resources below remain available. Approval-pending statements, file counts and preservation checks below describe the historical calibration stage, before this consolidation.

Experimental scene: **`res://scenes/world/AshenfallDwarfLocomotionExperiment.tscn`**. Open it and press **F6**. Move with WASD or left-click/hold; approach and retreat from the Guardian as before. Target speed is **2.5 m/s**, only in this opt-in variant.

Historical comparison, now retained at `res://scenes/world/AshenfallDwarfFiveMeterComparison.tscn`: **5.0 m/s**. The approved 06B preview, Idle/Walk library, model, materials and rig are unchanged. F5 remains `World.tscn`.

This is a calibration experiment, **not an approved replacement**. It substantially improves straight-line foot contact, but uses a rapid, bent-knee gait and still slips during starts and sharp turns. Review these tradeoffs before adopting it.

## What was measured

Both clips are in-place loops: their feet return to the starting position, so net foot displacement over a full cycle is **zero**. Their root-motion displacement is also zero. Neither zero is the distance an actor should cover while playing the loop.

To infer compatible forward travel, measure the actual ankle displacement during **ground support**, when the rigid boot has constant orientation and clearance. The full equivalent travel is support displacement divided by the fraction of the cycle spent supporting. Tests also inspect actual boot mesh vertices and a translating actor, rather than trusting track values alone.

```text
reference_speed = support_displacement / (native_cycle_duration * support_fraction)
equivalent_forward_distance_per_cycle = support_displacement / support_fraction
required_playback_rate = actual_horizontal_speed / reference_speed
effective_cycle_duration = native_cycle_duration / playback_rate
footfalls_per_second = 2 / effective_cycle_duration
```

| Measurement | Approved Walk / existing demo | Experimental stride |
| --- | ---: | ---: |
| Native cycle | 1.4 s | 0.6 s |
| Support fraction per foot | 60% | 55% |
| Measured backward travel during support | 0.140000 m | 0.500000 m |
| Equivalent actor travel per full cycle | **0.233333 m** | **0.909091 m** |
| Reference speed at 1x | 0.166667 m/s | 1.515152 m/s |
| Playback needed at 5.0 m/s | 30x | Not the experimental target |
| Playback needed at 2.5 m/s | **15x**, unsuitable | **1.65x** |
| Effective cycle at target 2.5 m/s | Would be 0.093333 s at 15x | **0.363636 s** |
| Footfalls at target 2.5 m/s | Would be 21.4286/s | **5.5/s** |

The existing 5 m/s scene still caps its original Walk at 2x. That produces approximately **1.96 m** of forward support-foot drift per support interval: `5 * (1.4 * .60 / 2) - .14`. Merely reducing that scene to 2.5 with the same clip/cap would still leave **0.91 m** per support interval. This counterfactual is calculated, not offered as a third scene. Speed reduction alone is insufficient.

The experimental support lasts 0.20 seconds at 1.65x. The actor covers 0.50 m in those 0.20 seconds, matching the foot's 0.50 m backward motion. Its nominal straight-line stance drift is therefore zero. Native duration and actual cadence are reported separately; a moderate 1.65x rate does not make the resulting 5.5-footfall cadence slow or necessarily artistically acceptable.

## Independent experimental resources

`DwarfGroundedExperiment.tres` contains a new experimental curve under the existing `Walk` library key, compatible with the unchanged presentation adapter. Its descriptive resource name is `ExperimentalGroundedStride`. Idle keys are copied unchanged from the approved native library; the original library is never edited.

The experimental curve uses:

- 201 samples over 0.6 seconds, with matching loop endpoints.
- Alternating legs, 55% support / 45% swing and 10% total double support; no flight phase.
- Support Z from -0.25 to +0.25, with constant backward velocity and zero sole lift.
- Hermite swing return with matching endpoint horizontal velocity; a smooth 7 cm sole-lift arch with zero endpoint vertical velocity.
- Small ankle roll with the existing boot-geometry ground compensation.
- Restrained torso/head counter-motion, coordinated axe movement and minimal beard sway. Existing two-handed arm constraints are retained.
- No scale tracks, root motion, gameplay events, attack clips, physics/navigation changes or new equipment geometry.

`DwarfBerserkerGroundedExperiment.tscn` inherits the approved animated presentation and replaces only its AnimationLibrary and blend duration (**0.12 s**). All original mesh arrays, normals, colors, indices and material references compare equal. The approved 17 mesh components and 10,336 triangles are retained.

`DwarfLocomotionExperiment.cs` inherits the uncommitted 06C visual adapter. It configures only this scene's existing actor to 2.5, then uses the original adapter's measured-displacement clock, facing and grip behavior. Inspector settings on the experimental scene are:

- `ExperimentalMovementSpeed = 2.5`
- `WalkReferenceSpeed = 0.50 / (0.60 * 0.55) = 1.5151515`
- `MaximumWalkPlaybackRate = 2.0`; the target uses 1.65 and does **not** saturate.
- `RateSmoothingSeconds = 0`: the existing controller starts/stops horizontal motion immediately, so a delayed playback-rate ramp would introduce additional stance drift. Pose transitions still blend over 0.12 seconds.

## Ground alignment and necessary posture change

Measured original thigh and shin lengths are **0.24269322** and **0.20099752**, combined reach **0.44369073 m**. The original hip-to-ankle vertical separation is approximately 0.44 m, leaving little room for a long horizontal reach while standing nearly straight. Simply increasing foot offsets would make the IK clamp unreachable targets and potentially lift or slide the boots.

The experiment therefore bends the knees and lowers the pelvis by **0.095 m**, varying by only ±0.002 m during the loop. This gives adequate horizontal reach for ±0.25 m support targets without stretching or scaling the approved limbs. It is intentional crouched posture, not a displaced world root or a changed model height.

The neutral standing crown remains **1.460 m** and all proportions remain identical. The crouched animated crown reaches approximately **1.362998 m**. This is lower than the 06B animated height envelope and below 75% of the Guardian while crouching; **only neutral standing height retains the approved ratio**. If the previous 75% requirement is intended to constrain every crouched frame as well, this experimental posture must be rejected rather than described as complying with that stronger interpretation. No replacement of the approved scene has occurred.

PoseRoot remains fixed, the actor's original floor-aligned capsule is unchanged, and actual boot-vertex floor checks find only micrometre-scale numerical tolerance around Y=0 in model space. There is no vertical root-motion lift. The torso moves down into the bent-knee gait and returns to the original Idle height; it does not repeatedly bounce by 9.5 cm. Existing shadow bias may still suggest slight separation in distant screenshots; no lighting/camera adjustment was used to hide that.

## Contact results and limitations

| Check | Observed result |
| --- | ---: |
| Moving-body analytical support check, full experimental cycle | <0.000003 m support drift |
| Actual gameplay steady support, four WASD directions | **0.0000407 m maximum** |
| Boot minimum Y in model space | Approximately -0.0000032 m numerical tolerance |
| Maximum sole lift | Approximately 0.0700 m |
| Knee rotation range | Approximately 75.15 degrees |
| Maximum integrated axe grip error | **0.0000425 m** |
| Combined startup / abrupt-turn support-ankle drift exercise | **0.285533 m maximum** |

Straight-line stance is substantially improved. **There is no world-space plant constraint for starts, stopping or sharp direction changes.** Animation blending and interpolated facing still move supporting feet in those cases. The transient measurement is recorded as a limitation, not asserted to be zero by the test. Full naturalism is not achieved merely because the steady calibration passes.

The 5.5-footfall cadence is rapid for a heavy warrior. The deeper knee flexion also exposes the rigid shin/boot collar and armor creases more clearly in close views. The source geometry has not been resculpted to conceal these limitations. Both hands remain on the axe, but rigid cloth/armor can overlap during leg motion. This is a fast crouched locomotion candidate, not a claim that the approved slow Walk supports 2.5 m/s naturally.

Recommendations:

1. Compare both F6 scenes and approve/reject the rapid cadence and lower posture independently of the improved contact numbers.
2. If keeping 2.5, the next focused work should address start/stop steps, planted turns and heel/toe rollover; do not simply increase playback rate again.
3. If the fast cadence is unsuitable, a lower experimental speed is mathematically available through its exported setting. For example, 1.5 would imply approximately 0.99x and 3.3 footfalls/s with this stride. That alternative has not received visual approval or been made the default.
4. Preserve this isolated candidate until a locomotion direction is approved. Do not replace 06B, the original 5 m/s demo or F5 on the basis of automated tests alone.

## Validation performed

- `dotnet build`: passed, **0 errors / 0 warnings**.
- Godot headless editor import and bounded experimental scene startup: passed.
- `docs/validate_dwarf_calibration.gd`: headless fixed 60 FPS, **0 failures**. Measures actual support displacement, equivalent cycle travel, required rates, native loop closure, mesh/material preservation, ground vertices, translating-body support, knee movement, both grips, unit scale and fixed internal root. Exercises WASD, speed/facing, Idle transitions, direction changes, real ground clicks, Guardian pursuit/attacks, retreat/return and repeated encounters. Reports transient contact drift separately.
- Same script with Vulkan Forward+ and `-- --capture`: passed; six rendered images inspected, including actual gameplay-camera comparison and side/isometric cycle contact sheets.
- Existing `validate_dwarf_playable.gd`, `validate_playable_demo.gd`, `validate_dwarf_animation.gd`, `validate_mouse_navigation.gd`, `validate_gate_navigation.gd`, `validate_dual_speeds.gd` and `validate_enemy_attack.gd`: all passed, **0 failures**. The original 5 m/s behavior and approved animation assets remain functional.
- SHA-256 comparison confirms all **ten pre-existing uncommitted 06C files** remain byte-for-byte unchanged. No tracked files changed.

Reproduce from the project directory:

```powershell
dotnet build
& C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --editor --import --quit
& C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --fixed-fps 60 --script docs/validate_dwarf_calibration.gd
& C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe --path . --rendering-driver vulkan --resolution 1280x800 --fixed-fps 60 --script docs/validate_dwarf_calibration.gd -- --capture
```

The cycle contact sheets show eight equally spaced native poses, left-to-right then top-to-bottom. Gameplay images use the unchanged size-22 camera; cycle inspection uses the existing art-preview camera. Screenshots establish rendered appearance, not live human approval of cadence. Human gameplay approval remains pending.

## Exact files created

No existing file modified:

- `scripts/characters/DwarfLocomotionExperiment.cs`
- `scripts/characters/DwarfLocomotionExperiment.cs.uid`
- `scenes/characters/DwarfBerserkerGroundedExperiment.tscn`
- `scenes/world/AshenfallDwarfLocomotionExperiment.tscn`
- `resources/animations/DwarfGroundedExperiment.tres`
- `docs/validate_dwarf_calibration.gd`
- `docs/validate_dwarf_calibration.gd.uid`
- `docs/MILESTONE_06C_1_DWARF_LOCOMOTION_CALIBRATION.md`
- `docs/milestone_06c1_baseline_5.png`
- `docs/milestone_06c1_cycle_side.png`
- `docs/milestone_06c1_cycle_isometric.png`
- `docs/milestone_06c1_spawn.png`
- `docs/milestone_06c1_walk.png`
- `docs/milestone_06c1_encounter.png`

Godot also creates ignored PNG import sidecars and cache/build files. Branch/HEAD remain `feature/milestone-05-animation` / `0752fd9`. Fourteen new experiment files plus the ten preserved 06C files remain untracked; nothing staged or committed. No push, merge, reset, stash, clean or branch switch. Original world, camera, navigation, Guardian and 5 m/s demo are preserved. Stop for manual approval.
