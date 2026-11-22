# Milestone 09B.2 — live persistence validation

**Status: INCOMPLETE.** The migration is applied and substantial live checks pass,
but opposing-hand concurrency and isolated rollback execution remain unverified.
The bidirectional combat regression also logs Godot managed-handle errors.

## Database and migration

On 2026-11-15 the previous session applied the reviewed
`backend/MigrationReview/CharacterPersistence09B.sql` with PostgreSQL 18 `psql`,
`ON_ERROR_STOP` and the administrative `postgres` identity. Its transaction
completed with `COMMIT`. The present session verified `ashenfall_dev`, PostgreSQL
18.7, and did not rerun the migration. Before migration there were four catalog
tables and no six persistence tables; afterward all six are present: `accounts`,
`player_characters`, `class_level_rules`, `item_stat_modifiers`, `owned_items`,
and `character_equipment`. The existing EF history still contains only
`20261115111756_InitialRpgCatalog`: this is a standalone SQL migration, so a
future EF migration needs an explicit baseline.

PostgreSQL metadata verifies six primary keys; the expected foreign keys, unique
and CHECK constraints; the two partial active-character indexes; four trigger
functions; and five enabled triggers. `ashenfall_app` has schema USAGE but no
CREATE, SELECT on catalog tables, the reviewed persistence row privileges, and
no catalog INSERT/UPDATE/DELETE. The publisher has the reviewed extension
privileges but no schema CREATE. In the previous session actual application-role
attempts at schema DDL and catalog UPDATE failed with permission denied.

The catalog remains one published `prototype-1`, one class, one enemy and two
items. Its recorded hash remains
`a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17`.
The live API/catalog gameplay regression also loaded and verified that hash.

## Executed checks

* `ValidateCharacterPersistence09B.sql`: live PostgreSQL, transaction rolled
  back; character creation, update, stale revision, duplicate active character,
  soft deletion and replacement, quantity limits, equipment ownership and slot
  checks, equipped deletion and published class-extension guard passed. Some
  negative cases catch any SQL exception, so they establish rejection rather than
  a specific SQLSTATE.
* `TestCharacterConcurrency09B.ps1`: live committed disposable records; two
  simultaneous creates produced one active character and one unique conflict;
  concurrent revision updates produced one winner and one stale writer. Cleanup
  reported success.
* `TestCharacterApi09B.ps1`: isolated fresh API on loopback, private random
  process token and dedicated random subject. Live create, load, save, stale 409,
  missing-token 401, inventory, equip, unequip, stats, soft delete and replacement
  passed. A narrowly scoped administrative insert simulated a trusted item grant;
  this is not a test of `CharacterService.GrantItemAsync`. Cleanup removed the
  two test characters and their account. No test namespace accounts remained on
  the subsequent read-only check.
* Client-supplied experience increase was initially possible in the development
  save endpoint. `CharacterEndpoints.cs` now rejects changes to experience at
  that boundary; the live API test confirms a 400 response. Trusted internal
  persistence remains separate. This is a local development gate, not production
  authentication or a complete authority model.
* Backend isolated build: zero warnings and errors; backend suite: 46 passed,
  4 skipped, 0 failed. These counts include static and mocked tests and do not
  replace the live PostgreSQL results above.
* Godot build: zero warnings and errors; headless import and startup passed.
  First adventure, player melee and catalog gameplay scripts reported zero
  assertion failures. Bidirectional combat reported zero assertion failures but
  emitted six `System.InvalidOperationException: Handle is not initialized`
  errors from `ScriptManagerBridge` when fixtures were re-created; a separate
  rerun reproduced them. Treat that regression as unresolved.

## DELETE binding

The previous session observed an HTTP 500 from the old API executable because a
DELETE handler inferred a request body. The current source uses scalar
`expectedRevision` query parameters for both DELETE endpoints. Building current
source into `C:\Temp\Ashenfall09B2Build` and testing the fresh executable
confirmed both character deletion and equipment unequip succeed. The owner's
existing API was not stopped or replaced; restart/rebuild it separately when
appropriate to use current source.

## Remaining validation and limitations

* The published catalog has no off-hand item and allows only one published
  release. Both orders of two-handed/off-hand conflict and concurrent opposing
  hand operations have not been tested against PostgreSQL. Neither an existing
  release nor its content was modified to manufacture a fixture. A disposable
  isolated PostgreSQL environment with appropriate catalog fixtures is needed.
* Quantity above the authored limit, zero quantity, foreign ownership,
  incompatible slots, equipped deletion and one equip/unequip flow were exercised.
  A trusted service grant, stackable inventory, stale item update and all slot
  combinations still need independent live coverage. The SQL checks' broad
  exception handlers should be tightened to assert specific rejection causes.
* Rollback execution and concurrent rollback locking remain pending in an
  isolated database; the destructive rollback was not run against development.
* No schema correction was applied during this session. Catalog and existing user
  data were preserved. No test identities were left in the checked namespace.
* No GUI gameplay or production authentication validation is claimed.

## Files for this milestone

Existing validation files retained: `backend/MigrationReview/TestCharacterApi09B.ps1`,
`backend/MigrationReview/TestCharacterConcurrency09B.ps1`, and
`backend/MigrationReview/ValidateCharacterPersistence09B.sql`. This continuation
modified the API validation script to check the experience boundary and improve
cleanup on failed creation, and modified
`backend/Ashenfall.Api/Characters/CharacterEndpoints.cs` to enforce it. This
report is new. Existing work and Git state were preserved; no Git write operation
was performed.
