# Milestone 04B - Fallen Guardian detection and pursuit

Implemented on `feature/milestone-04-first-enemy` from clean commit `5446b95`. All existing tracked files remain unchanged. No staging, commits, pushes or branch operations were performed.

## Reusable actor and isolated world test

`res://scenes/enemies/FallenGuardian.tscn` is a CharacterBody3D controlled by `scripts/enemies/EnemyController.cs`. Its Visual is the approved, unchanged `SkeletonWarriorV2.tscn`. Assign the exported `PlayerTarget` node reference when instantiating the actor; there are no global lookups, singletons or player-controller dependencies.

The actor's capsule is radius 0.32, height 1.9, centered 0.95 above its feet, matching the player and current navigation clearance. Collision layer 2 and mask 11 match the player: ground/static obstacles, other actors and physical boundaries remain solid. The shared world bake parses static layer 1, so enemies do not become baked obstacles. The visual sword/shield are not collision geometry.

NavigationAgent3D uses radius 0.32, height 1.9, path desired distance 0.3 and target desired distance 0.15. Avoidance is disabled, as on the player; static obstacle avoidance comes from navigation paths, and MoveAndSlide provides physical safety. No navigation resource, bake configuration, terrain, player input, camera, existing art or main world scene was edited.

Open `res://scenes/world/EnemyPursuitTest.tscn` and press **F6**. It instances the unchanged World and places one actor at (0.8, 0, -2), with PlayerTarget wired to that world's player. Move toward it using the usual mouse or WASD controls, stop, move a little away, then leave its leash to observe the return. F5 continues running the original world without enemies. Expand the actor in the Remote Inspector to inspect State, SpawnPosition and the exported tuning values.

## State transitions

- Idle to Chase: valid target within DetectionRadius, cooldown expired, and both leash conditions valid.
- Chase to Hold: horizontal player distance reaches StoppingDistance (plus 0.01 numerical tolerance).
- Hold to Chase: player distance exceeds effective ResumeChaseDistance. Hold does not recompute paths.
- Chase or Hold to Return: target disappears, enemy/player distance exceeds effective DisengagementRadius, or enemy/player exceeds the spawn leash.
- Chase to Return also occurs for an invalid/disconnected navigation destination or sustained lack of movement.
- Return to Idle: within ArrivalTolerance of the recorded spawn. Return never reacquires the player. Idle waits ReacquisitionDelay before considering detection again.

Only the visual rotates toward actual movement, using exponential angular interpolation. Gravity and floor snapping remain active in all states. Path movement is capped to each waypoint and the stopping range to avoid overshoot. SpawnPosition is recorded once in Ready, rather than following the enemy as it moves.

## Inspector parameters

All distances below are horizontal XZ distances in world units. Detection is radial proximity, without line-of-sight occlusion; a nearby player behind a wall can be detected, but must be reached through navigation.

| Export | Default | Meaning |
| --- | ---: | --- |
| PlayerTarget | unassigned | Explicit reference to the player Node3D |
| DetectionRadius | 6 | Enemy to player; starts acquisition |
| DisengagementRadius | 10 | Enemy to player; abandons pursuit |
| MaximumDistanceFromSpawn | 12 | Spawn to enemy AND spawn to player; either can end pursuit |
| MovementSpeed | 3 | Maximum horizontal units per second |
| StoppingDistance | 1.3 | Enemy to player; enters Hold |
| ResumeChaseDistance | 1.7 | Enemy to player; leaves Hold |
| ArrivalTolerance | 0.2 | Enemy to spawn; completes Return |
| TargetUpdateInterval | 0.25 | Seconds between navigation endpoint validations/updates |
| RotationSpeed | 10 | Visual angular interpolation rate |
| StuckTimeout | 2 | Seconds without 0.05 units of progress before recovery |
| ReacquisitionDelay | 1 | Idle cooldown after Return |

Effective values prevent invalid tuning from removing hysteresis: arrival >=0.05; stopping >=arrival; resume >=stopping+0.1; disengagement >=detection+0.1; update interval >=0.05; stuck timeout >=0.5. Negative speeds/cooldowns are treated as zero. Detection and both leash checks apply before reacquisition, so returning home cannot immediately restart a pursuit of a player outside the spawn leash. An enemy can cross the leash by at most a movement step before the next physics transition; the leash is a pursuit rule, not a physical wall.

## Navigation and failure handling

The controller waits for a synchronized navigation map. It validates the projected target and a full path before assigning NavigationAgent3D.TargetPosition. Off-mesh targets beyond 0.5 units and partial paths to disconnected targets are rejected. GetNextPathPosition is then queried in physics time. Targets update at the configured interval instead of every frame. No navigation mesh is rebuilt by the controller.

An unreachable player causes Return. Missing targets are safe. A blocked/unreachable Return stops safely and retries at the configured interval, without teleporting or falsely reporting arrival. Idle cooldown limits repeated acquisition of an unreachable nearby player; persistent obstruction can still cause periodic attempts. With no target assigned, the enemy remains Idle. Dynamic actor crowds are not handled by RVO; physical contact may stall pursuit and trigger Return. Static poses slide while moving because animation remains outside scope.

## Validation

- `dotnet build`: passed, zero warnings and errors (.NET 8 / Godot.NET.Sdk 4.7.2).
- Godot headless editor import: passed.
- EnemyPursuitTest startup for 180 frames: passed, including both V2 generation and the existing world bake.
- `docs/validate_enemy_pursuit.gd`: zero failures. Exercises detection, Chase/Hold arrival, stable Hold, hysteresis, throttled updates, both player-distance and spawn-distance disengagement, ignoring targets during Return, return arrival, reacquisition, actual tree navigation in both pursuit and return, target removal, gravity, off-mesh target rejection and mesh preservation.
- Existing `validate_mouse_navigation.gd`: zero failures, including player mouse/hold/WASD, trees, rocks, ruin, terrain boundaries, unreachable requests and smooth camera tracking.
- Existing `validate_gate_navigation.gd`: zero failures, both openings in both directions and solid pillars. This is the existing player-based gate test; an enemy-specific gate crossing is not claimed.
- Existing `validate_enemy_v2.gd`: zero failures; approved anatomy fingerprint and all material independence/budget checks preserved.
- A first development test exposed floating-point arrival at the stopping threshold; the numerical tolerance fixes the issue. That failed run also emitted a Jolt job-capacity warning under accelerated headless execution; the final pursuit run and regression runs did not reproduce it.
- No manual interactive gameplay or new render inspection was performed for this behavior milestone. F6 testing of pursuit appearance, turning, prop contacts and camera presentation remains recommended. No crowd performance, animation, combat or long-duration soak test is claimed.

Reproduce from the project directory:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_enemy_pursuit.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan res://scenes/world/EnemyPursuitTest.tscn
```

## Files

New files only:

- `scripts/enemies/EnemyController.cs`
- `scripts/enemies/EnemyController.cs.uid`
- `scenes/enemies/FallenGuardian.tscn`
- `scenes/world/EnemyPursuitTest.tscn`
- `docs/validate_enemy_pursuit.gd`
- `docs/validate_enemy_pursuit.gd.uid`
- `docs/MILESTONE_04_ENEMY_BEHAVIOR.md`

No existing files modified. No combat, damage, attacks, health, death, networking, inventory or animation systems were added. All changes remain local for review.
