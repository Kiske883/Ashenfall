# 09C.3 — deterministic feedback validation and balance review

2026-11-23. Part A is implemented using the existing isolated validation script.
Part B is a proposal only. No API/backend/gameplay source, database, migration,
release, character or credentials were changed or accessed. No Git operations
were performed. Database integration opt-ins were disabled for testing.

## A. Actual renderer validation

Modified only `docs/validate_variable_damage.gd` and added this report.
The script already creates an isolated in-memory first-adventure instance,
issues real melee commands and waits for successful damage application. It
uses existing test-profile properties only inside that process; it adds no
player-accessible option and saves no scene changes. It observes the actual
`AdventureCombatFeedback` Label3D, not a duplicate renderer.

The existing production path is damage calculator result -> controller
`LastImpactCritical` -> successful `DamageApplied` -> deduplicated feedback ->
`SpawnNumber`. Recipient and critical flag remain separate. Normal 30 and
critical 30 deliberately have equal amounts, proving that color comes from
the confirmed critical flag rather than a guess based on damage. These test
inputs do not replace the character's 27 / 54 reference statistics.

This continuation adds explicit normal/critical RGB difference checks,
baseline size/pixel-size/duration checks and a validation-only 60-second watchdog.
Existing assertions cover numeric-only text, ivory normal and yellow critical,
shared font/size/outline/billboard, recipient offset, upward motion, fade,
duration, single-number emission and cleanup. Received normal damage stays
crimson; received critical damage is yellow at the same player offset.
No runtime renderer or combat formula change was necessary.

### Reproduce locally

Run in a fresh PowerShell window. Environment removals apply only to this
shell and its children, not the owner's existing editor or API processes.

```powershell
Set-Location 'C:\Propio\Proyectos\Ashenfall'
Remove-Item Env:ASHENFALL_CATALOG_API_URL, Env:ASHENFALL_DEV_VARIABLE_DAMAGE -ErrorAction SilentlyContinue
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan --script res://docs/validate_variable_damage.gd -- --capture
if ($LASTEXITCODE -ne 0) { throw 'Visual validation failed.' }
```

This opens an automated game window and exits after the checks. No token,
character selection, API or PostgreSQL connection is required. The catalog
endpoint must remain unset to avoid contacting an unrelated local API.
For nonvisual checks replace the command with:

```powershell
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --script res://docs/validate_variable_damage.gd
```

Actual fresh Vulkan Forward+ captures were generated and visually inspected:

- `%APPDATA%/Godot/app_userdata/Ashenfall/milestone09c3_normal_damage.png`
- `%APPDATA%/Godot/app_userdata/Ashenfall/milestone09c3_critical_damage.png`

Both show numeric `30` above the same Guardian, with matching font and position;
the normal is ivory and the critical yellow. Existing common impact feedback
and HUD remain present, unchanged. No new critical-specific effects exist.
Captures replace only these validation-generated user-data images.

### Executed checks

| Check | Result |
| --- | --- |
| Godot C# `dotnet build --no-restore` | Passed; zero warnings/errors |
| Backend build, isolated temporary output | Passed; zero warnings/errors |
| Backend suite, database opt-ins disabled | 76 passed, 0 failed, 13 skipped; 89 total |
| Vulkan visual validation and image capture | Passed; zero failures, GTX 650 Forward+ |
| Headless visual validation | Passed; zero failures |
| `validate_standalone_isolation.gd` | Passed; actual 100/100 HP and 25 damage, no catalog endpoint |

Backend command used existing restored dependencies:

```powershell
Remove-Item Env:ASHENFALL_09C3_PUBLISHER_LIVE, Env:ASHENFALL_09B5_LIVE, Env:ASHENFALL_RUN_DB_TESTS -ErrorAction SilentlyContinue
dotnet build backend/Ashenfall.Api/Ashenfall.Api.csproj --no-restore "-p:BaseOutputPath=$env:TEMP\Ashenfall09C3Feedback\" -p:UseAppHost=false
dotnet test backend/Ashenfall.Tests/Ashenfall.Tests.csproj --no-restore "-p:BaseOutputPath=$env:TEMP\Ashenfall09C3Feedback\" -p:UseAppHost=false
```

The 13 skipped tests require database opt-in. They were intentionally not run:
this task does not authorize changing either database. No new authenticated
HTTP or live persistence test is claimed. Part A verifies the actual renderer
deterministically; final owner acceptance in ordinary gameplay is separate.

