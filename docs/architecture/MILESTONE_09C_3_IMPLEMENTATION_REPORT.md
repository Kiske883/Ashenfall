# 09C.3 — V2 active; real authenticated gameplay snapshot validated

## Final critical-number polish — 2026-11-22

Implemented and automatically validated; ready for final owner manual acceptance.
This focused continuation made no database connections, writes, migrations,
catalog changes, gameplay formula changes, scene changes or Git operations.
The activation evidence below belongs to the earlier execution, not a new run.

### Changes and signal path

- `scripts/world/AdventureCombatFeedback.cs`: critical numbers now use yellow
  (`Colors.Yellow`), numeric-only text and the normal font size of 60. Removed
  the critical text toggle and enlarged critical font. Normal dealt ivory and
  received crimson remain unchanged. Font, outline, pixel size, recipient height,
  billboard, upward movement, fade and lifetime are shared. Received criticals
  also retain their recipient position while using yellow. No extra effects or
  UI were introduced; the existing common impact effects are unchanged.
- `backend/Ashenfall.Tests/CharacterAndCombatTests.cs`: deterministic tests
  connect effective character combat statistics to the existing damage calculator.
  The reference 5% chance and +100% bonus produce 27 normal / 54 critical damage;
  random values immediately below and at the chance boundary prove strict chance
  handling. An equipment chance modifier changes the outcome for the same roll.
  These are test inputs, not new runtime constants.
- `docs/validate_variable_damage.gd`: exercises real confirmed impacts and their
  Label3D output. A normal 30 and critical 30 prove that presentation uses the
  critical result rather than inferring it from damage. Checks normal and received
  colors, yellow criticals, numeric-only text, identical style/position/lifetime,
  upward fade, cleanup and one number per hit. Uses the pre-existing isolated
  damage-profile test seam; no runtime debug mode or forced-critical option was
  added. Optional `--capture` saves actual rendered frames to Godot user data.
- This implementation report records the results and remaining manual checks.

The existing calculator returns the critical flag to `MeleeDamageController`
(or the Guardian controller), which sets `LastImpactCritical` before emitting
`DamageApplied` after successful health reduction. Feedback reads that flag,
deduplicates the impact sequence, and passes it separately from recipient and
actual applied damage to `SpawnNumber`. The renderer performs no damage or
critical calculation. Lethal-hit clamping does not change that flag.

### Actual validation in this continuation

- Godot C# `dotnet build --no-restore`: passed, zero warnings/errors.
- Focused backend `CharacterAndCombatTests`: 20 passed, zero failed.
- Complete backend suite (isolated output, database opt-ins disabled):
  **76 passed, 0 failed, 13 skipped, 89 total**. The skipped tests require database
  opt-in; none were enabled because this request prohibits database changes.
  The backend projects compiled successfully during the test command.
- `validate_variable_damage.gd`: headless and Vulkan Forward+ runs both passed
  with zero failures. Vulkan used the NVIDIA GeForce GTX 650. Both normal and
  critical screenshots were captured and visually inspected at 1280 x 800.
- `validate_milestone_08.gd`: zero failures, including impact deduplication,
  transient cleanup, defeat presentation, restart and three active Guardians.
- `validate_standalone_isolation.gd`: passed; actual player initialized with
  100/100 HP and fixed damage 25 without a catalog endpoint.

An initial color assertion included tweened alpha and failed after fading had
already started. The test now compares RGB independently and asserts fading
separately; both final headless and rendered runs passed. Production fading was
not changed to satisfy the assertion. All Godot checks unset the catalog API
endpoint and variable-damage environment override; they contacted no local API.

Actual rendered evidence is outside the repository in Godot's user data folder:
`%APPDATA%/Godot/app_userdata/Ashenfall/milestone09c3_normal_damage.png` and
`milestone09c3_critical_damage.png`. They deliberately show the same final damage
to isolate the color distinction, rather than claiming a live database combat run.

### Manual acceptance and limits

Launch the existing configured persistence preview. Confirm HP 105 and the
accepted V2 statistics, then attack Guardians normally: ordinary numbers retain
their appearance and critical numbers are yellow with no added text or size
change. At the currently published unequipped values, a nonlethal normal hit is
27 and a critical is 54; a lethal displayed value may be limited by remaining HP.
At 5% chance, a critical is stochastic; no forced-critical configuration was added.
Confirm retreat/interruption, Guardian health-bar disappearance and restart.
No new authenticated HTTP/database test or owner manual acceptance is claimed
for this presentation-only continuation.

Rollback only these focused renderer and test edits, preserving all prior V2
work. No database or catalog rollback is needed. The approved desired behavior
is now implemented; final manual visual acceptance remains with the owner.

## Current activation result

