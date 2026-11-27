# 09C.2A.2 — dedicated integration database audit

## 1. Executive summary

**GO for a separately approved design and provisioning plan; NO-GO for
provisioning or test execution in this audit.** A third database named
`ashenfall_integration_test` in the existing PostgreSQL 18 cluster can hold its
own independently published synthetic catalog and retain that fixture between
tests. This is logical database isolation, not a separate administrative or
failure boundary. It is the recommended local development option if the owner
accepts shared-cluster risk and approves the safeguards below. A separate
container/cluster is preferable for unattended destructive tests or stronger
isolation.

This audit created only this document. No database was created, changed or
deleted; no script, migration, publisher, test, Git command or fixture write was
run. `ashenfall_dev` was not connected to, and no private configuration or
credentials were read.

## 2. Verified repository and PostgreSQL facts

- Read-only metadata from `ashenfall_09b3_test`, connected as `postgres` to
  local PostgreSQL 18.7 on port 5432, showed the proposed database name absent
  from the cluster's shared database catalog at inspection time. This does not
  authorize creation and must be rechecked immediately before future work.
- The existing isolated database has the partial unique index
  `catalog_one_published` and enabled release, class, item, level-rule and
  item-modifier guards. The release trigger prevents deleting published/retired
  releases; derived-definition triggers permit changes only while draft.
