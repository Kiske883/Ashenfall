# 09C.4 — catalog balance management implementation

## Final closure — 2026-11-26

**GO — 09C.4 manually accepted and closed.** The owner confirmed the full
5% -> 100% -> 5% workflow through the CLI, PostgreSQL, authenticated Gameplay
Snapshot V2 and Godot combat, including forward restoration and historical
release preservation. This final status supersedes the earlier "manual
acceptance pending" wording retained as execution history below.

Read-only CLI inspection independently confirms the current active release
`a5897962-2202-4fe1-b7bb-99a12597ab5d`,
`critical-validation-restored-20261125`, V2
`af01cb185a781e87a1619ca10d44f3550d841bdf904cb1c9de63c91f9fdd979e`.
The existing character keeps its original identity/class, XP 100/revision 1,
level 2, HP 105, damage 27–27, chance 5%, bonus +100%. Original V2 and temporary
100% releases remain retired with preserved hashes. No additional database
mutations or live write tests were performed during closure.

Investigated the reported missing `After` **before editing**. Actual `plan` and
`restore-plan` stdout, saved JSON and subsequent `validate` all contained
`Before=5, After=100`, with unchanged target hashes. The restore reproduction
used historical 100% content as a read-only candidate from the currently restored
5% release, ensuring a nonempty difference without activating anything.
Restoring original 5% content now correctly produces `NoOp=true` with no
differences. No serializer defect was reproduced; terminal/capture truncation
is consistent with the evidence, but the exact original display cause cannot
be established retrospectively. No C# serialization, plan format or hash change
was made or needed. The guide explains inspecting saved JSON.

Added `backend/Initialize-AshenfallBalance.ps1`: dot-source to register a
closure-backed `Invoke-AshenfallBalance` function with paths derived from script
location, lazy isolated build, content-signature cache, clean CLI JSON stdout
and explicit native exit-code handling. Loading does no build/database work;
no command or publication flag is injected. Validated parsing, use from another
directory, first build/cache reuse, fresh session and StrictMode, nonzero CLI
exit propagation and read-only JSON output. The wrapper never reads `.env.local`
or token configuration and leaves CLI guards intact.

Updated the CLI and manual preview guides with the actual development header
`X-Ashenfall-Dev-Token` (not Bearer) and a PowerShell GET example that reads
`ASHENFALL_DEV_IDENTITY_TOKEN` from Windows user environment into an in-memory
header without printing/persisting it. The example was not executed against a
new API instance during closure; real HTTP/Godot acceptance is owner-confirmed.

Final automated results: backend **98 passed, 0 failed, 15 skipped, 113 total**,
all database-writing opt-ins disabled. The two previously approved live balance
tests retain their earlier 2/2 passing evidence; they were not rerun. Backend,
CLI and Godot builds passed with zero warnings/errors. Standalone headless
isolation check passed with actual 100/100 HP and 25 damage without an API.

Closure files: the new initializer; this implementation report;
`CATALOG_BALANCE_CLI_GUIDE.md`; `MILESTONE_09C_1_MANUAL_PREVIEW_SETUP.md`; and new
`MILESTONE_09C_4_FINAL_REVIEW.md`. No gameplay/backend business logic, database,
migration, schema, published configuration or credentials were changed. No Git
operations were performed.
See [final review](MILESTONE_09C_4_FINAL_REVIEW.md) for current hashes, acceptance
attribution, remaining limits and technical debt.

## Authorized live validation — completed

**GO for owner-controlled manual acceptance.** The two authorized PostgreSQL
integration tests now pass, and independent fresh-connection checks prove their
catalog/player changes were rolled back. This section supersedes the pending
write-test status in the original implementation record below. It does **not**
authorize persistent publication: the manual 100% plan remains unapplied.

### Preflight and rollback scope

Before running tests, verified PostgreSQL **18.7**, actual database
`ashenfall_09b3_test`, server `::1:5432`, system identifier
`7708337984335018212`, all twelve expected enabled integrity triggers and the
valid unique published-release index. The existing CLI independently recomputed
both canonical hashes and the existing character's effective snapshot.

Original and final baseline:

