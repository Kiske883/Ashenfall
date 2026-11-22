# Milestone 07 — First playable adventure

## Launch and controls

Open `res://scenes/world/AshenfallFirstAdventure.tscn` in Godot 4.7.2 .NET and press **F6**. F5 still starts the original configured world. Move with WASD or left click/hold on terrain. Left click a Guardian to approach and perform one 25 HP axe strike; Space performs one untargeted axe swing. Guardians deal 10 HP per valid strike. Defeat all three, then reach the **eastern stone arch** at approximately `(5.2, 0, -7)`. On victory or defeat, click **RESTART ADVENTURE**. The button recreates the scene inside the running Godot application.

## Phases and architecture

- **07A reset:** `EncounterResetController` reloads only the current adventure scene, using `SceneTree.ChangeSceneToFile`. It reconstructs the player, three enemies, their health/collision/AI/presentation, all navigation orders and impact timers, the objective, HUDs and signal subscriptions. This avoids introducing reset methods into approved combat, health and animation components. Repeated victory and defeat restarts were tested.
- **07B enemies:** `AshenfallFirstAdventure.tscn` inherits the validated dwarf demo, retaining its original road Guardian and adding west/east Guardians at `(-5,0,-13)` and `(5,0,-13)`. Each is an independent instance of `FallenGuardian.tscn`. The two new enemies each have a dedicated `EnemyMeleeDamageController`; the inherited road enemy keeps its own resolver. Their health and world-space bar materials are distinct.
- **07C zones:** One `EncounterZone` per enemy controls whether that enemy receives a player target. Each zone has configurable activation and release radii (5.5 m and 8 m here). Distances use the horizontal plane and hysteresis. The existing `EnemyController` retains detection, attack and leash behavior; it returns toward spawn when a zone releases its target. Defeated zones stop processing.
- **07D objective:** `AdventureObjective` discovers root-level Guardian actors in the composed adventure, subscribes to their `HealthComponent.Died` signals, and completes only when all are defeated and a living player enters the eastern arch goal radius (1.8 m). It unsubscribes on scene exit. The goal uses the existing ancient ruin; no terrain or navigation geometry changed.
- **07E outcomes:** `AdventureSession` observes the objective and player health. Victory stops player input and active melee; defeat continues to use the existing player defeat controller. `AdventureHud` displays objective progress and a mouse-interactive victory/defeat overlay with a restart button. The overlay consumes pointer input; gameplay input is disabled at terminal outcomes.
- **07F integration:** The new scene is the only playable composition changed. Existing demos, original character models, animation resources, world, navigation mesh and F5 startup remain untouched.

The current encounter has one local player. No network messages, global singletons, inventory or new art assets were introduced.

## Files created

Runtime: `scenes/world/AshenfallFirstAdventure.tscn`, `scenes/ui/AdventureHud.tscn`, `scripts/world/EncounterResetController.cs`, `scripts/world/EncounterZone.cs`, `scripts/world/AdventureObjective.cs`, `scripts/world/AdventureSession.cs`, `scripts/ui/AdventureHud.cs`.

Validation and documentation: `docs/validate_first_adventure.gd`, `docs/capture_first_adventure.gd`, `docs/milestone_07_{start,encounter,victory,defeat}.png`, this document. Godot generated `.uid` sidecars for new scripts. No pre-existing source scene or script was edited for this milestone. Running the existing 03.2 render test regenerated its five comparison PNGs with the same enemy-bar states.

## Validation performed

- `dotnet build`: passed with zero errors and warnings.
- Godot headless editor import and headless startup of `AshenfallFirstAdventure.tscn`: passed without errors.
- `validate_first_adventure.gd`: passed with zero failures. It checks three independent enemies and bars, navigable spawn positions, dormant distant zones, activation/release and return, objective gating, actual navigation to the ruin, one victory, a real mouse click on restart, full health/AI/HUD restoration, defeat and another restart, a real western Guardian attack with 10 HP damage, duplicate-signal rejection, and a targeted dwarf hit that damages only one Guardian.
- Existing regression scripts passed with zero failures: `validate_player_melee.gd`, `validate_health_damage_death.gd`, `validate_bidirectional_combat.gd`, `validate_enemy_attack.gd`, `validate_enemy_pursuit.gd`, `validate_ai_animation.gd`, `validate_dwarf_combat_playable.gd`, `validate_mouse_navigation.gd`, `validate_gate_navigation.gd`, `inspect_terrain_edges.gd`, and `validate_playable_demo.gd`.
- `validate_health_bar_render.gd` passed under Vulkan Forward+ at 100/75/50/25/0 enemy HP, retaining one stable world-space bar with a left-anchored fill.
- Vulkan Forward+ rendered `milestone_07_start.png`, `milestone_07_encounter.png`, `milestone_07_victory.png` and `milestone_07_defeat.png` from the actual adventure scene at 1280×800. All four were visually inspected. The encounter frame shows three separate Guardian bars and a 90 HP player HUD. The terminal overlays and restart button render centered and readable.

## Manual gameplay review

1. Launch the adventure with F6 and verify that only nearby Guardians activate as you explore.
2. Use both WASD and terrain click/hold navigation; click each Guardian and verify a single approach and attack per command.
3. Retreat out of an encounter zone and observe the enemy returning. Re-enter to repeat.
4. Defeat all three Guardians, then travel through the eastern arch. Confirm victory appears only after both conditions hold.
5. Click restart and verify full health, all three live enemies, original positions and no duplicate bars or UI.
6. In another run, allow the dwarf to fall. Confirm defeat, blocked gameplay input and restart by mouse.

Automated execution and rendered captures passed, but a full uninterrupted human-played victory/defeat run was not performed in this implementation session.

## Limits before multiplayer

`EncounterZone` and `AdventureObjective` currently reference one local player, and `AdventureSession` treats that player's death as terminal. Cooperative play will need party-aware activation, objective completion, team defeat/revive rules and an authoritative host for damage and restart decisions. The current reset recreates the whole scene; a future networked session must coordinate that transition across peers. These are future design requirements, not implemented here.
