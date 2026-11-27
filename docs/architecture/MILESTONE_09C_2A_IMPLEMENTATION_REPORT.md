# Milestone 09C.2A — character gameplay snapshot

## Status and important limitation

The backend endpoint and its read-only transaction are implemented. **A live
successful snapshot remains unvalidated:** the currently published isolated
test release has no `class_level_rules`, and the existing 09C.2 audit reports
the approved prototype release has none. This endpoint deliberately rejects
missing rules rather than inventing progression. Consequently that prototype
cannot produce a successful snapshot until suitable authored rules are available
through an approved catalog process. The pre-existing `/stats` endpoint keeps
its legacy level-one fallback; its behavior was not changed.

## Contract and error behavior

`GET /api/v1/dev/character/gameplay-snapshot` is behind the existing Development,
explicit enablement, local subject, token and loopback gate. Account and active
character are resolved on the server; query parameters cannot select an account.
Missing account or active/soft-deleted character returns 404. Invalid or missing
authoritative data returns a generic 500. PostgreSQL connectivity errors return a
generic 503; absent/wrong token and nonlocal requests retain the existing 401 and
404 semantics. The endpoint makes no writes and does not create accounts.

Illustrative synthetic response (not a real catalog hash):

```json
{
  "schemaVersion": 1,
  "characterId": "11111111-1111-4111-8111-111111111111",
  "characterRevision": 3,
  "classDefinitionId": "22222222-2222-4222-8222-222222222222",
  "catalogReleaseId": "33333333-3333-4333-8333-333333333333",
  "catalogVersion": "example",
  "catalogHash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "effectiveStats": { "level": 2, "maxHealth": 121, "fixedDamage": 30 }
}
```

Transport bounds are level >= 1, maxHealth 1–10,000 and fixedDamage 0–1,000.
The latter two are provisional safety bounds matching existing Godot export
ranges, not approved long-term game design limits. Empty IDs, nonpositive
revision, missing rules, mismatched references, invalid published metadata and
out-of-range values fail closed. The factory uses `CharacterRules` once, with a
strict option that rejects negative/overflowing computed values instead of
silently clamping; the existing calculation's default behavior is preserved.
Nullable base attributes use the existing zero convention. Crit, speed and
armor-mitigation calculations are not added.

The catalog hash is the stored V1 metadata, validated for format and associated
with the character class's published release. The endpoint does **not** recompute
the V1 hash. V1 covers the original catalog tables, not progression or equipped
item modifiers; it must not be represented as attesting those extensions. A
future versioned extension provenance/hash and publisher policy needs separate
design and approval.

## Consistency and security

The reader uses one `CharacterDbContext`, one connection and a short PostgreSQL
`REPEATABLE READ` transaction with `SET TRANSACTION READ ONLY` before the first
query. Account, character, class, release, rules and equipped modifiers are read
within it. Class and release use the existing catalog EF mappings added to the
character context; no schema changes or migrations were made. A concurrent
service-driven update may commit, but the reader continues to see its initial
database snapshot. Character revision reflects that same view; it is not a hash
of catalog extension content.

## Tests and evidence

- Backend build: zero warnings/errors. Full backend suite with explicitly opted
  in isolated tests: **59 passed, 0 failed, 4 skipped**. The four skips are the
  publisher tests targeting the development catalog; they were not authorized.
- Unit tests verify DTO JSON field names, identity/revision/provenance, level,
  flat and percent modifiers, zero damage, health/damage bounds, missing rules,
  invalid release/IDs and negative computed values. No critical-hit field or
  armor mitigation is introduced.
- In-process HTTP tests verify missing/wrong token, nonlocal rejection,
  production environment route absence and controlled database failure.
  Isolated PostgreSQL HTTP tests verify 404 for absent account, a client-supplied
  account/character query cannot bypass that identity, and a real character with
  missing progression yields 500 rather than a fabricated successful snapshot.
- A live isolated PostgreSQL test instruments the actual reader. It verifies
  `transaction_read_only=on` and `transaction_isolation=repeatable read`, pauses
  after its account read, commits a soft delete on another connection, and shows
  the reader still sees the old active character while a subsequent transaction
  sees no active character. The old view fails closed due to the deliberately
  missing progression. Test rows are uniquely identified and scoped for cleanup.
- A real **200** snapshot, equipment-modifier read and concurrent equipment
  update have not been tested against PostgreSQL: no valid published isolated
  rules/modifiers exist and altering the published test catalog was outside the
  authorized scope. Unit-calculated synthetic responses are not a substitute for
  this missing live coverage. The test database remains available for separately
  authorized fixture preparation.
- A read-only check after the final live run found zero test accounts, characters,
  items or equipment and the synthetic catalog unchanged. Cleanup should be
  rechecked after future live runs. No development database was accessed or
  modified by this milestone, and no Git operations were performed.

## Files and rollback

Created `backend/Ashenfall.Api/Characters/CharacterGameplaySnapshot.cs`,
`backend/Ashenfall.Tests/CharacterGameplaySnapshotTests.cs` and this report.
Modified `CharacterDbContext.cs`, `CharacterRules.cs`, `CharacterEndpoints.cs`,
`CharacterEndpointSecurityTests.cs`, `CharacterConcurrencyLiveTests.cs`,
`CharacterMigrationStaticTests.cs` and `Ashenfall.Tests.csproj`. The static
mapping test now also checks catalog mappings against the existing reviewed
catalog SQL. Preserve all pre-existing uncommitted work when reviewing or rolling
back: remove only these 09C.2A additions and restore the prior function signature
and local changes selectively; do not reset whole files. No database rollback is
needed.

## Before 09C.2B

Prepare and approve a safe isolated published fixture with level rules and
equipment modifiers, then validate real successful snapshots and concurrent
equipment changes. Decide how approved catalog releases acquire required rules
and how extension provenance is verified. Approve client-side snapshot use,
HP/reconnect semantics and bounds before applying anything to gameplay. No
Godot integration or gameplay change is included here.
