# 09C.4 — final review and closure

## Pre-commit security remediation — 2026-11-27

**GO for owner review and inclusion of this remediation in the consolidated
commit.** The earlier staged-only audit identified one medium-severity issue:
the persisted-character client validated the initial loopback URL but the preview
constructed a default `HttpClient`. Its handler could automatically follow 30x
responses and retain the custom `X-Ashenfall-Dev-Token` header at another
destination. This was a transport-boundary weakness, not a discovered committed
real credential or evidence of an actual leak.

### Correction and construction-path review

- `scripts/characters/PersistedCharacterClient.cs`: added a shared owned-client
  factory with `HttpClientHandler.AllowAutoRedirect=false`. Both authenticated
  methods explicitly reject all 300–399 responses with a generic error that
  includes neither Location nor token. No manual redirect following exists.
- `scripts/characters/CharacterPersistencePreview.cs`: the sole game/runtime
  construction path now uses this factory. Caller ownership/disposal, initial
  URL restrictions, authentication header, timeout/size limits, validation and
  encounter-stat pinning remain unchanged.
- `backend/Ashenfall.Tests/CharacterConcurrencyLiveTests.cs`: both TestServer
  clients injected into the persisted-character client explicitly disable
  redirects via `WebApplicationFactoryClientOptions`. These database tests
  were compiled but not executed in this remediation.
- `backend/Ashenfall.Tests/PersistedCharacterClientTests.cs`: the offline socket
  test uses the production factory; added real-loopback transport security and
  success/URL-validation cases described below. Mock transports have no automatic
  redirect mechanism. The unauthenticated catalog client carries no development
  token and was not changed.

The existing injected-`HttpClient` constructor is retained for test doubles and
in-process specialized clients. Such callers must supply a nonredirecting
transport; a post-response URI check alone cannot prevent a header being sent.
All current construction paths have been reviewed. Future network consumers
must use the shared safe factory rather than inject a default redirecting client.

### Actual regression evidence

Focused suite: **42 passed, 0 failed, 0 skipped**.

- 30 TCP cases: statuses **301, 302, 303, 307, 308**, each with relative,
  absolute same-origin and cross-origin Location, for both character retrieval
  and gameplay snapshot retrieval. Cross-origin uses a separate loopback port.
- Each case creates actual ephemeral `TcpListener` HTTP servers and uses the
  exact production `CreateHttpClient()` factory. The original listener receives
  one authenticated request and the client rejects the redirect. Neither a
  second same-origin target request nor a cross-origin destination request is
  observed. The error exposes no token/destination.
- Each destination then receives a separate unauthenticated control probe,
  proving the listener is live and observable; that request contains no auth
  header and is the only destination request. This is transport-level evidence,
  not a mocked 30x response standing in for redirect behavior.
- Two real TCP 200 cases verify successful authenticated retrieval using
  synthetic tokens for both methods. Two further cases reject invalid initial
  URL forms before any server request. Existing invalid snapshot, missing,
  authentication/service error and offline tests remain passing.

All listeners bind only `127.0.0.1` with OS-assigned ports, have bounded lifetime
and are disposed. Only synthetic credentials are used. No external HTTP service,
API process, PostgreSQL connection, real token or persisted character is needed.

Complete backend suite with database write opt-ins disabled:
**132 passed, 0 failed, 15 skipped, 147 total**. The fifteen skips are the
pre-existing opt-in database tests, including live balance tests; none ran here.
Godot C#, backend/API/publisher/tests and Balance CLI builds passed with zero
warnings/errors. Headless standalone isolation passed with 100/100 HP and fixed
damage 25 without a catalog endpoint. Headless persistence-offline validation
also passed: no encounter starts without an accepted snapshot.

Example focused command, from repository root (all database opt-ins unset):

```powershell
dotnet test backend/Ashenfall.Tests/Ashenfall.Tests.csproj --no-restore "-p:BaseOutputPath=$env:TEMP\AshenfallRedirectSecurity\" -p:UseAppHost=false --filter FullyQualifiedName~PersistedCharacterClientTests
```

No live authenticated API or manual GUI encounter was rerun; the existing
manual gameplay acceptance remains historical evidence. Injection of an unsafe
custom transport by a future caller remains a responsibility documented on the
factory; current production and in-process construction paths are corrected.

### Review and index ownership

Only the four source/test files above and this review changed. Corrections are
**unstaged**. The owner’s 91 staged files are untouched; staged diff content was
fingerprinted before/after to verify index preservation. Whitespace checks on
both staged and unstaged diffs pass. No database access, writes, migrations,
schema/catalog/player changes, staging or other Git write operation occurred.
The original staged version still contains the finding until the owner reviews
and stages these changes; this recommendation applies to the remediated working
tree, not to committing the unchanged old index.

