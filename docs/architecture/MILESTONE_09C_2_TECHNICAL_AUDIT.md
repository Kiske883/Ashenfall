# Milestone 09C.2 — persisted statistics technical audit

## Executive summary and decision

**Recommendation: NO-GO for implementation until the owner approves the data
contract and runtime policies below.** A small integration is feasible: use a
single, consistent backend-produced character snapshot to initialize maximum HP
and fixed damage before a new encounter enters the scene tree. The current
`/stats` response is insufficient for that purpose because it lacks identity,
revision and catalog provenance, and its component queries are not a single
snapshot. The current Godot preview only displays a character name and revision;
it does not apply persisted statistics.

This is a read-only audit. Existing code, the standalone demo and the database
were not changed. The only new repository file is this report. Prior conversation
and inspected files show substantial existing uncommitted and untracked milestone
work, including 09C.1 and the combat timing correction. Current Git status was
not queried because the owner prohibited all Git operations; therefore no claim
is made that this is a complete current working-tree inventory.

## Verified architecture and data flow

1. PostgreSQL stores immutable published class definitions and their base
   `base_max_health`, `fixed_damage`, nullable base attributes and armor,
   `move_speed_mps`, `melee_range_m`, `attack_cycle_ms` and `impact_ms`
   (`backend/MigrationReview/InitialRpgCatalog.sql:29`). `player_characters`
   stores account/class IDs, XP, allocated strength/dexterity/vitality,
   revision and soft-delete state; it does **not** store current HP
   (`backend/MigrationReview/CharacterPersistence09B.sql:119`). Equipment
   references owned instances; class-level rules and item modifiers are separate
   catalog extensions. The schema allows modifier codes for attack speed and
   criticals, but their presence in the schema does not make them functional.
2. `CharacterService.GetActiveAsync` selects the active character for an
   account. `EffectiveStatsAsync` loads its class, progression and equipped
   definitions/modifiers, then calls `CharacterRules.CalculateStats`
   (`backend/Ashenfall.Api/Characters/CharacterService.cs:102`). Missing rules
   currently fall back to level 1 with zero bonuses. Level is derived from XP;
   allocation is checked against earned points on save. Equipment and inventory
   operations use a character-first lock and revision checks.
3. `CharacterRules.cs:74` is the existing backend calculation. For each of
   max HP, fixed damage, strength, dexterity, vitality and armor it sums flat
   item modifiers and percent modifiers, applies `(base + flat) * (1 + percent /
   100) + temporary`, rounds away from zero and clamps to 0..`int.MaxValue`.
   Health/damage use the **current level rule's** bonuses; attributes add saved
   allocations. `EffectiveStatsAsync` passes no temporary modifiers. Attributes
   do not themselves currently add health or damage. The record returned is
   `EffectiveCharacterStats(Level, MaxHealth, Damage, Strength, Dexterity,
   Vitality, Armor)`; it has no crit, attack speed, character ID or revision.
4. The development API exposes separately `GET /api/v1/dev/character/`,
   `/stats`, `/inventory` and `/equipment` (`CharacterEndpoints.cs:37,69-82`).
   It resolves the account from a configured local subject, checks a private
   token and loopback source and is disabled unless explicitly enabled in
   Development. If the account exists but has no active character, the current
   GET handlers call `Results.Ok(null)` rather than explicitly returning 404;
   clients must handle an empty/null response as absent. This is not a production
   authentication or multiplayer boundary.
5. `scripts/characters/PersistedCharacterClient.cs` requests only the character
   row, verifies the explicitly configured expected GUID and returns a typed
   record. `CharacterPersistencePreview.cs` displays name/revision in an
   optional wrapper around `AshenfallDwarfPlayableDemo.tscn`; it does not fetch
   `/stats` or set gameplay fields. It uses loopback HTTP, a private process
   token, five-second timeout and bounded response. Godot has no PostgreSQL
   connection in this flow.
6. Separately, the existing catalog adventure resolves a validated catalog into
   `DwarfGameplayStats` and configures the encounter before `AddChild`
   (`scripts/catalog/CatalogAdventureBootstrap.cs:86-114`). It sets movement,
   health, range, duration, impact fraction and fixed damage from catalog data,
   disabling the presentation's speed override. This is catalog-driven but is
   not an individual persisted-character load. Catalog version/hash are pinned
   to the encounter; a catalog reload does not automatically alter active stats.

