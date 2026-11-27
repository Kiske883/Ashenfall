# Catalog balance CLI — isolated development administration

**Closure, 2026-11-26: 09C.4 manually accepted.** The owner completed discovery,
planning, publication, independent PostgreSQL/authenticated HTTP/Godot checks,
forward restoration and final verification. The active release is now
`a5897962-2202-4fe1-b7bb-99a12597ab5d`,
`critical-validation-restored-20261125`, V2 hash
`af01cb185a781e87a1619ca10d44f3550d841bdf904cb1c9de63c91f9fdd979e`.
The existing character remains at XP 100/revision 1, HP 105, damage 27–27,
5% critical chance and +100% critical bonus. Original/temporary releases are
retired and preserved. The earlier acceptance scenario below is historical;
do not rerun its apply commands or reuse its version names as part of closure.
Future changes require a fresh plan and explicit execution intent.

09C.4, 2026-11-24. This tool supports only the verified local
`ashenfall_09b3_test` database. It cannot target `ashenfall_dev` or another
database. No command switches targets, creates databases, runs migrations or
changes global roles. Read-only commands are safe to review and run now.
**Publication commands below must wait for owner authorization.** They were
not executed during implementation. SQL-only diagnostic files from 09C.3 are
obsolete for balance authoring; use this C# workflow instead.

## Build and credentials

Use a private PowerShell window; do not enable transcripts containing private
environment configuration. The CLI does not load `.env.local`, need an API
token or accept password/connection arguments. Existing private Windows pgpass
authentication supplies the PostgreSQL password. Current publication infrastructure
requires the local `postgres` identity; the runtime app role must not migrate
or publish. No extra permissions are granted by the tool.

```powershell
. 'C:\Propio\Proyectos\Ashenfall\backend\Initialize-AshenfallBalance.ps1'
Invoke-AshenfallBalance help
```

Dot-sourcing (the leading dot and space) registers the function in this session;
executing the script with `&` instead does not retain the function. The repository
and project paths come from the initializer's location, so both loading and
later invocation work independently of the current directory. Relative plan-file
paths still resolve against the caller's current directory; use absolute paths
for repeatable workflows.

Loading performs no build or database access. The first invocation builds if
needed, using a workspace-specific output folder under Windows TEMP. Subsequent
invocations check a source/project/MSBuild-input content signature and required
outputs, rebuilding only on changes/missing outputs. Build logs go to the host;
CLI stdout stays JSON and can be piped into `ConvertFrom-Json`. Native build or
CLI failure throws a PowerShell error after checking the exit code. No arguments
are added automatically; especially no `apply`, `--apply` or restore command.
No environment file, token, credential or database target is read by this wrapper.
The existing CLI still enforces every target, cluster and publication safeguard.
The isolated output avoids replacing an owner's running API binaries. No new
dependencies were introduced; an initial build restores existing project assets.

The cache includes the selected .NET SDK version and registered project/source
inputs. External custom MSBuild imports or in-place machine package changes
outside those inputs are not comprehensively tracked; explicit rebuild and
validation remain necessary after such development environment changes.

## Commands and expected output

All database commands display `Target: localhost:5432/ashenfall_09b3_test` on
stderr; stdout is JSON. Nonzero exit status means rejection or operational
failure. Unknown commands/options, duplicate options or unsupported parameters
are rejected. New plan files use exclusive creation; existing files are never
overwritten. Plans contain nonsecret IDs, values, hashes and a selected character
snapshot; keep them as review/audit artifacts, not as credentials.

The `Differences` entries serialize both `Before` and `After` on initial `plan`
and `restore-plan` output, in saved JSON, and in `validate` output. A read-only
reproduction after closure found no omission or hash change; no serialization
fix or plan-format change was made. If terminal scrollback/capture looks incomplete,
inspect the actual plan instead of relying on a truncated display:

```powershell
$reviewedPlan = Get-Content -LiteralPath $increasePlan -Raw | ConvertFrom-Json
$reviewedPlan.Differences | Select-Object Parameter, Before, After | Format-Table -AutoSize
```

`NoOp=true` may legitimately have an empty `Differences` array. Keep raw JSON
when piping CLI output; PowerShell object formatting is only a display view.

## Authenticated development HTTP verification

