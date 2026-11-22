# Combat foundation 03.3 — bidirectional combat

## Play

Open `res://scenes/world/AshenfallDwarfPlayableDemo.tscn` in Godot 4.7.2 .NET and press F6. Move with WASD or click/hold the left mouse button on terrain. Click the Guardian to approach and swing once; Space performs one untargeted axe swing. The Guardian pursues, attacks and returns as before. Restart the scene manually after the dwarf is defeated.

## Architecture and timing

- The player's `HealthComponent` is 100 HP and belongs only to the definitive dwarf demo variant. The reusable component clamps health and rejects damage after its one death event.
- The Guardian's existing `EnemyController` remains the sole owner of its 0.60 s windup, 1.00 s recovery and monotonically increasing `AttackCount`. It emits `AttackExecuted` with its committed target once when recovery begins. `EnemyMeleeDamageController` observes this event and resolves a single 10 HP impact after a configurable 0.10 s delay, at 0.70 s from attack start. No animation signal causes damage. The sequence is consumed even when the impact misses. Guardian death, player death, target changes or leaving recovery cancel the pending impact.
- `MeleeImpactValidator` checks horizontal center distance, 0.5 m vertical tolerance and first collider in collision mask 3 on a ray from y+1 to y+1, excluding the attacker. This is the same calculation previously embedded in `PlayerMeleeController`. Player damage remains 25 HP at 0.94 s of its unchanged 1.70 s cycle.
- `PlayerDefeatController` cancels player navigation and active melee, then disables WASD, mouse movement and Space attacks. The actor node and physics remain alive. `EnemyController` ignores a player with a dead `HealthComponent`; scenes without player health retain their former target behavior.
- `DwarfPlayerPresentation` freezes the current approved clip and eases its visual child into a restrained side collapse. This is a presentation fallback, not an animation resource or gameplay state.
- `PlayerHealthHud` observes health changes through a `CanvasLayer`. Its red `ProgressBar` and numeric label are anchored at the bottom left. Every Control uses `MOUSE_FILTER_IGNORE`. The existing world-space Guardian health bar is unchanged.

The local sequence, committed target and impact-time validation provide a boundary where a future authoritative host can substitute command and damage validation. No networking is implemented.

## Files

Created: `scripts/combat/MeleeImpactValidator.cs`, `scripts/combat/EnemyMeleeDamageController.cs`, `scripts/characters/PlayerDefeatController.cs`, `scripts/ui/PlayerHealthHud.cs`, `scenes/ui/PlayerHealthHud.tscn`, `docs/validate_bidirectional_combat.gd`, `docs/capture_player_health_hud.gd`, this document, and `docs/combat_03_3_hud_{100,90,90_small,0}.png`. Godot may generate `.uid` sidecars.

Modified: `scripts/combat/PlayerMeleeController.cs`, `scripts/enemies/EnemyController.cs`, `scripts/characters/DwarfPlayerPresentation.cs`, `scenes/world/AshenfallDwarfPlayableDemo.tscn`, and `docs/validate_health_bar_render.gd`. The last script hides only the new player HUD while capturing the established 03.2 enemy-bar comparison images.

## Validation

- `dotnet build`: passed, zero warnings and errors.
- Godot headless editor import and definitive scene startup: passed, no errors.
- `validate_bidirectional_combat.gd`: passed, zero failures. It covers initial health, 0.70 s logical impact, one 10 HP hit despite repeated execution signals, consumed misses from distance/occlusion, pending-impact cancellation on Guardian death/player death/attack interruption, player defeat during its own active attack, and HUD values at 100/90/50/10/0. It checks fixed camera positioning, bottom anchors and ignored mouse filters.
- Existing regressions passed with zero failures: `validate_player_melee.gd`, `validate_health_damage_death.gd`, `validate_enemy_attack.gd`, `validate_dwarf_combat_playable.gd`, `validate_dwarf_animation.gd`, `validate_dwarf_playable.gd`, `validate_enemy_pursuit.gd`, `validate_ai_animation.gd`, `validate_mouse_navigation.gd`, `validate_gate_navigation.gd`, `inspect_terrain_edges.gd`, `validate_playable_demo.gd`.
- `validate_health_bar_render.gd` passed under Vulkan Forward+ at 100/75/50/25/0 enemy HP, confirming one stationary bar and left-anchored fill. It hides the new player HUD for the established 03.2 comparison captures and intentionally skips in headless mode.
- `capture_player_health_hud.gd` rendered the actual playable scene under Vulkan Forward+ at 1280×800 and 800×600. The four screenshots were inspected: the red HUD is bottom-left, displays the correct numeric values, shrinks at 90 HP and is empty at zero. The Guardian bar remains separate above the enemy. These are scripted renders, not manual gameplay approval.

## Limitations and manual review

The dwarf has no approved death animation. The temporary visual collapse can intersect uneven terrain; physics and camera remain attached to the live actor node. HUD uses a compact fixed pixel width and is intended for desktop resolutions. There is no respawn, damage feedback, health regeneration or networking.

For manual review: approach the Guardian, confirm exactly 10 HP lost per successful swing, retreat during windup, repeat until defeat and verify all movement/attack controls stop. Restart F6. Attack the Guardian to confirm the existing 25 HP hits and enemy death, including traversal through its former position. Check terrain clicks, held-click navigation, camera following and both health bars. Automatic tests validate the command paths, but a full encounter has not been manually played in this implementation session.

## Safe rollback

Preserve the pre-existing uncommitted 06D, 03.1 and 03.2 work. Review the four modified files and remove only the 03.3 additions listed above; then remove only the newly created 03.3 files and screenshots if rollback is desired. Do not reset, clean, stash or revert whole files: several contain earlier uncommitted work.