Read-only PostgreSQL metadata inspection on `ashenfall_09b3_test` confirmed the
six persistence tables and the columns above, one synthetic published class,
zero characters, equipment rows, level rules and modifier rows at inspection.
Its all-zero synthetic hash is not the approved development catalog hash.
`ashenfall_dev` was not accessed. The 09B persistence report warns that SHA-256
V1 covers the original catalog tables, **not** the progression/modifier
extensions (`docs/architecture/MILESTONE_09B_CHARACTER_PERSISTENCE_REPORT.md:31`).
Published extension triggers restrict writes, but the current hash alone cannot
attest those extension values.

## Gameplay values and proposed ownership

Values below are verified in source and scenes. The recommendation column is a
proposal, not current behavior.

| Value and current location | Current standalone dwarf demo | Existing persisted/catalog equivalent | Proposed 09C.2 ownership |
| --- | --- | --- | --- |
| Max HP: `HealthComponent.cs:13-37`; `AshenfallDwarfPlayableDemo.tscn:16` | Exported max 100 HP; `_Ready` sets current to max | Class base HP, level and equipment effective `MaxHealth`; no saved current HP | Apply validated effective max before `_Ready`; initialize full for a new encounter only, subject to owner approval |
| Current HP: `HealthComponent.cs:30-46` | Runtime 100 initially; decreases to 0; clamp and one death | None | Keep encounter runtime state; do not write or heal on refresh. Explicitly approve restart behavior |
| Player damage: `MeleeDamageController.cs:10,39-61`; demo scene:36 | Exported fixed 25 HP; once per successful, validated impact | Effective `Damage` derived by backend from class, level and equipped modifiers | Apply as fixed damage for persistence mode; preserve target/range/sequence validation |
| Variable damage/crit: `MeleeDamageController.cs:12-16,50-58`; `CombatDamageCalculator.cs:17-47` | Default fixed; debug-only optional 20–30 damage, 10% crit, 1.5 multiplier; env flag can activate it | Schema has `critical_chance`/`critical_multiplier` modifier codes, but effective DTO/calculation ignores them | Do not enable or infer crits in 09C.2; ensure debug override cannot silently replace persisted fixed damage |
| Armor/defense: `CharacterRules.cs:95`; `EnemyMeleeDamageController.cs:89-103` | Guardian deals fixed 10 HP per valid hit; no armor mitigation or resistance system | Effective `Armor` is calculated; schema has armor modifiers | Can expose for inspection, but do not apply mitigation without approved formula and tests |
| Allocated attributes: `CharacterRules.cs:92-94` | No separate gameplay mapping | Saved allocations, nullable base attributes and effective values | Retain as data; no unapproved formula connecting attributes to combat |
| Movement speed: `PlayerController.cs:9`; `DwarfPlayerPresentation.cs:13,57`; demo scene:21 | Prefab default 5 m/s; dwarf scene overrides to 2.5 m/s | Class catalog has 2.5 m/s, not per-character movement speed | Keep approved 2.5 m/s/catalog gameplay configuration in first integration; avoid `_Ready` overwrite |
| Melee reach: `PlayerMeleeController.cs:16,179,224` | Exported 1.45 world units center-to-center; minimum 0.7, vertical tolerance 0.5 | Class catalog 1.45 m; not effective `/stats` | Keep catalog/game configuration; preserve range and line-of-sight checks |
| Player cycle / impact / recovery: `PlayerMeleeController.cs:17-19,132-157`; `DwarfPlayerPresentation.cs:100-104` | 1.70 s total; impact 0.94 s; remaining recovery 0.76 s; animation rate follows cycle | Class catalog 1700/940 ms; item `attack_speed` code exists but is not calculated in `/stats` | Pin approved timing; do not apply attack-speed modifier until duration, animation and impact semantics are approved |
| Guardian windup / impact / recovery: `EnemyController.cs:13-14,92-104`; demo scene:13; `EnemyMeleeDamageController.cs:20-21` | Demo/preview 1.20 / 1.30 / 1.00 s; prefab default windup 0.60 s | Enemy catalog stores windup/recovery/impact delay; adventure has explicit windup overrides | Preserve current scene/encounter values and independent enemy authority |
| Movement interruption and targeting: `PlayerMeleeController.cs:65-110,142-165` | One attack per command; WASD/ground click interrupt, including before impact | Not character attributes | Keep unchanged. No dodge invulnerability; don't make these stats |
| Physics/navigation: `PlayerController.cs:9-21`; `scenes/characters/Player.tscn` | 0.32 collider radius; 0.2 arrival; 1.0 slowdown; 0.12 s held-click update; rotation 12 | No per-character persisted equivalent | Keep navigation/collision configuration, not RPG stats |

