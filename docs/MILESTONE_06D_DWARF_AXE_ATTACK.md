# Milestone 06D - Two-handed dwarf axe attack prototype

Isolated visual prototype, implemented from clean HEAD `2735828` on `feature/milestone-05-animation`. Awaiting manual visual approval. No gameplay attack integration, damage, health or hitboxes.

## Preview

Open **`res://scenes/art/DwarfBerserkerAttackDemo.tscn`** in Godot 4.7.2 Mono and press **F6**.

- **IDLE**: approved stationary presentation.
- **WALK**: approved grounded locomotion at native preview rate; no root motion.
- **ATTACK**: play the full axe attack once, then automatically return to Idle. Requests to change/restart the clip while the attack is playing are ignored so the cycle remains inspectable.
- **Front**, **Side**, **Isometric**: inspection viewpoints.
- **Inspection**: orthographic size 4, framed to include the raised axe.
- **Gameplay / 22**: true gameplay-scale isometric inspection. Small joint details are naturally less legible here.

The initial state is Idle. There are no joint sliders, global input bindings, player movement or AI controls in this preview. The playable demo and F5 entry point remain unchanged.

## Cycle and design

Native resource: `res://resources/animations/DwarfAxeAttack.tres`, animation key **`Combat/AxeAttack`**, length **1.70 seconds**, non-looping. Timing is visual-only and independent of the Guardian's 1.6-second AI attack cycle.

| Phase | Time | Presentation |
| --- | --- | --- |
| Anticipation | 0.00-0.18 | Small preload, slight rear weight shift, restrained counter-rotation |
| Windup | 0.18-0.72 | Raise both hands and axe above the head; deliberate pause near the top |
| Strike | 0.72-0.94 | Accelerate into a descending diagonal cross-body cut |
| Follow-through | 0.94-1.24 | Continue the edge downward, transfer weight and flex hips/knees; deepest pose around 1.08 |
| Recovery | 1.24-1.70 | Controlled return through reach-safe intermediate hand positions to the original guard |

The weapon swings in a plane in front of the body, with torso counter-rotation providing additional depth. Both original grip markers remain fixed to the rigid axe; hands follow those same markers. The blade's measured outer cutting point descends from approximately **1.8922** to **0.89565** in the measured stroke, a drop of **0.99657 units**.

The pelvis transfers weight laterally/fore-aft by less than 2 cm and lowers by at most 5 cm. It is not translated as root motion. Hip pivots participate through the existing planted-leg IK, with approximately 15.69 degrees hip and 40.51 degrees knee rotation range. Torso/head coordination and delayed beard motion support the strike without large bouncing. Both foot targets remain at their resting ground positions; there is no lunge, step or foot scale change in the attack resource.

The clip contains 341 samples at 5 ms spacing. Eleven value tracks animate the existing ten pose controls plus an attack-only forearm-twist weight. No method, gameplay-event, collision, navigation or scale tracks exist. Duration can be revised in this separate resource after review; approved Idle/Walk curves are not edited.

## Architecture and preservation

```text
DwarfBerserkerAttackDemo - preview UI and inspection camera only
  Dwarf - DwarfBerserkerAxeAttack.tscn
    DwarfAxeAttackPresentation - thin subclass of the approved presentation
    Model - original procedural dwarf scene and articulated hierarchy
    AnimationPlayer
      default library - original approved grounded Idle/Walk reference
      Combat library - new DwarfAxeAttack.tres only
  neutral ground / studio lights / Camera3D
```

The new character scene inherits `DwarfBerserkerGroundedExperiment.tscn`. The default library remains the exact same resource reference; the attack is added under a separate Combat library. Original geometry recipes, meshes, materials, shaders, proportions, bald head, beard and weapon are not changed, copied into another model or regenerated differently. All mesh attribute/index hashes and material references compare equal against an independently loaded approved model.

`DwarfAxeAttackPresentation` reuses the original `ApplyPose` / two-bone solving and manual AnimationPlayer clock. It adds only:

1. Preview clip selection, full-cycle playback and automatic completion to Idle. It owns `PreviewClip`; the inherited locomotion-only `ActiveClip` is not a gameplay attack state.
2. An opt-in forearm axial twist correction. After the existing rig solve, project wrist-relative rotation onto each forearm axis and distribute part of that twist to the forearm. Restore the wrist's world transform so hand/equipment alignment is retained. This rotates existing parts; it does not stretch bones or alter mesh data.

Twist weight is zero outside the attack. Idle/Walk therefore retain the approved presentation. This subclass is an isolated preview presentation, not a replacement for PlayerController or a drop-in combat/AI controller. `PlayClip` is its visual preview API. There are no gameplay signals or authoritative attack events.

During authoring, early weapon-centre paths exceeded the 0.6-unit arm reach and separated a grip by several centimetres. Those poses were corrected, rather than stretching an arm or moving a hand off its grip. A deterministic authoring-time projection adjusted weapon-centre samples into the intersection of both 0.59-unit wrist-reach spheres, leaving a small reach margin. The largest baked centre adjustment was approximately 0.0653 units. The result is serialized in the new native resource; no per-frame animation rebaking or mesh rebuilding occurs.

The Guardian's separate attack library, one-shot preview and completion pattern were inspected for architecture only. Its timing, geometry, animation resources, AI and `AttackExecuted` semantics are untouched.

## Validation performed

