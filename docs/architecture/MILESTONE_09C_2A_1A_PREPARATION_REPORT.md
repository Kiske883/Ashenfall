# 09C.2A.1a — preparation in progress

**Update: isolated rebuild and snapshot validation executed.** The owner later
authorized writes and rebuilding `ashenfall_09b3_test`. The prior preparation-only
record below describes the earlier session; this section supersedes its NO-GO
and no-write statements, not its technical cautions.

### Execution and results

Preflight verified PostgreSQL 18.7, loopback `::1:5432`, database
`ashenfall_09b3_test`, owner `postgres`, cluster ID `7708337984335018212`, zero
other test-database sessions and zero player rows. A guarded maintenance-session
command dropped and recreated only that database. The catalog script and the
test persistence adapter were applied, along with database-local CONNECT,
schema USAGE and catalog SELECT grants. No global roles or configuration were
changed. The standalone test seeder connected as `postgres` using the local
credential mechanism, rechecked its target and empty schema, inserted and
validated draft definitions and extensions, calculated the V1 hash with the
existing code, and published in one transaction. The seeder is deliberately
not part of API startup and requires `--publish-isolated-test-fixture`.

The final published release is `snapshot-09c2a1-test`, SHA-256 V1
`a59cb3897f7764923b3e2c41c08d0ae618981e1cd8187835ceb428b6062c8438`.
This hash does not cover progression or modifiers. Final read-only checks found
one published release, two level rules, three modifiers, nine enabled named
guards, and zero account, character, owned item or equipment rows. The app
role has CONNECT and lacks database/schema CREATE and catalog release UPDATE.
The release and extension guards rejected changes in a transaction that was
explicitly rolled back. No development database was connected to or modified.

The isolated build completed with zero warnings/errors for the seeder; the
test-project build also succeeded using temporary output. The focused real
in-process HTTP/service concurrency suite passed **6/6**. The full backend
suite, with isolated live tests enabled and development-catalog tests disabled,
passed **61**, failed **0**, skipped **4**; the skips are the separate
development-catalog publisher tests, which remain outside authorization.
An initial new test failed because its XP update did not increment the required
row revision; the update and expected revisions were corrected, and the suite
was rerun successfully. Successful HTTP snapshots reported level 2, 105/27
unequipped and 121/30 equipped, and the concurrent actual reader retained the
old equipped revision while a fresh read observed the unequipped revision.
The transaction interceptor checked repeatable-read/read-only settings.
Unauthorized, absent account, unit missing/invalid progression, and test-row
cleanup are covered. The HTTP host is in-process, not an external TCP test; GET
repetition and read-only transaction checks are not an exhaustive audit of every
possible database write. No live malformed-progression fixture was created.

### Files and limitations

Created: `backend/Ashenfall.TestCatalogSeeder/Ashenfall.TestCatalogSeeder.csproj`
and `backend/Ashenfall.TestCatalogSeeder/Program.cs`. Modified from the prior
preparation: `backend/Ashenfall.Tests/SnapshotFixtureManifest.cs`,
`backend/Ashenfall.Tests/CharacterConcurrencyLiveTests.cs`, and this report.
The SQL adapter and fixture unit test listed below were created in the prior
session. The operator seeder currently uses the administrative `postgres` role
because verifying `pg_control_system()` requires privileged access; it is
explicitly test-only, fixed-target and opt-in. This deserves review before
reuse. Existing 09C.1 manual setup documentation still assumes the old
`test_class` fixture and must not be used unchanged. No Git operations were
performed. The synthetic published catalog remains in the disposable database;
removing it requires a separate authorized rebuild.

The remainder of this file is the prior preparation record.

## Created files

- `backend/MigrationReview/CharacterPersistence09C2A1.Test.sql`: a review-only
  adaptation of the 09B persistence SQL.
- `backend/Ashenfall.Tests/SnapshotFixtureManifest.cs`: an in-memory fixture
  specification, not a database seeder.
- `backend/Ashenfall.Tests/SnapshotFixtureManifestTests.cs`: a pure unit test.
- This report.

No existing source or migration was intentionally modified. Build artifacts
were generated. The owner prohibits Git operations, so working-tree status was
not inspected and no claim is made about other existing changes.

## Adapter and fixture

The adapter replaces the original production database preflight with checks for
`ashenfall_09b3_test`, PostgreSQL cluster system identifier
`7708337984335018212`, loopback, port 5432 and `postgres`. It requires the
initial migration, an empty catalog and four enabled catalog triggers and its
unique index. It locks catalog tables and rechecks emptiness before creating
persistence objects. The original schema definitions, indexes, functions,
triggers and grants from `CREATE TABLE accounts` through `COMMIT` were compared
as text and match the original exactly. It retains the original schema role
prerequisites. This static comparison is not a substitute for a PostgreSQL
execution or a full security review. Initialization of the original catalog SQL
can leave the migration history table after failure, as documented in the rebuild
plan. No database statement in the adapter has been executed.

The in-memory fixture derives from the existing draft seed, sets test release and
class identities, supplies level rules and the three requested sword modifiers.
The unit test uses `CatalogValidator`, `CatalogHash` and `CharacterRules` and
confirms 105/27 and 121/30. It is not yet a complete manifest for existing
equipment tests, and cannot currently be shared with a seeder. Hash V1 does not
cover progression or modifiers. Existing missing-progression tests are untouched.

## Validation

- Static text comparison of original and adapted SQL after the first table:
  identical.
- The normal test-project build failed because an existing API process had
  `Ashenfall.Api.exe` open; that process was not stopped. A separate temporary
  output build with `UseAppHost=false` succeeded.
- `SnapshotFixtureManifestTests` using that output: **1 passed**. This builds
  the API, publisher and test projects but is not the full regression suite.
- No PostgreSQL writes, migrations, publication, or live integration tests were
  run. The seeder, real HTTP tests, concurrent equipment snapshot tests,
  opt-in identity safeguards and existing test adaptation remain unimplemented.
  The existing all-zero-hash and missing-rules fixture assumptions therefore
  still prevent using the new fixture with the existing live suite.

## Remaining work and proposed operator sequence

Implement and review a separately invoked, guarded test-only seeder and shared
fixture manifest with all equipment definitions; reload and validate the draft,
compute its real V1 hash and publish in one transaction. Strengthen every live
writing test with opt-in and verified database/cluster identity, adapt the old
fixture-dependent tests without losing negative coverage, and implement the
requested HTTP and concurrent-read tests with exact-ID cleanup. Review and test
the adapter and all privileges before any rebuild.

The review-only operator sequence in
`docs/architecture/MILESTONE_09C_2A_1_TEST_DATABASE_REBUILD_PLAN.md` covers
identity/session checks, optional backup, controlled drop/create, schema and
grants, publication, live validation, cleanup and failure handling. Its missing
seeder and test commands still need to be supplied and reviewed. None is approved
for execution by this milestone.

For rollback, remove only the four files listed above after checking for later
work that depends on them; do not reset or overwrite unrelated files. There is
no database rollback because no database writes occurred. Do not proceed to
09C.2A.1b until the missing tooling and tests have been completed and reviewed.
