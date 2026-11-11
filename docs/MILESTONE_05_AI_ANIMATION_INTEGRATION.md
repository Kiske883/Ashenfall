# Milestone 05B.2 - AI and V3 animation integration

> Current actual-enemy speeds are Chase 1.0 m/s and Return 0.7 m/s (Milestone 05B.5 below). The 05B.3 calibration remains in effect; historical 3 m/s examples below describe the earlier baseline. AI attack synchronization remains unchanged.

Implemented on `feature/milestone-05-animation` from clean HEAD `32a82bb`. No staging, commits, pushes, merges, stashes, resets, cleans or branch changes. Approved art/animation resources, main world, player, navigation and collision configuration remain unchanged.

## Source contract verified before implementation

`scripts/enemies/EnemyController.cs` actually defines Idle, Chase, Hold, Return, AttackWindup and AttackRecovery (enum values 0-5). Detection, horizontal ranges, hysteresis, navigation and movement run in physics time. The controller rotates its `Visual` child toward movement/the player; the CharacterBody/capsule root remains the physics actor.

Default attack windup is 0.6 seconds, recovery 1 second; each phase clamps its duration to at least 0.05 seconds on entry. Windup entry captures the player. Windup completion FIRST enters AttackRecovery, THEN increments AttackCount and emits exactly one `AttackExecuted(target, sequence)`. Recovery completion selects Hold or Chase. Leash/target loss cancels into Return, target replacement or disabling attacks cancels preparation/recovery into Hold. A small range change does not cancel a committed windup. These behaviors and existing event authority are preserved.

The original enemy used static V2 under `Visual`. The approved V3 attack presentation already provides AnimationPlayer with Idle/Walk under the empty library namespace and the one-shot `Combat/Attack`. Its root script only binds the approved sword grip. The isolated attack-demo script is NOT part of that presentation, so its automatic Idle callback is not imported into gameplay.

## Integration and ownership

```text
FallenGuardian (EnemyController / unchanged CharacterBody configuration)
├── CollisionShape3D                       unchanged
├── NavigationAgent3D                     unchanged
├── Visual (approved V3 attack presentation)
│   ├── Presentation / PoseRoot / articulated meshes
│   └── AnimationPlayer
└── AnimationController (EnemyAnimationController)
```

EnemyController owns all gameplay decisions, movement, facing and AttackExecuted. It does not reference the animation adapter. The new adapter has explicit Actor and AnimationPlayer references; it reads state/position/phase clock, selects clips and evaluates presentation only. It never writes actor position, velocity, state, target or attack settings. Animation tracks target descendants of Visual, leaving controller-owned Visual yaw and collider transforms untouched.

The only AI code additions are read-only presentation observations:

- AttackCycleId increments on each windup entry, including attacks subsequently cancelled.
- AttackPhaseDuration snapshots the same clamped timer already assigned on phase entry.
- AttackPhaseRemaining exposes the existing timer, clamped to zero while an attack phase is active.

Timer assignments, branches and event emission order are unchanged. Snapshotting duration means Inspector changes during a phase do not create a second clock or alter its interpretation. There is no AI-to-presentation dependency or duplicated state machine.

## Playback and timing

The adapter runs at physics priority Actor.ProcessPhysicsPriority +1, after AI movement/state updates. AnimationPlayer uses Manual callback mode; only the adapter advances it. There is no competing frame-based animation clock and no AnimationFinished-driven gameplay transition.

- Idle/Hold and stationary actors use Idle.
- Chase/Return use Walk only when actual measured XZ displacement indicates movement. Start threshold 0.08 units/s and stop threshold 0.02 reduce threshold flicker. A stopped/blocked Chase uses Idle.
- Walk playback rate is actual speed / WalkReferenceSpeed (default 3), clamped 0.25-1.75. It preserves the approved clip at nominal enemy speed and slows it during reduced movement. This is visual speed adjustment, not a change to actor movement.
- Entering a new AI cycle starts Combat/Attack exactly once; the adapter does not call Play every frame. Cycle identity handles adjacent repeated attacks and cancellations.
- Windup maps the AI phase fraction to clip 0.0-0.6. Recovery maps its fraction to 0.6-1.6. Thus the default 0.6+1.0 AI cycle plays the approved clip at its intended timing. Non-default AI durations independently retime the visual phases without changing gameplay timers or resources.
- AttackExecuted remains emitted only by EnemyController. At its transition, the rendered animation advances to 0.6 in the same physics frame, i.e. visible strike onset. Signal callbacks invoked synchronously inside AI run before the later presentation update and can still see the preceding pose; no hit detection is implemented here. No second execution event or animation method track was added.
- Leash, target loss, attack disable or target replacement cancel the animation according to the resulting AI state. Presentation blends to Walk/Idle; a pending animation cannot later emit a stale gameplay event.
- Clip changes use a 0.12-second blend by default. The approved attack already ends in Idle guard; the adapter follows the AI's phase exit at physics-tick resolution and blends into the next selected clip.

