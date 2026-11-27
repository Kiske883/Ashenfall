# 09C.2B.2 — persisted encounter initialization

## Final validation update — API isolation

The catalog autoload previously defaulted to `http://localhost:5000` and began
loading in `_Ready`, before the current scene could isolate it. A loopback
address does not establish which database an API uses. `CatalogRuntime` now
requires an explicit `ASHENFALL_CATALOG_API_URL`; when absent it does not
create a client or make a request. Catalog-dependent scenes therefore require
that variable, while standalone and persistence preview scenes do not require a
catalog service. The persistence preview separately requires an explicit
`ASHENFALL_CHARACTER_API_URL`; its loopback check and development token do not
prove which database the selected API uses. The operator must verify that API's
process configuration before providing its endpoint. No API process was
started or assumed to target the test database in this validation.

On the latest run, Godot compilation passed with zero warnings/errors. A new
headless standalone check with the catalog URL unset observed health 100/100,
damage 25 and the HUD at 100/100; the encounter loaded without a catalog API.
The offline persistence test also passed with an explicitly unavailable
character endpoint and the catalog URL unset. Read-only PostgreSQL inspection
confirmed the specified manual character remains at 100 XP/revision 1 in
`ashenfall_09b3_test` on the expected local cluster. No database writes occurred.
The earlier melee and health regression results below remain valid historical
results, but the melee run's accidental default catalog access is also recorded
below. No new direct or indirect access to the development database was intended
or required in this validation.

**Still unverified:** positive 105/105 player health, 27 actual attack damage,
HUD scaling, retry after reconnection and persistence during mid-encounter API
loss. The earlier automatic review blocked launching the mock API and the owner
instructed us not to bypass that block; no further attempt was made. The exact
manual procedure remains: launch the previously configured test-only API and
Godot editor with the same private process token, open
`res://scenes/world/CharacterPersistencePreview.tscn` with F6, check 105/105
and a 27-damage hit, take damage then stop the API and verify HP stays reduced.
Restart the scene with the API stopped, check gameplay remains blocked, restart
the verified test API and press Retry. Test movement, dodge and Guardian timing.
Do not treat the snapshot label alone as proof that damage was applied.

Additional files in this validation: `scripts/catalog/CatalogRuntime.cs` and
`docs/validate_standalone_isolation.gd`. No Git operations were performed.

---

The persistence preview now requests and validates a gameplay snapshot before
instantiating the dwarf playable demo. It assigns player maximum health and
fixed melee damage on the instantiated nodes before adding them to the scene
tree. `HealthComponent._Ready` therefore initializes current HP to that maximum,
and the existing health HUD reads the initialized health. The accepted snapshot
is retained for the encounter and is not refreshed during combat. Failure before
startup leaves the encounter absent and presents an error and retry button.
Restarting the preview scene requests a new snapshot. The standalone scene keeps
its existing 100 HP and 25 damage settings.

Persistence mode sets `UseFixedDamageOnly` to prevent the optional development
variable-damage environment flag from overriding the authoritative value. This
does not change standalone defaults or combat timing. The snapshot client's
existing validation handles identity, revision, version, hash format and stat
bounds; the hash is metadata and is not recomputed in the preview. A valid
snapshot is not a production authorization or multiplayer authority mechanism.

## Files

- `scripts/characters/CharacterPersistencePreview.cs`: load gate, retry, pinned
  snapshot and pre-ready configuration.
- `scenes/world/CharacterPersistencePreview.tscn`: packed scene reference instead
  of an eagerly instantiated encounter.
- `scripts/combat/MeleeDamageController.cs`: per-instance fixed-damage override.
- `docs/validate_persisted_offline.gd` and `docs/validate_persisted_gameplay.gd`:
  offline and successful initialization checks.
- This report.

## Validation and outstanding work

- Godot C# build: passed, zero warnings and errors.
- Offline persistence startup: passed; no encounter was instantiated.
- Existing player melee regression: passed, zero failures, including cancellation
  and movement semantics. Existing health/damage/death regression: passed, zero
  failures. Seven persisted-client tests passed.
- **Successful persistence-mode initialization was not executed.** The attempt
  to start a local mock server for the positive test was rejected by automatic
  review with only `blocked by policy`. `validate_persisted_gameplay.gd` is
  present but unexecuted. The requested real manual-character test, HUD visual
  scaling, disconnection during combat and manual gameplay are unverified.
- During the first melee regression the existing catalog autoload reported a
  successful connection to its default local API. Its backing database was not
  inspected and cannot be confirmed from this test; this is an unintended
  local-service access. Later checks explicitly redirected the autoload to an
  unavailable loopback port. No direct PostgreSQL access or database writes
  were performed during this milestone.

For manual validation use the established 09C.2B.1 setup and launch
`res://scenes/world/CharacterPersistencePreview.tscn` with F6. With the existing
level-2 unequipped character, verify HUD 105/105 and 27 damage against a
Guardian, then take a hit and check the maximum remains 105. Disconnect the API
after startup and check the encounter continues without resetting HP; stop and
rerun the scene while offline and check the retry/error screen prevents play.
Separately run `AshenfallDwarfPlayableDemo.tscn` and verify 100/100 and 25 damage.

Rollback by selectively reverting only the files above, preserving unrelated
uncommitted work. The standalone scene and database require no rollback.