The owner authorized real activation in this continuation. **The V2 catalog is
now published in `ashenfall_09b3_test`, and the actual authenticated TCP endpoint
returned and validated its schemaVersion 2 snapshot for the existing manual
character.** Manual Godot visual/gameplay acceptance remains outstanding. The
earlier sections below are chronological evidence, not the current activation
status; their statements that V2 is inactive or that the publisher is missing
are superseded by this section.

### Real candidate and publication

Verified before writes: PostgreSQL 18.7, loopback `::1:5432`, database
`ashenfall_09b3_test`, administrative identity `postgres`, cluster system ID
`7708337984335018212`, the expected V1 release and original character. No
connection to `ashenfall_dev`, protected-token decryption, global role/config
change or Git operation was performed.

The opt-in test seeder now exposes `--prepare-v2-test-candidate` and
`--activate-v2-test-candidate <id>`. Preparation loads and verifies the actual
source catalog hash, copies only its existing class/enemy/four item definitions,
two level rules and three equipment modifiers, and creates explicit successor
links for the source class and all four historical item definitions. New base
damage bounds are both 25; level-two +2 damage yields effective 27–27. Critical
base values are the approved 5% chance and +100% bonus. No new equipment,
progression thresholds or modifiers were invented. All other source gameplay
values were copied. Nullable V2 level bonuses are explicitly zero in the new
definitions, and the pre-existing health/equipment bonuses are retained.

Before committing the draft, preparation called the actual publisher inside a
savepoint, verified successful persisted-candidate publication and rolled back
that lifecycle transition. It then committed only the validated draft. The
separate activation command called **`V2CatalogPublication.PublishAsync`**, which
loaded and validated the candidate again and committed the authorized lifecycle
transition. This preserves the publisher's locks and full inventory validation;
no direct activation shortcut was used.

Published identifiers:

| Value | Actual result |
| --- | --- |
| Release ID | `91000000-0000-0000-0000-000000000001` |
| Version | `snapshot-09c3-v2` |
| State | `published`, exactly one active release |
| V2 hash format | `ASHENFALL-CATALOG-CONTENT-V2` |
| Canonical V2 hash | `d1ef0a212b83df17300088210d081a63951621b2602e7ff35cf2a711c4de7d4d` |
| V1 base-content hash | `a33262049a29f4bde6d8e62d2f1d3837ed474a79d163b806917c4dde7fc6e5e1` |
| Historical V1 release | `90000000-0000-0000-0000-000000000001`, `snapshot-09c2a1-test`, now `retired` |
| Historical V1 hash | `a59cb3897f7764923b3e2c41c08d0ae618981e1cd8187835ceb428b6062c8438`, unchanged |

The base catalog schema/API remains V1 for compatibility, with its own genuine
base-content hash. The separately versioned V2 manifest covers combat fields,
progression, equipment modifiers and class/item mappings. The gameplay V2
response uses the V2 manifest hash, not the base hash. Historical published
definitions were not updated; retirement uses the existing permitted lifecycle.

No DDL was needed in this continuation. Actual SQL writes were draft release,
definition, rule, modifier, mapping and manifest inserts; draft combat-field and
hash updates; and the publisher's atomic retirement/activation updates. The
regression fixtures performed their existing scoped inserts/deletes and
rolled-back publication checks. No manual character or inventory write occurred.

### Real TCP evidence

`backend/MigrationReview/TestGameplaySnapshotV2Tcp09C3.ps1` launched a freshly
built, separate ASP.NET process listening on `127.0.0.1:5098`. Its process
environment explicitly selected `ashenfall_09b3_test` and `ashenfall_app`; a
private random token was generated in memory and inherited by the API. The
script verified that this exact process owned the listener, sent an authenticated
request, checked all response fields and repeated the database/character checks.
It did not read any stored token or print credentials. The process was stopped
after validation; this URL is evidence of the test, not a promise that an API
remains running.

Actual `GET /api/v1/dev/character/gameplay-snapshot`: **HTTP 200**:

```json
{
  "schemaVersion": 2,
  "characterId": "d9a62a3c-4996-4276-8ab8-2c4d6f2ec988",
  "characterRevision": 1,
  "classDefinitionId": "90000000-0000-0000-0000-000000000002",
  "catalogReleaseId": "91000000-0000-0000-0000-000000000001",
  "catalogVersion": "snapshot-09c3-v2",
  "catalogHash": "d1ef0a212b83df17300088210d081a63951621b2602e7ff35cf2a711c4de7d4d",
  "effectiveStats": {
    "level": 2,
    "maxHealth": 105,
    "fixedDamage": 27,
    "minDamage": 27,
    "maxDamage": 27,
    "criticalChancePercent": 5,
    "criticalDamageBonusPercent": 100
  }
}
```

The same real endpoint without authentication returned **401**. The historical
V1 and current base catalog snapshot endpoints both returned **200**, exercising
the backend's independent canonical V1 hash recomputation for each. The V2
gameplay reader recomputed the complete V2 hash before accepting the response.

