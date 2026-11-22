# Milestone 09A.6 — development API startup

## Implementation

Created `Start-AshenfallDevelopment.cmd` for double-click use in Explorer; it invokes
`backend/Start-AshenfallDevelopment.ps1` and opens the editor after API verification.
The command-line equivalent from the project root is
`& .\backend\Start-AshenfallDevelopment.ps1`. It checks port 5000 and validates both
`/api/v1/catalog/version` and the `prototype-1` snapshot against the expected schema,
SHA-256 V1 hash and definition counts. A verified existing API is reused; an occupied
port with a wrong or unhealthy response fails with the owning PID and is never killed.
If the port is free, the helper starts the existing Development API with a hidden
background process, waits up to 15 seconds for the verified catalog, and then opens
the Godot editor. Run `scenes/world/AshenfallCatalogAdventure.tscn` with F6.
`-NoEditor` performs the API half only. `-ExpectedCatalogVersion` and
`-ExpectedCatalogHash` are available when a later catalog is approved.

The helper refuses conflicting environment settings, uses the API's existing private
Development configuration, prints no credentials, does not start PostgreSQL, and does
not alter the API or Godot production workflow. It deliberately leaves a launched API
running after Godot closes so the next launch can reuse it. Stop that process from its
own development session when no longer needed; the helper does not terminate it.

## Validation

- With port 5000 occupied by the verified API, `-NoEditor` reported reuse.
- After the owner freed port 5000, `-NoEditor` started the API; a second invocation
  reused it rather than creating a second listener.
- `Start-AshenfallDevelopment.cmd -NoEditor` also started and verified the API from
  a free port. Only the process started for that test was stopped afterward.
- Catalog version/snapshot checks and the focused Godot catalog/variable-damage test
  succeeded against that API. Vulkan Forward+ startup used the GTX 650 and exited 0.
- Backend build: 0 warnings/errors. Backend tests: 40 passed, 4 skipped (the skipped
  tests require opt-in live database writes, prohibited for this assignment).
- Editor launch itself remains a manual GUI check. Port-conflict failure was reviewed
  in code; an unrelated port occupant was not fabricated or terminated for a test.

Only `Start-AshenfallDevelopment.cmd` and `backend/Start-AshenfallDevelopment.ps1`
were created for this phase. The original
`backend/Start-AshenfallApi.ps1` and private PostgreSQL settings remain intact. No
database or Git operation was performed.