- PostgreSQL connections select one database; tables, indexes, schemas and
  ordinary trigger definitions belong to that database. Thus the one-published
  index and its triggers in a new database would constrain that database's
  tables, not the existing test database's tables. Roles and database identities
  are cluster-wide, and superusers and shared server resources remain common.
  [PostgreSQL 18 schemas](https://www.postgresql.org/docs/18/ddl-schemas.html),
  [system catalog overview](https://www.postgresql.org/docs/18/catalogs-overview.html).
- Observed `ashenfall_app` and `ashenfall_publisher` are login roles without
  superuser, CREATEDB, CREATEROLE, replication or BYPASSRLS. They are still
  cluster-wide identities, not separate roles per database. No conclusion about
  their effective rights in other databases is drawn from this inspection.
- The only extension observed in the current isolated database was `plpgsql`.
  The reviewed schema scripts contain no `CREATE EXTENSION`; future fresh
  database/template and server prerequisites must be verified during an
  authorized setup, not assumed from this one database.

## 3. Third-database feasibility

A fresh database can have the same catalog table and trigger names, a separate
one-published index and its own published fixture. The schema's guards and
foreign keys can enforce the same rules there. A new database must not be cloned
from the development or existing test database: that would carry unwanted rows
and potentially a published release. A clean, reviewed template/baseline is
needed.

This is feasible only after separately approved database initialization and a
test-only publisher exist. A database name is not itself a security boundary
against a shared superuser, role membership, accidental configuration or
server-wide failure. `postgres`, shared storage/WAL, services and cluster settings
can affect all three databases. PostgreSQL notes the shared-WAL implications of
multiple databases in one cluster. [Database cluster overview](https://www.postgresql.org/docs/18/manage-ag-overview.html).

## 4. Security and privilege model — proposed

Use distinct cluster-wide test roles with no SUPERUSER, CREATEDB, CREATEROLE,
REPLICATION or BYPASSRLS, no membership in development/publisher roles, and
credentials supplied privately. Recheck existing names and effective privileges
before creating or granting anything. PostgreSQL grants `PUBLIC` CONNECT and
TEMPORARY on databases by default unless changed; reject an overly broad target
configuration and review effective access, role inheritance and `PUBLIC`
privileges rather than inspecting direct grants alone. Any changes to other
databases or global authentication configuration require their own explicit
authorization. [PostgreSQL 18 privileges](https://www.postgresql.org/docs/18/ddl-priv.html).

| Identity | Proposed minimum and lifetime |
| --- | --- |
| Provisioner / schema owner | Separately authorized administrative identity for database creation, schema initialization and exact grants only; not used by API or routine tests. Prefer a non-login object owner where feasible. |
| Test catalog seeder | CONNECT and schema USAGE; SELECT/INSERT and narrowly required UPDATE on integration catalog release/definitions and draft extension tables, plus necessary FK/trigger permissions. No DDL, no broad release DELETE, no access to other databases. Verify exact grants through real tests. |
| Test runtime | CONNECT and schema USAGE; catalog and rule/modifier SELECT; only the account/character/inventory/equipment row privileges actually required by the service. No schema CREATE or catalog DML. |
| Fixture setup/cleanup | Separate narrowly scoped test identity or controlled test-only routine for exact fixture account and owned rows; cleanup needs account/character DELETE which the current runtime role does not have. Never grant broad cleanup rights to the API. |

**Current compatibility constraint:** `backend/Ashenfall.Api/Program.cs` accepts
only username `ashenfall_app`. A genuinely distinct runtime login will not work
with the unmodified external API. Options for separate approval are: a test-host
DI adapter that binds both contexts to the dedicated role, or a narrowly
reviewed configuration/identity change. Reusing `ashenfall_app` with grants only
in the new database tests the production-shaped runtime but shares an existing
cluster-wide identity and cannot by itself guarantee no access elsewhere. The
choice and effective privilege audit are unresolved; do not silently loosen the
production guard. Existing TestServer tests explicitly replace both contexts,
but their `RequireIsolated` guard currently hardcodes `ashenfall_09b3_test`.

All future test entry points must fail closed on exact database name, host,
port, expected role, server/cluster identity and a separately provisioned marker
in the target; reject service parameters or environment overrides that can
redirect connections. Assert `current_database()`, `current_user` and server
identity before **every** mutating transaction. A marker must be independently
protected from a low-privilege test role; a name or a user-controlled marker
alone is not proof of server identity. Pin both EF contexts and verify resolved
connections before requests. No password/token in source, logs or arguments.

## 5. Schema initialization sequence — feasibility, not execution

1. After explicit owner authorization, verify exact server and absent database;
   create only the approved target with reviewed owner and database privileges.
   Verify no inherited PUBLIC privileges or unexpected template objects.
2. Initialize the original catalog schema using a **separately reviewed,
   integration-targeted** process derived from
   `backend/MigrationReview/InitialRpgCatalog.sql`. It creates the catalog
   tables, indexes, two functions/four triggers and inserts EF history row
   `20261115111756_InitialRpgCatalog` for the reviewed initial migration.
   The script creates `__EFMigrationsHistory` with `IF NOT EXISTS` **before**
   its main transaction; it is not a fail-closed blank-schema verifier and a
   failure can leave that table. A future adapter must preflight drift and
   handle/report partial initialization rather than masking it.
3. Initialize the six persistence tables and their four functions/five triggers
   from separately reviewed integration-targeted SQL. The existing
   `CharacterPersistence09B.sql` **cannot run unchanged**: it explicitly
   requires `ashenfall_dev`, the approved published `prototype-1` hash,
   particular roles/grants and prior catalog objects. Its embedded grants target
   `ashenfall_app`/`ashenfall_publisher`. Do not change these guards in place or
   fake a production hash to pass them. An integration-only adaptation needs a
   different, reviewed target and role/preflight strategy; decide whether it is
   applied after a real computed fixture publication or uses a separately
   reviewed schema-only preflight. Neither option was implemented here.
4. Verify catalog, persistence objects, functions, triggers and grants before
   enabling fixture publication. The persistence SQL is standalone and does not
   add an EF migration-history row. Decide and document an explicit baseline
   before future EF migrations; never run `EnsureCreated` or automatic
   migrations on startup merely to fill this gap.

Schema initialization and fixture publication are separable in principle, but
the current persistence script's published-catalog preflight imposes ordering.
The current scripts alone do not establish a ready-to-run third-database workflow.

## 6. Test-only catalog publication design — proposed

An independently reviewed, explicitly invoked test-only adapter—not the normal
API startup and not the existing publisher CLI—should construct a synthetic
draft using the existing `CatalogValidator.Validate`, `CatalogHash.Compute` and
`CharacterRules.ValidateCatalogProgression`. Include the required
`dwarf_warrior`/`dwarf_berserker`, `fallen_guardian`/`fallen_guardian_v3`,
`training_battle_axe`/`dwarf_battle_axe` and `guardian_sword`/`guardian_sword`
definitions, valid progression rules and valid equippable item modifiers. The
current catalog validator validates the original catalog definitions; the
adapter must also validate the extension relationships and values explicitly.

Within a bounded transaction and publication lock: verify the exact target,
role, schema and no existing published release; insert a draft and its original
definitions, level rules and modifiers while draft; validate and independently
recompute the V1 content hash from the complete original catalog snapshot; then
set hash, publication time and published status through existing triggers. On
failure roll back. On rerun, require exact fixture identity/content/provenance or
stop; do not retire an unexpected release. Do not reuse an all-zero marker or
the approved development hash for different content.

`CatalogValidator` and `CatalogHash.Compute` are reusable library code;
`CatalogPublication.PublishInitialAsync` is **not** directly reusable as this
publisher because it has development-database/role preflight, a fixed initial
seed and hash, and can retire an existing release. Reusing functions is a design
proposal, not a completed implementation.

V1 hashing covers the original class, enemy and item definitions, **not**
progression or item modifiers. The 09C.2A endpoint returns stored V1 metadata;
it validates its format but does not recompute it. Tests must independently
verify it and separately compare the expected extension rows. A future extension
provenance policy remains outside this milestone.

## 7. Immutable fixture lifecycle

Publish a reviewed fixture once and retain it as an immutable reusable baseline.
For every test, generate unique account, character, item and subject IDs and
record an exact ledger. Use the real service where appropriate. Cleanup only
those IDs, in equipment → owned item → character → account order, checking
ownership and affected counts in a transaction; the equipped-item deletion
guard requires unequipping first. Verify absence by exact identifiers, and on
failure roll back/report them. Never use broad DELETE, TRUNCATE or trigger
bypasses.

Changing fixture catalog content requires a separately approved lifecycle: the
existing unique-published rule and immutability prevent replacing/deleting it
casually. Options include a reviewed retirement/publication of a new version in
the dedicated database, retaining history, or provisioning a new explicitly
authorized disposable environment. Neither may affect an existing release in a
different database. The test fixture is not removed by deleting characters.

## 8. Pending snapshot validation readiness

With the proposed environment and additional tests, the expected case is
feasible: level 2 at XP 100; base HP 100 plus level bonus 5 and item flat 5,
then +10%, gives 121; base damage 25 plus level bonus 2 and item flat 3 gives
30. A service-driven unequip yields HP 105/damage 27 and increments character
revision. An instrumented real snapshot reader can pause after its first read,
allow the real service to commit an equipment change, and assert old coherent
equipped snapshot versus new coherent unequipped snapshot, along with
`REPEATABLE READ`/read-only settings.

Still needed: a test seeder and a guarded integration fixture; actual HTTP 200
contract/provenance checks; real equipment-update/read race; explicit
authorization/missing-character/invalid-progression tests, read-only and cleanup
verification. Existing unit tests and the isolated soft-delete race cover some
behavior; they do not establish successful PostgreSQL snapshots or extension
hash coverage. An invalid-rule test must not mutate the retained published
fixture; use independently designed transaction-local or alternate controlled
test cases consistent with guards.

## 9. Alternatives

| | Third database in existing cluster | Separate container or cluster |
| --- | --- | --- |
| Isolation | Database-local objects; shared superuser, roles, WAL, configuration and server resources | Separate administrative and failure boundary if ports, credentials, volumes and network are isolated correctly |
| Setup and resources | Less setup and overhead; requires exact guards and role/privilege review | More provisioning, storage and process management; PostgreSQL version must be pinned |
| Reproducibility | Persistent fixture is convenient but shared server state can drift | Disposable baseline is easier to reproduce in automation; requires controlled image/binary provenance |
| Cleanup | Exact row cleanup; published fixture retained; database disposal needs explicit approval | Exact row cleanup or separately authorized disposal of a verified disposable instance/volume |
| Maintenance | Lower local burden; shared-cluster operations need care | Higher operational burden but stronger CI and future destructive-test fit |

Recommend the third database for routine local read/write integration checks,
conditional on owner acceptance and fail-closed test isolation. Use a separate
cluster/container if future automation needs to reset schemas, test destructive
rollback, run untrusted workloads or avoid shared-server blast radius.

## 10. Risks and unresolved decisions

- Owner approval is needed for provisioning, role design, schema baseline,
  test-only seeder and any future disposal. Test role names, authentication and
  precise grants have not been chosen or verified.
- `PUBLIC` and inherited privileges can permit access not visible from direct
  grants. Do not claim a dedicated cluster-wide role cannot connect elsewhere
  until effective privileges and server authentication are verified safely.
- Dedicated test-role compatibility with the API's fixed `ashenfall_app` guard
  needs an approved test-host or application strategy. External socket testing
  differs from in-process TestServer and remains to be designed.
- Catalog extension rows lack V1 hash coverage, and authored production
  progression is not supplied by this proposal. Do not change the approved
  catalog or infer a new provenance scheme.
- The old reviewed SQL files include hardcoded target/role/hash assumptions and
  the first script's non-atomic history-table creation. Derived scripts and their
  exact ordering must be independently reviewed before use.

## 11. Proposed implementation phases

1. Owner chooses local third database or separate cluster and approves exact
   provisioner, roles, privilege boundaries and target identity mechanism.
2. Prepare and review test-only schema initialization and privilege scripts,
   including failure/retry behavior and EF-history baseline. No automatic
   application startup migration.
3. Prepare and review the test-only seeder and hash/extension validation; test
   refusal on wrong targets and existing publication before any fixture write.
4. Under separately authorized execution, provision the isolated environment,
   publish once, run real API/service snapshot and concurrency tests, then
   verify exact scoped cleanup and the untouched existing environments.
5. Document results and decide fixture versioning. Do not start 09C.2B merely
   because the environment exists.

## 12. Rollback and disposal

Before a publication commits, a failed fixture transaction should roll back.
After publication, retain the immutable catalog and clean only ledgered test
character data. Database or cluster removal is a separate destructive action:
only after explicit owner authorization, verify the absolute target identity,
ownership, active connections and dependency/backup needs; never infer safety
from a similar name. Shared roles may have dependencies in other databases and
must not be dropped or altered without separate review. No disposal command is
authorized or executed by this audit.

## 13. GO / NO-GO

**GO** to prepare a detailed, reviewable implementation proposal for a third
database with the controls above. **NO-GO** to create it, run migrations, publish
fixtures or begin 09C.2B until the owner explicitly approves those operations
and unresolved identity, script and role decisions.