There was no timing conflict requiring gameplay changes. The approved visible strike runs 0.6-0.82; the existing authoritative event remains at its onset, not a newly invented contact marker.

## Manual test scene

Reuse **`res://scenes/world/EnemyPursuitTest.tscn`**, open and press **F6**. It already instances the actual `FallenGuardian.tscn` prefab and explicitly assigns the existing player target; updating that prefab integrates V3 into this scene without changing the world generator or duplicating the actor.

1. At startup the enemy is beyond detection range and idles at its spawn.
2. Approach using existing click/hold or WASD controls; confirm detection and Walk pursuit.
3. Stop near the enemy: confirm facing, windup, slash/recovery, then repeated cycles while in range.
4. Retreat a short distance: after committed recovery, pursuit/Walk resumes. Stop to observe Idle/Hold if attacks are disabled in the Inspector.
5. Leave the spawn/disengagement leash: attack presentation cancels, the enemy Walks home and resumes Idle.
6. Check trees, rocks, arches and boundaries while retreating. Confirm player/camera behavior is unchanged.
7. For tuning checks, change windup/recovery on the remote enemy and observe that visual phases follow the AI, without additional attack counts. Existing ShowAttackDebug can be disabled if its state/count label distracts from visual review.

F5 continues to run the original world, which still has no enemy placement. The actual reusable enemy prefab is integrated; main-world spawning was not added. Original isolated art/locomotion/attack demos remain unchanged and available for close inspection.

## Executed validation

- dotnet build: passed, zero warnings/errors.
- Godot headless editor import: passed.
- EnemyPursuitTest headless startup for 180 frames: passed; original world seed 731, 10 trees, 16 rocks and 2766 navigation polygons retained.
- Existing enemy pursuit and attack suites: zero failures. Only the pursuit suite's two art-path assertions were migrated from static V2 paths to the new V3 Head/eyes paths; all behavior assertions remain.
- New `validate_ai_animation.gd`: zero failures, headless and actual Vulkan Forward+ rendering. Tests stationary Idle, moving Chase/Return Walk, stopped Chase Idle, Hold, phase-by-phase timing, authoritative single execution, no per-frame restart, repeated cycles, windup cancellation, Return/Idle, custom 0.3/0.5 phase mapping, recovery cancellation on target removal, grip/shield attachment, angular continuity and immobile root/collider under animation evaluation.
- Focused integration tests confirm collider layer 2/mask 11, capsule/agent radius 0.32 and avoidance disabled. Initial assertions used exact floating-point equality and falsely failed the radius checks; approximate comparisons fixed the test, with no configuration change.
- Existing player mouse/navigation, both ancient-gate, approved attack-animation, guard-grip and V3 walk suites: zero failures in the final run. No final Godot runtime warnings/errors were reported.
- Actual world render captures saved: `docs/milestone_05_ai_animation_idle.png`, `..._walk.png`, `..._windup.png`, `..._strike.png`. Walk and strike-onset captures were opened and inspected at the unchanged gameplay framing. This is automated rendered validation, not a claim of manual interactive gameplay approval.