The development character endpoints require **`X-Ashenfall-Dev-Token`**.
Do not send `Authorization: Bearer`. The token must match the existing verified
Development API's configured token and subject. CLI PostgreSQL authentication
is separate and does not use this header. Before contacting a loopback API,
ensure that the actual API process targets `ashenfall_09b3_test`; the URL alone
does not establish database isolation. This example does not start/reconfigure
an API or create a character.

Read the existing token directly from the Windows **user** environment, even
if the current shell was opened before it was configured. Do not copy it to a
file, command argument, transcript or console. If the named user variable is
absent, stop and configure it privately to match the API; there is no fallback
to another identity/token. The token is used only in the in-memory header.

```powershell
$snapshotToken = [Environment]::GetEnvironmentVariable('ASHENFALL_DEV_IDENTITY_TOKEN', [EnvironmentVariableTarget]::User)
if ([string]::IsNullOrWhiteSpace($snapshotToken)) { throw 'The Windows user development token is not configured.' }
try {
    $snapshotHeaders = @{ 'X-Ashenfall-Dev-Token' = $snapshotToken }
    try {
        $snapshot = Invoke-RestMethod -Method Get `
            -Uri 'http://127.0.0.1:5093/api/v1/dev/character/gameplay-snapshot' `
            -Headers $snapshotHeaders -TimeoutSec 10 -ErrorAction Stop -Verbose:$false -Debug:$false
    } catch {
        throw 'Snapshot request failed; check the verified API, development identity and private token configuration.'
    }
    $snapshot | ConvertTo-Json -Depth 6
} finally {
    Remove-Variable snapshotHeaders, snapshotToken -ErrorAction SilentlyContinue
}
```

Expected current response: schemaVersion 2, the existing character and historical
class IDs, revision 1, restored release/hash above, level 2, HP 105, fixed/range
damage 27/27–27, chance 5 and bonus 100. XP is checked separately using CLI
`inspect`; it is not a field of the gameplay snapshot contract. Restart only the
Godot encounter for a freshly accepted snapshot after a future catalog change.
This example was reviewed as documentation, not executed as a new HTTP test
during closure; HTTP/Godot end-to-end success was confirmed by the owner.

## Command reference

| Command | Purpose |
| --- | --- |
| `status` | Active ID/version, publication date, V1/V2 hashes, schema and categories |
| `parameters [--category ...] [--filter ...] [--release GUID]` | Registered parameter keys, owning definition IDs, raw values, units, min/max and scale |
| `inspect --character GUID` | Explicit character XP/revision/original class and effective V2 snapshot; never selects an arbitrary character |
| `history` | All release identities, states, dates and available hashes |
| `diff --from GUID --to GUID` | Numeric balance differences and whether authored configuration differs |
| `plan --version VERSION --set KEY=VALUE [--set ...] [--character GUID] --out NEW.json` | Read-only planning from the current V2 release |
| `restore-plan --from GUID --version VERSION [--character GUID] --out NEW.json` | Read-only forward restoration from historical V2 content |
| `validate --plan FILE.json` | Regenerates and validates the reviewed plan against current state, without writes |
| `apply --plan FILE.json [--apply]` | Guarded atomic draft creation, validation and publication |

`apply` displays the target and requires either `--apply` (explicit unattended
execution intent), or an interactive exact `ashenfall_09b3_test` confirmation.
Redirected input cannot replace interactive confirmation. There is no automatic
apply after planning. Future GUI clients can call the same application services.

## Parameters and units

Use exact keys from `parameters`; no table/column names or arbitrary SQL are
accepted. Registry additions are compiled allowlist entries with numeric types,
units, scale and bounds, still checked by the existing domain validators.

- `class/<code>/...`: base HP, fixed damage, nullable base attributes/armor,
  movement/reach/timing, V2 damage bounds and critical fields.
- `enemy/<code>/...`: HP, damage, nullable base attributes and existing attack,
  movement and distance fields.
- `level/<class-code>/<level>/...`: existing thresholds, HP/damage/attribute-point
  budgets and V2 damage/critical bonuses. It edits existing rules, not new levels.
- `modifier/<item-code>/<stat-code>/<flat-or-percent>/amount`: existing item
  bonuses, including armor/weapon bonuses if present. It does not invent new
  modifiers or change item category, slot or stack structure.