### Regression and final database evidence

After activation, tests were updated to use the explicit verified V1 or V2 test
release and current definition IDs for newly created disposable characters.
Publisher-test candidates also carry forward the existing explicit historical
links, so the manual V1 character remains valid during those tests. No test was
silently disabled or assertions weakened to tolerate an arbitrary catalog.

- Full backend suite with isolated concurrency/publication tests enabled:
  **81 passed, 0 failed, 4 skipped**. The four skips remain the protected
  development-catalog publisher tests.
- Test-seeder/backend build and Godot build: succeeded, zero warnings/errors.
- `validate_standalone_isolation.gd`: passed; standalone health/HUD **100/100**
  and fixed damage **25**, with catalog networking unconfigured.
- `validate_player_melee.gd`: passed, zero failures; movement/click semantics,
  cancellation, attack duration and navigation remained valid.
- PostgreSQL final state: one published V2 and one retired V1 release; no
  drafts or `publisher-test-*` releases; one V2 manifest, one class link and four
  item links; zero unvalidated public constraints; all twelve named guards enabled.
- Manual character retains ID `d9a62a3c-4996-4276-8ab8-2c4d6f2ec988`, account
  `4ede66e9-f0dd-4dae-89f5-3dbddae826e1`, original class
  `90000000-0000-0000-0000-000000000002`, **100 XP, revision 1**, and active state.
  Database totals remain one account, one character, zero owned and zero equipped
  items. No character was deleted/recreated or silently migrated.

The expected values follow the actual published base 100 HP/25 damage plus
level-two +5 HP/+2 damage; the manual character is unequipped. Default critical
stats are class-authored. Runtime profiles are configurable; the initial
min=max range intentionally preserves the approved fixed-damage reference.

### Exact files in this continuation and repeatable checks

Created:

- `backend/Ashenfall.TestCatalogSeeder/V2TestCatalogActivation.cs`.
- `backend/MigrationReview/TestGameplaySnapshotV2Tcp09C3.ps1`.

Modified:

- `backend/Ashenfall.TestCatalogSeeder/Program.cs` and its `.csproj` (explicit
  test-only activation commands and reference to the existing publisher).
- `backend/Ashenfall.Tests/CharacterConcurrencyLiveTests.cs`.
- `backend/Ashenfall.Tests/V2CatalogPublicationLiveTests.cs`.
- `docs/architecture/MILESTONE_09C_1_MANUAL_PREVIEW_SETUP.md` (active-class
  configuration for new disposable characters and existing-character guidance).
- This report.

Actual invocation sequence used temporary output directories:

```powershell
dotnet build backend/Ashenfall.TestCatalogSeeder/Ashenfall.TestCatalogSeeder.csproj "-p:BaseOutputPath=$env:TEMP\Ashenfall09C3Activation\" -p:UseAppHost=false
dotnet "$env:TEMP\Ashenfall09C3Activation\Debug\net8.0\Ashenfall.TestCatalogSeeder.dll" --prepare-v2-test-candidate
dotnet "$env:TEMP\Ashenfall09C3Activation\Debug\net8.0\Ashenfall.TestCatalogSeeder.dll" --activate-v2-test-candidate 91000000-0000-0000-0000-000000000001
```

Those preparation/publication commands have already succeeded. Do not repeat
them as a routine startup step: preparation rejects an existing version and the
publisher requires an unpublished draft. For a read-only HTTP recheck, build a
current isolated API binary and run `TestGameplaySnapshotV2Tcp09C3.ps1 -ApiDll
<absolute API dll path>`; it selects only the test database and creates no rows.

### Manual validation and recovery

Manual Godot rendering/gameplay remains to be accepted by the owner: launch a
fresh API binary against the isolated database, then the editor with the same
private process token, the existing manual subject and character ID. Run
`res://scenes/world/CharacterPersistencePreview.tscn` with F6, confirm initial
105/105 health and normal 27 damage, gold critical feedback, movement/dodge and
frozen encounter stats during API loss. Critical hits are probabilistic during
manual play; deterministic formula tests do not substitute for that visual check.
Previously running API processes may contain older binaries and were not stopped
or reused by this validation.

Before commit, publication failures roll back atomically. **After a successful
commit there is no in-place catalog rollback:** do not reopen the retired V1,
delete the V2 release, reset characters or run the empty-data schema rollbacks.
Recovery is a reviewed compensating draft release with the desired content,
explicit mappings for all persisted historical identities, freshly computed
hashes and the same transactional publisher. Keep V2 reader support deployed
while V2 is active; reverting it alone would break the historical-class path.
No compensating release was needed or executed.

---

## Current result: requested publisher implementation and transaction tests complete

