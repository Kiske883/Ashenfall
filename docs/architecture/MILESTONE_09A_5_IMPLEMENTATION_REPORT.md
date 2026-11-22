# Milestone 09A.5 — catalog-driven first adventure

**Status:** implementation and automated validation completed on 2026-11-15. Manual gameplay review remains pending. The published `prototype-1` rows, database schema, roles and credentials were not changed.

## Architecture and launch

Open `res://scenes/world/AshenfallCatalogAdventure.tscn` and press **F6**. Start the existing read-only API as described in [09A.4](MILESTONE_09A_4_IMPLEMENTATION_REPORT.md) first. F5 and the original scene-configured `AshenfallFirstAdventure.tscn` remain unchanged. This separate entry scene waits for `CatalogRuntime.ActiveSnapshot`, resolves one class and one enemy by stable code, configures a fresh first-adventure instance **before** adding it to the scene tree, then pins that immutable snapshot and the resolved statistics for the lifetime of the encounter. `HealthComponent._Ready` therefore initializes current HP from the catalog maximum. No gameplay actor sends HTTP or connects to PostgreSQL.

Without a validated snapshot, the entry scene contains no adventure actors. It shows an error and a **Retry catalog load** button; the retry is asynchronous. If a prior validated snapshot exists after an API failure, the scene can use it. Shift+R may replace the service's available snapshot, but does not rebind existing actors, reset health, or interrupt combat. The existing `EncounterResetController` reloads the current entry scene; restart instantiates a fresh adventure using the latest available validated snapshot. There is no persistent offline cache. The catalog service's existing bounded timeout and overlap gate remain in force.

`GameplayStatsResolver` is model-independent and returns immutable gameplay records. `CatalogAdventureBootstrap` is the narrow Godot scene adapter. `GuardianCatalogOverride` represents explicit encounter values with a separate Boolean flag for each override, so an unset value inherits the catalog even when a scene's old default differs. The new entry scene explicitly reproduces the existing first-adventure overrides. Its adapter disables only the old `DwarfPlayerPresentation.MovementSpeedOverride` after assigning the catalog speed; otherwise that pre-existing scene value would overwrite the catalog during `_Ready`. Visual presentation, navigation, input, collision, health calculation, damage sequencing, animation resources and the accepted Guardian health bar are unchanged.

## Catalog-to-runtime mapping

| Catalog field | Runtime field | `prototype-1` base | First-adventure effective value / consumer |
| --- | --- | ---: | --- |
| `dwarf_warrior.baseMaxHealth` | Player `Health.MaximumHealth` | 100 | 100; initializes current HP before `_Ready` |
| `dwarf_warrior.fixedDamage` | `MeleeDamage.Damage` | 25 | 25; existing impact validation |
| `dwarf_warrior.moveSpeedMps` | Player `MovementSpeed` | 2.500 m/s | 2.500; legacy presentation override disabled in catalog entry |
| `dwarf_warrior.meleeRangeM` | `PlayerMelee.MeleeRange` | 1.450 m | 1.450 |
| `dwarf_warrior.attackCycleMs` | `PlayerMelee.AttackDuration` | 1700 ms | 1.700 s |
| `dwarf_warrior.impactMs` | `PlayerMelee.ImpactFraction` | 940 ms | `940/1700`; logical impact remains 0.940 s |
| `fallen_guardian.maxHealth` | Each Guardian `Health.MaximumHealth` | 100 | 100 each |
| `fallen_guardian.fixedDamage` | Each enemy damage controller `Damage` | 10 | 10 each |
| `fallen_guardian.attackRangeM` | `EnemyController.AttackRange` | 1.450 m | 1.450; existing `EffectiveAttackRange` safety rule retained |
| `fallen_guardian.windupMs` | `AttackWindupDuration` | 600 ms | **1.200 s for all three** via explicit adventure overrides |
| `fallen_guardian.recoveryMs` | `AttackRecoveryDuration` | 1000 ms | 1.000 s each |
| `fallen_guardian.impactAfterExecuteMs` | Enemy damage controller `ImpactDelay` | 100 ms | 0.100 s after `AttackExecuted` each |
| `fallen_guardian.chaseSpeedMps` | `MovementSpeed` | 1.000 m/s | 1.000 each |
| `fallen_guardian.returnSpeedMps` | `ReturnSpeed` | 0.700 m/s | 0.700 each |
| `fallen_guardian.detectionRadiusM` | `DetectionRadius` | 6.000 m | Road 6.000; West/East **5.500** override |
| `fallen_guardian.disengagementRadiusM` | `DisengagementRadius` | 10.000 m | Road 10.000; West/East **8.000** override |
| `fallen_guardian.maxSpawnDistanceM` | `MaximumDistanceFromSpawn` | 12.000 m | Road 12.000; West/East **8.000** override |
| `fallen_guardian.stoppingDistanceM` | `StoppingDistance` | 1.300 m | 1.300 each |
| `fallen_guardian.resumeDistanceM` | `ResumeChaseDistance` | 1.700 m | 1.700 each |

