# Milestone 09A.5.1 — local API catalog connectivity

## Root cause and evidence

`Program.cs` reads the configuration key `ASHENFALL_DB_CONNECTION`. When it is absent, it registers `UnconfiguredCatalogReader`, which deliberately returns HTTP 503 for catalog requests. The owner's Development `dotnet run` command did not supply that key. Before the correction, a separate Development API started without the key on port 5099 and `/api/v1/catalog/version` returned **503**, reproducing the symptom. The project had no `appsettings*.json`, launch profile or `UserSecretsId`; `dotnet user-secrets list` therefore could not resolve an API secret store. Missing User Secrets was an obstacle to using that configuration method, while the direct cause of the 503 was the missing connection setting.

PostgreSQL itself was healthy: service `postgresql-x64-18` was running; `psql -w` authenticated as `ashenfall_app` to `ashenfall_dev`; the role could SELECT all four catalog tables and had no INSERT or schema CREATE privilege. The one published release was `prototype-1` with SHA-256 V1 `a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17` (one class, one enemy, two items). No database grant or password change was needed.

## Configuration and secure startup

The API project now has a stable `UserSecretsId`. In Development, the existing ASP.NET Core configuration provider loads the same `ASHENFALL_DB_CONNECTION` key from the local user-secret store; a process environment value still takes precedence. This machine's local store was configured with **only non-secret host, port, database and restricted role settings**. No password was set, copied or printed by the application; PostgreSQL authentication uses the owner's private local passfile. The user-secret store is outside the repository. Production configuration remains environment-driven; user secrets are not a production credential source.

During the one-time setup on this machine, the `dotnet user-secrets set` CLI echoed that non-secret setting to the local console. It contained no password. The reproducible command below suppresses this CLI output so future setup logs do not include a complete connection setting.

On a new developer machine, first configure a private PostgreSQL credential for `ashenfall_app` and check that `psql -w` can authenticate. Then, from the project root, run the one-time local setup below. The CLI's success output is suppressed because it echoes the setting. Do not run `dotnet user-secrets list` in a shared log; it may display the setting.

```powershell
$parts = @('Host=localhost', 'Port=5432', 'Database=ashenfall_dev', 'Username=ashenfall_app')
dotnet user-secrets set ASHENFALL_DB_CONNECTION ($parts -join ';') --project backend/Ashenfall.Api *> $null
Remove-Variable parts
```

For normal startup, close any previous API instance listening on port 5000, then run `& .\backend\Start-AshenfallApi.ps1` from the project root. The helper sets Development only when there is no conflicting environment setting, uses the private user secret, refuses to override an existing `ASHENFALL_DB_CONNECTION`, and reports an occupied port without stopping another process. After the one-time setup, the owner's original Development `dotnet run` command in `backend/Ashenfall.Api` also works once port 5000 is free. If authentication fails, repair the private PostgreSQL credential locally; never put a password in the repository or send it in chat.

Check availability without exposing credentials:

```powershell
Invoke-RestMethod 'http://localhost:5000/api/v1/catalog/version' | Select-Object catalogVersion, contentSha256
$snapshot = Invoke-RestMethod 'http://localhost:5000/api/v1/catalog/snapshot?version=prototype-1'
$snapshot | Select-Object catalogVersion, contentSha256
```

The runtime API always uses `ashenfall_app`; `postgres` and `ashenfall_publisher` are not used for API requests. The Godot client still uses HTTP only.

## Development diagnostics

The API now logs a safe startup warning when the configuration key is missing and a safe confirmation when Development configuration is present. Database failures are categorized in server logs as authentication, permission, missing database, connection refusal, timeout or another database failure. EF Core may wrap provider exceptions, so `CatalogReader` inspects the exception chain without logging exception text or connection settings. A missing published release and an unexpected backend failure receive separate server-side categories. Public 503 responses remain generic and contain no connection string, credential or stack trace. Invalid version, missing version and ETag responses retain their prior contract.

## Validation results