This continuation implements **only** the transactional publisher requested by
the owner. The real V2 release has **not** been activated. Earlier partial
implementation entries below are historical; statements there that the
publisher does not exist are superseded by this section. This does not declare
the entire 09C.3 milestone accepted or a live V2 HTTP snapshot validated.

### Entry points and files

Created `backend/Ashenfall.CatalogPublisher/V2CatalogPublication.cs`:

- `V2CatalogPublication.PublishAsync(connectionString, candidateId, ct)` owns
  the connection, READ COMMITTED transaction, commit and complete rollback.
- `PublishInTransactionAsync(connection, transaction, candidateId, ct)` executes
  the same publication operation within a caller-owned transaction. It creates
  a savepoint and rolls back all its changes on failure; the caller decides
  whether to commit a successful operation. This permits real publication
  lifecycle tests followed by a mandatory outer rollback.
- `RequireIsolatedConnection` rejects any database other than
  `ashenfall_09b3_test`, nonloopback host, port other than 5432 and identity other
  than `postgres` before opening the owning connection. The existing production
  publisher and its command-line behavior are untouched.

Created `backend/Ashenfall.Tests/V2CatalogPublicationLiveTests.cs`. The only
other changed file in this continuation is this report. No migration, Godot,
snapshot-reader, stat-calculation or existing-validator refactoring was needed.
No Git operation was performed.

### Actual queries, consistency and validation

Before mutation, the publisher checks the live database/user, loopback server
address/5432, cluster ID `7708337984335018212`, twelve enabled guard triggers
and the valid unique `catalog_one_published` index. It sets transaction-local
lock and statement timeouts and `search_path=public,pg_catalog`. It takes the
existing `CatalogPublication.LockNamespace`/`LockResource` advisory transaction
lock, then SHARE ROW EXCLUSIVE table locks on catalog releases, class/enemy/item
definitions, progression, modifiers, V2 manifests, both mapping tables,
characters, owned items and equipment. These prevent relevant writes and phantom
inserts while allowing ordinary SELECT/MVCC readers. Reads occur **after** the
locks at READ COMMITTED, so a publisher that waited observes the latest committed
state rather than a snapshot captured before the preceding publisher.

The publisher loads the actual draft and previous published release from
`catalog_releases`; definitions and all persisted character/inventory/equipment
rows through the existing EF mappings; and the actual V2 combat fields,
progression fields, modifier rows and class/item mappings through parameterized
queries scoped by the candidate release ID. It does not validate caller-supplied
copies or use the runtime HTTP client. It validates:

1. Draft state, previous active release, `CatalogValidator` and recomputed V1
   content hash.
2. Manifest format and recomputed complete V2 hash, including both mappings.
3. Every provided historical class/item mapping and source release status;
   category, code, slot, handedness, stackability and exact `max_stack` match.
4. Modifier definitions and critical percentage-point semantics.
5. The existing inventory validator over **all** owned instances and equipment,
   including unequipped stacks, quantities, ownership and hand/slot conflicts.
6. Every persisted character's class resolution and allocation; effective
   snapshot/stat bounds for all class levels and each active character's actual
   equipment. Soft-deleted characters still have references, allocations and
   inventory checked without fabricating a live gameplay snapshot.

Only after validation does it update the previous release from published to
retired and the candidate from draft to published with `published_at=now()`.
Affected-row assertions and a final aggregate require exactly one active release
and that it is the candidate. Historical content and hashes are never rewritten;
no character, owned-item or equipment update is issued by the publisher.

### Executed integration tests and evidence

The five tests below executed against the real isolated PostgreSQL database
with `ASHENFALL_09C3_PUBLISHER_LIVE=1`:

- Successful candidate publication: the real loaded candidate became the sole
  published release inside the transaction, its predecessor retired, every
  character/inventory/equipment JSON fingerprint remained identical, and the
  outer rollback restored the original state.
- Invalid candidates: incorrect V2 hash; missing historical class mapping;
  missing historical item mapping; incompatible `max_stack` with a real
  unequipped quantity-five stack; and a changed equipment slot. Legitimate
  hashes were recomputed for compatibility cases so these tests reached the
  intended mapping/inventory checks instead of failing only at hash comparison.
- Failure **after retirement**: an additional fixture-scoped trigger rejected
  candidate activation. The publisher rolled back its savepoint, leaving the
  predecessor published and the candidate draft. The added trigger/function
  disappeared with the fixture transaction rollback; no guard was disabled.
- Concurrent attempts: two independent real connections attempted publication
  of the same committed disposable draft. The second was observed waiting on
  a PostgreSQL lock; after the first attempt's outer rollback, it revalidated
  and successfully staged publication. Its transaction was also rolled back.
- Owning entry point: `PublishAsync` rejected an invalid committed test draft
  and rolled back its own transaction. A separate pure guard test rejects
  nonisolated connections before opening them.

