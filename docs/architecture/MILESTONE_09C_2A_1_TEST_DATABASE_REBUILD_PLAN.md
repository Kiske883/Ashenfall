# 09C.2A.1 — disposable test database rebuild plan

**Decision: NO-GO for execution today.** The owner has identified
`ashenfall_09b3_test` as disposable, but has not approved execution of this
procedure. Furthermore, the reviewed persistence script and the existing live
tests cannot be used unchanged. A test-only schema script, synthetic publisher
and updated tests must first be implemented and reviewed. The commands below are
proposals, not an execution log. No backup, database write, migration, fixture,
application change or Git operation was performed for this plan.

## Current evidence and constraints

On 2026-11-22 a read-only connection to `ashenfall_09b3_test` as `postgres`
reported PostgreSQL 18.7 at `::1:5432`, system identifier
`7708337984335018212`, database owner `postgres`, one published release, zero
accounts, characters, level rules and item modifiers, and zero other connections
at that instant. The latter is not a promise about future sessions. `postgres`
is a superuser; `ashenfall_app` and `ashenfall_publisher` are login roles without
superuser, CREATEDB or CREATEROLE. No connection was made to `ashenfall_dev`.

The initial catalog migration creates the four catalog tables, migration
history, two guard functions, four triggers and the one-published-release index.
The 09B persistence SQL creates six additional tables, four functions and five
triggers, including guards on published catalog extensions. Its preflight
*requires* `ashenfall_dev` and the approved published prototype hash. It is
therefore unsafe and guaranteed to fail on the requested target: **never run it
unchanged, or redirect it by changing environment variables**. Its current
precondition also conflicts with adding extension rows while the fixture is
draft. `InitialRpgCatalog.sql` creates the EF history table before its explicit
transaction, so a failed initialization may leave partial state.

The production publisher is likewise pinned to `ashenfall_dev`, its dedicated
publisher identity and a specific approved seed/hash; it can retire an existing
release. Do not invoke it for this test. Existing `CharacterConcurrencyLiveTests`
also require `prototype-1` with an all-zero hash and fixed synthetic IDs; the
09C.1 manual guide assumes `test_class`. A correctly hashed, complete rebuilt
fixture would break these assumptions, and some existing negative tests expect
missing rules. Preserve those tests' intent with separately designed fixtures
or mocks; do not silently skip or change expected outcomes.

## Required work before requesting execution approval

1. Review a **new test-only** persistence-schema script, derived from
   `backend/MigrationReview/CharacterPersistence09B.sql`. Its preflight must
   require the exact test database, the verified cluster, the single initial EF
   migration and an empty catalog, while retaining the reviewed schema,
   constraints, functions, trigger bodies and least-privilege grants. Its only
   lifecycle difference is permitting schema creation *before* fixture
   publication. Compare SQL objects and privileges against the reviewed original;
   do not weaken or disable guards. This script does not exist yet.
2. Review a separate opt-in test seeder, never invoked at API startup. It must
   verify the database, loopback host, port and independently confirmed cluster
   ID inside its own connection/transaction, check an empty catalog and exact
   schema/role permissions, then fail closed. Reuse
   `CatalogValidator.Validate`, `CatalogHash.Compute`, and
   `CharacterRules.ValidateCatalogProgression` on the actual persisted draft.
   Insert draft definitions and extension rows, validate them, compute and store
   the real V1 hash, and publish in one transaction. Never invoke or change the
   production publisher. This seeder does not exist yet.
3. Adapt the isolated live harness to the reviewed valid fixture and its real
   hash, including its fixed-ID dependencies, without relaxing target checks.
   Ensure missing-rules negative coverage remains meaningful. Review the test-only
   fixture manifest and update the manual preview guide as a separate later task.
4. Add tests for 200 snapshots, equipment modifiers and concurrent equipment
   updates; arrange exact-ID cleanup. Review all proposed code and commands,
   including any test-only privileges, before a separate execution approval.

## Synthetic fixture specification

Use a collision-checked, test-specific release/version and UUID manifest. The
published fixture must contain `dwarf_warrior` / visual `dwarf_berserker`,
`fallen_guardian` / `fallen_guardian_v3`, `training_battle_axe` /
`dwarf_battle_axe`, and `guardian_sword` / `guardian_sword`. Include any
additional definitions needed for the existing equipment regressions, with
compatible slot, handedness, quantity and visual values. Validate the complete
snapshot with the existing validator rather than assuming these four suffice.

Set dwarf base health 100 and damage 25, level 1 at XP 0 with zero bonuses,
level 2 at XP 100 with health +5 and damage +2, and zero allocated points.
The owned one-hand test weapon supplies flat health +5, health +10%, and flat
damage +3. An unequipped level-2 character has **105 HP, 27 damage**; equipped,
`(100 + 5 + 5) * 1.10 = 121` HP and `25 + 2 + 3 = 30` damage. Apply
`CharacterRules`, not a second bespoke implementation. Ensure level-rule and
modifier constraints and all foreign keys are satisfied.

