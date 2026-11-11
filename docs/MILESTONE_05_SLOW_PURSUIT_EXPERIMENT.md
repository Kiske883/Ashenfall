# Milestone 05B.3 - Isolated slow pursuit experiment

> Milestone 05B.5 preserves these two uniform-speed comparisons. The actual enemy now defaults to 1.0 m/s Chase and 0.7 m/s Return; each comparison explicitly overrides both speeds.

Two new inherited test scenes override only the test enemy's MovementSpeed. Existing uncommitted 05B.2/05B.3 work, global enemy defaults, approved art/animations and gameplay code are preserved. Both pursuit and return use the chosen speed, because the existing AI intentionally has one movement-speed export.

## Manual comparison

1. Build the project and open **`res://scenes/world/EnemySlowPursuitTest.tscn`**.
2. Press F6. This is the preferred **0.7 m/s** experiment. The on-screen label identifies the configuration.
3. Approach the guardian, stop to observe attacks, retreat slightly to resume pursuit, then leave its leash and watch it return home.
4. Stop execution, open **`res://scenes/world/EnemySlowPursuitAlternativeTest.tscn`**, press F6 and repeat the same route. This uses **1.0 m/s**.
5. For the actual **1.0 m/s Chase / 0.7 m/s Return** configuration, run `res://scenes/world/EnemyPursuitTest.tscn`.

Each run uses the same inherited world, seed, spawn positions, camera and player controls. F5/main world remains unchanged. The FallenGuardian defaults now use MovementSpeed=1.0 for Chase and ReturnSpeed=0.7 for Return. To abandon the uniform-speed experiment, run EnemyPursuitTest; there is no runtime global setting to restore. Further local tuning can override MovementSpeed and ReturnSpeed on the inherited enemy instance without editing the source prefab.

## Animation calibration and unchanged settings

The existing animation adapter still measures actual XZ displacement after physics and derives requested playback as measured speed / 0.333333, with the existing 0.1-second smoothing and 2x maximum. No adapter setting or animation resource was changed.

| Test | Measured speed | Requested rate | Used steady rate | Residual stance mismatch |
| --- | ---: | ---: | ---: | ---: |
| Preferred | approximately 0.700 m/s | 2.10x | approximately 2.00x | approximately 0.033 m/s (4.8%) |
| Alternative | approximately 1.000 m/s | 3.00x | approximately 2.00x | approximately 0.333 m/s (33.3%) |

The preferred test is much closer to the approved gait's capped effective support speed of 0.667 m/s. This does not claim complete foot locking. Both tests retain the cadence cap intentionally so the only gameplay comparison variable is movement speed. A heavier/more threatening feel is for user visual review; it has not been asserted from automated tests.

Windup 0.6 s, recovery 1.0 s, detection 6, attack range 1.45, all other ranges/hysteresis, navigation, collisions, authoritative AttackExecuted and attack synchronization remain unchanged. No new behaviors, effects, combat mechanics or input bindings.

## Validation performed

- dotnet build: passed, zero warnings/errors.
- `docs/validate_slow_pursuit.gd`: zero failures. Loads both actual inherited scenes; checks original prefab default, scene-only speeds, Idle startup, actual displacement-driven Walk rate, approach, attack preparation, single execution, repeated cycle timing, pursuit resumption after retreat, leash Return and final Idle within existing arrival tolerance.
- Measured rates: 0.699999 m/s -> 2.099998 requested / 1.999909 used; 0.999999 m/s -> 2.999997 requested / 1.999909 used.
- Existing AI/animation integration, enemy pursuit, enemy attack and Walk calibration suites: passed with zero failures.
- Godot headless editor import passed.
- Tests are automated. No interactive visual approval or judgment of the enemy's threatening appearance is claimed.

Reproduce:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_slow_pursuit.gd
```

## Files

New only:

- `scenes/world/EnemySlowPursuitTest.tscn`
- `scenes/world/EnemySlowPursuitAlternativeTest.tscn`
- `docs/validate_slow_pursuit.gd` and `.uid`
- `docs/MILESTONE_05_SLOW_PURSUIT_EXPERIMENT.md`

No existing files modified in this experiment. All previous local changes remain intact. No stage, commit, push, merge, reset, clean, stash or branch switch. Stop for manual comparison before choosing a global tuning change.
