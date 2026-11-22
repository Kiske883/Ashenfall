# Milestone 09B.1 — persistence hardening and migration review

## Executive summary

The proposed six-table persistence migration was hardened for owner review. **It was
not executed.** PostgreSQL `ashenfall_dev` remains at the `InitialRpgCatalog` migration
with four catalog tables and the published, unchanged `prototype-1` release. The
development character routes remain disabled by default. Live persistence validation
requires separate migration approval.

## Issues found and corrected

| Finding | Correction |
| --- | --- |
| The original SQL had no database/catalog/role preflight and psql could continue after an error. | Added `ON_ERROR_STOP`, a transaction, short lock/statement timeouts, exact database, migration, catalog hash, role, privilege and object-absence checks. No `IF NOT EXISTS` masks drift. |
| The API used a body-inferred DTO on an equipment DELETE route; ASP.NET Core could not construct the route when the development gate was enabled. | Revision is a query parameter; a security test now initializes every gated route without database access. |
| EF mapped `LevelRuleRow.Level` to quoted `"Level"`, while SQL defines `level`. | Explicit lowercase mapping plus a test comparing every mapped property with the reviewed SQL. |
| Equipment compatibility, two-handed exclusions and stackability were only service checks. | Service checks now reject stackable/equipment quantities other than one. A proposed `BEFORE` trigger locks the character and enforces ownership, slot compatibility and hand exclusions for all runtime-role writes. |
| The service could not delete an inventory item, and SQL had no DELETE grant for `owned_items`. | Added a revisioned, transactional delete that refuses equipped items; proposed only the needed DELETE grant. An item trigger also blocks deletion of equipped items. |
| Inventory grants and quantity changes did not consistently serialize with equipment changes. | All trusted inventory writes lock the active character row, update its revision, and commit with the item write. Proposed item trigger locks the same row and checks authored stack limits. |
| Character and item revisions could be bypassed by direct runtime-role UPDATEs. | Proposed row guards require exactly one revision increment, immutable identity columns, published class/item references for new rows, and active ownership. |
| Progression validation did not check bounded awards or every class. | Added complete, consecutive per-class validation and bounded XP/level/bonus/attribute checks in code; SQL checks enforce row-local bounds. Cross-row completeness belongs in a future publisher for the next hash format. |

## Schema and permissions review

Live read-only inspection found PostgreSQL **18.7**, `ashenfall_dev`, one
`20261115111756_InitialRpgCatalog` history row, the expected UUID catalog keys and
`draft`/`published`/`retired` release model. `ashenfall_app` has `USAGE` but no `CREATE`
on `public`, and SELECT only on the four existing catalog tables. The publisher also
has schema `USAGE` but no `CREATE`. The SQL creates only the six reviewed tables.

| New table | Proposed `ashenfall_app` rights | Reason |
| --- | --- | --- |
| `accounts` | SELECT, INSERT | Resolve/create server-derived local identity. No account UPDATE or DELETE path. |
| `player_characters` | SELECT, INSERT, UPDATE | Create, revisioned save and soft delete. No hard delete. |
| `owned_items` | SELECT, INSERT, UPDATE, DELETE | Trusted grant, revisioned quantity update and explicit deletion. |
| `character_equipment` | SELECT, INSERT, UPDATE, DELETE | Atomic equip, replacement and unequip. |
| `class_level_rules`, `item_stat_modifiers` | SELECT | Read authored progression and modifiers only. |

`ashenfall_publisher` receives draft DML rights only on the two new authored extension
tables; their release guard rejects writes after publication or retirement. PUBLIC
gets no rights on the six tables or four new trigger functions. The runtime role gets
no schema CREATE, catalog DML or DDL. Existing grants are unchanged until approval.

## Integrity and concurrency

- One active character per account uses a partial unique index; a case-insensitive
  active-name index remains useful if the limit later increases. Soft deletion frees
  the active slot without erasing historical rows. Concurrent creates cannot both
  pass the unique index; the service converts the conflict to a state error.
- A composite `(owned_item_id, character_id)` FK plus unique owned-item ID protects
  ownership and one-slot-per-item. The slot primary key allows only one item per slot.
- Equipment and inventory operations lock the same character row. This serializes
  opposing hand updates, quantity changes, deletion and grants; a stale character or
  item revision is rejected. The proposed trigger checks the existing other-hand state
  before each equipment write. Stackable items are inventory only, including any
  authored stackable weapon/armor definition.
- Deleting an equipped item is rejected by service and proposed trigger. Unequip it
  first. The composite FK would prevent dangling ownership even if a future purge
  bypassed the service. Zero/negative quantities are rejected by SQL and service.