## B. Catalog balance architecture assessment

The immutable release model is appropriate for reproducibility. The current
authoring workflow is cumbersome because its test seeder is a one-off, pinned
to specific release identities and values. Routine changes should not require
manual cloning, hand-authored successor rows or recomputing hashes externally.
An administrative preparation layer can reuse existing services without a new
runtime balance subsystem or weakened database integrity.

### Existing data ownership

| Table / metadata | Existing balance content and role |
| --- | --- |
| `character_classes` | Base maximum HP, fixed damage, nullable strength/dexterity/vitality/armor, movement/reach/timing; V2 minimum/maximum damage, critical chance and critical damage bonus |
| `enemy_definitions` | HP, fixed damage, attributes, attack range/windup/recovery/impact delay and movement/AI distances |
| `item_definitions` | Release-specific item identity, category, equip slot/handedness, stackability/max stack and visual metadata; actual bonuses are separate |
| `class_level_rules` | XP thresholds, health/damage bonuses, allocated-point budgets, V2 damage-range and critical bonuses |
| `item_stat_modifiers` | Flat/percentage health, damage and attribute modifiers; existing critical codes use flat percentage points |
| `catalog_releases` | Version, draft/published/retired lifecycle, V1 hash and publication metadata |
| `catalog_v2_manifest` | Versioned V2 canonical content hash |
| `catalog_class_successors`, `catalog_item_successors` | Explicit historical-to-active definition resolution, covered by the V2 hash |
| `player_characters`, `owned_items`, `character_equipment` | Player XP, allocations, revision and original owned/equipped references; persistence state, not release authoring data |

Published and retired definitions, progression/modifiers, manifests and their
target-release mappings are guarded. The only supported release-state changes
are draft publication and published retirement; identity is immutable.
Player persistence is mutable through its existing authorized services and
revision rules, not through balance publication.

Gems, skills and spells have no dedicated implemented catalog/calculation path
in the reviewed code. Some modifier codes exist without implemented gameplay
semantics (for example attack speed). A future authoring tool must not pretend
such data automatically works. No speculative tables or formulas are proposed.

### What one supported change requires today

For critical chance 5 -> 8, create a new draft release and new release-specific
definitions, copy the existing complete content, change the intended class
field while draft, copy progression/modifiers and build explicit mappings.
Compute V1 using `CatalogHash.Compute`, then V2 using
`CombatCatalogHashV2.Compute`. V2 covers combat statistics, progression,
modifiers and mappings as well as the V1 content. The current seeder does not
offer a generic command for this; raw DBeaver edits cannot invoke the C# publisher.

`V2CatalogPublication.PublishAsync` already validates the persisted candidate,
hashes, classes, inventory quantities, equipment constraints and effective
snapshots. It uses READ COMMITTED after advisory/table locks to see the latest
committed state while excluding relevant writers, then retires the previous
release and activates the candidate atomically. Failure rolls back. Exactly
one published release is checked and enforced by the partial unique index.
Ordinary snapshot readers remain available through MVCC. The current entry point
is explicitly restricted to the verified isolated database and administrative
identity; it is not a production-ready publication service or authorization API.

Retired releases cannot be reactivated because `catalog_guard_release` explicitly
rejects that transition and the publisher accepts only drafts. This is a current
lifecycle policy, not a mathematical requirement of hashing. It provides a
monotonic publication history; that benefit is an architectural interpretation,
not proof of the original author's intent. No change to this policy is needed
for practical balance editing.

A previous configuration can be republished as a **new** release with fresh
definitions and mappings, after current compatibility checks. Its values may
match the old configuration, but its release ID, version and hashes differ.
Original IDs/hashes remain intact historically. Rollback may be rejected if
newer character allocations or inventory are incompatible with the old values;
never repair player state silently to force rollback.

### Recommended developer workflow — proposal only

Extend the existing C# test authoring/publisher tooling with a bounded CLI,
initially restricted to `ashenfall_09b3_test`:

1. Select the verified active release and a typed, allowlisted existing field
   using definition code plus expected identity, e.g. dwarf critical chance.
2. Enter the proposed value and request a dry-run diff. Validate field bounds,
   intended scope and unchanged unrelated values. Record expected source hash.
