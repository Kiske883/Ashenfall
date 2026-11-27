# Milestone 09B.5 — API concurrency validation

## 09C.1 review

The four skipped tests in the 49-pass run are `PublishedSnapshotIsConsistentAndRepeatIsNoOp`,
`ConcurrentReplaysAreSerializedAndIdempotent`, `InvalidDraftWriteIsRolledBack`,
and `PublishedReleaseCannotBeReopened`. They are gated by
`ASHENFALL_RUN_DB_TESTS=1` because they use the publisher role and the real
development catalog, including transactional write attempts. They were not run
for 09C.1, which forbids modifying development data.

The isolated HTTP host explicitly replaces both character and catalog EF context
registrations. A new fail-closed connection-string guard rejects other database
names, remote hosts, wrong roles and inline passwords. A non-database unit test
checks rejection, and the live test verifies both resolved context connection
strings before issuing requests. The HTTP transport remains in-process TestServer;
external network transport was not tested.

**Recommendation: PASS** for the isolated, opt-in scenarios listed below.
Real HTTP pipeline and C# service operations overlapped against PostgreSQL;
the development identity gate remains a prototype and the tests do not prove
that every possible ordering is safe.

## Successful live validation — 2026-11-22

The owner repaired the private pgpass configuration. `psql` authenticated as
`ashenfall_app` to `ashenfall_09b3_test`; live Npgsql connections then succeeded
for both the restricted application and administrative test setup roles. The
application role had USAGE without CREATE, SELECT-only catalog privileges and
the expected persistence permissions. The isolated database contained the six
persistence tables, five enabled triggers, synthetic fixture release and no
player rows before the final run.

With `ASHENFALL_09B5_LIVE=1`, the full backend test suite reported **49 passed,
0 failed, 4 skipped**, including all three new opt-in tests. The four skips are
pre-existing. Backend build passed with zero warnings and errors. Each same-row
race used a separate administrative connection holding the character row lock;
the test waited until independent application connections were observed in
`pg_stat_activity` waiting on locks before releasing it.

* **Real HTTP pipeline, in-process TestServer:** concurrent same-slot equips and
  concurrent two-handed/off-hand equips each returned one 204 and one 409. The
  tests verified a single valid equipment assignment, character revisions and
  unchanged inventory quantities and item revisions. A follow-up HTTP request
  with the current revision confirmed the incompatible opposing item is rejected
  with 400 and leaves the state unchanged. This exercises the real
  ASP.NET endpoints, but does not exercise an external network socket.
* **Direct real C# services:** independent EF contexts exercised equip versus
  quantity change, equip versus deletion of the equipped item, and two updates
  using the same stale item revision. The tests accepted the valid winner and
  expected argument/concurrency exception, checked the winning quantity and
  revision, retained equipment and item, and rejected zero/over-limit quantities.
* **Direct real C# services, different characters:** an equip for a second
  character completed while the first character's row was intentionally locked;
  after release the first completed, with both persisted at revision two.

The first post-authentication attempt failed because the lock observer reused
the holding transaction's cached PostgreSQL statistics snapshot. A separate
autocommit observer connection fixed the harness. One test record left by the
interrupted diagnostic run was deleted by its exact generated account ID and
subject. The initial HTTP setup also retained the original context registration:
account-lookup **reads** went to the configured developer database and returned
400; no writes to that database were issued. Both EF context registrations are
now explicitly rebound to the isolated connection in the TestServer. This is a
test-harness correction, not a production application change.

After the successful full run, a direct isolated-database query showed **zero**
accounts, characters, owned items and equipment; the synthetic catalog marker
remained unchanged. No migration, published-catalog update or Git operation was
performed. The developer database was not modified. The earlier SQL-only 09B.3
checks remain separate evidence and are not counted as C# service tests.

## Earlier blocked attempt — 2026-11-22

The owner configured access and catalog grants in the isolated database. A
read-only `psql` connection as `ashenfall_app` succeeded against
`ashenfall_09b3_test`: schema USAGE was present, CREATE absent, catalog SELECT
present, catalog writes absent, and persistence grants matched the runtime
contract. PostgreSQL 18.7, five enabled triggers and the synthetic catalog marker
were verified. No player-owned rows existed before the attempt.

