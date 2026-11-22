# Milestone 09A.4.2 — physical F9 investigation

**Status:** implementation and automated checks complete on 2026-11-15; physical-key editor validation remains with the project owner.

## Reproduction and cause

In the Godot 4.7.2 editor, run `res://scenes/world/AshenfallFirstAdventure.tscn` with game embedding enabled and press physical F9 twice. The owner's observed first press appears to freeze gameplay; the second resumes it. Godot's [game embedding documentation](https://docs.godotengine.org/en/stable/tutorials/editor/game_embedding.html) assigns **F9 to Pause**, which stops physics and idle processing while rendering and input remain active; a second press resumes. This matches the two-press symptom. The editor's [shortcut list](https://docs.godotengine.org/en/stable/tutorials/editor/default_key_mapping.html) also assigns F9 to Toggle Breakpoint in the script editor.

Repository inspection found one game F9 handler (`CatalogRuntime._UnhandledKeyInput`), no F9 project input action, and no game code that toggles `SceneTree.Paused`, `Engine.TimeScale`, input mode, or window focus on F9. The prior `Input.parse_input_event` test injected events into the game process, bypassing the editor's physical-key shortcut. It therefore could establish that the HTTP path was responsive but could not validate editor-run F9 behavior. The previous 09A.4.1 asynchronous service correction remains useful for a separate, measured pre-await stall; it did not resolve this editor shortcut conflict. The editor interception is supported by the documented shortcut and the owner's exact observation; it has not been directly traced on the owner's physical keyboard by this agent.

## Correction

Explicit catalog reload is now **Shift+R** while the game window is in Input mode. F9 remains Godot's editor Pause key. The game no longer handles F9, so the editor and the game do not compete for it. This intentionally changes the requested F9 binding because a game handler cannot make the editor's embedded-game Pause shortcut safe without changing editor configuration. No editor setting or global shortcut was changed. No existing `project.godot` movement action uses R, and no other project handler uses this chord.

`CatalogRuntime` logs an operation ID, monotonic timestamp, source, pause state, time scale and mouse mode on each request, followed by a success with version, a failure reason, cancellation, or a busy rejection. Logs contain no API URL, connection string, or credentials. The existing `CatalogService` loading gate prevents overlapping requests; its five-second request timeout, validated snapshot publication and preservation of a previous snapshot on failure are unchanged. This instrumentation has no per-frame output. The only new runtime test accessor is `ReloadRequestCount`.

Files changed for 09A.4.2: `scripts/catalog/CatalogRuntime.cs`, `docs/validate_catalog_reload.gd`, `docs/architecture/MILESTONE_09A_4_IMPLEMENTATION_REPORT.md`, `docs/architecture/MILESTONE_09A_4_1_BUGFIX_REPORT.md`, and `docs/architecture/MILESTONE_09A_IMPLEMENTATION_PLAN.md`. This report is new. The accepted Guardian health bar code and tests were not modified.

## Automated validation

- `dotnet build Ashenfall.csproj --no-restore`: passed, 0 warnings/errors.
- `dotnet build backend/Ashenfall.sln --no-restore`: passed, 0 warnings/errors.
- `ASHENFALL_RUN_DB_TESTS=1 dotnet test backend/Ashenfall.sln --no-restore`: 30 passed, 0 failed, 0 skipped. These include catalog timeout, overlap, failure preservation and snapshot publication coverage.
- Godot 4.7.2 Mono headless editor import: exit 0. Headless first-adventure startup: exit 0; API startup loaded `prototype-1`.
- Focused catalog test, headless, with real API: 0 failures; injected F9 caused no game reload, each Shift+R key press reached the handler, rapid presses were rejected as busy, pause state and time scale did not change, and the active catalog remained ready. First-frame latency 3 ms, maximum measured frame gap 24 ms.
- Focused catalog test, headless, with unavailable API: 0 failures; request failure was logged and the game continued. First-frame latency 7 ms, maximum gap 9 ms.
- Focused catalog test, rendered Vulkan Forward+, with real API: 0 failures; first-frame latency 28 ms, maximum gap 30 ms. The API returned published `prototype-1`.
- Focused catalog test, rendered Vulkan Forward+, with unavailable API: 0 assertion failures; first-frame latency 15 ms, maximum gap 30 ms. The final direct run reported process exit 0. One piped PowerShell log-filter run reported exit 1 despite the same zero-failure summary, so the direct run is the exit-code evidence.
- Headless gameplay regressions: `validate_player_melee.gd`, `validate_bidirectional_combat.gd`, `validate_health_damage_death.gd`, `validate_first_adventure.gd`, `validate_combat_feel.gd`, `validate_milestone_08.gd`, `validate_milestone_08g.gd`, and `validate_mouse_navigation.gd`: all exited 0 with zero reported failures.

Synthetic input cannot exercise the Godot editor's keyboard shortcut routing. Rendered standalone runs show that the game continues processing frames, but they are not physical-key tests and cannot prove editor-run acceptance.

## Manual owner validation

1. Start the local API as in `MILESTONE_09A_4_IMPLEMENTATION_REPORT.md`. Run the first adventure from the Godot editor with game embedding and Input mode enabled. Confirm the game is running, then press **Shift+R** once. Expect one `Catalog reload requested` log and one `Catalog reload succeeded` log with `prototype-1`; movement and animation must continue without a second press.
2. Press Shift+R again, then rapidly several times. Expect one request per non-echo key press, some `already in progress` messages, no simultaneous network loads, and no pause. Walk and fight during a reload.
3. Press physical **F9** once and confirm the editor's Pause state changes; press it again to resume. This is now editor behavior, not catalog reload. If embedding is disabled, F9 should simply have no game reload action.
4. Stop the API and press Shift+R. Expect a bounded `Catalog reload failed` log; gameplay must remain responsive and any previously valid snapshot must stay available. Restart the scene with the API unavailable and confirm the adventure still plays from scene exports.

No PostgreSQL catalog data or roles were changed. No Git write operations were performed. The owner must confirm the physical-key behavior before declaring the milestone fully validated.

**MILESTONE 09A.4.2 — F9 FIX IMPLEMENTED, MANUAL VALIDATION PENDING**
