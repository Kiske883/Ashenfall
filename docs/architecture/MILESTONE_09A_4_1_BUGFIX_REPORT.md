# Milestone 09A.4.1 — catalog reload and Guardian health bar

**Later finding (09A.4.2):** Godot's embedded-game Pause shortcut owns physical F9. The 09A.4.1 injected-key test bypassed that editor shortcut. The current game reload shortcut is **Shift+R**; see `MILESTONE_09A_4_2_F9_INVESTIGATION.md`. The Guardian health bar fix remains accepted.

**Status:** fixes implemented and automated/rendered validation completed on 2026-11-15. The owner should still repeat the physical-key GUI scenario that originally froze.

## F9 diagnosis and correction

`CatalogRuntime._UnhandledKeyInput` called `LoadAsync` directly from the Godot input callback. `CatalogService.ReloadAsync` then began `HttpClient.GetAsync` on that same thread before reaching a genuine asynchronous suspension; JSON decoding and canonical hashing also resumed on the Godot synchronization context. There were no `.Result`, `.Wait()`, synchronous `GetAwaiter().GetResult()`, semaphores, recursive signals or infinite loops in this path. The interlocked `_loading` gate already rejected overlaps, and each request had a five-second timeout.

A deterministic test using an HTTP handler that performs synchronous work before returning a Task **failed before the fix**: calling `ReloadAsync` blocked the caller for **672 ms**. This demonstrates a real input-thread stall even though the method's signature is asynchronous. After the fix, the same test passes its under-150-ms entry bound. `CatalogService` now performs HTTP acquisition, decoding, validation and SHA-256 hashing inside `Task.Run`; it still atomically publishes only a fully validated immutable snapshot. The service contains no Godot scene-tree operations, and all shared state uses the existing interlocked/volatile boundary. `CatalogRuntime` awaits the result from the Godot context before logging. Unexpected transport errors now produce a safe failure state without exposing URL or credential details. Failed reloads retain the last valid snapshot. No gameplay stats are changed.

The exact duration or mechanism of the owner's original GUI freeze was not captured from that session. The synchronous pre-await path is a confirmed frame-blocking defect and the smallest code path changed. The owner should verify the physical F9 key after this correction.

## Guardian health bar lifecycle

The reusable `WorldHealthBar` previously drew an empty dark quad at zero HP and remained visible because it only changed texture pixels. `HealthComponent.ApplyDamage` emits `HealthChanged(0, max)` **before** `Died`. `WorldHealthBar.OnHealthChanged` now sets `Visible = current > 0` immediately on that transition, while retaining the same bar node, quad, material and texture. Further post-death health changes cannot make it visible while current HP is zero. A restarted adventure creates living Guardians whose `_Ready` callback restores visibility. No enemy root, collision, targeting, AI, objective or animation code was changed.

## Files

Modified: `scripts/catalog/CatalogService.cs`, `scripts/ui/WorldHealthBar.cs`, `backend/Ashenfall.Tests/GodotCatalogClientTests.cs`, `docs/validate_health_damage_death.gd`, `docs/validate_first_adventure.gd`, and `docs/validate_health_bar_render.gd` (capture prefix can be overridden to preserve earlier screenshots). Created: `docs/validate_catalog_reload.gd` and its Godot `.uid`, this report, and five new rendered captures `docs/architecture/milestone_09a_4_1_bar_{100,75,50,25,0}.png`. Existing screenshots and gameplay scenes were not overwritten.

## Validation

- `dotnet build Ashenfall.csproj` and `dotnet build backend/Ashenfall.sln`: passed, zero warnings/errors.
- `dotnet test backend/Ashenfall.sln --no-restore` with `ASHENFALL_RUN_DB_TESTS=1`: **30 passed, 0 failed, 0 skipped**. The new pre-await blocking regression failed before the fix and passed afterward. Existing tests cover failure preservation, cancellation, timeouts, overlap rejection and atomic replacement.
- Godot 4.7.2 Mono headless editor import: passed.
- `validate_catalog_reload.gd` simulated twelve F9 presses against the real published API and against an unavailable endpoint. Both headless and rendered Vulkan Forward+ runs passed with zero failures. In the final rendered runs, first-frame latencies were 60 ms (available) and 37 ms (unavailable), and the largest inter-frame gap during reload completion was 32 ms in both cases. Busy presses were ignored; one load completed or failed; the scene kept running. These are automated injected key events, not a human physical-key playtest.
- `validate_health_damage_death.gd`, `validate_first_adventure.gd`, `validate_bidirectional_combat.gd`, `validate_player_melee.gd`, `validate_combat_feel.gd`, `validate_milestone_08.gd`, `validate_milestone_08g.gd`, and `validate_mouse_navigation.gd`: all returned zero failures. The first two now assert bars disappear for independently instanced Guardians and return on restart.
- `validate_health_bar_render.gd` rendered the existing playable scene under Vulkan Forward+ at 100/75/50/25/0 HP with zero failures. The bar was visible above zero and hidden at zero. The 100 and 0 HP captures were inspected: the enemy remained visible and the old bar was absent at zero. The D3D12 capability warning and automatic Vulkan fallback are the project's existing GPU behavior.

One separate validation-harness warning remains: `validate_bidirectional_combat.gd` can log Godot Mono `InvalidOperationException: Handle is not initialized` while adding a new fixture, despite completing with zero assertion failures. It reproduced twice while the catalog API was available and not in one API-unavailable run. The exception originates at `root.add_child(demo)` in that older test harness; its relation to the new catalog autoload is unconfirmed. It did not occur in the first-adventure, death, render or F9 tests. This warrants a focused engine/harness follow-up if it appears during actual play.

## Manual owner check

Start the read-only backend as documented in [09A.4](MILESTONE_09A_4_IMPLEMENTATION_REPORT.md), run `res://scenes/world/AshenfallFirstAdventure.tscn` in the Godot GUI, move and fight while pressing **physical F9** repeatedly, then stop the backend and repeat. Confirm input/rendering continue, one reload or one clear failure is logged, and current combat values do not change. Kill all three Guardians and confirm each overhead bar disappears on the lethal hit while its death presentation remains visible; restart and confirm full bars return. This physical-key and full-playthrough observation has not yet been performed by this agent.

No PostgreSQL catalog rows, roles or credentials were changed; no Git write operation was performed.
