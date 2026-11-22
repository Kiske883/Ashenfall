# Milestone 09B — character persistence foundation

## Implemented without applying a migration

`backend/MigrationReview/CharacterPersistence09B.sql` is a **review-only** PostgreSQL
18 script. It proposes `accounts`, `player_characters`, `class_level_rules`,
`item_stat_modifiers`, `owned_items`, and `character_equipment`. UUIDs identify rows;
one active character per account is enforced by a partial unique index and soft
deletion. Revisions protect character and owned-item updates. A composite foreign key
enforces equipment ownership. New catalog-extension triggers reject edits to level
rules and modifiers attached to published or retired definitions. Existing catalog
tables and `prototype-1` are untouched. The script includes only the grants needed for
the restricted application role and draft-authoring publisher. Milestone 09B.1
subsequently added explicit preflight checks, inventory/equipment guard triggers,
minimal owned-item DELETE permission, and an empty-data rollback proposal. See
`MILESTONE_09B_1_PERSISTENCE_HARDENING_REPORT.md` for the current review state.

`CharacterDbContext`, `CharacterService`, `CharacterRules`, and `CharacterEndpoints`
were added under `backend/Ashenfall.Api/Characters`. The service creates, reads, saves
and soft-deletes characters; stores inventory quantities; validates slot ownership,
compatibility and two-handed exclusions; locks the character row during equipment
transactions; and uses optimistic revisions. Trusted server-side item granting is
available in the service, but no client grant endpoint exists. The stat pipeline
applies class base, level, allocated attributes, equipment flat/percent modifiers,
then temporary runtime modifiers with nonnegative clamping and away-from-zero
rounding. Transient health/combat state is not persisted. The current published class
has no level rules; it derives level 1 locally until a reviewed future release adds
progression. Equipment bonuses are not silently assigned to the training axe or
Guardian sword.

The existing SHA-256 V1 catalog hash covers only the original four catalog tables.
It does not attest the new level-rule/modifier rows. Therefore a future release that
uses these rows needs a separately reviewed hash/schema revision and publisher
validation before those values can be considered catalog-authoritative. This phase
does not publish or extend `prototype-1`.

The local character routes are **off by default**. They are mapped only in Development
when `ASHENFALL_CHARACTERS_ENABLED=1`, a private token of at least 32 characters,
and a server-configured subject are present. Requests must be loopback and supply the
token; the account identity is derived from server configuration, never a client ID.
This is a development identity boundary, not production authentication. Do not enable
these routes before the approved migration and runtime grants. Godot does not connect
to PostgreSQL. Future production use requires an authenticated identity provider and
server-authoritative XP/inventory grants.

## Validation and approval gate

- Read-only PostgreSQL query confirmed `ashenfall_dev` still has four catalog tables
  and **zero** of the six proposed persistence tables.
- Static review covered the six table names, catalog FKs, one-active index, ownership
  FK, revision fields, role grants and draft guards. The script was **not parsed or
  executed against the owner's database**, even inside a rollback transaction.
- After 09B.1 hardening, the backend build passed with 0 warnings/errors. Unit tests
  cover level derivation, earned attribute budget, stat order, EF column mapping,
  two-handed exclusions, migration structure and endpoint security. The current
  default backend suite is 46 passed, 4 skipped.
  The development-route-disabled test passes. Actual concurrent character creation,
  equipment transactions and persistent API integration remain untested because the
  required tables do not exist and applying SQL is not authorized.

**Exact approval needed:** the owner must review and explicitly authorize applying
`backend/MigrationReview/CharacterPersistence09B.sql` to `ashenfall_dev`, including
its catalog, character, inventory and equipment guard triggers and grants. Before execution, recheck database identity,
catalog hash and table absence; use a schema-administrative identity, never
`ashenfall_app`. Run with `psql -X -w -v ON_ERROR_STOP=1 -d ashenfall_dev -f
backend/MigrationReview/CharacterPersistence09B.sql` only after that approval.
The script wraps creation in `BEGIN`/`COMMIT`, so an error before commit rolls back.
After commit, the prepared `CharacterPersistence09B.Rollback.sql` is suitable only
while all six new tables are empty. It refuses real character or authored data; a
data-preserving rollback requires a separate owner-approved plan. No backup or extra
database was created in this assignment.

## Files

Created `backend/MigrationReview/CharacterPersistence09B.sql` and the four
`backend/Ashenfall.Api/Characters/*.cs` files. Modified `backend/Ashenfall.Api/Program.cs`,
`backend/Ashenfall.Tests/CatalogApiTests.cs`, and added focused cases in
`backend/Ashenfall.Tests/CharacterAndCombatTests.cs`. No Godot scene changed.

Manual verification after the migration is approved: enable the local gate with a
private token and subject, create one character, verify a second concurrent create
conflicts, save using a revision, reject a stale revision, grant owned items through a
trusted server path, equip and unequip a two-handed weapon and off-hand item, delete
an unequipped item, then
soft-delete and re-create. These live checks are **pending**, not claimed here.