Reproduce from project directory:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_ai_animation.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan --fixed-fps 60 --script res://docs/validate_ai_animation.gd -- --capture
```

## Known limitations

No damage, health, hitboxes, effects or combat result logic. The existing early logical execution marker is preserved; future contact timing must be designed explicitly rather than moved by an animation callback. Extremely short custom AI phases necessarily compress presentation.

The approved in-place gait is not a root-motion/travel-calibrated animation. At the preserved AI speed of 3 units/s it can show foot sliding; movement speed and approved stride geometry were not redesigned to hide that. Cancellation blends and gameplay-scale readability still need manual review. The small dark actor uses the existing world lighting/camera, unlike the magnified studio demos. No production crowd performance, collision sweep of decorative sword/shield, or long-duration soak test was performed.

## Complete file inventory

Modified:

- `scripts/enemies/EnemyController.cs` - read-only phase clock/cycle observations only.
- `scenes/enemies/FallenGuardian.tscn` - approved V3 visual and dedicated adapter wiring.
- `docs/validate_enemy_pursuit.gd` - two migrated art-path assertions.

Created:

- `scripts/enemies/EnemyAnimationController.cs` and `.uid`
- `docs/validate_ai_animation.gd` and `.uid`
- `docs/MILESTONE_05_AI_ANIMATION_INTEGRATION.md`
- Four new PNGs listed above, with ignored Godot `.import` metadata.

No animation/art resource, main world, player, navigation, collision or isolated demo modifications. All changes remain local for review; stop for manual gameplay approval before further work.

## Milestone 05B.3 - measured Walk speed calibration

The existing uncommitted 05B.2 integration was preserved. This refinement changes only locomotion-rate handling in EnemyAnimationController, adds a focused calibration test, and updates this document. EnemyController, navigation, actor movement, detection/ranges, approved poses/resources, attack clocks and AttackExecuted emission are untouched.

### Calibration and limitation found

The approved Walk cycle is 1.4 seconds at unit character scale. Sampling LeftAnklePivot at support start and support end (phase 0 to 0.6) measures 0.28000003 units of horizontal travel with unchanged ground height. Support therefore lasts 0.84 seconds and the appropriate **1x reference is 0.333333 units/s**. The test measures the instantiated approved clip rather than deriving reference from desired movement speed. Reference uses world units at the current unit scale; future differently scaled actors need proportional reference calibration.

The AI actually moves at approximately 3.0 units/s on unobstructed pursuit/return. It has no acceleration system: it assigns horizontal speed directly, capped near path waypoints/arrival, then MoveAndSlide produces actual displacement. A perfect constant-contact match would require 3 / 0.333333 = **9x** playback, i.e. a complete 1.4-second cycle every 0.156 seconds. That cadence is not a convincing walk. The previous reference of 3 merely played the short-stride clip at 1x and hid the scale mismatch.

**This cannot eliminate foot sliding convincingly through playback speed alone under the current constraints.** The calibrated implementation exposes the mismatch and uses a conservative 2x cadence cap rather than silently running the approved walk at 9x. At 2x, the clip's effective support speed is about 0.667 units/s while the actor still moves at 3.0, leaving approximately 2.333 units/s of residual support mismatch at steady speed. This is an explicit limitation, not a claim that foot locking is solved.

Smallest safe next correction requiring separate visual approval: add a new locomotion asset calibrated for the preserved 3-unit/s travel speed, with suitable stride, cadence and support/swing timing (potentially a run rather than the current short-step walk). Keep approved Idle/Walk/Attack assets intact. Simply enlarging the stride ninefold at the same duration would exceed plausible limb reach; simply raising the playback cap to 9 creates an excessive cadence. Reducing gameplay movement speed would also match the clip, but is expressly outside this task and was not done.

### Runtime formula and exports

After the actor's physics update, measured XZ displacement divided by physics delta gives actual horizontal velocity; its magnitude drives the rate. Desired navigation speed is not used as the measurement.

```text
requested = actual_horizontal_speed / WalkReferenceSpeed
target = clamp(requested, MinimumWalkPlaybackRate, MaximumWalkPlaybackRate)
weight = 1 - exp(-physics_delta / WalkRateSmoothingSeconds)
smoothed = lerp(previous_smoothed, target, weight)
AnimationPlayer.Advance(physics_delta * smoothed)
```

| Parameter | Default |
| --- | ---: |
| WalkReferenceSpeed | 0.28 / (1.4 * 0.6) = 0.333333 |
| MinimumWalkPlaybackRate | 0.1x |
| MaximumWalkPlaybackRate | 2.0x |
| WalkRateSmoothingSeconds | 0.1 s time constant |

Zero smoothing time selects the target immediately. Bounds are sanitized to positive values with maximum >= minimum. The smoothed rate ramps from zero on entry, so the minimum is a target-rate bound rather than a forced first-frame jump. RequestedWalkPlaybackRate, WalkPlaybackRate and WalkRateLimited expose the requested/used result for inspection.

Existing movement hysteresis is retained. When movement stops, Idle is selected immediately and the Walk filter is cleared; smoothing does not prolong foot cycling. Attack entry also clears only locomotion-rate diagnostics/filter. Attack processing still advances from the AI phase clock before the Walk-rate branch, so neither smoothing nor locomotion limits can alter attack duration or execution signals.

### Executed tests

- dotnet build passed, zero warnings/errors.
- Godot headless editor import passed.
- New `docs/validate_walk_speed.gd`: zero failures. Measures character scale, cycle/support duration and actual ankle travel; verifies 1x at calibrated speed, 0.5x at half speed, smoothing on upward/downward speed changes, real displacement rather than desired speed, capped normal pursuit, calibrated Return and Idle on stop/arrival.
- Normal pursuit measured 2.999997 units/s, requesting 8.999991x and converging to 1.999961x with the cap. The test deliberately checks and reports this limitation rather than accepting the cap as perfect synchronization.
- Existing AI/animation integration suite passed: normal approach/return, Walk -> Attack, recovery/cancellation, Idle, repeated attacks, exact 0.6 strike onset and custom phase durations.
- Existing enemy attack and pursuit, player mouse/navigation and approved V3 walk suites passed with zero failures. The mouse/navigation run emitted the previously observed Jolt job-capacity warning under accelerated headless execution; no engine setting was changed to suppress it.
- No new manual interactive visual approval is claimed. Original 05B.2 manual validation remains the user's reported baseline; the revised capped cadence needs fresh review with F6 on EnemyPursuitTest.

Reproduce focused calibration:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_walk_speed.gd
```

