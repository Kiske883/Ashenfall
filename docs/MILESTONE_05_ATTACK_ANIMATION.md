# Milestone 05B.1 - V3 sword attack visual prototype

Started from clean `02db4b9` on `feature/milestone-05-animation`. New resources/scenes/tools only. Approved V1/V2/V3 assets, locomotion and low guard, EnemyController, PlayerController, world, physics and navigation remain unchanged. No staging, commits, pushes, merges or branch/history changes.

## Open the demo

Open **`res://scenes/art/FallenGuardianAttackDemo.tscn`**, build C# if needed and press **F6**. It starts in IDLE and has only three primary controls: IDLE, WALK and ATTACK. ATTACK plays once, then automatically returns to Idle. Repeated ATTACK clicks during the strike do not restart the cycle. IDLE/WALK explicitly interrupt the preview if selected.

The demo retains the approved local stage setup: fixed 45-degree isometric camera, orthographic size 22, neutral floor and lighting. As in the walk demo, a 2048-square SubViewport renders size 22 and the central 384-square region is magnified for inspection. No character scaling or world-camera edits. This is magnified inspection framing, not ordinary gameplay screen size.

## Reusable animation architecture

- `resources/animations/FallenGuardianV3SwordAttack.tres`: independent native AnimationLibrary with non-looping `Attack`.
- `scenes/enemies/FallenGuardianV3AttackPresentation.tscn`: instances the unchanged V3 geometry and uses the existing guard-grip script. AnimationPlayer retains the approved locomotion library under the empty namespace and adds the new library under `Combat`.
- Play `Combat/Attack` to evaluate the reusable clip. No script or method track emits AttackExecuted or applies any gameplay effect.
- `scripts/art/FallenGuardianAttackDemo.cs`: local button wiring, 0.12-second presentation blends and AnimationFinished -> Idle handling. It does not reference enemy AI or player systems.

Idle and Walk are the same original Animation resources, not revised copies. Attack has rotation tracks for articulated parts plus the same fixed pelvis guard position. Leg keys remain in the approved phase-zero Idle pose. Root and PoseRoot do not move; scales stay one. No collision/navigation nodes or root-motion extraction are animated.

## Timing and action

Total duration: **1.6 seconds**, one shot.

| Phase | Time | Behavior |
| --- | --- | --- |
| Windup | 0.00-0.60 s | Lift from low guard into a high diagonal preparation; elbow folds, torso turns about 9 degrees, shield stays attached defensively |
| Strike | 0.60-0.82 s | Fast downward diagonal slash, coordinated elbow extension, forearm turn, shoulder and torso rotation |
| Follow-through | 0.82-1.00 s | Decelerate in the low end pose |
| Recovery | 1.00-1.60 s | Ease back to the exact approved Idle phase-zero guard |

Keys are sampled from smoothstep phase curves. Shoulder flexion peaks at 55 degrees, while lateral rotation stays between -4 and -12 degrees; wrist changes stay between -5 and +8 degrees. Elbow hinge keys range from 25 to 95 degrees of additional rotation relative to the original bent rest shape, composed with forearm rotation. These are deliberately coordinated whole-arm transforms rather than a sword-only rotation. Torso yaw ranges roughly -9 to +8 degrees and forward pitch peaks at 5 degrees; head counter-motion limits visual instability. Shield follows its original forearm with the original attachment transform.

Measured sword-tip vertical excursion is about **1.49 units** (peak Y 1.860, low strike Y 0.374). The front/isometric frames show a high preparation followed by a descending cut; the side view confirms forearm extension and the low follow-through.

## Relationship to the existing attack controller

EnemyController is unchanged: default windup is 0.6 seconds and recovery is 1 second. It emits AttackExecuted once when entering recovery. The visual prototype borrows that total duration, but its visible slash starts at 0.6 and continues to 0.82. It is NOT wired to the controller. No damage, hitboxes, events or combat timing changes are present.

Future gameplay synchronization must deliberately resolve whether the logical execution marker should correspond to slash onset or a later contact pose. This phase does not silently move the controller's event or create a second event source. Clip completion is used only for the demo's return to Idle.

## Executed validation

- dotnet build: passed, zero warnings/errors (.NET 8 / Godot 4.7.2 .NET).
- Godot headless editor import: passed.
- `docs/validate_v3_attack_animation.gd`, headless and actual Vulkan Forward+ capture: zero failures. Verifies original Idle/Walk resource identity, three controls, one-shot duration, no event/method tracks, handle and shield alignment through 161 phases, unchanged mesh arrays/materials, unit scales/no root motion, exact final guard and automatic return to Idle.
- Angular continuity check over 10 ms samples passed; no per-node angular jump above 0.35 rad was found. This is a numerical discontinuity check, not a guarantee of visually perfect motion.
- Existing focused guard-grip, V3 walk, earlier Idle/Walk, enemy attack and player mouse/navigation suites: zero failures in each. No final run warning/error reported.
- Actual render evidence saved and opened for inspection: `docs/milestone_05_attack_windup.png`, `docs/milestone_05_attack_strike.png`, `docs/milestone_05_attack_phases.png`. Phase-sheet rows are isometric/front/side; columns are 0, 0.2, 0.4, 0.6, 0.68, 0.76, 1.0 and 1.4 seconds. Numerical tests also cover the exact endpoint at 1.6.
- Initial multi-view capture accidentally resumed an already paused attack near its end, showing Idle in one row. The capture tool now resets through Idle before every view, and final images were regenerated and inspected.
- Review was based on rendered phase sequences and playback/completion tests. No manual interactive artistic approval or production collision/intersection sweep is claimed.

Reproduce:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_v3_attack_animation.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan --fixed-fps 60 --script res://docs/validate_v3_attack_animation.gd -- --capture
```

## Visual limitations

This is a single static-foot, rigid segmented attack. There is no step, weight-transfer footwork, body recoil physics, finger articulation, cloth deformation or impact response. Original combined shoulder armor remains fixed to the torso; raising the upper arm can expose its existing contact limitations. No visible catastrophic separation was observed in the reviewed poses, but fine rigid-joint overlap is not eliminated. The shield occludes some weapon/arm motion in a pure side view. Magnified inspection retains the previous soft thin-detail appearance.

Transitions from Walk use a short blend into the attack's stationary guard legs; there is no production gait-aware attack entry. Pressing Idle/Walk intentionally cancels the isolated preview. Large enemy-group performance and gameplay timing synchronization are untested/out of scope. Artistic approval remains pending before any controller integration.

## Complete file inventory

New files:

- `resources/animations/FallenGuardianV3SwordAttack.tres`
- `scenes/enemies/FallenGuardianV3AttackPresentation.tscn`
- `scenes/art/FallenGuardianAttackDemo.tscn`
- `scripts/art/FallenGuardianAttackDemo.cs` and `.uid`
- `docs/validate_v3_attack_animation.gd` and `.uid`
- `docs/MILESTONE_05_ATTACK_ANIMATION.md`
- Three new evidence PNGs listed above, with ignored Godot `.import` sidecars.

Existing files modified: none. All additions remain local/untracked. Stop for visual approval before gameplay or combat integration.