- Active `91000000-0000-0000-0000-000000000001`, `snapshot-09c3-v2`.
- V1 `a33262049a29f4bde6d8e62d2f1d3837ed474a79d163b806917c4dde7fc6e5e1`.
- V2 `d1ef0a212b83df17300088210d081a63951621b2602e7ff35cf2a711c4de7d4d`.
- Character `d9a62a3c-4996-4276-8ab8-2c4d6f2ec988`, original class
  `90000000-0000-0000-0000-000000000002`, **100 XP, revision 1**, level 2,
  HP 105, damage 27–27, critical chance 5%, critical bonus +100%.

Reviewed the complete call path before execution: tests call only
`ApplyInTransactionAsync`, which delegates to
`V2CatalogPublication.PublishInTransactionAsync`. Neither commits the supplied
transaction. Both tests own explicit outer READ COMMITTED transactions with
rollback in `finally`. In the concurrency test both connections own their own
outer transaction; neither calls the committing `ApplyAsync` entry point.
Read-only inspection transactions can complete normally, without catalog writes.
The test runner enabled only `ASHENFALL_09C4_WRITE_TESTS`, selected exactly
`BalanceManagementLiveTests`, and removed the opt-in afterward. Other live-test
flags remained disabled. No manual apply, persistent release, character creation,
deletion, migration, schema change, role change or trigger disabling occurred.

### Executed tests and correction

Initial authorized run: **1 passed, 1 failed, 0 skipped**. Publication/restoration
passed; concurrency failed during cleanup because `NpgsqlTransaction.Connection`
was still non-null after rollback. The cleanup incorrectly treated that as an
active transaction and attempted a second rollback, raising:
`This NpgsqlTransaction has completed; it is no longer usable.`

A fresh read-only audit immediately after that run matched every captured row
fingerprint, confirming no persistent data change. Corrected only the test
cleanup: track successful rollback explicitly, and use nested `try/finally` so
secondary rollback is reached even if first-transaction cleanup fails. No
production service, integrity check or assertion was weakened.

Also strengthened the same tests with direct database lock observation, explicit
restored HP/damage/bonus/revision assertions, canonical hash verification,
exactly-one-active verification, a no-op release-count assertion and nonsecret
test evidence output. The concurrency assertion now queries `pg_stat_activity`
and `pg_blocking_pids`: the second connection must actually wait on the first
connection's `Lock/advisory`, rather than merely remaining unfinished for 200 ms.

Corrected run: **2 passed, 0 failed, 0 skipped**. Final instrumented run also:
**2 passed, 0 failed, 0 skipped**, approximately four seconds.

Final observed transactional evidence (all temporary IDs below disappeared
when the outer transaction rolled back):

| Check | Actual evidence |
| --- | --- |
| Draft creation and publication | `65a64704-41e0-48ef-91ed-fbe470910343`; V2 `9f1af6c32f79ae707099a32c811b0abbcdac48c1defcc8f2177ac61cbade41ee`; predicted and persisted/reloaded snapshot HP 105, damage 27–27, chance 100, bonus 100, revision 1 |
| Duplicate operation | Returned `AlreadyApplied`, without another release |
| No-op | Returned `NoOp`; catalog release count unchanged |
| Forward restoration | New `103d53d0-22f2-4a9c-8024-ab540c9a7e47`; V2 `767e4654e2e2eb0faa9669c2b2f06a3d30e72b107976048945ae92cd0857da21`; chance 5 and other effective values unchanged |
| Immutability and mapping integrity | Original release retained its hash and became retired only inside the test transaction; new active canonical hash/factory validated; exactly one published release |
| Invalid hash / stale plan | Rejected; current active restoration preserved; stale candidate absent after rejection |
| Concurrent serialization | Waiter backend PID 9572 was observed blocked on holder PID 27532 via advisory lock and `pg_blocking_pids`; waiter publication completed only after holder rollback |
| Secondary connection | Its publication also rolled back explicitly; no commit path was used |

These are actual service/publisher operations on PostgreSQL connections, not
SQL-only simulations or in-memory fixtures. The tests did not start a network
API or Godot encounter and do not claim new authenticated HTTP/visual acceptance.
The rejection cases prove savepoint handling of rejected operations and no
residual candidate; they do not introduce an artificial failure after every
possible individual SQL insertion.

### Independent before/after verification