- Character/account identity and class-definition references cannot be changed by
  a character UPDATE. A new character must use a published class. The development
  endpoint derives account identity from private server settings, not a client ID.

PostgreSQL's `BEFORE` row-trigger behavior supports checking existing other-slot
rows before the new row is written. The character-row lock serializes concurrent
attempts; the same checks also remain in the service for clear API errors. See the
[PostgreSQL 18 trigger definition](https://www.postgresql.org/docs/18/sql-createtrigger.html)
and [visibility rules](https://www.postgresql.org/docs/18/trigger-datachanges.html).

## Progression and catalog compatibility

SQL enforces positive levels up to 1000, level 1 at zero XP, nonnegative XP, unique
XP thresholds per class, and bounded nonnegative awards. `CharacterRules` validates
consecutive levels, strictly increasing XP, overflow bounds and coverage of every
class before a future catalog publication. Current service reads also reject malformed
progression. `prototype-1` has no extension rows and keeps its explicit local level-1
fallback. Its SHA-256 V1 hash and data are unchanged.

The V1 hash does **not** include level rules or item modifiers. A future release must
use an approved schema/hash version that includes canonical, sorted extension rows;
its trusted publisher must validate every class and modifier before publication. The
API and Godot client must support that schema before activating such a release. The
current publisher publishes only the approved initial V1 seed and was not changed.

## Migration safety and rollback

Forward script: `backend/MigrationReview/CharacterPersistence09B.sql`. It is
psql-specific (`\set ON_ERROR_STOP on`) and has one `BEGIN`/`COMMIT`. Preflight locks
the relevant catalog tables in SHARE mode, rejects wrong database/history/hash,
missing roles or guards, unexpected existing persistence objects, and insufficient
administrative/schema rights. A failed statement before commit aborts the transaction.
No catalog table is altered or recreated.

Prepared empty-data rollback: `backend/MigrationReview/CharacterPersistence09B.Rollback.sql`.
It is **not authorized for execution**. Before commit, the forward transaction itself
rolls back. Immediately after a successful migration with all six new tables empty,
the separate rollback refuses any data and drops only its new tables/functions.
After real character or authored data exists, that script refuses to run; an owner-
approved data-preserving migration plan is required. No database or backup was made.

### Final rollback concurrency correction

The first rollback proposal checked all six tables for emptiness before it explicitly
locked them. A concurrent writer could potentially commit a row in that interval.
The corrected script now sets `READ COMMITTED`, checks object presence, then acquires
`ACCESS EXCLUSIVE` on **all six tables in one fixed order** before its separate
emptiness check. The lock order is `accounts` → `player_characters` → `owned_items` →
`character_equipment` → `class_level_rules` → `item_stat_modifiers`: parent before
dependent tables, with the same character-before-inventory/equipment order used by
the service. The locks remain held through every `DROP` and the final transaction
outcome. A lock timeout (5 s), statement timeout (30 s), missing object, external
dependency, or nonempty table stops the script without a partial committed removal.
The rollback has no `CASCADE` and drops no catalog table.

`SHARE` would block the `ROW EXCLUSIVE` locks used by INSERT/UPDATE/DELETE, but
`DROP TABLE` subsequently requires `ACCESS EXCLUSIVE`. Taking that required mode
up front prevents a later lock-upgrade deadlock. It also briefly blocks ordinary
reads; this is appropriate for an offline, empty-data rollback with short timeouts.
The second emptiness check runs after lock acquisition under `READ COMMITTED`, so it
sees transactions that committed while the rollback waited for locks. PostgreSQL
documents the [lock modes and conflicts](https://www.postgresql.org/docs/18/explicit-locking.html)
and that [LOCK holds its locks until transaction end](https://www.postgresql.org/docs/18/sql-lock.html).

No forward-migration defect was found in this final focused review. Its preflight
matches the inspected database, approved hash, roles and schema grants; six tables
are created in FK-safe order, four trigger functions attach five triggers, and runtime
and publisher grants remain restricted. Trigger functions set a fixed search path.
Catalog extension guards lock their parent release row to serialize draft edits with
publication. This review is static plus read-only metadata inspection; the forward
SQL and concurrent trigger behavior have **not** been server-executed.

The migration remains standalone psql SQL; it does not add an EF Core migration-history
row or a `CharacterDbContext` model snapshot. Future EF migration generation must first
baseline this reviewed schema explicitly. This is a documented tooling limitation,
not permission to run EF migrations against the current database.

## Validation

- Read-only PostgreSQL inspection: database/version/history, catalog columns and UUID
  relationships, existing guards, roles and grants verified. Persistence tables: **0**.
- Static SQL tests verify transaction/preflight markers, exact six-table scope,
  grants, trigger presence and operation types, the explicit six-table rollback lock
  preceding every emptiness check and DROP, absence of CASCADE, rollback data guards,
  and every EF-mapped column against
  the reviewed table definitions. These tests do **not** server-parse or execute SQL.
- Backend solution build to isolated output: **0 warnings, 0 errors**. The normal
  output is locked by an owner-running API process (PID 12444), which was left running.
- Backend suite: **46 passed, 4 skipped, 0 failed**. It covers progression, stat order,
  equipment compatibility, the default-off development route and gated rejection of
  absent/wrong/nonlocal tokens, plus existing catalog tests.
- Real PostgreSQL writes, concurrent creates, actual equipment transactions and
  rollback execution were **not run** because the migration is unapproved. No mock
  result is represented as a live integration pass.

## Post-approval PostgreSQL validation procedure — not executed

1. Close or leave idle any development API that could write character data. Using a
   schema-administrative identity with private local authentication, recheck
   `current_database()`, server version, `__EFMigrationsHistory`, the one published
   `prototype-1` hash, role/schema grants and the absence of all six new tables.
   Abort if any value differs from the preflight in the forward script.
2. After the owner explicitly authorizes the exact script, run from the project root
   in PowerShell (no password argument):

   ```powershell
   $psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
   & $psql -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d ashenfall_dev -f backend/MigrationReview/CharacterPersistence09B.sql
   ```

   A failure before COMMIT aborts the transaction. Do not rerun blindly: inspect the
   resulting schema and server error first. Do not run the rollback automatically.
3. Read-only verification: count exactly six new tables, their PK/FK/CHECK constraints,
   partial/unique indexes, four functions and five enabled triggers. Compare
   `information_schema.role_table_grants` with the permission matrix above; confirm
   `ashenfall_app` still has SELECT-only rights on the original catalog and no schema
   CREATE. Confirm `prototype-1` hash and row counts did not change.
4. With separate approval for test writes, configure the private development identity
   gate. Exercise create/read/save/soft-delete/re-create; attempt two simultaneous
   creates for one account and require one conflict. Test stale character and item
   revisions. Via a trusted server test path, grant, change quantity and delete owned
   items; verify zero/negative and over-stack quantities fail, and equipped deletion
   fails until unequipped. Test cross-character ownership, duplicate slot/item,
   stackable equipment, both orders of two-handed/off-hand equip and concurrent
   opposing-hand writes. Verify failed transactions leave inventory and revisions
   unchanged. Do not treat these as completed until observed against PostgreSQL.
5. In separately authorized test transactions, verify extension writes on published
   or retired definitions fail, and draft rows obey the guard; explicitly roll back
   negative cases. Run the existing catalog/API and Godot adventure regressions.
6. If an empty-data rollback is later approved, first verify all six tables empty.
   Run only the reviewed rollback script with the same `-X -w -v ON_ERROR_STOP=1`
   options and administrative identity. If any real data exists, preserve it and
   design a separate data-preserving migration instead.

## Files and approval checklist

Modified: `backend/MigrationReview/CharacterPersistence09B.sql`,
`backend/Ashenfall.Api/Characters/CharacterDbContext.cs`, `CharacterRules.cs`,
`CharacterService.cs`, `CharacterEndpoints.cs`,
`backend/Ashenfall.Tests/Ashenfall.Tests.csproj`, and
`backend/Ashenfall.Tests/CharacterAndCombatTests.cs`.
Created: `backend/MigrationReview/CharacterPersistence09B.Rollback.sql`,
`backend/Ashenfall.Tests/CharacterEndpointSecurityTests.cs`,
`backend/Ashenfall.Tests/CharacterMigrationStaticTests.cs`, and this report.
The earlier `MILESTONE_09B_CHARACTER_PERSISTENCE_REPORT.md` is updated separately.
This final correction modified only `CharacterPersistence09B.Rollback.sql`,
`CharacterMigrationStaticTests.cs`, and this report. The forward SQL, services,
catalog, gameplay and grants were not changed in the final correction.

Before any database operation, the owner must review the exact forward SQL, trigger
semantics, permission matrix, standalone-SQL/EF-history decision, and empty-data
rollback. Explicit authorization must name `ashenfall_dev` and the forward script.
After approved execution, run live create/load/save/soft-delete, concurrent create,
stale revision, inventory quantity/deletion, equip/unequip/two-handed, permission and
trigger tests. Do not enable development endpoints before migration validation.

**09B.1 — FINAL MIGRATION REVIEW READY FOR OWNER APPROVAL.**

**09B — LIVE PERSISTENCE VALIDATED is not claimed.**
