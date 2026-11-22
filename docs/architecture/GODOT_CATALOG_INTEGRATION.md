# Godot catalog integration proposal

**Implementation update:** Milestone 09A.4 added the HTTP client, validated in-memory catalog and non-blocking startup autoload described in [its implementation report](MILESTONE_09A_4_IMPLEMENTATION_REPORT.md). The encounter binder and gameplay adapter remain design only for 09A.5.

## Proposed small C# boundary

| Component (conceptual) | Responsibility | Initial necessity |
| --- | --- | --- |
| `CatalogApiClient` | One asynchronous `HttpRequest`-backed call for version/snapshot; bounded timeout, cancellation, ETag/304 handling | 09A.4 |
| `CatalogSnapshot` and typed definitions | Immutable validated dictionaries by UUID and stable `code`, with schema/catalog versions; no mutable Godot Nodes | 09A.4 |
| `CatalogLoader` | Fetch, deserialize, validate, atomically publish the available snapshot; explicit reload method | 09A.4 |
| `EncounterDefinitionBinder` (or a narrow spawn factory) | Pin one snapshot, apply class/enemy values and scene overrides to existing nodes **before they enter the scene tree** | 09A.5 |
| `CharacterRuntimeStats` | Resolved fixed health/damage/range/timing/movement values for one encounter | 09A.5 |
| `CombatStatCalculator` | Later combines progression, gear and buffs with deterministic rounding | **Defer** until 09B |

Godot art, animation, navigation and collision remain in scenes/resources. Catalog DTOs are plain C# data, not dynamically generated `.tres` art assets. A local allowlist maps `dwarf_berserker` to the approved dwarf presentation and `fallen_guardian_v3` to the existing Guardian scene. Unknown visual keys reject the snapshot; never load a path supplied by the API. `HttpRequest` belongs to the loading/menu boundary, not `PlayerMeleeController`, `EnemyController` or a physics tick.

The proposed training axe and Guardian sword definitions identify existing procedural visual parts only. They do not become swappable equipment, do not modify the current 25/10 fixed damage, and do not create a runtime inventory in 09A. The first encounter binder applies only the approved class/enemy gameplay values. See the [09A.2 final review](MILESTONE_09A_2_FINAL_REVIEW.md).

## Startup and pinning

1. The current scene-based demo remains available while 09A.2–09A.4 are built. The future catalog-enabled entry point requests `/api/v1/catalog/version`, then one exact `/api/v1/catalog/snapshot?version=...` before entering a new encounter.
2. Validate supported `schemaMajor`, exact requested version, well-formed hash field, distinct IDs/codes, bounded numbers, attack timing relationships, required Dwarf/Guardian keys and known visual keys. The 09A.4 client now recomputes the approved canonical SHA-256 V1 content hash independently and checks it against both API responses; the HTTP ETag remains separate.
3. Build typed immutable dictionaries off-scene. If any definition fails validation, discard the entire candidate and preserve the prior available snapshot. Do not partially publish classes without enemies/items.
4. A new encounter captures the snapshot reference and applies values to each **new** actor before `AddChild`. This matters because `HealthComponent._Ready()` initializes current HP from its exported maximum. Applying the maximum after `_Ready` would change max HP without creating the intended initial current HP. An explicit initialization API is an alternative for a later approved migration.
5. During an encounter, the snapshot and resolved stats are fixed. Existing sequence IDs, impact clocks, range and line-of-sight validation continue unchanged. No HTTP or database call occurs during movement, attack, animation or damage resolution.

Mapping at the first migration: `base_max_health/max_health` → `HealthComponent.MaximumHealth`; `fixed_damage` → the existing player/enemy damage controllers; class movement → `PlayerController.MovementSpeed`; class `attack_cycle_ms` → `PlayerMeleeController.AttackDuration`; class `impact_ms / attack_cycle_ms` → its existing `ImpactFraction`; enemy `windup_ms/recovery_ms` → `EnemyController`; enemy impact delay → `EnemyMeleeDamageController.ImpactDelay`; enemy chase/return/detection/leash settings → current exports. Convert integer milliseconds to seconds once. Retain the first adventure's three 1200 ms windup overrides, West/East detection/leash overrides, and navigation/scene layout. This preserves the validated encounter rather than silently replacing its scene tuning with base catalog values.

## Explicit reload and failure policy

- Check for a new release only at menu/startup or on a user/developer initiated reload. A successful reload changes the *available* snapshot for the **next** encounter or restart; it never rewrites active actor stats or current health. Keep an old published release reachable while a client remains pinned to it.
- When the API is unavailable, a current encounter continues with its pinned snapshot. For local Development only, an explicit, versioned bundled snapshot or the unchanged existing demo scene may be selected. Production online startup without a validated snapshot shows a retry/error state; it does not create an unauthenticated character or silently switch to development data. The exact offline product policy is an owner decision.
- Timeouts, cancellation on scene exit, 304, malformed JSON, missing IDs, incompatible schema and unknown visual keys have distinct results. Cache only validated immutable catalog data in memory initially; a disk cache is optional later and must have expiry/version checks. Never cache DB credentials in Godot.

## Validation in later implementation stages

Unit tests will cover DTO validation, duplicate/unknown keys, numeric ranges, schema mismatch, 304/reload, unavailable API and snapshot pinning. A focused Godot test will instantiate the approved player/Guardian scenes from the seed catalog, assert **100/25/10 HP/damage, 2.5/1.0/0.7 m/s, 1.70/0.94 s dwarf timings, 0.60/1.00/0.10 s base Guardian timing, and 1.20 s first-adventure overrides**, then run the existing movement/combat/adventure regressions. A test transport or local fixture substitutes for the real API; no DB is needed for physics-loop tests. Manual validation repeats F6 gameplay, encounter restart and an explicit catalog reload between encounters.

For future LAN/Internet play, the authoritative host loads the same versioned definitions and computes stats/hits itself. The client may render from its cached snapshot but cannot authorize damage, inventory or exploration writes. The present single-player prototype remains client authoritative until that separate milestone.