The opt-in test invocation was then run with `ASHENFALL_09B5_LIVE=1` and the
`CharacterConcurrencyLiveTests` filter. **0 passed, 3 failed, 0 skipped.** All
three failed in `OpenAdmin` before any fixture setup: Npgsql raised
`System.FormatException: pgpass entry was not well-formed`. The stack trace
originates in `Npgsql.PgPassFile.Entry.Parse`. `psql` connectivity therefore
does not establish that Npgsql can parse the whole existing file. No file
contents or credentials were inspected or copied. The owner was asked to check
all non-comment entries locally, including escaping of literal colons and
backslashes. No new password file was created.

Afterward the isolated database still had zero accounts, characters, items and
equipment; its synthetic catalog marker was unchanged. Backend build passed with
zero warnings and errors. With live tests disabled, the full suite reported
**46 passed, 0 failed, 7 skipped** (three new opt-in tests and four earlier
skips). No real HTTP or service concurrency operation reached the database. The
test harness was not changed and `ashenfall_dev` was not accessed in that attempt.

## Architecture

`CharacterService` uses a separate EF context per request and transactions for
inventory and equipment mutations. It locks the character row first, then
checks revisions and equipment constraints. The development character API is
disabled unless its Development environment, explicit enablement, private token
and subject are configured; requests must originate from loopback. Its equipment
PUT and DELETE endpoints exist. Quantity updates and item deletion are trusted
service methods, not HTTP endpoints. Character conflicts map to HTTP 409 and
invalid arguments to 400.

## Original environment and blocker

Read-only checks found PostgreSQL 18.7, test database `ashenfall_09b3_test`, six
persistence tables, five enabled triggers, expected synthetic item definitions
and no player-owned records. Its synthetic catalog has the all-zero marker,
not the real catalog hash. The restricted `ashenfall_app` role failed to
authenticate to this database via the available private pgpass configuration.
Also, metadata shows the test database lacks the role's SELECT privileges on
the four catalog tables, needed by the service. The owner was asked to configure
private local authentication and authorize the missing isolated-database SELECT
grants. No credentials were requested in chat or recorded here. This milestone
does not authorize automatically repairing missing prerequisites, so neither
grants nor migrations were applied.

The real `ashenfall_dev` still has published `prototype-1` hash
`a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17`;
its player-owned tables had zero rows on read-only inspection. No write was
made to that database.

## Harness

`backend/Ashenfall.Tests/CharacterConcurrencyLiveTests.cs` adds three opt-in
xUnit tests. They use the actual `CharacterService`, separate EF contexts and,
for equipment scenarios, two independent `WebApplicationFactory` HTTP clients
with the development token and a loopback startup filter. The HTTP clients use
ASP.NET Core's in-process TestServer; they are real HTTP pipeline requests, not
socket transport. The harness preflights database identity and restricted-role
privileges before creating uniquely identified rows, then deletes only those
rows. A separate PostgreSQL connection holds the character row lock while the
test waits for the service requests to appear in `pg_stat_activity` as lock
waiters before releasing it; test commands have bounded timeouts.

The cases cover same-slot and opposing-hand HTTP races, actual-service
equip versus quantity change and equipped-item deletion, stale item revisions,
and isolation across different characters. The test
setup uses `postgres` only for fixture setup, lock coordination and cleanup;
requests and service contexts use `ashenfall_app`. No production token or
password is embedded in the harness.

To repeat the opt-in test run from the repository root in PowerShell:

```powershell
$env:ASHENFALL_09B5_LIVE = '1'
dotnet test backend/Ashenfall.sln --no-restore -v:normal -p:BaseOutputPath=C:\Temp\Ashenfall09B5Build\ --filter FullyQualifiedName~CharacterConcurrencyLiveTests
Remove-Item Env:ASHENFALL_09B5_LIVE
```

Inspect test outcomes and verify the isolated test accounts and inventory have
been removed. No SQL-only test can substitute for this run.

## Earlier validation before the successful live run

- Backend build succeeded, zero warnings and errors.
- Backend suite: **46 passed, 0 failed, 7 skipped**; three of the skips are the
  new opt-in live tests, four are pre-existing skips. No live HTTP or C# service
  concurrency scenario was executed.
- `ValidateEquipment09B3.sql` passed against the isolated database inside a
  rolled-back transaction. This is a SQL-only integrity check.
- No new application defect was confirmed or production code changed. The test
  harness and this report are the only files created in this milestone.
- No database migration, published catalog change, or Git operation was performed.

## Remaining risks

Dedicated race tests do not establish that every possible ordering is safe.
Authentication is a development-only gate and is not a production identity
system. External network transport and production deployment were not tested.
