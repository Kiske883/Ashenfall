# Milestone 09B.3 — advanced persistence validation

**09B.3 — ADVANCED PERSISTENCE VALIDATED**, for the service-order PostgreSQL
transactions described below. The SQL harness models the service's checks; it
does not invoke the C# service itself under concurrency.

## Environment and fixtures

Created the disposable `ashenfall_09b3_test` database on local PostgreSQL 18.7,
owned by `postgres`. No user or character data were copied. The catalog baseline
was built using `InitialRpgCatalog.sql`; `Fixtures09B3.sql` creates a synthetic
class, one-handed and two-handed weapons, off-hand armor, a helmet and stackable
consumables as a draft, then publishes the synthetic release. Its all-zero hash
is an explicitly synthetic marker, **not** a verified content hash or the real
prototype hash. The production publisher intentionally refuses other database
targets and was not run. The database uses existing local roles and grants for
the schema under test, rather than new test credentials.

The forward persistence script was run only in the test database with two
test-only substitutions in the streamed SQL: its database guard and expected
hash. Trigger bodies, constraints and grants remained unchanged. The original
files were not edited. Before testing, `ashenfall_dev` had the expected published
`prototype-1` hash; it was never used as the target for test writes or rollback.

## Live checks

`ValidateEquipment09B3.sql` used a transaction rolled back at the end. It
verified both two-handed/off-hand operation orders, one-handed plus off-hand,
unequip/re-equip, cross-character ownership, incompatible slot, stackable item
and duplicate-item rejection, and unchanged inventory and character revision
after rejected writes. `Concurrency09B3.ps1` used independent PostgreSQL
connections: a two-handed insert held its transaction open while the off-hand
insert attempted to proceed. One committed and the other was rejected; the final
state contained only the main-hand item. Its account and items were removed.

The earlier 09B.2 live test also covered simultaneous character creation and
optimistic character updates; those are separate from the equipment races below.

## Rollback

The rollback script was streamed with its database-name guard changed to the
test database. With a test account present it refused and all six tables and
data remained. A test view depending on `accounts` caused DROP to fail; the
transaction restored the earlier drops, and the test view was then removed.
Holding an ACCESS SHARE lock forced the five-second rollback lock timeout; both
checked tables remained. In another run a five-second test-only pause was
inserted after the empty check: a concurrent account insert timed out waiting
for the rollback's ACCESS EXCLUSIVE lock. The rollback committed afterward.
An additional uninstrumented run removed exactly the six persistence tables and
four functions; all four catalog tables and the synthetic published row remain.
No rollback was run on `ashenfall_dev`.

After those rollback checks, the test-only forward SQL was reapplied in the
isolated database for closure testing. The database is retained with the synthetic
catalog and six empty persistence tables. No schema correction was applied to
the development database.

## Final concurrency closure

Executed `python backend/MigrationReview/ConcurrencyClosure09B3.py` against
`ashenfall_09b3_test`. Two independent `psql` connections were used for each
scenario. Transaction A acquired the character row lock and performed its
change, then emitted an explicit ready marker without committing. Transaction B
started and the harness polled `pg_stat_activity` until B actually waited on a
lock; only then did A commit. Both transactions used lock and statement
timeouts. The harness asserts the returned SQLSTATE **and message**, followed
by a fresh query of final character revision, equipment, item count, quantities
and item revisions. No scenario encountered a deadlock or unexpected error.

| Scenario | A commits | B outcome after lock wait | Final state |
| --- | --- | --- | --- |
| Same slot | Equip item one, character revision 1 → 2 | `40001`, stale character | One main-hand assignment to item one; both weapons quantity 1/revision 1 |
| Equip vs unequip | Replace item one with item two, revision 2 → 3 | `40001`, stale character | Item two remains equipped; no unintended removal |
| Equip vs quantity | Equip item one, revision 4 → 5 | `23514`, equipped-item guard | Item one remains equipped at quantity 1/revision 1; no partial change |
| Equip vs delete | Replace item one with item two, revision 5 → 6 | `P0001`, `unequip item before deleting it` | Item two and its equipment reference remain; both items exist |
| Stale item | Change stack quantity 2 → 3 and item revision 1 → 2; character 6 → 7 | `40001`, stale item | Quantity 3/revision 2; losing update did not commit |

The stale-character and stale-item `40001` errors and the equipped-quantity
`23514` error are explicitly raised by the **test harness** to model
`CharacterService`'s checks after it locks the character. The equipped-delete
`P0001` originates from the actual database trigger. Thus these tests demonstrate
real database locking and final states with the application's lock order, but
they do not independently prove the C# service raises a PostgreSQL SQLSTATE or
that arbitrary direct SQL using a different lock order is deadlock-free. The
service instead reports its own concurrency/argument exceptions. The stackable
consumable is tested for stale inventory updates and is rejected as equipment by
the earlier integrity script; the equipped quantity race uses a nonstackable
weapon and verifies its quantity and revision remain valid.

The five-race harness passed twice, including after strengthening final-state
checks. One intermediate parallel run correctly refused to start because the
opposing-hand harness had a temporary account open; it was rerun sequentially
after that account's cleanup. The equipment integrity SQL and opposing-hand race
were rerun and passed. The prior rollback evidence above remains applicable:
the rollback script was unchanged and was not rerun on development.

Backend isolated build: zero errors and warnings; backend suite: 46 passed,
4 skipped, 0 failed. After the tests, a read-only query found zero test accounts,
characters, owned items and equipment rows in the test database. The development
database retained its published `prototype-1` hash
`a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17`
and zero rows in its four player-owned tables. No test data or published catalog
was modified there. No Godot regressions were repeated because no gameplay code
changed.

## Files

Created `backend/MigrationReview/Fixtures09B3.sql`,
`backend/MigrationReview/ValidateEquipment09B3.sql`,
`backend/MigrationReview/Concurrency09B3.ps1`, and this report. The commands
also executed the reviewed initial catalog, persistence and rollback SQL in the
test database as described. Git was not written.

Final closure additionally created `backend/MigrationReview/ConcurrencyClosure09B3.py`
and updated this report. Useful regression commands are the Python command above,
`psql -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d
ashenfall_09b3_test -f backend/MigrationReview/ValidateEquipment09B3.sql`, and
`& backend/MigrationReview/Concurrency09B3.ps1`. SQL statements for each race,
the bounded synchronization and exact state assertions are in the Python file.