Record the real hash returned by `CatalogHash.Compute` and independently reload
and recompute it. V1 attests the original catalog tables; it does **not** attest
level rules or modifiers. Never use the old all-zero marker as a verified hash.
Keep the published catalog in the disposable database; guards prohibit deleting
it row by row. Only uniquely identified disposable player records are cleaned
up after tests.

## Proposed operational procedure — do not run yet

All commands below are for **future review and explicit approval**, not
authorization. Use a private PowerShell session, existing pgpass authentication,
and no secret-bearing arguments, output, files or transcripts. They target
loopback and name the database explicitly. Do not replace failed checks with a
different target. Repeat preflight immediately before any write; inspect and
stop any API/test process separately. Owner approval must cover any termination
of other users' sessions. PostgreSQL 18 supports the syntax below.

### 1. Confirm identity and inspect active sessions

```powershell
Set-Location 'C:\Propio\Proyectos\Ashenfall'
$psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
& $psql -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d ashenfall_09b3_test -c "BEGIN READ ONLY; SELECT current_database(), current_user, current_setting('server_version'), inet_server_addr(), inet_server_port(); SELECT system_identifier FROM pg_control_system(); SELECT datname, pg_get_userbyid(datdba) AS owner FROM pg_database WHERE datname=current_database(); SELECT pid, usename, application_name, state FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid(); COMMIT;"
if ($LASTEXITCODE -ne 0) { throw 'Read-only preflight failed.' }
$verifiedSystemId = Read-Host 'Enter the independently verified cluster system identifier (not a secret)'
if ($verifiedSystemId -ne '7708337984335018212') { throw 'Cluster identity differs from this review; stop and obtain a new review.' }
```

Compare the server-reported identifier with the independently verified cluster,
database owner, version and endpoint. The identifier above is evidence from
this review, not a substitute for independent confirmation. Check role flags,
existing schema/trigger state and current data counts; stop on unexpected
connections, privileges or data. No environment variable alone proves identity.

### 2. Optional backup

If the owner elects a backup, choose a private, protected, **nonexistent** file
outside the repository. Review its contents and retention policy. Never print
credentials. Recheck the cluster and database immediately before this command:

```powershell
$backupPath = Read-Host 'Absolute path for a new private backup file'
if (-not [IO.Path]::IsPathRooted($backupPath)) { throw 'Use an absolute path.' }
$backupPath = [IO.Path]::GetFullPath($backupPath)
$repoPath = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
if ($backupPath.StartsWith($repoPath, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $backupPath)) { throw 'Backup path must be new and outside the repository.' }
& 'C:\Program Files\PostgreSQL\18\bin\pg_dump.exe' -w -h localhost -p 5432 -U postgres -d ashenfall_09b3_test -Fc --no-owner --no-privileges -f $backupPath
if ($LASTEXITCODE -ne 0) { throw 'Backup failed; stop.' }
& 'C:\Program Files\PostgreSQL\18\bin\pg_restore.exe' --list $backupPath
if ($LASTEXITCODE -ne 0) { throw 'Backup archive could not be listed; stop.' }
```

Listing checks archive readability only, not a successful restore. This archive
omits owners and grants; restoring it requires a separately reviewed role/grant
procedure, a verified target and fresh authorization. It is not automatic.

### 3. Disconnect, drop and recreate

Prefer stopping the specific test clients cleanly. If any remain, stop and obtain
explicit approval for each PID; recheck its database, role and application just
before issuing `SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE
datname='ashenfall_09b3_test' AND pid=<approved_integer_pid> AND
pid<>pg_backend_pid();` on the verified cluster. Never use a broad termination
or `DROP ... WITH (FORCE)`. Recheck zero sessions. The following uses the
`postgres` maintenance database because a session cannot drop its own database;
it never connects to `ashenfall_dev`. It stops if the existing database is not
owned by `postgres`, if sessions exist, or if cluster identity changes:

```powershell
@'
\set ON_ERROR_STOP on
SELECT current_database()='postgres' AND current_user='postgres'
 AND inet_server_port()=5432
 AND inet_server_addr() IN ('127.0.0.1'::inet, '::1'::inet)
 AND (SELECT system_identifier::text FROM pg_control_system())=:'expected_system_id'
 AND EXISTS (SELECT 1 FROM pg_database WHERE datname='ashenfall_09b3_test'
             AND NOT datistemplate AND pg_get_userbyid(datdba)='postgres')
 AND NOT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname='ashenfall_09b3_test') AS safe_to_drop \gset
\if :safe_to_drop
DROP DATABASE ashenfall_09b3_test;
\else
SELECT 1/0;
\endif
SELECT current_database()='postgres' AND current_user='postgres'
 AND inet_server_port()=5432
 AND (SELECT system_identifier::text FROM pg_control_system())=:'expected_system_id'
 AND NOT EXISTS (SELECT 1 FROM pg_database WHERE datname='ashenfall_09b3_test') AS safe_to_create \gset
\if :safe_to_create
CREATE DATABASE ashenfall_09b3_test OWNER postgres TEMPLATE template0;
\else
SELECT 1/0;
\endif
'@ | & $psql -X -w -v ON_ERROR_STOP=1 -v "expected_system_id=$verifiedSystemId" -h localhost -p 5432 -U postgres -d postgres -f -
if ($LASTEXITCODE -ne 0) { throw 'Rebuild stopped. Inspect exact state; do not retry blindly.' }
```

