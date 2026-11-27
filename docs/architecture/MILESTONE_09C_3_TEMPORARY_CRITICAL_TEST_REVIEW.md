# 09C.3 — temporary 100% critical chance safety review

**NO-GO for activation with the existing SQL-only workflow.** No database
modifications or Git operations were performed. No application or Godot code
was changed. The two requested SQL paths contain explicitly labelled read-only
diagnostic preflights which stop with an explanatory error. They do **not**
enable or restore a release, and must not be presented as completed activation
scripts. This follows the request to stop if the architecture cannot support
the requested procedure safely.

## Actual inspection and baseline

A manually constructed `BEGIN READ ONLY` inspection on 2026-11-22 confirmed
`ashenfall_09b3_test`, loopback `::1:5432`, cluster `7708337984335018212`.
It found the published release `91000000-0000-0000-0000-000000000001`, version
`snapshot-09c3-v2`, and stored V2 manifest hash
`d1ef0a212b83df17300088210d081a63951621b2602e7ff35cf2a711c4de7d4d`.
The original character `d9a62a3c-4996-4276-8ab8-2c4d6f2ec988` still has class
`90000000-0000-0000-0000-000000000002`, 100 XP and revision 1. The active
class `91000000-0000-0000-0000-000000000002` has base HP 100, fixed and
minimum/maximum damage 25, chance 5 and critical bonus 100.

This verifies stored metadata, not a fresh canonical hash recomputation or a
new authenticated HTTP response. The expected level-two effective baseline
105 HP / 27 damage / 5% / +100% comes from the existing validated fixture and
reader. The supplied diagnostic queries additionally check its level and
unequipped assumptions when separately reviewed and executed. Neither supplied
SQL file was executed. `ashenfall_dev` was not accessed; no credentials or
private configuration were read or exposed.

## Architecture and concrete limitations

- `CharacterRules.CalculateCombatStats` derives chance from class
  `critical_chance_percent`, current `class_level_rules` row's
  `critical_chance_bonus_percent`, and equipped `item_stat_modifiers` with
  `stat_code='critical_chance'` and `modifier_kind='flat'`. These are additive
  percentage points, capped at 100. Critical bonus similarly uses the class
  bonus, level bonus and `critical_multiplier` flat modifier. The factory passes
  only resolved equipped modifiers; unequipped inventory does not add stats.
- `V2TestCatalogActivation.PrepareCommandAsync` originally copied the V1
  fixture to new release-specific class/enemy/item IDs, copied progression and
  modifiers, inserted explicit historical class/item successors, computed V1
  and V2 hashes, and exercised publication in a savepoint before committing the
  draft. It is hard-pinned to that old V1 source and the existing V2 target.
  It refuses to overwrite the existing candidate. `ActivateCommandAsync`
  accepts only the already published V2 release ID. Neither command supports
  preparing a new temporary release or a restoration release today.
- `CombatCatalogHashV2.Compute` is a C# canonical binary serialization using
  normalized text, decimal formatting, ordered rows and mappings, then SHA-256.
  It includes combat fields, progression, item modifiers and successor mappings
  in addition to V1 content. A changed critical chance requires a new manifest
  hash; copying the old hash is invalid. New release/definition IDs also change
  hashes even when other values match. No SQL function wraps this implementation.
- `V2CatalogPublication.PublishAsync` / `PublishInTransactionAsync` are C#
  entry points, not PostgreSQL procedures callable from DBeaver. They load and
  validate actual rows, lock all relevant tables and the publication advisory
  lock, check mappings and inventory compatibility, verify both hashes and
  effective snapshots, and atomically retire the previous release and publish
  the draft. Handwritten status UPDATEs would omit this required validation.
- `InitialRpgCatalog.sql`'s `catalog_guard_release` allows draft-to-published
  and published-to-retired, but **never retired-to-published**. The partial
  unique index permits only one published release. Publishing the temporary
  release necessarily retires the current V2 release. Its ID and hash remain
  intact historically, but restoring it as active is unsupported. Definitions,
  manifests and mappings belonging to published/retired releases stay immutable.
- `GameplaySnapshotV2Reader` selects the single published release on each
  read-only REPEATABLE READ request and resolves the original class and equipped
  item IDs through explicit mappings. It verifies the canonical V2 hash through
  the factory. It does not accept an arbitrary old release selection.

## Smallest compatible future procedure — not implemented or executed

If a different active restoration release is acceptable, approve a small
extension to the **test-only** preparation tooling; do not change gameplay or
the production publisher. Then prepare two forward releases:

1. A temporary release cloned from the verified baseline, changing only the
   resolved dwarf class's base chance from 5 to 100. Copy all existing items,
   rules and modifiers unchanged. This yields effective 100% for the current
   unequipped character without touching its XP, revision or inventory. Existing
   modifiers for other characters must still pass the real publisher checks.
