# Milestone 04C - Attack preparation and combat readiness

Implemented from clean HEAD `727cd76` on `feature/milestone-04-first-enemy`. This adds logical attack timing only. No damage, health, hit detection, weapon collision, animation or combat UI exists. World, player, navigation resources, trees and both approved enemy art assets remain unchanged.

## States and timing

The existing enum values Idle=0, Chase=1, Hold=2 and Return=3 remain stable. AttackWindup=4 and AttackRecovery=5 are appended.

- Idle/Chase/Return retain the 04B detection, pathfinding and return rules.
- Chase enters Hold at the existing stopping distance. Hold smoothly faces the player; once within effective attack range, within 0.5 vertical units and facing within approximately 11.5 degrees, it enters AttackWindup.
- Entering Windup snapshots the target and duration. The entry tick consumes no windup time. Horizontal movement is zero during Windup and Recovery; gravity/floor contact remain active.
- Windup tracks the player's direction smoothly. Small range changes do not reset or interrupt it. At expiry it transitions to Recovery BEFORE emitting one AttackExecuted signal.
- The committed signal can fire if the target has left attack range. It represents an attempted logical attack, never proof of a hit. This avoids distance oscillation; future hit detection must validate actual impact separately.
- Recovery lasts its full configured duration even if the target moves away. On expiry, Chase resumes if the player is outside effective attack range, otherwise Hold resumes and may prepare a fresh cycle on a later tick.
- In Hold, the pursuit threshold with attacks enabled is the smaller of ResumeChaseDistance and effective attack range +0.15. This retains a small deadband without leaving the enemy stuck in Hold beyond useful range.
- Existing leash checks have priority in BOTH attack states: losing the target or crossing disengagement/spawn limits immediately enters Return and cancels pending execution. Changing the assigned target also cancels the current attack cycle. Return never attacks or reacquires the player; the existing Idle reacquisition delay remains.
- Disabling AttacksEnabled cancels attack preparation/recovery into Hold and restores the exact 04B movement thresholds. This is also the explicit mode used by the original pursuit test suite.

## Exports and relationships

| Export | Default | Meaning |
| --- | ---: | --- |
| AttacksEnabled | true | Enables preparation and logical events |
| AttackRange | 1.45 | Horizontal center-to-center enemy/player range to begin preparation |
| AttackWindupDuration | 0.6 s | Preparation time, minimum effective 0.05 s |
| AttackRecoveryDuration | 1 s | Recovery after execution, minimum effective 0.05 s |
| AttackFacingSpeed | 10 | Exponential visual rotation rate, minimum effective 0.1 |
| ShowAttackDebug | false | Optional local world-space state/count label |

Existing StoppingDistance defaults to 1.3. Read-only EffectiveAttackRange is max(AttackRange, effective stopping distance +0.01), ensuring a too-small Inspector range cannot prevent an enemy that stopped correctly from attacking. Detection, disengagement and spawn leash retain their existing meanings. Timing advances only in physics processing; durations are quantized to physics ticks. Editing an active phase's duration applies to the next phase entry rather than restarting its timer.

Facing rotates only the Visual Node3D, preserving the CharacterBody/capsule and navigation orientation. Canonical art forward is -Z, so no extra model offset is required. Movement continues using the existing RotationSpeed. No art geometry or material is changed. The optional label is a separate runtime child of the actor, outside Visual; no per-attack log spam is emitted.

## Attack event contract / Milestone 05 integration

Godot signal and generated C# event: `AttackExecuted(Node3D target, int sequence)`. Subscribe in C# with `enemy.AttackExecuted += ...` or connect `AttackExecuted` from Godot tooling. The target is the valid Node3D captured at Windup entry. Sequence starts at 1 per actor and increases with each emitted attack; read-only AttackCount exposes the same count. Cancelled windups emit nothing. The controller has already entered Recovery when observers run, so an event cannot be emitted twice for the same cycle.

Future animation, hit detection or combat systems can subscribe without adding dependencies to this controller. They must independently verify impact range/occlusion and target eligibility, and safely manage subscriptions. The signal itself applies no effects to the player. Current detection/attack preparation has no line-of-sight checks and can prepare through nearby walls; this prototype does not claim physical hit validity.

## Manual review in Godot

1. Open project.godot in Godot 4.7.2 .NET and build.
2. Open `res://scenes/world/EnemyPursuitTest.tscn`, press F6. Its existing enemy has ShowAttackDebug enabled; the reusable actor defaults to hidden debug feedback.
3. Approach the enemy and stop. Observe Chase, Hold/facing, AttackWindup, AttackRecovery and the attack count increasing once per cycle. The skeleton does not swing a weapon.
4. Move slightly during Windup: the cycle continues. Move beyond range during Recovery: pursuit resumes after recovery.
5. Leave the leash during Windup: no pending event should execute; the enemy returns home. Approach again after it returns to verify reacquisition.
6. Toggle AttacksEnabled off to inspect the original stable Hold behavior. Toggle ShowAttackDebug off to hide the label.
7. F5 still runs the original enemy-free world.

No interactive manual or rendered visual review was performed for this milestone; these presentation checks remain for user approval.

## Automated validation

- dotnet build: passed, zero warnings/errors.
- Godot headless editor import: passed.
- `validate_enemy_attack.gd`: zero failures. Tests no attacks while outside range, entry after pursuit, stopping, facing, windup duration, one event with correct payload per cycle, recovery duration, repeated cycle intervals, committed preparation after a range change, resuming Chase, leash cancellation, returning home, absence of stale events, reacquisition, missing target in Recovery, disabling attacks and conflicting range configuration.
- `validate_enemy_pursuit.gd`: all 04B assertions retained and passed with AttacksEnabled=false. The only test change selects this movement-only mode.
- `validate_mouse_navigation.gd`: zero failures, covering player movement, obstacle routes, terrain containment, unreachable destinations and camera tracking.
- `validate_enemy_v2.gd`: zero failures. V1/V2 geometry counts, anatomical fingerprint and independent material/shader references preserved. Git diff confirms no art asset modifications.
- Original world and EnemyPursuitTest headless startup for 180 frames: passed.
- A concurrent accelerated regression run emitted the previously observed Jolt job-capacity warning in the pursuit test despite all assertions passing. It is an engine runtime warning, not a C# compilation warning; no physics engine setting was changed to hide it.

Reproduce:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_enemy_attack.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_enemy_pursuit.gd
```

## Files and limitations

Modified:

- `scripts/enemies/EnemyController.cs`
- `scenes/world/EnemyPursuitTest.tscn`
- `docs/validate_enemy_pursuit.gd`

Created:

- `docs/validate_enemy_attack.gd`
- `docs/validate_enemy_attack.gd.uid`
- `docs/MILESTONE_04_ENEMY_ATTACK.md`

No existing reusable actor scene or approved visual resources were edited. No new dependencies or external assets. No hitboxes, skeletal animation, damage, health, death, loot or network behavior. No crowd/long-duration performance benchmark. Remain on the existing feature branch; all changes local, no staging, commits, pushes, merges or branch switches. Stop for user approval before further integration.