| Check | Actual result |
| --- | --- |
| `dotnet build` | Passed, 0 warnings / 0 errors |
| Godot 4.7.2 headless editor import | Passed |
| Bounded direct attack-preview startup | Passed |
| `docs/validate_dwarf_axe_attack.gd`, fixed 60 FPS | Passed, 0 failures |
| Same focused test with Vulkan Forward+ and `-- --capture` | Passed, 0 failures; four images rendered and inspected |
| `validate_dwarf_playable.gd` | Passed, 0 failures; approved playable dwarf unchanged |
| `validate_dwarf_calibration.gd --definitive` | Passed, 0 failures; grounded locomotion unchanged |
| `validate_dwarf_animation.gd` | Passed, 0 failures; original 06B presentation unchanged |
| `validate_playable_demo.gd` | Passed, 0 failures; original 05C demo unchanged |
| `validate_v3_attack_animation.gd` | Passed, 0 failures; Guardian art attack unchanged |
| `validate_enemy_attack.gd` | Passed, 0 failures |
| `validate_mouse_navigation.gd` | Passed, 0 failures at 60 Hz |
| `validate_gate_navigation.gd` | Passed, 0 failures |

Focused validation samples 401 points across the complete attack, with cached real mesh vertices:

- Maximum grip-anchor error approximately **0.00000306 units**.
- Boot minima approximately **-0.00000470 to +0.00000006**, numerical tolerance around the ground plane. Both soles remain planted in the attack curve.
- Original bone lengths, fixed model/PoseRoot transforms, unit scale and resource data preserved.
- Largest sampled joint change approximately **3.845 degrees over 4.25 ms**; no discontinuity exceeding the test threshold.
- Endpoint transforms match: recovery restores the original guard.
- No axe vertices deeply inside the conservative skull/torso ellipsoid proxies during the sampled motion.
- Repeated ATTACK requests do not restart the active clip; real one-shot playback completes and resumes Idle. Walk remains available afterward.

Initial reach-check failures and a validation-script type inference issue were fixed before the passing runs. Final runs reported no errors or warnings. The animation emits no `AttackExecuted`, damage or gameplay events.

Reproduction from the project directory:

```powershell
dotnet build
& C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --editor --import --quit
& C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --fixed-fps 60 --script docs/validate_dwarf_axe_attack.gd
& C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe --path . --rendering-driver vulkan --resolution 1280x800 --fixed-fps 60 --script docs/validate_dwarf_axe_attack.gd -- --capture
```

## Visual review and remaining limitations

Rendered and inspected front, side and isometric contact sheets at **0, .18, .58, .72, .84, .94, 1.08 and 1.70 seconds**, left-to-right then top-to-bottom. Also inspected the raised-axe silhouette at actual orthographic size 22. The raised preparation and downward cross-body follow-through are visible; both boots stay grounded and both grips follow the axe. This is rendered phase inspection plus continuous numerical sampling, not live human approval of weight or rhythm.

Rigid shoulder plates move with the upper arms and can visually overlap the bracers or beard when the arms cross. Windup partly occludes the face. The side view naturally sees the flat axe head edge-on. Proxy penetration checks only cover deep skull/torso intrusion; they do not certify clearance from beard strands, shoulder armor or other arm surfaces. No cosmetic resculpting or deformation was added to conceal those constraints.

Hand/forearm orientation changes remain large in the crossed-arm follow-through. Diagnostics reach approximately 143.35 degrees of full local wrist orientation change and about 92.94 degrees between the forearm direction and wrist-to-grip axis. These are rig-coordinate diagnostics, not a claim of anatomically isolated wrist angles or verified joint limits. Review those poses closely. The twist correction preserves coherent grips and avoids concentrating all axial rotation at the wrist, but does not make the rigid procedural model a skinned anatomical rig.

The attack's foot targets are fixed; entering it from an in-place Walk can still settle the existing foot lift during the initial 0.12-second blend. Gameplay movement cancellation, attack facing, attack locks, hit timing, collision/hitboxes and damage are deliberately not integrated. The authoritative player demo continues to use only its approved locomotion presentation. No claim that this prototype is ready for gameplay combat is made.

## Exact new files and Git status

New files only:

- `scripts/characters/DwarfAxeAttackPresentation.cs`
- `scripts/characters/DwarfAxeAttackPresentation.cs.uid`
- `scripts/art/DwarfAxeAttackDemo.cs`
- `scripts/art/DwarfAxeAttackDemo.cs.uid`
- `scenes/characters/DwarfBerserkerAxeAttack.tscn`
- `scenes/art/DwarfBerserkerAttackDemo.tscn`
- `resources/animations/DwarfAxeAttack.tres`
- `docs/validate_dwarf_axe_attack.gd`
- `docs/validate_dwarf_axe_attack.gd.uid`
- `docs/MILESTONE_06D_DWARF_AXE_ATTACK.md`
- `docs/milestone_06d_axe_front.png`
- `docs/milestone_06d_axe_side.png`
- `docs/milestone_06d_axe_isometric.png`
- `docs/milestone_06d_axe_gameplay.png`

Godot also creates ignored PNG import sidecars and build/import caches. Initial working tree was clean. Branch/HEAD remain `feature/milestone-05-animation` / `2735828`. Fourteen new untracked files, no existing tracked files changed, nothing staged or committed. No push, merge, reset, stash, clean or branch switch. Stop for manual visual approval before any further animation refinement or gameplay integration.
