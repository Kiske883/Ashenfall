# Milestone 08 — recovery of phases 08A–08F

**Later visual polish:** the Guardian's `!` warning described below was replaced by a body-and-sword anticipation pose. See [Milestone 08 combat presentation polish](MILESTONE_08_COMBAT_PRESENTATION_POLISH.md) for the current presentation and dwarf death clip. The account below records the earlier implementation and visual-review steps.

## Scope and current status

This work extends only `res://scenes/world/AshenfallFirstAdventure.tscn`. The original demos and all approved Idle, Walk and Attack animation resources remain unchanged. The owner has already accepted 08G; its walking implementation was preserved. No damage values, health calculations, attack clocks, movement speed, dodge rules, AI decisions, navigation, collision or adventure objective rules changed.

| Phase | Previously available | Added here | Status before owner review |
| --- | --- | --- | --- |
| 08A — hit reactions | Damage resolvers already validated and consumed attack sequences exactly once; no hit effects | `DamageApplied` signals fire only after `HealthComponent.ApplyDamage` succeeds. Adventure presentation uses a short per-actor material overlay and five tiny transient mesh sparks. Effects deduplicate by resolver and sequence. Original material overlays are restored. | Implemented; visual feel awaits manual review |
| 08B — damage numbers | Guardian world health bar and player HUD, but no floating damage values | One short-lived billboard `Label3D` per successful impact, amber above Guardians and red above the dwarf. Each floats and fades, then frees itself. | Implemented; readability awaits manual review |
| 08C — telegraphs | Guardian AI exposed `AttackWindup` and actual attack phase; optional debug label was not a gameplay warning | A separate amber `!` above each Guardian appears only in `AttackWindup` and hides after cancellation, disengagement, execution or defeat. It makes no area-of-damage claim. | Implemented; corrected warning awaits manual review |
| 08D — death presentation | Dwarf and Guardian already had visual-only fallbacks; health, targetability, collision and restart behavior were implemented | Existing falls now settle the lowest non-equipment mesh bound onto the actor ground plane. Visual pose updates stop after 1.5 s for the dwarf and 0.65 s for a Guardian. No gameplay death behavior changed. | Implemented procedurally; visual quality awaits manual review |
| 08E — atmosphere | `AshenAtmosphere.tres` already provided fog, tonemapping and SSAO | Adventure-only `AdventureAtmosphere.tres` adjusts fog, ambient light and SSAO; the adventure overrides its inherited moonlight energy. The original resource and demos remain unchanged. | Implemented; lighting preference awaits manual review |
| 08F — audio | No suitable audio files or audio playback system were found under `assets/` or `resources/` | Reusable `SoundCueRequested(cue, position)` event hook emits `AxeSwing`, `GuardianSwing`, `GuardianImpact`, `PlayerImpact`, `GuardianDefeat`, `PlayerDefeat` and `PlayerFootstep`. The hook is silent until original or appropriately licensed audio streams and a listener are provided. It does not affect timing. | Hooks complete; audible feedback remains pending assets |
| 08G — locomotion | Accepted, tested natural upper-body movement in `DwarfPlayerPresentation` | Unchanged; a separate defeated-pose branch now uses ground settling | Accepted by project owner |

`AdventureCombatFeedback` owns only presentation. It discovers the adventure's player and root-level Guardians/resolvers, subscribes to existing combat events and releases subscriptions on scene exit. Each resolver emits `DamageApplied` only after a valid successful hit, so misses, blocked attacks and duplicate impact attempts create no effects. Distinct resolver instance IDs keep simultaneous Guardians' sequence numbers independent. The warning samples each Guardian's AI state after the AI physics tick; it never predicts or widens the hit area. The audio event carries no playback or health logic.

## Owner visual review corrections

- **Orange circle:** the previous `MeleeWarning` was a ground disc under `FallenGuardian/Visual`, offset 0.78 m forward. At melee distance this placed it under the dwarf even though the Guardian owned it. It appeared during `AttackWindup` and disappeared on attack execution/cancellation; it was neither target selection nor a damage radius. It is now an amber `!` above the attacking Guardian's health bar. The old actual-render capture `milestone_08_telegraph_close.png` shows the circle; `milestone_08_review_telegraph_after_close.png` shows the corrected warning from the gameplay camera at temporary size 8.
- **Defeat UI:** `AdventureSession` still declares defeat immediately and `PlayerDefeatController` still disables movement/combat immediately. `AdventureHud` now waits a configurable 2.0 s before displaying the covering overlay. The dwarf's procedural fall settles after 1.5 s, leaving about 0.5 s to see the final pose. The countdown belongs to the HUD's process loop, stops on scene exit, and cannot outlive a restart. Victory overlay remains immediate. Rendered mid-fall and final-pose captures are `milestone_08_review_dwarf_death_mid.png` and `milestone_08_review_dwarf_final_pose_close.png`.
- **Atmosphere:** runtime inspection confirmed the first adventure overrides the inherited `World/WorldEnvironment` with `AdventureAtmosphere.tres`. The earlier values (fog density 0.009 versus 0.006 in the original, ambient energy 0.68 versus 0.65) were too similar at the orthographic size-22 camera with distance 48. The adventure now uses fog density 0.0145, ambient energy 0.58, stronger 1.55 moonlight and restrained SSAO 1.18. The shared world and camera are untouched. `milestone_08_review_atmosphere_disabled.png` and `milestone_08_review_atmosphere_enabled.png` were captured from the same camera, with only the adventure fog toggled for comparison. The final setting retains readable road stones, ruins, trees and Guardians.