Result: **5 publisher tests passed, 0 failed, 0 skipped** (the six failure
variants share one test). The complete backend suite then passed **81 tests,
0 failed, 4 skipped**. The four skips are the pre-existing
`CatalogPublicationDatabaseTests` targeting the protected development catalog;
they were kept disabled. The isolated backend build completed with zero warnings
and errors. No external API process or Godot validation was needed for this
publisher-only continuation.

Actual SQL writes consisted of disposable draft definitions, level rules,
modifiers, mappings/manifests, uniquely identified test accounts/characters/items
and equipment; transient retirement/activation updates through the publisher;
and exact-ID cleanup. Most fixtures existed only in rolled-back transactions.
The concurrency and owning-entry-point tests committed **draft fixtures only**
for cross-connection visibility, then removed only their exact IDs in dependency
order while asserting the candidate was still draft. Post-cleanup counts were
checked. No committed publication or real candidate activation occurred.

Final read-only evidence:

- Database `ashenfall_09b3_test`, `::1:5432`, expected cluster ID.
- Sole release `90000000-0000-0000-0000-000000000001`, version
  `snapshot-09c2a1-test`, still published with hash
  `a59cb3897f7764923b3e2c41c08d0ae618981e1cd8187835ceb428b6062c8438`.
- Manual character `d9a62a3c-4996-4276-8ab8-2c4d6f2ec988` retains class
  `90000000-0000-0000-0000-000000000002`, 100 XP and revision 1.
- One account and one character; zero owned/equipped items, V2 manifests,
  class mappings or item mappings; zero `publisher-test-*` releases and zero
  injected `reject_v2_*` functions.

### Limits, next activation and rollback

The successful publication path was deliberately **rolled back**, as requested.
It was not committed and no real V2 HTTP response is claimed. Remaining work
before real activation is to author/finalize the real candidate and its hashes,
run this publisher for that explicit candidate under separate activation scope,
and validate the actual authenticated V2 endpoint/manual gameplay. Concurrent
tests demonstrate serialization with rollbacks rather than two committed
publications. Table locks briefly block gameplay persistence writes during
offline publication; a lock/statement timeout fails safely without activation.
The publisher is currently restricted to the isolated database and administrative
identity; production deployment/role policy is outside this continuation.

No schema rollback is required for these code additions. To revert this
continuation, selectively remove the publisher and its new test file while
preserving earlier uncommitted work. The test candidates were already cleaned;
do not delete the manual character or any published/retired release. The earlier
schema migration rollback scripts retain their own empty-data requirements.

---

The following sections are the chronological record of earlier incomplete work.

## Inventory prepublication continuation

Added `CatalogInventoryCompatibility.Validate` and focused tests. The check
examines equipped and unequipped owned instances, explicit successor mappings,
duplicate identities, category, slot, handedness, stackability, exact
`max_stack`, quantity, ownership and equipment conflicts; it does not mutate
the supplied character or inventory objects. Three focused inventory, V2
factory and hash tests passed. The backend test project compiled.

**Not integrated or activated:** the validator is not connected to a database
candidate loader or transactional publisher. No V2 candidate publication,
rollback test, real V2 HTTP response, full backend suite or Godot build was
performed in this continuation. Thus the requested critical prepublication
checks have not passed as an integrated operation and publication was not
attempted; this is unfinished implementation, not a policy or database error.
The exact next implementation point is the transactional publisher: load the
candidate, complete V2 manifest and all persisted character/inventory/equipment
rows under transaction locks, invoke class/hash/compatibility validation, test
failure rollback and only then atomically transition releases. It must also
validate candidate modifier references and historical source release status,
which the standalone inventory checker does not establish itself.

Read-only PostgreSQL verification confirmed the isolated database/cluster, the
original release still published, and the manual character's original class,
100 XP and revision 1. No PostgreSQL write, publication or Git operation took
place in this continuation. New files:
`backend/Ashenfall.Api/Characters/CatalogInventoryCompatibility.cs` and
`backend/Ashenfall.Tests/CatalogInventoryCompatibilityTests.cs`; this report was
updated. Rollback consists of selectively removing these new files if no later
work depends on them; no database rollback is required for this continuation.

---

## V2 read-path continuation

Implemented `GameplaySnapshotV2Reader` and wired the existing authorized
gameplay-snapshot endpoint to it. When the active release has a V2 manifest,
the reader loads the character, catalog, combat definitions, level rules,
modifiers, class/item links and equipped historical definitions in one
`REPEATABLE READ`, read-only transaction, and invokes the existing hash-
validating V2 factory. When no active V2 manifest exists, the endpoint retains
the V1 reader. Catalog EF mappings for items and enemies were added to
`CharacterDbContext`. The V2 database path has not been exercised against a
published or candidate V2 fixture, so successful V2 endpoint behavior and
transactional consistency are **not yet validated**.