Gems, skills, spells and experience rewards have no implemented catalog field
here and are not exposed. Existing modifier rows are discoverable; a stored
code does not imply a new gameplay mechanic. Attack-speed/armor effects are
not implemented by this milestone. Nullable attributes display `null`; setting
a numeric value authors a base value, and no new formula is introduced.

Values use invariant decimal points. Enter `5` for 5% chance, `100` for 100%
chance, and `100` for a **+100% damage bonus** (a total multiplier of 2).
Entering `0.05` means 0.05%, not 5%. Do not use `%` or comma separators.
Critical item bonuses are additive percentage points. Class/level critical
fields and modifier amounts accept at most four decimal places; distances/speeds
three; counts, health/damage and XP use integers. `parameters` prints exact
ranges; dependent constraints such as minimum <= maximum, timing order and
progression are additionally validated. HP/damage are also bounded by existing
snapshot safety limits; combined equipment bonuses can cause rejection even
when a single base value is in range.

Examples (read-only):

```powershell
Invoke-AshenfallBalance status
Invoke-AshenfallBalance parameters --category class --filter critical
Invoke-AshenfallBalance parameters --category modifier
Invoke-AshenfallBalance inspect --character d9a62a3c-4996-4276-8ab8-2c4d6f2ec988
Invoke-AshenfallBalance history
```

Multiple changes are one operation, e.g. `--set
class/dwarf_warrior/critical_chance_percent=8 --set
class/dwarf_warrior/critical_damage_bonus_percent=75` on the same `plan` command.
The plan lists before/after values, source ID/hash, historical configuration
ID/hash, proposed target hash, mapping counts and optional predicted effective
snapshot. It is a prediction from detached data, **not an already-published API
response**. Omit `--character` only if no individual prediction is needed;
compatibility still validates all persisted characters and inventory.

## Manual acceptance: 5% -> 100% -> 5%

These steps reuse the existing character; never create or delete it. Plans
contain random operation IDs, so hashes below are generated, not fixed examples.
Use fresh version names if a version already exists; never overwrite history.

### 1. Prepare and review, without database writes

```powershell
$manualCharacterId = 'd9a62a3c-4996-4276-8ab8-2c4d6f2ec988'
$baselineReleaseId = '91000000-0000-0000-0000-000000000001'
$manualPlans = Join-Path $env:TEMP ('AshenfallBalanceReview-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $manualPlans | Out-Null
$increasePlan = Join-Path $manualPlans 'critical100.json'
$restorePlan = Join-Path $manualPlans 'restore5.json'
Invoke-AshenfallBalance status
Invoke-AshenfallBalance inspect --character $manualCharacterId
Invoke-AshenfallBalance plan --version critical-validation-100-20261124 --set class/dwarf_warrior/critical_chance_percent=100 --character $manualCharacterId --out $increasePlan
Invoke-AshenfallBalance validate --plan $increasePlan
```

Baseline must be `snapshot-09c3-v2`, hash
`d1ef0a212b83df17300088210d081a63951621b2602e7ff35cf2a711c4de7d4d`, XP 100,
revision 1, level 2, HP 105, damage 27–27, chance 5, bonus 100. The plan must
show one difference: chance 5 -> 100, with predicted HP 105, damage 27–27,
bonus 100, unchanged original class and revision. The current dataset produces
two class and eight item mappings. Check the actual output; do not force these
counts if the environment changes. Stop on unexpected source, values or warnings.

### 2. Phase A — execute only after owner authorizes publication

```powershell
Invoke-AshenfallBalance apply --plan $increasePlan --apply
Invoke-AshenfallBalance status
Invoke-AshenfallBalance inspect --character $manualCharacterId
```

Expected result `Published`, new active ID/version/hash, XP 100/revision 1,
HP 105, damage 27–27, chance 100 and bonus 100. Repeat the **same plan** to
recover from an uncertain command outcome: matching published/retired operation
returns `AlreadyApplied`, never reactivation or another release. If the command
failed, inspect status/history; do not change IDs or retry against another DB.

Use the existing authorized development API and Godot editor environment for
this character. Ensure that API process points to the test database; loopback
alone does not prove the database target. The API subject must remain
`manual-09c2b1-d9a62a3c-4996-4276-8ab8-2c4d6f2ec988`, with the same private
process token already used by the editor. Do not run the character-creation
section of the older preview guide. Call the authenticated
`GET /api/v1/dev/character/gameplay-snapshot` through the existing authorized
local client/session and check V2 release/hash and effective values. The CLI's
`inspect` uses the same factory on PostgreSQL data but is **not HTTP validation**.