This is irreversible once DROP succeeds; a failure before CREATE can leave no
test database. Existing connections can race with preflight; PostgreSQL's DROP
then fails rather than force-disconnecting them. Do not automatically retry.

### 4. Schema and permissions

After rechecking identity and blank state, initialize the existing catalog SQL
in the **same psql session** as the guard below. Then run the separately reviewed
test-only persistence script with an equivalent in-session guard and empty-catalog
preconditions. The latter is not yet available; do not substitute the original
09B script. Both operations require schema-administrative `postgres`, never
the runtime role. Do not run EF migrations automatically.

```powershell
@'
\set ON_ERROR_STOP on
SELECT current_database()='ashenfall_09b3_test' AND current_user='postgres'
 AND inet_server_port()=5432
 AND inet_server_addr() IN ('127.0.0.1'::inet, '::1'::inet)
 AND (SELECT system_identifier::text FROM pg_control_system())=:'expected_system_id'
 AND to_regclass('public.catalog_releases') IS NULL
 AND to_regclass('public."__EFMigrationsHistory"') IS NULL AS safe_to_initialize \gset
\if :safe_to_initialize
\i backend/MigrationReview/InitialRpgCatalog.sql
\else
SELECT 1/0;
\endif
'@ | & $psql -X -w -v ON_ERROR_STOP=1 -v "expected_system_id=$verifiedSystemId" -h localhost -p 5432 -U postgres -d ashenfall_09b3_test -f -
if ($LASTEXITCODE -ne 0) { throw 'Catalog initialization failed; inspect partial state.' }
```

The future test-only script and seeder require their own exact reviewed command
and transaction guard; no executable command is invented here for files that do
not exist. Required database-local grants: revoke PUBLIC database and public
schema privileges; grant CONNECT and schema USAGE to the app and publisher;
grant the app SELECT on the four catalog tables and only the persistence grants
in the reviewed 09B SQL; grant publisher only the catalog/extension rights
needed for draft creation/publication. No app schema CREATE or catalog DML; no
global ALTER ROLE, passwords or privileges on other databases. Verify actual
effective privileges, role memberships, ownership and PUBLIC inheritance after
creation. Role and ownership details must be part of the new script review.
There is deliberately no claim that the old grants alone provide all seeder
rights. Never change cluster-wide defaults to make this work.

### 5. Publish, test, and clean up

After the reviewed test-only artifacts are approved and the schema verified,
run the opt-in seeder as its restricted test publisher using private credentials.
Verify the full hash, all enabled guards and one published release. Create a
unique disposable account, character, owned item and equipment with a ledger of
exact IDs; never use the owner's account. Configure a separate Development API
process against `ashenfall_09b3_test` and `ashenfall_app`, using pgpass and a
private process token, and ensure both EF contexts independently verify the
database and cluster. Bind only loopback and confirm the listener belongs to that
process. Do not log tokens or connection strings. The API config and live-test
commands must be reviewed alongside the future fixture harness; currently there
is no implemented successful-fixture command to claim as reproducible.

Run backend builds and the existing isolated tests with their explicit opt-in;
keep publisher tests targeting the development catalog disabled. Add real HTTP
200 checks for exact identity, revision, release/version and computed hash;
assert 105/27 unequipped and 121/30 equipped. Pause the actual repeatable-read,
read-only snapshot reader after its first read, commit unequip via the real
service on a second connection, then assert the old complete 121/30 revision and
a fresh complete 105/27 revision. Also exercise missing/invalid progression,
unauthorized and missing character cases, and verify GET does not write.
Distinguish in-process HTTP from external loopback transport and run both if
available. Do not run any tests against the development database.

Within a single transaction, cleanup must verify the ledger's exact account,
subject, character and item relationships, then delete equipment, owned items,
character and account in dependency order. Assert expected affected counts;
rollback and report on mismatch. Never use broad DELETE, TRUNCATE or disable
triggers. Recheck no ledger IDs remain, catalog hash and guards remain intact,
and record results. The synthetic published catalog persists until a later,
separately approved rebuild.

## Failure handling and approval checkpoint

On any failed identity, permission, checksum, guard, fixture or count check,
stop all affected work and preserve evidence without secrets. Inspect whether
the DROP, CREATE, catalog initialization or publication committed before deciding
on recovery; never assume atomicity across database commands. No automatic retry,
cleanup beyond the exact test ledger, schema migration or restoration is
authorized. A failed published fixture may require another explicitly approved
whole-database rebuild because the guards prevent catalog deletion.

**Remaining dependencies:** reviewed test-only schema adapter and seeder,
updated fixture-compatible regression tests, complete executable publication
and test commands, independent cluster verification, and explicit owner approval
of the destructive operation and any required session termination. Until those
exist, this plan is **NO-GO**, and no live successful snapshot or concurrency
validation is claimed.