2. A restoration release cloned from the same baseline, with chance 5. It will
   restore the effective values but have a **new active ID, version and hashes**.
   Preserve the original V2 ID/hash as historical evidence. If the original ID
   must become active again, the requested round trip remains NO-GO.

Both need fresh collision-checked identities, all required definitions and
explicit **direct** mappings from every persisted historical class/item ID to
the new definitions. Do not assume transitive mapping traversal or match by
display name. Include baseline-active definitions as historical sources where
necessary for any existing character/inventory references. Canonical hashing
and publication must use the existing C# implementations. New releases and all
their definitions are required; an independent new draft class cannot be added
to an immutable published release.

Before any activation, test preparation and restoration end to end, including
original character/inventory fingerprints, exactly one active release, canonical
hash verification, idempotent rerun handling and rollback of rejected candidates.
Prepare a valid restoration path before retiring the baseline. Published test
releases cannot be deleted afterward and must remain historical. No reset,
cleanup, schema change or guard bypass is part of this alternative.

There is currently no truthful DBeaver-only command sequence that invokes the
existing C# validation/hash/publisher. A future reviewed C# tool is a prerequisite,
not an unattempted operation being described as an execution-policy failure.

## Review files and exact DBeaver order today

1. Review `docs/sql/09c3_enable_100_percent_critical_test.sql` and
   `docs/sql/09c3_restore_original_critical_release.sql`; both are **NO-GO
   diagnostics**, not mutation scripts. Do not activate anything.
2. If choosing to run diagnostics locally after review, use a PostgreSQL
   connection explicitly to localhost:5432 / `ashenfall_09b3_test` with existing
   private administrative authentication. Do not copy credentials into scripts.
   Cluster identity inspection requires permission on `pg_control_system()`;
   permission failure must stop the script, not trigger grant changes.
3. Execute the entire enable file as a script, stopping on first error. Its
   read-only transaction checks database/cluster before application queries,
   displays release and unequipped values, checks baseline assumptions, rolls
   back, then deliberately raises the unsupported-activation error. **Do not
   proceed to a critical-hit gameplay test:** chance is still 5%.
4. The restore file is independently safe to inspect or run as a diagnostic.
   It checks the original baseline and deliberately rejects the unsupported
   reactivation. If a temporary release were active, its baseline check would
   stop. It never claims restoration. On any earlier error inside the read-only
   transaction, use DBeaver Rollback before further work; do not continue selected
   statements. Repeating either file performs no mutations.

The diagnostic arithmetic is limited to the unequipped fixture. It does not
replace the backend formula or prove full hash integrity; use the authenticated
snapshot for authoritative verification in a future approved execution task.

## Future verification checkpoints

After supported preparation/publication tooling exists and execution is approved:

| Check | Temporary publication | Forward restoration |
| --- | --- | --- |
| Snapshot schema | 2 | 2 |
| Character / original class / revision / XP | unchanged / unchanged / 1 / 100 | same |
| Level / HP / damage range | 2 / 105 / 27–27 | same |
| Chance / critical bonus | 100% / +100% | 5% / +100% |
| New active release / canonical hash | match temporary manifest | match restoration manifest |
| Original V2 ID / hash | preserved, retired | preserved, retired |

Use `GET /api/v1/dev/character/gameplay-snapshot` with the existing local private
token and subject; never paste the token into DBeaver SQL, URLs, logs or reports.
Confirm the actual API process targets the verified test database. Compare the
full character, owned-item and equipment records before/after, not just HP.

The backend reader queries the current published catalog per request; no backend
restart is normally necessary for a committed release transition. Godot pins
stats per encounter: stop the running F6 scene and run
`res://scenes/world/CharacterPersistencePreview.tscn` again after **each**
activation. Do not expect catalog F9 refresh or an already-running encounter to
change combat stats. A valid new snapshot must be accepted first.

In the temporary encounter every eligible successful player hit should produce
yellow numeric-only damage, normally 54. Lethal numbers can be smaller because
the renderer displays actual HP removed. Missed/interrupted attacks are not
successful hits. Confirm identical number font, size, movement and duration;
existing common impact effects remain unchanged. After forward restoration,
normal hits are normally 27, occasional criticals 54 at 5%; no forced override.

## Outcome

Created only the two review-only SQL diagnostics and this report. No SQL files,
builds, live mutation tests or catalog tooling were executed. The only database
execution was the separately constructed read-only inspection described above.
The safe limitation is established by the actual guard and C# entry points;
no destructive trial was necessary or authorized. Activation and restoration
are **not implemented** under the requested existing SQL-only mechanism. Stop
here for owner review of the new-release restoration tradeoff and tooling scope.