- API project and full backend solution built to isolated local outputs with 0 warnings/errors. The ordinary `backend/Ashenfall.sln` build initially failed because an owner-started `Ashenfall.Api.exe` (PID 10892) held the normal Debug binary open; it was left running. Isolated output avoided modifying that process. After the owner closed it, the ordinary backend solution build passed with **0 warnings/errors**, and the ordinary complete backend test suite passed: **35 passed, 0 failed, 0 skipped**, including opt-in PostgreSQL tests and a new safe-response test for unexpected reader failures.
- Real configured API on port 5099, running in Development with no process connection variable: version **HTTP 200**, `prototype-1`, approved hash; snapshot **HTTP 200**, one class, one enemy, two items and matching hash; conditional ETag **304**; unknown version **404**; invalid/missing version **400**. The local user-secret provider and private PostgreSQL credential were exercised end to end. After the owner freed port 5000, the documented `Start-AshenfallApi.ps1` launched the normal Development API there. The same requests returned **200/200/304/404/400** with the approved hash and 1/1/2 definition counts.
- An isolated API pointed to an unused PostgreSQL port returned **503** and logged the safe category `PostgreSQL connection timed out`; no PostgreSQL service was stopped. The existing owner-started API on port 5000 still returned 503 at inspection time. That process was not stopped or overwritten.
- Godot C# build and 4.7.2 Mono headless import passed. `validate_catalog_gameplay.gd` using the corrected real API on port 5099 passed with zero failures: validated `prototype-1`, approved hash, catalog-backed dwarf/Guardian values, three instance overrides, Shift+R pinning, restart, health bar death behavior, victory and defeat. The Godot API URL was set only for that test process; no gameplay scene or database row was changed. Against the normal API on port 5000, the same focused headless test, `validate_catalog_reload.gd`, and a rendered Vulkan Forward+ catalog adventure test all exited 0 with zero failures.
- Headless Godot regressions all exited 0 with zero reported failures: `validate_catalog_reload.gd`, `validate_first_adventure.gd`, `validate_player_melee.gd`, `validate_bidirectional_combat.gd`, `validate_health_damage_death.gd`, `validate_combat_feel.gd`, `validate_milestone_08.gd`, `validate_milestone_08g.gd`, and `validate_mouse_navigation.gd`.
- Rendered Vulkan Forward+ `validate_catalog_gameplay.gd` against the real configured API: exit 0, zero failures. Headless direct startup of `AshenfallCatalogAdventure.tscn`: exit 0 and logged the approved catalog version/hash.

Files modified: `backend/Ashenfall.Api/Ashenfall.Api.csproj`, `backend/Ashenfall.Api/Program.cs`, `backend/Ashenfall.Api/Catalog/Services/CatalogReader.cs`, `backend/Ashenfall.Api/Catalog/Endpoints/CatalogEndpoints.cs`, `backend/Ashenfall.Tests/CatalogApiTests.cs`, and `docs/architecture/MILESTONE_09A_IMPLEMENTATION_PLAN.md`. Files created: `backend/Start-AshenfallApi.ps1` and this report. The private Development user-secret entry was created outside the repository; it contains no password.

## Owner action and GUI check

The owner freed port 5000, and the corrected API was started there with `Start-AshenfallApi.ps1`. For future sessions, run that helper or the documented Development `dotnet run` command and confirm `/api/v1/catalog/version` returns 200. Open `res://scenes/world/AshenfallCatalogAdventure.tscn` with F6, confirm the adventure starts, press Shift+R, play an encounter, kill a Guardian and verify its health bar disappears, then restart. This full physical GUI playthrough remains the owner's manual check.

The initial normal-output build was blocked while the old owner-started process held its Debug files. The full solution built and its tests passed using isolated output; after the old process was closed by the owner, the normal build, tests and documented normal-port launch succeeded. No unrelated process was terminated by this task.

No PostgreSQL schema, catalog content, roles or grants changed. No Git write operation was performed.

**MILESTONE 09A.5.1 — LOCAL API CONNECTIVITY FIXED AND VALIDATED**. Physical GUI gameplay review remains pending; no claim of a manual playthrough is made.