Health UI subscribes to `HealthChanged` and synchronizes immediately in `_Ready`
(`scripts/ui/PlayerHealthHud.cs:13-23`). Assigning `MaximumHealth` **after**
health `_Ready` only clamps existing current HP; raising the maximum does not
fill it. Assigning before `_Ready`, as catalog bootstrap does, initializes full
health once. An API refresh must not recreate/reinitialize health or revive a
dead player. Enemy armor, resistance and multiplayer-authoritative damage are
not implemented. The debug variable-damage flag is an important conflict with
an otherwise authoritative fixed damage snapshot.

## Contract gaps and risks

- `/stats` and character GET are separate and do not share a revision or
  transaction. `EffectiveStatsAsync` performs multiple reads without a wrapping
  repeatable-read transaction. A concurrent equip or character update can mix
  revisions, and a pair of responses can describe different moments.
- The per-character row revision is updated by current service equipment and
  item operations, but must not be assumed to cover every possible direct SQL
  mutation or independently changed authored catalog extension. A repeatable-read
  read transaction, class/catalog identifiers and a versioned contract are needed
  to characterize what was read. The current catalog hash does not cover extension
  rows, so a future extension-content version/hash policy needs owner approval.
- `CharacterRules` allows an effective maximum of 0; Godot's `MaximumHealth`
  clamps to at least 1. Values above the editor's documented range can also
  diverge from UI/design expectations. Define and validate acceptable bounds
  instead of silently clamping across the network. Damage permits 0, but the
  combat system only emits damage when positive.
- Runtime catalog snapshots and persisted character class IDs must match. The
  09C.1 isolated synthetic class is `test_class`, not the published gameplay
  `dwarf_warrior`; do not treat it as a production character or bypass catalog
  hash verification. A real published fixture with appropriate rules/modifiers
  must be prepared in an isolated environment under separate authorization.
- The API currently serializes a persistence entity for character GET and uses a
  separate record for stats. A dedicated read-only response avoids exposing
  unnecessary persistence fields and can represent absence explicitly. Version,
  identity and provenance need consistent, tested semantics.
- The current dev-only token is a process secret sent over loopback HTTP. It is
  suitable only for the explicitly local prototype. Do not infer account
  ownership from a client-supplied ID or ship it as production authorization.
- Async loading may outlive scene ownership. Apply validated data on the Godot
  main thread; cancel on exit, prevent reentrancy, and do not partially mutate a
  scene after timeout, mismatch, death or restart.

## Proposed design — requires approval

1. Add one owner-scoped, read-only gameplay snapshot endpoint behind the existing
   development gate. From one short read-only `REPEATABLE READ` transaction,
   resolve the configured account, active character and equipped modifiers;
   calculate stats once using `CharacterRules`. Return schema version,
   character GUID, row revision, class GUID, catalog release identity/version,
   known catalog hash and effective max HP/damage (plus optional display-only
   attributes/armor). Explicitly return not-found for missing/deleted identity.
   Specify extension provenance separately; do not label V1 hash as covering it.
2. Extend the typed HTTP layer with one bounded, cancellable fetch and strict
   validation: exact expected GUID, matching class/release, required fields,
   approved bounds and finite values. Reject malformed, null, unauthorized,
   stale, incompatible or partial data. Do not recalculate equipment stats in
   Godot. Compare character identity/revision and catalog provenance before use.