3. Automatically allocate a unique draft/version and definition IDs, copy the
   full configuration, patch only the requested value, and copy rules/modifiers.
   Generate explicit mappings from **every required historical persisted
   reference** directly to candidate definitions; no name matching or assumed
   transitive chains. Use known authored identity relationships and reviewed
   source mappings; reject ambiguous or unmapped references.
4. Run existing validators and effective-stat examples, compute both canonical
   hashes, and display the exact diff and predicted snapshot changes. Never
   accept a user-supplied hash as proof. No automatic publication at API startup.
5. Publish through the existing transaction implementation. Recheck expected
   source release/hash under the publication lock so a dry-run cannot publish
   against a silently changed baseline. Reuse the savepoint entry point where
   preparation/publication share a transaction; don't validate stale state.
6. Verify exactly one active release and retrieve the actual authenticated
   gameplay snapshot. Compare values and character/inventory fingerprints.
7. Start a new Godot encounter to accept its new pinned snapshot. Existing
   encounters continue with their accepted statistics; no HP reset or mid-attack
   damage replacement.
8. To restore, clone the previous configuration into another new draft and repeat
   all checks. Keep every published historical release; no trigger bypass.

Preparation-only and publish operations should be explicit, with collision
checks and safe rerun behavior. Reject an already published candidate instead
of overwriting it. Store a nonsecret audit diff, source and result identities,
hashes and result; never tokens or connection strings. Prefer a CLI initially;
an administrative network service would require separately approved identity,
authorization and audit design.

### Characters, snapshots and future live balancing

Characters keep their original class ID, XP, revision, inventory and equipment
IDs. Readers resolve those historical references to the active release. Changing
balance can therefore change effective statistics without a character revision
increment; cache keys must include character revision **and** catalog release/hash.
Client identity and revision checks alone are insufficient. In-process or remote
caches must not serve the old effective snapshot as the current catalog result.

The gameplay snapshot reader already selects the active release per request
inside a read-only REPEATABLE READ transaction and recomputes its V2 hash. Each
request sees one consistent release; a concurrent publication can legitimately
leave that request on the old snapshot. A subsequent request sees the new one.
Godot currently pins the accepted snapshot until the encounter ends. A new
encounter must fetch and validate again; F9 catalog reload does not authorize
hot-changing active persistence combat statistics. No current-HP healing or
character persistence rewrite should accompany a balance publication.

Existing supported HP/damage/critical fields can support future balance edits
without new Godot code through the current V2 contract. Enemy parameters and
movement/timing have a separate catalog bootstrap path; the persistence preview
does not automatically apply every catalog field. Legacy `/stats` and other
equipment/inventory consumers must be audited for consistent release resolution
before claiming system-wide balancing. New mechanics, gems, spells or new
formulas will require explicit contract/calculation/client work.

Future multiplayer should apply accepted release transitions at an agreed
server-controlled encounter boundary, with authoritative server hit resolution.
The current local Godot simulation and development token are not a multiplayer
security boundary. Immediate mid-encounter balancing is a separate design.

### Comparison and bounded future milestone

| Aspect | Current workflow | Proposed extension |
| --- | --- | --- |
| Routine effort | Hand-prepared full clone, IDs, mappings and hashes; pinned seeder | Typed field edit, automatic draft generation, reviewed diff, explicit publish |
| Integrity | Immutable history, canonical hashes, guarded atomic publication | Same infrastructure and rules |
| Restoration | Cannot reactivate; manually prepare forward release | Automate forward restoration with fresh compatibility validation |
| Maintenance | One-off fixtures and scattered operational instructions | One test-only authoring entry point reusing validators/hash/publisher |
| Main risk | Manual mapping omissions or bypassing publisher in SQL | Tool must check source staleness, complete history and all affected consumers |

Proposed next milestone: **Catalog Balance Authoring CLI — isolated prototype**.
Scope: draft-clone existing fields, allowlisted edits, deterministic diff/hashes,
complete historical mappings, source-staleness check, guarded publication and
forward restoration. Validate effective snapshots, rejected edits, rollback,
concurrent publishers and preservation of player fingerprints using separately
authorized disposable fixtures. No new schema, GUI, production target, roles,
authentication system, hot combat updates, gems/skills/spells or Godot mechanics.
Review production identity and permissions separately before expanding scope.

No part of this administrative workflow was implemented in this task. Rollback
of this task means removing only its validation assertions/watchdog and this
report, preserving all pre-existing renderer, catalog and uncommitted work.