Separate `psql` processes, using fresh read-only REPEATABLE READ transactions,
captured sorted-row JSONB SHA-256 fingerprints and counts before tests, after
the initial failure, and after all final live runs. All thirteen tables matched
exactly; trigger definitions/enabled states, active metadata, character metadata
and the valid uniqueness index matched too.

| Table | Before = after row count |
| --- | --- |
| `catalog_releases` | 2 |
| `character_classes` / `enemy_definitions` | 2 / 2 |
| `item_definitions` | 8 |
| `class_level_rules` / `item_stat_modifiers` | 4 / 6 |
| `catalog_v2_manifest` | 1 |
| `catalog_class_successors` / `catalog_item_successors` | 1 / 4 |
| `accounts` / `player_characters` | 1 / 1 |
| `owned_items` / `character_equipment` | 0 / 0 |

Persistence fingerprints, identical before and after:

```text
accounts:            233933893592570b41eaa228ae8095454533546b0da49f3e892a87cc82ab2521
player_characters:   ce94c10933b66dc900234007bce5300e57e0ed40da3c5f9f8b3ec0a4de9caaeb
owned_items:         4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945
character_equipment: 4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945
catalog_releases:    c5af93b60efbebe97b2a3acf1f1abc531b9ec4961001052c53bdddeb37a9d39f
```

The full catalog definition/extension/mapping table fingerprints also matched,
not merely their counts. Thus no temporary release, draft, definition, level,
modifier or mapping remained; no cleanup DELETE was needed. A further independent
CLI inspection returned the original effective 105/27–27/5/+100 snapshot.

Inventory and equipment were empty in this baseline. Their unchanged empty
fingerprints prove preservation here, not a new live equipped-character test.
Existing pure equipped/modifier/compatibility tests remain available; adding
live inventory fixtures would need separate scope/authorization.

### Regression, files and recommendation

After the test correction, the backend regression run with all database opt-ins
disabled reported **98 passed, 0 failed, 15 skipped, 113 total**. Those skips
include the two live balance cases already run successfully in the separate
authorized run; the other thirteen database-writing tests were not authorized.
Do not interpret that regression-run skip as missing execution of these two cases.
Godot C# build and backend/CLI/test builds passed, zero warnings/errors.

Changed in this continuation: `backend/Ashenfall.Tests/BalanceManagementLiveTests.cs`,
this report and the CLI guide's validation-status note. No production source or
Godot behavior changed; no Git operation or credential exposure occurred.

**GO for owner manual acceptance** using the guide's separately reviewed
persistent publication and restoration commands. Persistent commit recovery,
the actual authenticated 100%/restored API snapshots and the yellow-number
encounter remain for that manual acceptance. This continuation stops before
applying the manual plan; the original 5% release remains active.

## Original implementation record — before live-test authorization

2026-11-24. **Application services and CLI implemented; read-only PostgreSQL
planning verified. Real publication/restoration validation remains pending the
owner's authorization for test-database writes.** No release was activated,
retired or modified in this task. Do not interpret passing pure tests as proof
that the new database write path executed. The full milestone's publication
acceptance remains open until that path and the manual encounter are validated.

The owner reports 09C.3 manually accepted. Its combat formulas, Godot integration
and yellow-number renderer were not changed. No database schema, migrations,
global configuration, roles, credentials, character state or Git operations were
changed. No connection to `ashenfall_dev` was made.

## Created files

| File | Purpose |
| --- | --- |
| `backend/Ashenfall.CatalogPublisher/Balance/BalanceModels.cs` | Detached catalog/plan/result types, typed numeric parameter registry and deterministic identities |
| `backend/Ashenfall.CatalogPublisher/Balance/BalancePlanner.cs` | Pure edit/diff/no-op/restore planning, mappings, canonical hashes, effective-stat and inventory compatibility validation |
| `backend/Ashenfall.CatalogPublisher/Balance/BalanceManagementService.cs` | Verified read-only inspection and atomic PostgreSQL draft creation/publication through existing publisher |
| `backend/Ashenfall.BalanceCli/Ashenfall.BalanceCli.csproj` | .NET 8 executable referencing the existing publisher; no new package dependency |
| `backend/Ashenfall.BalanceCli/Program.cs` | Command parsing, JSON output, review files and execution confirmation |
| `backend/Ashenfall.Tests/BalanceManagementTests.cs` | 22 executed pure application-layer cases, including theory cases |
| `backend/Ashenfall.Tests/BalanceManagementLiveTests.cs` | Two opt-in rolled-back PostgreSQL apply/restore/concurrency tests; not executed |
| `docs/architecture/CATALOG_BALANCE_CLI_GUIDE.md` | Exact PowerShell command reference, acceptance procedure and safety limits |
| This report | Actual implementation and execution evidence |