3. Add an explicitly selected persistence-backed bootstrap/scene variant. Load
   the validated catalog and complete character snapshot **before** creating or
   enabling an encounter; configure a fresh scene before `AddChild`, following
   `CatalogAdventureBootstrap`. Set only effective maximum HP and fixed damage;
   retain approved catalog/scene timing and movement. Make publication atomic:
   on any error discard the candidate and keep gameplay unavailable with visible
   retry. Do not silently substitute demo values in persistence mode.
4. Keep the standalone demo explicitly separate and functional offline. For an
   active persistence-backed encounter, pin the accepted snapshot through combat;
   do not hot-apply new equipment, max HP, damage or attack duration mid-cycle.
   On API loss after start, retain the already validated snapshot for the active
   encounter and visibly report loss; retry for a future encounter. Do not
   automatically create or save a character. Confirm this availability policy
   with the owner.

If future changes require persistent current HP, armor mitigation, crits or
attack-speed effects, design and approve those separately. The correct place for
live hit validation and random damage in future multiplayer is the authoritative
host; a local Godot simulation does not provide anti-cheat authority.

## Proposed implementation and validation phases

1. **Contract and consistency:** specify snapshot bounds, versioning, extension
   provenance, ownership and absence behavior; add read-only transaction and DTO.
   Unit-test `CharacterRules` flat/percent order, level thresholds and bounds;
   use isolated PostgreSQL for consistent equipment snapshots and concurrent
   update/read cases. Verify permissions and absence of writes.
2. **Client and bootstrap:** unit-test DTO decoding, missing/null/malformed fields,
   wrong ID/class/revision/catalog, timeout/offline, cancellation and bounded
   responses without PostgreSQL. Use mocked HTTP; add actual isolated API/DB
   tests for real transport and ownership. Verify no token leaks in diagnostics.
3. **Gameplay mapping:** use non-default test HP/damage and test initialization
   before `_Ready`, HUD initial/full values, maximum changes without accidental
   healing, invalid snapshots, no partial setup, defeated state and restart.
   Verify fixed damage uses the backend effective value and equipment-modified
   values match backend results. Crits and armor should remain disabled unless a
   separate approved feature defines them.
4. **Regression and manual:** run backend and Godot builds, existing
   `validate_counterattack.gd` in both standalone and preview modes,
   `validate_combat_feel.gd`, `validate_player_melee.gd`,
   `validate_bidirectional_combat.gd`, `validate_health_damage_death.gd`,
   `validate_catalog_gameplay.gd`, and `validate_dwarf_playable.gd`. Exercise
   30/60/120 fixed-FPS headless runs while acknowledging fixed physics ticks and
   no human-performance claim. Test offline startup/retry, API loss during combat,
   standalone offline play, enemy encounters, HUD, movement, camera, interruption,
   four-hit zero-damage counterplay and a deliberately reckless hit. The owner
   should also manually test the actual Godot scene and external loopback API.
5. **Isolation:** run DB integration only with uniquely identified disposable
   fixtures in an explicitly verified isolated database; preflight identity,
   permissions and catalog provenance, and delete only own rows. No schema
   migration or development database write is part of this proposed phase.

## Decisions for owner approval

1. Approve the single transactional identity/revision/stats response and its
   endpoint/version, as well as exact validation bounds and missing response.
2. Approve **full HP on new encounter** until current HP persistence is designed;
   choose whether an already-running encounter may continue offline with pinned
   stats, and what should happen on restart/reconnect.
3. Approve applying only effective max HP and fixed damage initially; keep crit,
   armor effects, attack speed, movement and attack timing out of scope.
4. Decide extension catalog provenance/hash and how to provision a valid isolated
   progression/equipment test fixture without changing the approved release.
5. Approve a separate persistence-backed scene/bootstrap and the UI policy for
   missing identity, server failure and retry. No automatic writes or creation.

## Rollback and audit limits

Future rollback should disable/remove only the new persistence-backed variant,
client additions and endpoint/DTO, preserving the existing demo, catalog and
uncommitted work; no database rollback should be needed for a read-only design.
No tests or builds were run in this audit: they could generate artifacts or write
to databases. The isolated database inspection used `BEGIN READ ONLY`; no
production/development database was touched. Manual gameplay acceptance reported
by the owner is not independently re-tested here. This report is a proposal,
not authorization to implement it.
