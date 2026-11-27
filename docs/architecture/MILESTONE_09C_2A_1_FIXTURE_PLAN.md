# Milestone 09C.2A.1 — isolated snapshot fixture plan

## Recommendation: NO-GO under the present constraints

The requested complete fixture and cleanup cannot safely be executed in the
current `ashenfall_09b3_test` database. This is a schema/lifecycle conflict, not
an authorization to change it:

1. The database already has one published synthetic `prototype-1`. The partial
   unique index `catalog_one_published` permits only one published row. A second
   published release would fail unless the existing one changed state, which this
   task expressly forbids.
2. Published/retired releases, their definitions and their level-rule/item
   modifier extensions are protected by enabled guards. A fixture release cannot
   be fully deleted after it is published, even if its character rows are
   deleted. Retiring it does not make it deletable.
3. A draft cannot stand in for the fixture: the gameplay snapshot factory
   requires `published`, and the character and owned-item insert guards require
   published definitions. Uncommitted fixtures in one transaction are invisible
   to an independent API connection and therefore cannot exercise concurrent
   HTTP/read-write behavior.
4. The existing publisher CLI and its database preflight explicitly require
   `ashenfall_dev` and `ashenfall_publisher`, and publish only the fixed approved
   seed/hash. Its publication method can retire an existing published release.
   It is therefore unsuitable for this fixture and must not be invoked here.

There is no safe, complete SQL or existing publisher command meeting all of the
current target, publication and cleanup requirements. The SQL below is a
**review-only preflight**, not a fixture creation script. No INSERT, UPDATE,
DELETE, DDL, migration or fixture validation was executed in this task.

## Verified evidence

Read-only inspection of PostgreSQL 18.7 on `ashenfall_09b3_test` found one
published `prototype-1` with the synthetic all-zero hash, zero account/character,
level-rule and item-modifier rows, and an enabled partial unique index. The
release, class, item, level-rule and item-modifier guards were enabled. Both
`ashenfall_app` and `ashenfall_publisher` lack schema CREATE. No credentials or
private configuration were read or displayed; `ashenfall_dev` was not accessed.

Relevant implementation: `InitialRpgCatalog.sql:115,125-194`,
`CharacterPersistence09B.sql:80-118,165-188`,
`backend/Ashenfall.CatalogPublisher/Program.cs` and `CatalogPublication.cs`, and
`CharacterGameplaySnapshot.cs`. The existing fixture's all-zero marker is not
a verified V1 content hash. Do not copy or pass it off as one.

## Read-only preflight — review only

Run only after separate approval for a later validation task. The transaction
has no writes. It verifies the database name and exposes the blocking published
release; it does not expose account identities or secrets.

```sql
-- REVIEW ONLY — target: ashenfall_09b3_test; do not execute in this task.
BEGIN READ ONLY;
SELECT current_database(), current_user, current_setting('server_version');
SELECT version_key, status, content_sha256
  FROM public.catalog_releases WHERE status = 'published';
SELECT indexdef FROM pg_indexes
 WHERE schemaname='public' AND indexname='catalog_one_published';
SELECT tgname, tgenabled FROM pg_trigger
 WHERE NOT tgisinternal AND tgname IN
 ('catalog_release_guard','character_class_catalog_guard',
  'item_definition_catalog_guard','class_level_rule_catalog_guard',
  'item_modifier_catalog_guard') ORDER BY tgname;
SELECT 'accounts' AS kind, count(*) FROM public.accounts
UNION ALL SELECT 'characters', count(*) FROM public.player_characters
UNION ALL SELECT 'rules', count(*) FROM public.class_level_rules
UNION ALL SELECT 'modifiers', count(*) FROM public.item_stat_modifiers;
COMMIT;
```

If a future approved implementation uses `psql`, explicitly connect with
`-X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -d ashenfall_09b3_test` and a
privately authenticated identity. Every mutating transaction must additionally
fail closed on `current_database()`, server/cluster identity and its unique
fixture IDs. The database name alone is insufficient to distinguish clusters.

## Proposed synthetic fixture — not created

Use a fresh random namespace per run, collision-checked UUIDs, and a unique
version such as `snapshot-fixture-<uuid>`. Example values for a fixture that
would be valid **only in an approved environment with no existing published
release**:

| Record | Proposed values |
| --- | --- |
| Release | schema 1; draft initially; publish only after validation and computed V1 hash |
| Class | `dwarf_warrior`, `dwarf_berserker`; base HP 100, fixed damage 25; movement 2.5, range 1.45, cycle 1700 ms, impact 940 ms |
| Level rules | level 1: XP 0, HP/damage bonus 0; level 2: XP 100, HP bonus 5, damage bonus 2; zero allocated points required |
| Weapon | nonstackable `weapon`, `main_hand`, one hand, quantity 1 |
| Weapon modifiers | `max_health` flat +5 and percent +10; `fixed_damage` flat +3 |
| Character | unique local-development subject/account and character UUID; XP 100; allocations 0; revision 1 |
| Equipment | owned weapon attached to that character's `main_hand` |