## Validation performed

- `dotnet build --no-restore`: passed with zero warnings and errors.
- Godot 4.7.2 Mono headless editor import and 180-frame adventure startup: passed.
- `validate_milestone_08.gd`: passed headlessly and under Vulkan Forward+. It covers successful and duplicate player impact, transient cleanup, individual Guardian warnings, canceled/restarted windup, Guardian damage, independent audio cue hooks, defeat, restart, isolated atmosphere and three active Guardians. With all three zones active and attacks disabled for the performance sample, Vulkan reported 60 FPS with vertical synchronization; this is one machine/sample, not a benchmark.
- Complete relevant regression set passed with zero failures: `validate_player_melee`, `validate_health_damage_death`, `validate_bidirectional_combat`, `validate_combat_feel`, `validate_dwarf_combat_playable`, `validate_first_adventure`, `validate_milestone_08g`, `validate_dwarf_playable`, `validate_mouse_navigation`, `validate_gate_navigation`, `inspect_terrain_edges`, `validate_unrestricted_targeting`, `validate_milestone_02`, `validate_playable_demo`, `validate_dwarf_combat_guard_variant`, `validate_dwarf_approved_axe_attack`, `validate_dwarf_animation`, `validate_ai_animation`, `validate_enemy_attack` and `validate_enemy_pursuit`. The Vulkan `validate_health_bar_render.gd` check also passed at 100, 75, 50, 25 and 0 HP.
- The adventure regression's UI restart helper now waits for the configured defeat presentation delay before clicking the visible Restart button. Its original immediate click was invalid after the UI change. The focused 08 test also restarts directly while the delay is pending and verifies that the new scene has no pending countdown or stale effects. Both checks passed.
- Actual Vulkan captures were rendered and inspected: `milestone_08_atmosphere.png`, `guardian_hit`, `telegraph`, `dwarf_hit`, `guardian_defeat`, `dwarf_defeat`, `three_active`, plus close versions of the five character/action states. The focused script temporarily sets camera size 8 only for close captures; the playable adventure remains at orthographic size 22.

## Manual validation before milestone acceptance

Open `res://scenes/world/AshenfallFirstAdventure.tscn` and press F6. Use WASD and click/hold movement; attack each Guardian, interrupt or dodge an attack, observe both damage number colors and hit sparks, and confirm warnings stop on retreat and defeat. Complete and restart the adventure, then intentionally lose and restart. Review the new fog, flash strength, warning readability, grounded corpses and frame pacing with all three Guardians. 08G has already passed owner review; phases 08A–08F have not.

## Known limits

- No audible streams are bundled. The 08F events are ready for a future audio listener and approved assets; the game remains silent.
- Hit and death motions use procedural presentation, not authored reaction or death clips. The segmented character rigs constrain more nuanced falls and limb response.
- The existing empty dark Guardian health bar remains visible above a defeated body, matching the prior health-bar behavior.
- The warning is a billboard above the attacker and has no collision or targeting role. Its readability at the actual gameplay camera requires owner review.
- The 60 FPS observation has vertical synchronization and does not establish headroom on other hardware.

## Files

Created: `scripts/world/AdventureCombatFeedback.cs`, `scripts/characters/VisualGrounding.cs`, `resources/AdventureAtmosphere.tres`, `docs/validate_milestone_08.gd`, this document, their Godot-generated `.uid` sidecars, and twelve `docs/milestone_08_*.png` captures.

Modified: `scripts/combat/MeleeDamageController.cs`, `scripts/combat/EnemyMeleeDamageController.cs`, `scripts/enemies/EnemyAnimationController.cs`, `scripts/characters/DwarfPlayerPresentation.cs`, `scripts/world/AdventureCombatFeedback.cs`, `scripts/ui/AdventureHud.cs`, `resources/AdventureAtmosphere.tres`, `scenes/world/AshenfallFirstAdventure.tscn`, `docs/validate_milestone_08.gd` and this document. The two damage resolver files, HUD script, feedback script and adventure scene were already untracked local work before this review; their existing content was preserved and extended in place. The visual review adds before/after, mid-fall and final-pose captures under `docs/milestone_08_review_*.png`.

The visual-review correction itself touched `scripts/world/AdventureCombatFeedback.cs`, `scripts/ui/AdventureHud.cs`, `resources/AdventureAtmosphere.tres`, `scenes/world/AshenfallFirstAdventure.tscn`, `docs/validate_milestone_08.gd`, `docs/validate_first_adventure.gd` and this document. Its seven new `docs/milestone_08_review_*.png` files are rendered evidence. The earlier orange-disc capture `docs/milestone_08_telegraph_close.png` remains available for comparison.

No Git write operations were performed. The owner manages staging and history.