The full backend suite ran with the isolated live tests enabled: **75 passed,
0 failed, 4 skipped**. The four skips are the development-catalog publisher
tests, outside authorization. This run validates V1 fallback and existing
isolated regressions, not an actual V2 release. A post-run read-only check
confirmed test database/cluster identity, old release
`snapshot-09c2a1-test` still published with its original hash, the manual
character's original class ID, 100 XP and revision 1, and counts of one
account, one character, zero items and zero equipped items.

**Still missing:** prepublication validation of all inventory quantities,
`max_stack`, slots and historical mappings; a transactional publisher; V2
candidate/rollback and real endpoint integration tests; activation and V2 hash
verification. No V2 publication was attempted. The owner required these tests
to pass before activation, and they do not yet exist. There was no policy block.
No SQL migration was executed in this continuation; no development database or
Git access occurred. Changed files: `CharacterDbContext.cs`,
`CharacterEndpoints.cs`, new `GameplaySnapshotV2Reader.cs`, and this report.
Rollback selectively removes the V2 endpoint dispatch/reader and the two EF
mappings; preserve the existing V1 endpoint and unrelated changes. No database
rollback is required for this continuation.

---

## Historical item mapping continuation

Read-only PostgreSQL foreign-key inspection found character-bound references to
the release-specific class via `player_characters.class_definition_id` and to
release-specific items via `owned_items.item_definition_id`. Equipment points to
owned-item instances, not directly to catalog definitions. Level rules and item
modifiers belong to catalog definitions rather than directly to characters; no
additional character-to-enemy catalog relationship was found. Inventory entries
that are not equipped are not used to calculate snapshot combat stats. The current
inventory and equipment services retain their historical IDs; their active-release
interpretation has not been integrated.

Applied `CatalogItemTransition09C3.Test.sql` to the verified isolated test
database. It adds explicit item-successor metadata with immutable-after-target-
publication guards, foreign keys, identity/code/category/slot/hand/stackability
checks and app-role SELECT only. The conditional rollback refuses to erase
existing mappings. A PostgreSQL transaction created temporary draft data, checked
a valid mapping and rejection of a mismatched source, then rolled back. The
published release and manual character were not changed.

The in-memory V2 snapshot factory can now resolve equipped historical items
through explicit links included in the V2 hash; it rejects missing, ambiguous,
incompatible or nonhistorical sources. Modifiers are applied once per equipped
instance even when two instances have the same definition. Three focused tests
passed, including historical mapping, mapping hash coverage, invalid category
and repeated-definition aggregation. The backend test project compiled.
The factory also rejects changed `max_stack`; the database mapping trigger does
not yet enforce that equality, so publisher validation must reject it before
activation or a separate guarded database migration must add that check.

**Not complete:** the factory still is not wired to the real database reader or
HTTP endpoint; there is no transactional V2 publisher and no activated V2
release. Prepublication end-to-end and publication-rollback tests did not run,
so publication was not attempted. No failed publication or execution-policy
block is claimed. The old catalog remains active. Completing the requested
work still requires transactional publisher/reader integration, database-backed
hash verification and inventory compatibility validation before activation.
No `ashenfall_dev` or Git access occurred.

Files created: `backend/MigrationReview/CatalogItemTransition09C3.Test.sql` and
`backend/MigrationReview/CatalogItemTransition09C3.Test.Rollback.sql`. Modified:
`backend/Ashenfall.Api/Catalog/Services/CombatCatalogHashV2.cs`,
`backend/Ashenfall.Api/Characters/GameplaySnapshotV2Factory.cs`,
`backend/Ashenfall.Tests/GameplaySnapshotV2FactoryTests.cs`, and this report.
The guarded SQL rollback is permitted only while the mapping table remains empty;
do not remove any subsequently published mapping. The V2 hash now includes item
links, so no digest from an earlier incomplete format may be reused.

---

## Publisher/reader continuation — not activated

Added `GameplaySnapshotV2Factory` and its unit test. It checks the complete V2
manifest hash, resolves a historical class through an explicit mapping, preserves
the original character class ID and revision in the response, derives health and
combat stats, and rejects equipped definitions outside the active catalog. The
canonical V2 hash now also covers ordered class successor mappings. Three focused
factory/hash/transition tests passed; the isolated backend build succeeded.

**This is not a working backend path yet.** The factory is not wired to the
database reader or HTTP endpoint, no transactional V2 publisher is implemented,
and no V2 release has been published. The equipment model contains immutable
`owned_items.item_definition_id` references to release-specific item definitions;
the present class-only successor table does not resolve historical equipped
items into the active release. The new factory deliberately rejects such items
rather than silently applying the wrong release's modifiers. Safe item mapping,
manifest coverage, publisher/reader wiring and the requested transactional
prepublication tests still need implementation. Thus the user's explicit
condition to pass those tests before activation was not met. Publication was
not attempted and no publication failure or execution-policy block is claimed.