The first adventure's three `EncounterZone` activation/release radii (5.5/8.0 m) are scene-only encounter gates. The road Guardian's effective detection property remains 6.0 m but cannot acquire a target until its zone activates at 5.5 m, matching the prior scene behavior. `training_battle_axe` and `guardian_sword` remain definition-only; no equipment bonus is applied. Optional class/enemy attributes are null in `prototype-1` and have no current runtime consumer. Player arrival/slowdown, Guardian arrival/repath/facing/cooldown and collision/navigation dimensions have no catalog equivalent and retain their scene values. No speculative mapping was added.

## Files

Created: `scripts/catalog/GameplayStatsResolver.cs`, `scripts/catalog/GuardianCatalogOverride.cs`, `scripts/catalog/CatalogAdventureBootstrap.cs` and their three `.cs.uid` sidecars; `scenes/world/AshenfallCatalogAdventure.tscn`; `backend/Ashenfall.Tests/GameplayStatsResolverTests.cs`; `docs/validate_catalog_gameplay.gd` and its `.gd.uid` sidecar; and this report.

Modified: `backend/Ashenfall.Tests/Ashenfall.Tests.csproj` (link the platform-neutral resolver for unit tests), `scripts/catalog/CatalogRuntime.cs` (accurate log wording for pinned encounters), and `docs/architecture/MILESTONE_09A_IMPLEMENTATION_PLAN.md`. No existing gameplay or original adventure scene was edited.

## Validation

- Godot C# and backend solution builds: passed, zero warnings and errors.
- Backend suite with `ASHENFALL_RUN_DB_TESTS=1`: **34 passed**, including four resolver tests and existing hash, API and database checks. The unit tests cover approved value resolution by stable code, missing/incompatible definitions, unaffected item order, and immutable encounter statistics across a replacement snapshot.
- Godot 4.7.2 Mono headless import and catalog adventure startup against the real read-only API: passed. The entry scene logged version `prototype-1` and approved SHA-256 `a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17` before the first world/navigation frame.
- `validate_catalog_gameplay.gd` against the live API: zero failures. It checked current/max HP, fixed damage, movement, range, both attack clocks, all three Guardian overrides, Shift+R stability, restart, three-Guardian objective accounting, bars hidden on death, victory and defeat after restart. The victory/defeat assertions drive health directly; they do not replace manual combat review.
- `validate_catalog_gameplay.gd` against an unavailable endpoint: zero failures. No actor was created; retry remained available and did not create an unvalidated adventure.
- Existing headless regressions all exited 0 with zero failures: `validate_catalog_reload.gd`, `validate_first_adventure.gd`, `validate_player_melee.gd`, `validate_bidirectional_combat.gd`, `validate_health_damage_death.gd`, `validate_combat_feel.gd`, `validate_milestone_08.gd`, `validate_milestone_08g.gd`, and `validate_mouse_navigation.gd`.
- Rendered Vulkan Forward+ integration test against the live API: zero failures; all three Guardians, reload stability and restart assertions passed. Rendered Vulkan test against an unavailable endpoint: zero failures; no partial actor initialization and retry remained available. The initially frame-count-based test reached its assertion before the network response under fast rendered frames; the test was corrected to wait with a bounded wall-clock deadline and passed. No visual quality claim is based on these automated checks.

## Remaining limitations and manual check

Only `prototype-1` is published, so a live restart onto a *different* catalog release was not exercised; immutable replacement/pinning was tested in memory without changing PostgreSQL. The original first-adventure scene remains available as a scene-configured comparison; the catalog cutover is the new entry scene. Automatic startup waits for the API and a valid snapshot; with no snapshot it requires the explicit Retry button. The visual availability and focus of that button, movement feel, Guardian attack telegraphs, health bars, victory/defeat and restart should be checked by the owner in the Godot GUI. No manual GUI playthrough is claimed here.

1. Start the read-only API, open `AshenfallCatalogAdventure.tscn`, press F6 and verify 2.5 m/s dwarf movement, three independent Guardians, combat and victory/defeat.
2. During combat press Shift+R. Confirm no health, speed, attack timing or animation changes. Restart and confirm full health and the expected scene overrides.
3. Stop the API, restart the scene, inspect the error/retry screen, restore the API and press Retry. Confirm the adventure starts only after a validated catalog arrives.

**MILESTONE 09A.5 — CATALOG-DRIVEN GAMEPLAY IMPLEMENTED, MANUAL VALIDATION PENDING**