No pre-existing source, project, scene, SQL or documentation file was edited.
Normal build/restore caches and temporary test binaries were generated. One
nonsecret reviewed JSON plan was created in the Windows temporary directory;
it was never applied. Preserve all earlier uncommitted work.

## Reuse and separation

The application layer resides in the existing publisher assembly, exposing
`BalanceManagementService`, `BalancePlanner`, `BalanceCatalog`, `BalancePlan`
and parameter definitions. It has no console/UI dependency and can be called
from a future authorized administration client. The new CLI handles arguments,
formatting and confirmation only. A graphical service/authentication architecture
is not implemented or implied by this local administrative tool.

There is **one** publication mechanism: `V2CatalogPublication.PublishInTransactionAsync`.
The new service prepares drafts and delegates activation to it. It reuses
`CatalogValidator`, `CatalogHash.Compute`, `CombatCatalogHashV2.Compute`,
`CatalogInventoryCompatibility`, `CatalogClassTransition`, `CharacterRules` and
`GameplaySnapshotV2Factory`. No parallel hash, combat formula, activation SQL
implementation or SQL editor was introduced.

## Supported content

The registry exposes existing class HP/damage/attributes, movement/timing,
V2 damage bounds/critical fields; enemy HP/damage/attributes and existing
timing/distance fields; existing level XP thresholds/budgets/bonuses; and existing
item modifier amounts. Owners and units are explicit. Integer values reject
fractions; numeric precision and dependent constraints are checked. Percentages
are entered as 5 for 5%, 100 for 100%, and 100 for a +100% critical bonus.
Descriptions distinguish class bases, progression/item modifiers and the
separately inspected effective snapshot.

Registry entries are compiled, typed property allowlists with units and bounds;
user input never supplies SQL or arbitrary reflection names. New implemented
parameters can be added to this registry without changing CLI parsing or
publication. Items/levels/modifiers are edited only where they already exist.
No creation of gameplay definitions, structural item editing, gems, skills,
spells, XP reward system or new formulas were added. A stored modifier code
still needs pre-existing gameplay semantics; the tool does not enable armor
mitigation or attack-speed mechanics.

## Plan, mappings and restoration

`plan` and `restore-plan` work on detached data from a read-only REPEATABLE READ
transaction. Both verify current V1/V2 hashes, build fresh deterministic release
and definition identities from the operation ID, copy all unrelated content,
validate existing persistence references and compute candidate hashes. No draft
is inserted while planning. A JSON plan records source ID/hash, historical
configuration ID/hash, operation/version, edits/diffs, mapping counts and an
optional explicit character's predicted V2 snapshot. It is not a publication
receipt or a real authenticated HTTP response.

Mappings use only persisted, explicit identity edges belonging to published or
retired releases, never names. Connected historical identities are converted
to direct source-to-candidate links so the existing runtime reader needs no
transitive traversal. Ambiguous components or incompatible code/category/slot/
handedness/stack definitions reject. All owned inventory, including unequipped
items, quantities and equipment ownership/slots are checked. All characters'
class/allocation compatibility is checked; active characters get factory
validation of their effective snapshots. Soft-deleted characters remain checked
for class/allocation/inventory compatibility.

Restoration clones selected historical V2 content into a **new** release.
Prior IDs/hashes remain intact; a retired row is never reactivated. The restored
canonical hash differs because release/version/definition/mapping provenance
differs. Player identity, original class IDs and owned item definition IDs stay
unchanged. A structurally incompatible restoration is rejected rather than
silently repairing players. V1 configurations without V2 metadata are not
silently upgraded; they remain visible in history.

