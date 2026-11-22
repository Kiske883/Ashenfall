# Milestone 09A.4 — Godot catalog API client

**Status:** Godot catalog integration validated on 2026-11-15. This is a non-blocking development integration; gameplay still uses its existing scene values.

## Files

Created: `scripts/catalog/CatalogData.cs`, `CatalogContentHash.cs`, `CatalogCodec.cs`, `CatalogApiClient.cs`, `CatalogService.cs`, `CatalogRuntime.cs`, and their Godot-generated `.uid` sidecars; `backend/Ashenfall.Tests/GodotCatalogClientTests.cs`; this report.

Modified: `project.godot` (one catalog autoload), `backend/Ashenfall.Tests/Ashenfall.Tests.csproj` (link the five platform-neutral client files into existing xUnit tests), `docs/architecture/MILESTONE_09A_IMPLEMENTATION_PLAN.md`, and `docs/architecture/GODOT_CATALOG_INTEGRATION.md`. No player, enemy, combat, health, navigation, camera, first-adventure scene, database schema or API source was changed. Existing modified/untracked work was preserved.

## Architecture

`CatalogApiClient` makes bounded asynchronous HTTP GET requests to the existing read-only API; the Godot project has no PostgreSQL driver or database credential. `CatalogCodec` strictly decodes the actual API fields into immutable records and `ImmutableArray` collections, checks supported schema major 1, version/hash metadata agreement, required IDs/codes, stat and item constraints, and known visual keys. The API does not return the database release UUID, so `catalogVersion` is the exposed release identity. `CatalogContentHash` independently recomputes the approved SHA-256 V1 binary content hash: ordinal code sorting, lower-case `D` UUIDs, UTF-8 NFC strings, little-endian integers, presence-marked nullable values, and exact decimal ×1000 encoding. The HTTP ETag is a separate response cache validator; this milestone does not cache it.

`CatalogService` requests version then the exact versioned snapshot, validates the complete candidate, and publishes one immutable reference with `Volatile.Write`. A failed/cancelled reload retains the previous valid reference, sets `Failed` and records a short error. `IsReady` means a validated snapshot is available even if the most recent reload failed. Concurrent reload requests return `Busy`; there is no polling or unbounded retry. Future encounters can pin `ActiveSnapshot` without being affected by later replacement. No current gameplay system consumes it.

`CatalogRuntime` is a lightweight autoload. At Godot startup it starts loading without blocking scene creation and logs success/failure in Godot output. The default API URL is `http://localhost:5000`, verified from the backend's no-profile launch on this machine. Override it with the non-secret environment variable `ASHENFALL_CATALOG_API_URL`; the client accepts HTTP(S) URLs without embedded credentials. Individual requests have a 5-second timeout and a 1 MiB response limit. A missing API leaves the catalog unready but does not stop the scene-configured adventure. This is a temporary development mode, not a production offline policy. **09A.4.2 update:** the explicit developer reload uses **Shift+R** because the Godot editor reserves F9 for embedded-game Pause; see `MILESTONE_09A_4_2_F9_INVESTIGATION.md`.

## Local launch

1. With the owner-maintained private PostgreSQL passfile configured for `ashenfall_app`, start the API from the project root in PowerShell: `$env:ASHENFALL_DB_CONNECTION='Host=localhost;Port=5432;Database=ashenfall_dev;Username=ashenfall_app'; dotnet run --no-launch-profile --project backend/Ashenfall.Api`. The value contains no password.
2. Run Godot F5 for the unchanged main world or open `res://scenes/world/AshenfallFirstAdventure.tscn` and use F6. The output should report `Catalog ready: prototype-1` and the published hash. The adventure continues to use its existing scene parameters.
3. Press **Shift+R** to reload the currently published version. F9 pauses/resumes an embedded game in the Godot editor. If the API is stopped, output reports failure while retaining any previously validated in-memory snapshot. `CatalogRuntime` under `/root` exposes `State`, `IsReady`, `LastError`, `ActiveSnapshot`, and `ReloadAsync` to future C# systems.

## Validation performed

- Backend solution: `dotnet test backend/Ashenfall.sln --no-restore` with `ASHENFALL_RUN_DB_TESTS=1`: **29 passed, 0 failed, 0 skipped**. The new deterministic client tests cover approved V1 cross-implementation hash vector, definition order, decimal scale, changed content, NULL versus zero, missing fields, unsupported schema, version mismatch, duplicate IDs, success, HTTP 400/404/503, unavailable network, timeout, cancellation, concurrent-load prevention, atomic replacement and failure retention. Four previously existing live PostgreSQL tests also passed.
- `dotnet build Ashenfall.csproj`: passed with zero warnings/errors. Godot 4.7.2 Mono headless editor import: passed.
- Live Godot startup of `AshenfallFirstAdventure.tscn` against the real locally running API and published PostgreSQL catalog: output reported `Catalog ready: prototype-1 (a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17)` while normal environment and navigation initialized. No stub was used in this check.
- With `ASHENFALL_CATALOG_API_URL=http://127.0.0.1:59999`, the same adventure started; after the request failed, Godot output reported `Catalog API is unavailable. Scene-configured gameplay continues.` This validates the non-blocking failure path, not visual playability.
- Existing headless regressions passed with zero failures: `validate_first_adventure.gd`, `validate_player_melee.gd`, `validate_bidirectional_combat.gd`, `validate_milestone_08g.gd`, `validate_mouse_navigation.gd`, `validate_health_damage_death.gd`, `validate_combat_feel.gd`, and `validate_milestone_08.gd`.

## Limits and next step

No GUI/manual playthrough was performed in this milestone; headless startup and regression scripts do not prove visual experience. The client does not persist a catalog to disk or implement ETag caching. Visual-key allowlisting currently knows the approved dwarf, Guardian, axe and sword; new art keys require a client update. Milestone 09A.5 must explicitly pin one validated snapshot to a new encounter and apply values before actor `_Ready`, while preserving first-adventure overrides. Until that approval, all gameplay remains scene-configured.

**MILESTONE 09A.4 — GODOT CATALOG INTEGRATION VALIDATED**