Stop the F6 game and rerun `res://scenes/world/CharacterPersistencePreview.tscn`.
Every eligible successful hit must be critical/yellow, normally 54. A lethal
number can be smaller because actual health removed is displayed. No extra
critical text or size change. Failed/interrupted attacks are not successful hits.

### 3. Phase B — plan and authorize forward restoration separately

```powershell
Invoke-AshenfallBalance restore-plan --from $baselineReleaseId --version critical-validation-restored-20261124 --character $manualCharacterId --out $restorePlan
Invoke-AshenfallBalance validate --plan $restorePlan
# Stop for owner authorization before the following publication command.
Invoke-AshenfallBalance apply --plan $restorePlan --apply
Invoke-AshenfallBalance status
Invoke-AshenfallBalance inspect --character $manualCharacterId
Invoke-AshenfallBalance history
```

Expected new active restoration release with 5% chance and all original balance
values, XP 100/revision 1 and original class ID. Original V2 release/hash remain
historical and retired; neither is overwritten or reactivated. Temporary release
also remains retired. The restored hash differs because provenance/IDs/version
are new. Repeat the HTTP check and restart the F6 scene for the restored snapshot.

If restore planning finds incompatible inventory or allocations, stop; do not
silently modify persistence to make restoration succeed. A restoration is a
normal validated publication, not a bypass. It may fail after newer progression.

## Refresh, errors and operational limits

The V2 reader queries PostgreSQL per request inside read-only REPEATABLE READ;
no reader cache or backend restart is needed for supported fields. Concurrent
requests may validly return the old pinned view if they began before commit.
Godot pins encounter statistics. Stop/restart the scene for a new snapshot;
F9 catalog refresh must not alter current HP or combat damage mid-encounter.
An active encounter can continue offline using its accepted data.

`validate` rechecks source identity/hash, planned content, mapping counts and
the selected character's expected snapshot. Publication acquires the existing
publisher advisory lock and table locks **before** reloading/rechecking. Another
publication, changed character revision/equipment, or hash mismatch can invalidate
the plan. Replan/review; do not edit its hashes manually. All effective characters
and owned inventory are revalidated at apply time even if not selected for preview.

No-op edits return `NoOp`, keep the active hash and create no draft/release.
Version/operation collisions with other content reject. Unknown fields, fractional
integers, unsupported precision and incompatible references reject. Provider
errors are reported by type without connection strings. Authentication errors
require repairing existing private credentials locally, not weakening guards.

Only V2 configurations with complete progression/manifests can be edited or
restored; V1 releases can appear in history but are not silently upgraded.
Numeric diffs cover supported balance fields. Structural content participates
in the configuration signature and is copied during restoration, but the CLI
is not a structural catalog editor. The original plan file plus release history
and `diff` provide the audit trail; no new database audit table was introduced.

The PostgreSQL apply/restore/concurrency tests are opt-in. They have now passed
under the owner's specific authorization for rolled-back integration testing:
**2 passed, 0 failed, 0 skipped**; independent fingerprints matched afterward.
The concurrency test cleanup was corrected to avoid a duplicate rollback and
now observes the actual advisory-lock blocker in PostgreSQL. See the current
implementation report for evidence. This authorization did not include persistent
publication or the manual 100% plan. For a separately authorized rerun:

```powershell
$env:ASHENFALL_09C4_WRITE_TESTS = '1'
try {
dotnet test backend/Ashenfall.Tests/Ashenfall.Tests.csproj --no-restore "-p:BaseOutputPath=$env:TEMP\Ashenfall09C4Tests\" -p:UseAppHost=false --filter FullyQualifiedName~BalanceManagementLiveTests
    if ($LASTEXITCODE -ne 0) { throw 'Live balance validation failed.' }
} finally { Remove-Item Env:ASHENFALL_09C4_WRITE_TESTS -ErrorAction SilentlyContinue }
```

These tests temporarily write drafts and transition releases inside transactions
that are explicitly rolled back. That still counts as database writes and
requires approval. There is no automatic fallback or new database provisioning.
The successful rollback-only tests do not replace manual persistent publication,
authenticated HTTP and Godot visual acceptance.