No-op comparison ignores release provenance but covers authored numeric values,
metadata and item structure. A no-op reports the existing active snapshot/hash
and inserts nothing. Changed content uses fresh IDs; reviewed plans are not
overwritten on disk. Numeric history diffs plus the original plan and immutable
release history form the available audit trail; no new audit schema was added.

## Database queries, atomicity and safety

Every connection is rejected before opening unless it names loopback port 5432,
`ashenfall_09b3_test`, administrative `postgres`. SQL then checks the actual
database, local server address/port and system identifier
`7708337984335018212` before application queries/writes. No target comes from
`.env.local`, ambient connection variables, arbitrary CLI strings or fallback.
Credentials use existing private pgpass; no secrets are read into output or plans.
The same preflight verifies all twelve expected enabled guards and the valid
unique published-release index before draft insertion. A final real read-only
`status` run passed these strengthened checks and retained the baseline hash.

Actual reads use existing EF mappings for releases/classes/enemies/items,
characters, owned items and equipment, and explicit SELECTs for combat columns,
level V2 columns, modifiers, manifests and historical successor rows. They run
inside `SET TRANSACTION READ ONLY`. The service loads a consistent detached
view; no automatic startup task or migration is involved.

The implemented write path (not executed in this task) performs:

1. READ COMMITTED transaction and operation savepoint; identity verification,
   bounded lock/statement timeouts and public/pg_catalog search path.
2. The existing publisher's advisory lock keys and SHARE ROW EXCLUSIVE locks
   on the same catalog, extension, mapping and character/inventory/equipment
   tables, acquired before state reload. All relevant writers are excluded;
   ordinary readers remain supported by MVCC.
3. Reload and verify active source ID/hash, configuration hash, target hash,
   diff, mappings and selected character prediction. If source publication or
   selected revision/equipment/stat prediction changed, reject the stale plan.
   Other characters/inventory are revalidated against the current state too.
4. Detect a previously completed operation by deterministic release ID, version,
   valid canonical hash and published/retired state. Return `AlreadyApplied`
   without reactivation. Different content/version collision rejects.
5. Insert draft release and copied classes/enemies/items using existing EF
   mappings; parameterized statements populate V2 combat fields, all progression
   fields, modifiers, successor rows and `catalog_v2_manifest`. Only candidate
   definitions are updated while draft; player rows are never written.
6. Call the existing transactional V2 publisher on those persisted rows under
   the same transaction/locks. It rechecks guards, hashes and compatibility,
   then performs its existing retire/publish transition and exactly-one-active
   check. The new layer never performs lifecycle UPDATEs itself.
7. Reload and verify the active canonical hash and optional effective snapshot,
   and compare full character/owned-item/equipment fingerprints before commit.
   Any exception rolls back preparation and publication; public outer entry
   point rolls back the transaction, and caller-owned entry point rolls back
   its operation savepoint.

`apply` needs explicit `--apply` for unattended use or interactive typed database
confirmation. It prints the target first. Provider errors are redacted to their
type; tokens and secret-bearing connection strings are never printed. No grants,
DDL, trigger disabling, database lifecycle command or unrestricted editor exists.

## Actual execution evidence

Executed real read-only CLI `status`, parameter filtering, `inspect`, `diff`,
`plan` and `validate` against the verified existing test database. Current state:

- Active ID `91000000-0000-0000-0000-000000000001`, `snapshot-09c3-v2`.
- Stored V1 hash `a33262049a29f4bde6d8e62d2f1d3837ed474a79d163b806917c4dde7fc6e5e1`.
- V2 hash `d1ef0a212b83df17300088210d081a63951621b2602e7ff35cf2a711c4de7d4d`.
  Both recomputed successfully through the existing implementations.
- Character `d9a62a3c-4996-4276-8ab8-2c4d6f2ec988`, original class
  `90000000-0000-0000-0000-000000000002`, XP 100, revision 1, level 2,
  HP 105, damage 27–27, critical chance 5, bonus 100.
- Actual read-only plan operation `290c18a1-7c33-46f7-9f2e-e28b2822e78c`, version
  `review-critical-100`, predicted hash
  `78bc0b323c54f6b58a1101714f18d0267620a450f7c86662d0caeeb657f9e124`.
  One diff: `class/dwarf_warrior/critical_chance_percent`, 5 -> 100;
  two class/eight item mappings; predicted HP 105, damage 27–27, bonus 100 and
  revision 1. The independent `validate` command succeeded. **This release
  does not exist in PostgreSQL and was never activated.**