Changed/created in this continuation:
`backend/Ashenfall.Api/Characters/GameplaySnapshotV2Factory.cs`,
`backend/Ashenfall.Api/Catalog/Services/CombatCatalogHashV2.cs`,
`backend/Ashenfall.Tests/GameplaySnapshotV2FactoryTests.cs`, and this report.
No SQL or PostgreSQL writes were performed in this continuation. The old
published release and existing character were not touched. The V2 hash format
changed to include class mappings and therefore has not yet been frozen or
published; never reuse a digest generated by the earlier incomplete format.
Rollback selectively removes the factory, mapping-hash change and tests; no
database rollback is involved in this continuation.

---

## Catalog transition continuation

Applied `backend/MigrationReview/CatalogTransition09C3.Test.sql` on the verified
`ashenfall_09b3_test` cluster. It creates an immutable-after-publication V2
manifest table and explicit class successor links; the app role receives only
SELECT on them. Successor triggers require a matching class code, a previously
published source and a draft target in a different release, with one successor
per source and target release. They do not rewrite a character's class ID.
`CatalogTransition09C3.Test.Rollback.sql` refuses to remove nonempty metadata.
In a rolled-back transaction, PostgreSQL accepted a valid draft successor and
rejected a mismatched class and an attempt to attach a V2 manifest to an already
published release. No catalog publication was committed.

Added `CombatCatalogHashV2`, a versioned, canonically ordered SHA-256 over the
V1 content hash plus release ID, class combat values, progression and equipment
modifiers; invalid class/item relationships are rejected. Added
`CatalogClassTransition.Resolve`, which requires an explicit link to a unique
same-code class in the active release; matching code alone is insufficient.
The two focused unit tests passed. These helpers are **not yet connected** to
the publisher or snapshot reader; the hash has not been stored in a published
release and no V2 snapshot has been served. The migration guards and resolver
need further concurrency/security review and live integration tests before use.

The manual character was not modified. The old published release remains active;
retiring it before the reader and publisher are wired would break the existing
snapshot path, so publication was **not attempted**. This is unfinished work,
not an execution-policy rejection. Complete the transactional publisher, backend
resolution/read path, V2 integrity verification and live regression tests before
claiming completion. No development database or Git access occurred.

Additional files: the two transition SQL scripts,
`backend/Ashenfall.Api/Catalog/Services/CombatCatalogHashV2.cs`,
`backend/Ashenfall.Api/Characters/CatalogClassTransition.cs`,
`backend/Ashenfall.Tests/CombatCatalogHashV2Tests.cs` and
`backend/Ashenfall.Tests/CatalogClassTransitionTests.cs`. Rollback of transition
tables is conditional on them being empty; the earlier combat-stat migration has
its own guarded rollback. Never drop published data to force a rollback.

---

## PostgreSQL continuation

Applied `backend/MigrationReview/CombatStats09C3.Test.sql` to PostgreSQL 18.7
on `ashenfall_09b3_test` after verifying loopback, port 5432, `postgres` and
cluster identifier `7708337984335018212`. Its transaction adds nullable class
minimum/maximum damage and critical chance/bonus fields, nullable progression
bonuses and extends the allowed equipment stat codes. Constraints reject
partially populated class values, inverted ranges, chance outside 0–100 and
nonfinite or negative critical bonuses. Existing published rows remain null;
no published catalog row was updated. The corresponding conditional rollback
`backend/MigrationReview/CombatStats09C3.Test.Rollback.sql` refuses to erase
authored V2 values and was dry-run successfully inside a transaction that was
rolled back. A separate rolled-back PostgreSQL test on a temporary copy verified
partial, inverted, over-100 chance and NaN values are rejected.

After the migration the existing published release still has version
`snapshot-09c2a1-test` and V1 hash
`a59cb3897f7764923b3e2c41c08d0ae618981e1cd8187835ceb428b6062c8438`;
the manual character retains its original class, 100 XP and revision 1.
The original catalog and extension guard triggers remain enabled. The V1 hash
does not attest the new columns. **No V2 release was published and no backend
V2 endpoint was implemented in this continuation.** These operations were not
attempted; they are not reported as execution-policy blocks. The current
one-published-release constraint and immutable character class reference must be
handled together with a versioned hash that covers the new fields. Merely
retiring the existing release would make the current snapshot reader reject the
manual character's class; publishing the new columns using the old V1 hash as if
it covered them would misrepresent integrity. The remaining implementation and
validation work has not been completed.

No Git operation or connection to `ashenfall_dev` was performed. The migration
can be rolled back with the conditional script only while no V2 data has been
authored; do not run rollback if its preflight rejects. The historical entries
below describing no schema change predate this continuation.

---

## Continued implementation