2026-11-26. **GO — Milestone 09C.4 accepted and closed.** The owner confirmed
manual end-to-end acceptance. Closure adds operational PowerShell convenience
and documentation; it does not change catalog business rules, authorization,
canonical hashing, plan format, gameplay or database state.

## Accepted implementation

Reusable `BalanceManagementService`/`BalancePlanner` services and the typed
parameter registry support discovery, inspection, read-only machine-readable
plans, validation, diffs/history, atomic publication, no-op/replay handling and
forward restoration. The CLI uses the existing C# canonical hashes and
transactional V2 publisher. Historical references are mapped explicitly without
rewriting character or inventory identities. Published content remains immutable.

Owner-confirmed manual evidence:

1. Discovered parameters and inspected effective character statistics.
2. Planned/validated the critical chance change, then published transactionally.
3. Independently checked PostgreSQL and the authenticated HTTP Gameplay Snapshot
   V2; verified 100% critical chance and yellow critical damage in Godot.
4. Planned/validated and transactionally published forward restoration.
5. Confirmed historical release preservation, final HTTP snapshot and Godot
   combat at the original 5% configuration.

These manual actions belong to the owner, not an automated publication performed
in this closure. Earlier rollback-only live tests passed 2/2 after their cleanup
correction, as recorded in the implementation report. They were not rerun here.

## Final catalog and character

Read-only CLI inspection during closure independently confirmed:

| Value | Current state |
| --- | --- |
| Database | `ashenfall_09b3_test` |
| Active release | `a5897962-2202-4fe1-b7bb-99a12597ab5d` |
| Version | `critical-validation-restored-20261125` |
| V1 hash | `aa3e1558ea6c96397f70cc2f957d9e1143756cbde9683c7e51a0408c2176cee9` |
| V2 hash | `af01cb185a781e87a1619ca10d44f3550d841bdf904cb1c9de63c91f9fdd979e` |
| Character | `d9a62a3c-4996-4276-8ab8-2c4d6f2ec988` |
| Historical class | `90000000-0000-0000-0000-000000000002` |
| Level / XP / revision | 2 / 100 / 1 |
| Maximum health | 105 |
| Fixed damage and min/max | 27 and 27/27 |
| Critical chance / damage bonus | 5% / +100% |

The active canonical hash was recomputed through the existing reader/factory
path. History showed the original V2 release
`91000000-0000-0000-0000-000000000001` retired, preserving V2
`d1ef0a212b83df17300088210d081a63951621b2602e7ff35cf2a711c4de7d4d`.
The temporary 100% release `cdf4fcff-112e-40b7-b3f2-3c9421030a49`, version
`critical-validation-100-20261125`, remains retired with V2
`c610d9a9c6872fb1e5db7a1be70f684166747f5ad3addccb5f0d706260c17bc5`.
Original and temporary content were also hash-verified while preparing read-only
restoration candidates. No historical release was reactivated.

No new publication, database write, fixture, schema change, migration, trigger
modification or cleanup was executed. `ashenfall_dev` was never accessed.
Additional live inventory fingerprints were not collected in this closure;
earlier live validation documented matched fingerprints, and owner acceptance
confirms persistence preservation. No new claim of live inventory testing is made.

## Findings and resolutions

### Apparent missing `After`

Reproduced both commands **before making changes**, with stdout captured in
memory, saved plan JSON parsed independently and `validate` output compared:

| Command path | Initial stdout | Saved JSON | Validated JSON | Hash |
| --- | --- | --- | --- | --- |
| `plan`, chance 5 -> 100 | Before 5, After 100 | After 100 | After 100 | unchanged |
| `restore-plan`, historical 100% content | Before 5, After 100 | After 100 | After 100 | unchanged |

Since the accepted current catalog is already back at 5%, the nonempty
restore-plan reproduction selected historical 100% content only as a detached
candidate. It was never applied. A further read-only restoration plan from the
original 5% baseline correctly returned `NoOp=true`, empty `Differences` and the
current active hash. Empty differences are not missing serialized properties.

No serialization defect was reproduced. Console truncation/capture or PowerShell
display formatting is consistent with the evidence; the exact original terminal
event cannot be reconstructed. Both commands and `validate` use the existing
serializer/options. Consequently no serializer fix, plan format change, hash
change or new C# regression test for a nonexistent defect was introduced.
Existing plan JSON round-trip tests passed. The guide explains how to inspect
the complete saved JSON and its Before/After columns.

### Permanent PowerShell initialization

Added `backend/Initialize-AshenfallBalance.ps1`. Load it with:

```powershell
. 'C:\Propio\Proyectos\Ashenfall\backend\Initialize-AshenfallBalance.ps1'
Invoke-AshenfallBalance help
Invoke-AshenfallBalance status
```