- Final read-only inspection still returns the original baseline above.
  No INSERT, UPDATE, DELETE, DDL or publication command was executed.

## Tests and build results

Final backend suite: **98 passed, 0 failed, 15 skipped, 113 total**.
The 22 new pure cases test discovery/units/percentage-point conversion,
single/multiple edits, bounds and precision, unsupported/duplicate edits,
read-only input preservation, no-op detection, deterministic draft IDs and
JSON plan replay, stale source/tampered plans/changed selected revision,
canonical hashes, historical mapping completeness/ambiguity, inventory/equipment
compatibility, 5 -> 100 -> 5 forward restoration, persistence fingerprints and
target/intent rejection before a connection is opened.

That forward scenario is an **in-memory application-layer simulation**, not a
PostgreSQL publication or authenticated HTTP round trip. No such substitution
is claimed as live validation. Existing V1/snapshot/authentication/combat tests
remain in the executed backend suite.

Skipped tests: four existing development publisher tests, five existing live
character/concurrency tests, four existing live V2 publisher tests and two new
live balance tests. All require database-writing opt-in; none were enabled.
The two new tests are implemented to exercise real transactional apply/restore,
duplicate/no-op behavior, rejection rollback, character preservation and two
connection publication-lock serialization, with outer rollback in finally.
They have compiled but have not run. Their success is an outstanding requirement,
not a passing result. Existing write tests were not redirected to another DB.

Builds: Godot C#, backend API/publisher/tests and new CLI passed with zero
warnings/errors. New CLI project assets restored successfully; no new package
references or external dependencies were introduced.

Godot headless checks passed:

- `validate_variable_damage.gd`: zero failures; normal/critical color and
  matched style, real impacts and transient cleanup.
- `validate_standalone_isolation.gd`: actual 100/100 HP and fixed damage 25
  without a catalog endpoint.
- `validate_counterattack.gd`: zero failures; four counters, Guardian defeat,
  player remains at 100 HP in the standalone encounter.

All Godot checks unset catalog API and variable-damage environment overrides;
they did not contact an unverified local API. No new rendered capture or manual
visual acceptance is claimed; the renderer was not modified.

## Remaining acceptance and operational limitations

The [CLI guide](CATALOG_BALANCE_CLI_GUIDE.md) includes exact read-only commands,
separately authorized Phase A publication, Phase B forward restoration and the
opt-in PostgreSQL tests. It also specifies real authenticated HTTP checks and
the F6 scene restart. No owner database mutation is performed by this report.

No other verified disposable database compatible with the existing publisher's
hard target restriction was provisioned. The owner explicitly reserves writes
to the existing validation database. Consequently the new SQL insertion path,
transactional application/restore/concurrency tests, actual publication and the
authenticated 100%/restored snapshots remain **not executed**, not failed or
execution-policy blocked. Separate authorization is needed before those tests
or acceptance publications. No security or technical failure was bypassed.

The tool is implemented and ready for code review and read-only use. Full
09C.4 publication acceptance is not yet claimed; the database write validation
gap must be closed before treating it as proven operational publication tooling.

## Gameplay and rollback

The current V2 reader queries active release/hash per authenticated request in
read-only REPEATABLE READ. No cache or backend restart is needed for supported
stat changes. An already-running request/encounter may legitimately retain its
older snapshot. Start a new persistence encounter for a new release; no current
HP reset or mid-combat hot application is introduced. F9 does not authorize
changing the encounter's accepted combat statistics.

Legacy `/stats` and standalone equipment/inventory DTOs retain their prior
behavior; this milestone does not redesign them or claim that every existing
consumer now resolves all balance data through V2. Enemy/movement fields have
their separate catalog bootstrap consumers. No new mechanics or multiplayer
authority is implied by the CLI. Production publication identities/permissions
and a future GUI require separate scope; this local tool remains target-restricted.

Source rollback removes only these new files and local CLI/test build artifacts,
preserving previous files and all unrelated changes. No database rollback is
needed because no database writes occurred. After a future authorized publication,
restore through a newly validated forward release; never reactivate retired
releases, delete history or disable guards.