The backend's existing formula yields level 2, `maxHealth = (100 + 5 + 5) ×
1.10 = 121`, and `fixedDamage = 25 + 2 + 3 = 30` when equipped. With the item
unequipped it yields HP 105 and damage 27. A service-driven equip/unequip must
increment the character revision; expected values are asserted from the actual
result, not assumed for arbitrary raw SQL.

The default `CatalogValidator` also requires a valid `fallen_guardian`,
`training_battle_axe` and `guardian_sword` with their required visual keys.
A future test seeder must include and validate those definitions, or obtain
separate approval for an appropriate test-only validation mode. It must compute
the V1 hash from the complete synthetic snapshot using the existing
`CatalogHash.Compute`; never substitute the all-zero marker or the approved
development hash. V1 includes original class/enemy/item definitions but **does
not** attest the level rules or item modifiers. A separate extension provenance
policy remains unresolved.

## Smallest safe alternative requiring owner decision

Authorize a **separate disposable PostgreSQL cluster or container** bound only
to loopback on an unused, explicit test port, containing a fresh database also
named `ashenfall_09b3_test`. This preserves the requested database name while
isolating it from the existing database of that name. Verify cluster identity,
port, database name and an explicit test marker before every write; use only a
dedicated test identity with the necessary privileges. Do not clone real data.
This is a proposed change of environment and requires new explicit approval;
no cluster, database, role, fixture or file is created by this plan.

The currently available publisher cannot target that environment. A separately
reviewed test-only seeder would need to use the existing catalog validation and
hash functions, insert draft definitions and extension rows while their parent
is draft, validate progression with `CharacterRules`, then publish in one
transaction. It must not weaken guards or privileges. Once published, the
catalog fixture cannot be removed row-by-row under current guards. Preserve it
in the disposable cluster or obtain explicit authorization to dispose of the
entire verified test cluster afterward. Do not use an unrestricted DELETE,
TRUNCATE, trigger bypass or schema change to manufacture cleanup.

No exact executable fixture-creation script is supplied at this stage: without a
separate published slot, an approved seeder and a computed matching hash, such a
script would either fail or misrepresent provenance. Preparing the test-only
seeder and its exact SQL is future work requiring approval of this alternative.

## Future validation sequence

After the environment, seeder and fixture are explicitly approved:

1. Run preflight over an authenticated, explicitly pinned test cluster; check
   no unexpected published release or colliding UUID/version exists. Validate
   permissions and migrate nothing automatically. Provision schema only under
   separate authorization using reviewed scripts.
2. Publish the fixture once with the reviewed seeder. Record its version,
   release/class/item IDs and independently recomputed V1 hash. Verify guards
   reject edits to the published definitions and extensions.
3. Create one uniquely identified account/character and owned/equipped item in
   a transaction; verify all foreign keys, the real class and item association,
   XP and revision. Keep a ledger of exact IDs. Never create fixtures during
   normal API startup.
4. Configure the existing development-only API gate with a unique private
   subject and token using process-local secure configuration. Send a real HTTP
   request and verify 200, schema version 1, exact character and revision, class
   and release IDs, version, computed V1 hash, level 2, HP 121 and damage 30.
   Verify missing/unauthorized responses without logging secrets.
5. Instrument the actual `CharacterGameplaySnapshotReader` after its first read
   as in `Snapshot_reader_sees_consistent_character_when_soft_delete_commits_concurrently`.
   Pause the reader, commit an unequip on a second connection through the real
   service, release the reader and verify it returns the old complete equipped
   revision/121/30 snapshot; a new read returns the new complete unequipped
   revision/105/27 snapshot. Query PostgreSQL transaction settings to confirm
   `REPEATABLE READ` and read-only, and compare scoped rows before/after each GET.
6. In separately rolled-back negative cases, verify missing/incomplete level
   rules and invalid resulting statistics reject safely. Existing unit tests cover
   synthetic factory validation, token/loopback and production-disabled routing;
   the real HTTP 200 and equipment-update/read concurrency tests still need to be
   written. Existing live tests already prove 404, missing-rules 500 and a
   repeatable-read soft-delete case against the current isolated database.
7. Clean up only the exact ledger IDs in dependency order: equipment, owned
   item, character and then the test account. Check each ownership/subject and
   row count, within a transaction. If any check or delete fails, roll back and
   report the exact IDs; do not broaden the operation. Verify no test player
   rows remain. The published catalog fixture is retained until separately
   authorized whole-cluster disposal; individual deletion is prohibited.

No SQL writes were performed and no new SQL or application file was created.
Only this plan was added. Stop here for owner review and explicit approval of
any changed environment or fixture provisioning.