Dot-sourcing registers the function; loading performs no build or database call.
The closure preserves repository paths independently of later working-directory
changes. SDK resolution/build run from the repository, then restore the caller's
location; CLI commands run in the caller's location so relative plan paths retain
their expected meaning. The wrapper never adds publication arguments.

The first command builds into a workspace-specific TEMP cache if outputs or the
signature are missing/outdated. Signature inputs include the selected SDK version,
source/project files and registered repository MSBuild inputs. Build logs remain
on the host stream; CLI stdout stays parseable JSON. Both build and CLI native
exit codes are checked explicitly. Errors throw without terminating the caller's
PowerShell session. No .env/token/credential reads or target changes are added.

Validated on **Windows PowerShell 5.1.19041.6456**: parser, dot-source, alternate
working directory, first build, unchanged cache reuse, fresh session reload,
StrictMode, clean JSON stdout, nonzero CLI exit handling and caller location
preservation on success/failure. SDK-aware cache reuse also passed. PowerShell 7
has not been executed here; its native-command preference is handled locally,
but cross-version compatibility is not claimed as independently tested.

### Development HTTP authentication

Updated the CLI and manual preview guides: development endpoints require
`X-Ashenfall-Dev-Token`, **not** `Authorization: Bearer`. The GET example reads
`ASHENFALL_DEV_IDENTITY_TOKEN` directly from the Windows user environment and
uses it only in an in-memory header. It prints only the snapshot and removes
temporary token/header variables afterward. Missing configuration and request
failures stop with secret-free messages. It does not start or reconfigure an API.

The target example is
`GET http://127.0.0.1:5093/api/v1/dev/character/gameplay-snapshot`.
The process behind the endpoint must still be independently verified as using
the isolated database and expected subject/token. The example was not executed
as a new HTTP test in this closure; successful HTTP/Godot acceptance is attributed
to the owner. No authentication implementation was changed.

## Automated results from this closure

| Validation | Result |
| --- | --- |
| Backend regression suite | **98 passed, 0 failed, 15 skipped; 113 total** |
| Godot C# build | Passed, 0 warnings/errors |
| Backend/API/publisher/CLI builds | Passed, 0 warnings/errors |
| Standalone headless isolation | Passed, actual 100/100 HP and 25 damage without catalog endpoint |
| PowerShell initializer checks | All executed checks above passed |
| Read-only plan/restore-plan/stdout/file/validate comparisons | Passed; After present and hashes unchanged |
| Read-only final catalog/character inspection | Matches accepted restored state |

All database-writing opt-ins were explicitly unset. Fifteen skips comprise four
development publisher tests, five live character tests, four live V2 publisher
tests and two live balance tests. No new failed test is hidden as a skip.
Publication/restoration logic was not changed; existing pure plan/restoration,
hash and compatibility regressions passed, earlier authorized live evidence and
owner end-to-end acceptance remain the evidence for database mutation behavior.

## Changed files

- Created `backend/Initialize-AshenfallBalance.ps1`.
- Updated `docs/architecture/CATALOG_BALANCE_CLI_GUIDE.md`.
- Updated `docs/architecture/MILESTONE_09C_1_MANUAL_PREVIEW_SETUP.md`.
- Updated `docs/architecture/MILESTONE_09C_4_IMPLEMENTATION_REPORT.md`.
- Created this final review.

No C# business logic, serializer, gameplay, scene, catalog SQL or authentication
code changed. No Git operations were performed. Review plans created solely for
formatting checks remain nonsecret temporary local files, not PostgreSQL records.

## Known limitations and technical debt

- Administration remains deliberately tied to the verified local test database
  and existing administrative identity. Production targeting, least-privilege
  publisher deployment and future GUI authentication require separate design.
- V1 content without complete V2 metadata is not auto-upgraded for restoration.
  Restoration is forward publication with new identities/hashes, never reactivation.
- Live balance changes are accepted at a new encounter boundary. Godot pins
  active stats; no mid-combat change or HP refresh was introduced.
- Legacy stats/equipment consumers and unimplemented mechanics retain their
  previous limitations; no new gems, skills, spells or multiplayer authority.
- Initializer cache discovery covers the current project layout, not every
  possible external MSBuild import or in-place machine dependency change.
  Multi-session simultaneous builds are not coordinated by a new cache lock.
- PowerShell 7 and a freshly executed token-based HTTP example remain outside
  this closure's tested set. This does not negate the owner's confirmed HTTP
  and gameplay acceptance.

There is no demonstrated blocking defect remaining for 09C.4. **GO for closure**;
future balance mutations remain explicit administrative actions requiring their
own reviewed plans and execution intent. Do not replay the historical acceptance
publications or begin another milestone as part of this closure.
