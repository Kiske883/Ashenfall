# Milestone 07.1 — Combat feel and fair enemy attacks

## Root cause and timeline

The dwarf's logical attack impacted at **0.94 s**, but `PlayerMeleeController` held `PlayerController.MovementLocked` until the complete **1.70 s** attack ended. WASD was read but reduced to zero movement by the lock. A ground click could set a destination, yet the player remained stationary until recovery ended. The Guardian begins windup when it is in range (effective range at least 1.45 m) and facing the player. Its former 0.60 s windup plus 0.10 s logical damage delay put damage at **0.70 s after its own start**. When the attacks began close together, the Guardian could hit while the player was forced to remain in its range. `MeleeImpactValidator` did recheck actual positions and first-collider line of sight at impact; the fixed player position made those checks likely to pass. Collision and navigation were not the main cause.

The original code timeline, measured from a near-simultaneous attack start, was:

| Event | Time from own attack start |
| --- | ---: |
| Guardian visible windup / execution / damage | 0.00 / 0.60 / 0.70 s |
| Dwarf attack / hit / movement unlock | 0.00 / 0.94 / 1.70 s |

The updated adventure configures **only one balance value**: each Guardian's `AttackWindupDuration` is 1.20 s in `AshenfallFirstAdventure.tscn`. The existing 0.10 s delay makes damage occur at **1.30 s** after the Guardian begins. Recovery stays 1.00 s. Existing standalone demos retain the 0.60 s Guardian windup and their approved assets. The Guardian presentation maps the approved clip's first 0.60 s onto the 1.20 s logical windup; the 0.10 s damage delay lands within the visible sword strike in recovery. The approved clip resource is unchanged.

| Event | Updated adventure time from own attack start |
| --- | ---: |
| Guardian visible windup / execution / damage | 0.00 / 1.20 / 1.30 s |
| Dwarf attack / hit / full recovery | 0.00 / 0.94 / 1.70 s |

In a deterministic full approach from the player's original spawn at 60 Hz, the dwarf began its attack at physics frame **259**, hit at **316**, and the Guardian executed its swing at **338**. The 0.10 s impact delay puts the Guardian damage attempt around frame **344**: roughly **28 frames / 0.47 s after the dwarf's hit**. The test pressed WASD immediately after the dwarf hit, moved the character physically, and verified that the Guardian's strike consumed its sequence but dealt **0 HP**. Staying in range through that same visible strike dealt **10 HP**. The movement window is conditional on terrain and obstacles; it is not invulnerability.

## Interruption rules

- A deliberate WASD direction during an attack interrupts it before the player's movement physics tick. A ground click that is not an enemy target interrupts it during click resolution and starts the normal navigation order. Held mouse movement continues through the existing player controller.
- Before the 0.94 s player impact, interruption cancels the committed hit and its pending damage. After that impact, previously applied damage remains; interruption only cuts short recovery. No attack repeats automatically.
- Clicking an enemy during an active attack is consumed without starting a second attack or issuing a ground move.
- The visual adapter stops the one-shot on interruption and blends to Guard B or Combat Walk according to actual movement. The approved Idle, Walk and Attack animation resources and axe geometry remain unchanged.
- The Guardian still checks range, line of sight and both combatants' health at its own logical impact. Its missed attack retains the full configured recovery. No dodge roll, teleport or invulnerability was added.

## Files

Modified: `scripts/combat/PlayerMeleeController.cs`, `scripts/characters/DwarfPlayerPresentation.cs`, `scripts/characters/DwarfAxeAttackPresentation.cs`, `scripts/characters/DwarfApprovedAxeAttackPresentation.cs`, `scenes/world/AshenfallFirstAdventure.tscn`, `docs/validate_player_melee.gd`, `docs/validate_health_damage_death.gd`, `docs/validate_dwarf_combat_playable.gd`, and `docs/validate_first_adventure.gd`. The first three regression scripts now assert the intentionally changed movement-interruption rule while still checking attack timing, health, deduplication, animation and axe grip. The adventure test now checks that two Guardians can independently use the same local sequence number without sharing damage state.

Created: `docs/validate_combat_feel.gd`, `docs/milestone_07_1_anticipation.png`, `docs/milestone_07_1_dodge.png`, and this document. Godot may generate a `.uid` sidecar for the new validation script.

## Validation

- `dotnet build`: passed with zero warnings and errors.
- `validate_combat_feel.gd`: passed with zero failures under headless Godot and Vulkan Forward+. It covers real approach navigation, the 0.94 s player hit, actual WASD retreat, Guardian miss and recovery, late retreat receiving damage, duplicate execution rejection, pre-impact WASD cancellation, ground-click cancellation, and the visual strike's alignment with logical damage.
- `validate_player_melee.gd`, `validate_health_damage_death.gd`, `validate_first_adventure.gd`, `validate_bidirectional_combat.gd`, `validate_enemy_attack.gd`, `validate_enemy_pursuit.gd`, `validate_ai_animation.gd`, `validate_dwarf_combat_playable.gd`, `validate_dwarf_playable.gd`, `validate_mouse_navigation.gd`, `validate_gate_navigation.gd`, `inspect_terrain_edges.gd`, and `validate_playable_demo.gd` passed with zero failures after their respective expectations were checked.
- `validate_health_bar_render.gd` passed under Vulkan Forward+ at 100/75/50/25/0 Guardian HP. The one persistent enemy bar remained aligned.
- Two actual gameplay frames were rendered and inspected at 1280×800 under Vulkan: `milestone_07_1_anticipation.png` shows the Guardian in preparation after the dwarf hit, and `milestone_07_1_dodge.png` shows the player separated after the miss with 100 HP. These are scripted frames, not a claim of human gameplay approval.

## Manual validation

Open `res://scenes/world/AshenfallFirstAdventure.tscn` and press F6. Click the road Guardian once. Immediately after the dwarf's axe connects, hold a WASD direction **away from the Guardian**, or click open ground behind the dwarf. The Guardian should finish its visible swing without reducing the player's HP, then remain in recovery long enough for another approach. Repeat while deliberately waiting near the Guardian until its sword connects: the HUD should fall by 10 HP even if you retreat afterward. Try retreating before the dwarf's axe connects; the Guardian should retain its HP because that player attack was canceled. Check that rocks, trees and arch pillars still block movement as usual.

The camera-scale readability and difficulty of a complete three-Guardian adventure still need human visual/gameplay review. Terrain obstacles can prevent a chosen retreat direction; a missed click or walking into a wall does not grant protection. Dedicated dodge rolls, stamina, hit reactions and multiplayer remain outside this milestone.