Files changed relative to the preserved 05B.2 working tree: `scripts/enemies/EnemyAnimationController.cs`, this document. New: `docs/validate_walk_speed.gd` and `.uid`. No approved resource, AI controller, scene or gameplay file changed in 05B.3. Branch remains feature/milestone-05-animation at 32a82bb, with all work local/uncommitted. Stop for manual visual review and a decision on the stride limitation.

## Milestone 05B.5 - approved dual movement speeds

The actual FallenGuardian now uses **Chase 1.0 m/s** and **Return 0.7 m/s**. Both are exported on EnemyController:

- `MovementSpeed = 1.0`: Chase speed. Existing property name is retained to preserve scene/tool references.
- `ReturnSpeed = 0.7`: independent return-to-spawn speed.

The existing Chase/Return movement block selects the appropriate speed, then applies its unchanged nonnegative clamp, waypoint-step cap, arrival allowance, gravity and MoveAndSlide. No new state or transition, acceleration system, navigation behavior, target rule or attack rule was added. Idle/Hold, 0.6-second windup, 1-second recovery and AI-only AttackExecuted remain unchanged.

EnemyAnimationController and all approved resources are unchanged in this milestone. It still derives rate from actual horizontal displacement. Chase and Return both use Walk while moving; crossing between them does not restart the clip. At the default speeds both requested rates exceed the existing 2x cap (3x requested during Chase, 2.1x during Return), so the used cadence stays near 2x. Residual contact-speed mismatch remains approximately **33.3% at 1.0 m/s**, versus **4.8% at 0.7 m/s**. The two variants were manually approved in isolation; this change does not claim perfect foot locking or automatic approval of the state transition.

### Isolated comparisons preserved

`EnemySlowPursuitTest.tscn` explicitly overrides both MovementSpeed and ReturnSpeed to 0.7; `EnemySlowPursuitAlternativeTest.tscn` explicitly overrides both to 1.0. Thus their original uniform-speed comparison behavior is retained. The unchanged `EnemyPursuitTest.tscn` now exercises the actual enemy defaults (1.0 Chase / 0.7 Return). All previous uncommitted integration/calibration work remains present.

### Validation performed

- dotnet build: zero warnings/errors. Godot headless editor import: passed.
- New `validate_dual_speeds.gd`: zero failures. Verifies actual prefab defaults, Idle, detection, measured 1 m/s Chase, direct target-loss transition to measured 0.7 m/s Return, bounded per-frame displacement and angular continuity, ongoing Walk phase, arrival/Idle, reacquisition, windup, repeated attacks, single AI events and independently tuned ReturnSpeed.
- Existing enemy pursuit, enemy attack, AI/animation integration, slow comparison scenes, Walk calibration and player mouse/navigation suites: all passed with zero failures in the final run. No final warnings/errors observed.
- The rapid two-world comparison runner initially sampled before asynchronous navigation publication in its second world. Its setup now waits for a usable map/path, consistent with existing navigation tests. No runtime navigation code changed.
- Existing speed-calibration regression still explicitly tests the historical 3 m/s pursuit as a fixture, but its Return assertion now checks the independent ReturnSpeed instead of incorrectly assuming the Chase override applies to both states.
- No new manual visual test is claimed. Run the sequence below for approval.

### Manual approval sequence

Open `res://scenes/world/EnemyPursuitTest.tscn`, press F6. Approach: the guardian pursues at 1.0 m/s. Stop in range: observe repeated attacks. Retreat to resume pursuit, then leave its leash: it returns at 0.7 m/s and idles at spawn. Check that turning/cadence do not snap at the state change. Existing debug state labels can be disabled with ShowAttackDebug if desired. The main world, player and original camera remain unchanged.

Reproduce focused validation:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_dual_speeds.gd
```

### Exact 05B.5 file changes

Modified relative to the prior uncommitted working tree:

- `scripts/enemies/EnemyController.cs`
- `scenes/world/EnemySlowPursuitTest.tscn`
- `scenes/world/EnemySlowPursuitAlternativeTest.tscn`
- `docs/validate_slow_pursuit.gd`
- `docs/validate_walk_speed.gd`
- `docs/MILESTONE_05_AI_ANIMATION_INTEGRATION.md`
- `docs/MILESTONE_05_SLOW_PURSUIT_EXPERIMENT.md`

Created: `docs/validate_dual_speeds.gd` and `.uid`. No animation resource, speed adapter, player, main world, collision or navigation edits. No root motion, damage or combat additions. Branch remains feature/milestone-05-animation; all changes local, with no staging, commit, push, merge, stash, reset, clean or branch switch. Stop for manual approval.