The Godot character client now accepts schema V2 only when all four combat
fields are present and valid, while preserving schema V1. The persistence
bootstrap configures a validated V2 damage profile before the encounter enters
the tree. The melee resolver rolls the frozen profile only after target and hit
validation, uses the existing critical feedback, and leaves V1 and standalone
behavior unchanged. No currently implemented backend route produces V2, so this
path is **not yet available end to end** and has not been gameplay validated.
Tests cover V2 decoding and invalid/missing combat fields alongside existing
damage calculations. The combined focused suite passed 24 tests after fixing a
test-source compilation error; Godot build passed without warnings/errors.
The standalone headless check also passed with 100/100 HP and 25 damage.

Read-only PostgreSQL inspection reconfirmed `ashenfall_09b3_test`, loopback port
5432, expected cluster, the existing published release, and the owner's
character at 100 XP/revision 1. No database writes, schema changes, catalog
publication or Git operations were performed. The requested migration,
cryptographically meaningful V2 catalog and backend snapshot implementation
remain outstanding. In particular, retiring the sole published release without
a safe character migration would invalidate the owner's existing class reference;
this was not attempted. The user authorized the isolated database changes, but
the required safe implementation and tests have not yet been completed.

Additional modified files: `scripts/characters/PersistedCharacterClient.cs`,
`scripts/characters/CharacterPersistencePreview.cs`,
`scripts/combat/MeleeDamageController.cs`, and
`backend/Ashenfall.Tests/PersistedCharacterClientTests.cs`. For rollback,
selectively remove only these V2 branches and tests; retain the earlier V1
integration and unrelated work. No database rollback is necessary.

---

## Follow-up progress

Added `CharacterRules.CalculateCombatStats`, a deterministic, currently unused
backend calculation for damage bounds and critical bonuses. It combines base
values, supplied level bonuses and existing item modifier records, treats flat
critical modifiers as percentage points, clamps effective chance to 100%, rejects
negative or inverted results and uses midpoint-away-from-zero damage rounding.
No new runtime modifier sources, schema, publication or snapshot contract are
implied by this helper. A deterministic example verifies 5+10=15% chance,
100+50=150% bonus and 10 normal damage becoming 25 critical damage. The
focused `CharacterAndCombatTests` suite now passes 16 tests; Godot build passes
without warnings or errors. A subsequent read-only query reconfirmed the test
database/cluster identity and the existing character at 100 XP/revision 1.
No database mutation, development database access or Git operation occurred.

The additional changed file is `backend/Ashenfall.Api/Characters/CharacterRules.cs`;
the tests are in `backend/Ashenfall.Tests/CharacterAndCombatTests.cs`. These
methods are not integrated into the API or combat and do not make the milestone
complete. Remove only the new record, helper and focused tests for rollback.

---

This milestone is **not complete**. No schema migration, catalog publication,
snapshot V2, backend stat aggregation or gameplay integration was implemented.
The existing standalone and persisted gameplay behavior remains unchanged.

The existing `CombatDamageCalculator` already has an injectable random source,
inclusive damage range, strict probability boundary and midpoint-away-from-zero
integer rounding. Added `CalculatePercent` as a pure adapter: critical chance is
expressed in 0–100 percentage points, and bonus 100 means a multiplier of 2.
It rejects negative/nonfinite values and damage overflow; it does not impose a
low critical bonus cap. This method is not wired into any gameplay controller.
Deterministic tests cover the requested 100/150/200/300 bonus examples, chance
boundaries and invalid/overflow inputs. Existing debug-only variable-damage
experimentation predates this change and remains unchanged.

The existing snapshot remains schema V1 with `fixedDamage`. The current V1
catalog hash covers original catalog fields, not progression or item modifiers;
existing published release and extension guards prohibit modifying them in
place. The current test schema has `fixed_damage` and level `damage_bonus`, not
the four requested base fields. Correctly introducing the fields requires a
versioned catalog/hash and snapshot contract, reviewed additive migration,
compatible publication and a strategy preserving the existing character's
class relationship. These have not been implemented and must not be represented
as complete or as a validated migration.

Read-only inspection verified PostgreSQL 18.7 at `::1:5432`, database
`ashenfall_09b3_test`, cluster ID `7708337984335018212`, the existing published
synthetic release and the owner's character at 100 XP and revision 1. No
PostgreSQL mutations, migrations, backups, publication or Git operations were
performed. `ashenfall_dev` was not accessed. Godot build passed without warnings
or errors; the isolated backend test run for `CharacterAndCombatTests` passed
15 tests. No live API, persistence, equipment modifier or gameplay critical
validation was performed.

Modified files: `scripts/combat/CombatDamageCalculator.cs`,
`backend/Ashenfall.Tests/CharacterAndCombatTests.cs`, and this report. Rollback
only the newly added adapter method and its tests, retaining all prior work; no
database rollback is required. Completion still requires the explicitly requested
database, backend, snapshot, gameplay, fixture, rollback and integration work.
