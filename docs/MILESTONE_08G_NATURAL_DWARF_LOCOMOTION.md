# Milestone 08G — Natural dwarf locomotion

## Scope and implementation

The approved `Combat/CombatWalk`, `Combat/GuardB`, and `Combat/AxeAttack` resources remain unchanged. The definitive dwarf demo enables `NaturalWalkMotion` on `DwarfPresentation`; the first adventure inherits that setting. The old art and locomotion comparison scenes retain their original behavior.

`DwarfPlayerPresentation` adds a small deterministic secondary pose after `AnimationPlayer` samples the combat Walk clip. Its phase comes from the current Walk clip time, which already scales with measured horizontal player speed. The torso counter-turns around Y and rolls slightly around Z; the shared two-hand axe attachment counter-turns and shifts laterally. The existing limb solver moves both arms toward the axe grip markers, retaining the approved shoulder, elbow, wrist and forearm-twist constraints. The offsets fade in and out over 0.18 s, and the last step phase is held during the fade to Guard B. No locomotion offsets are applied during Attack or defeat.

The sampled torso and weapon control values are restored after each pose solve. The next frame therefore starts from the authored clip, without accumulated transform drift. Player root position, navigation, collision, foot targets, grounded leg curves, camera and logical impact timing are unchanged. Inspector parameters: `WalkTorsoYaw` 0.035 rad, `WalkTorsoRoll` 0.012 rad, `WalkAxeYaw` 0.022 rad, `WalkAxeSway` 0.012 world units and `WalkMotionBlendSeconds` 0.18 s.

These refinements are procedural pose offsets within the existing presentation solver. A future Blender rig and authored shoulder, elbow and hand keys would allow larger, asymmetric shoulder recoil and independent grip pressure. The rigid segmented limbs and a single two-handed axe limit safe independent arm swing today. At orthographic gameplay size 22, the extra motion is intentionally subtle.

## Validation

- `dotnet build --no-restore`: passed, zero warnings and errors.
- Godot 4.7.2 Mono headless editor import and first-adventure startup: passed.
- `validate_milestone_08g.gd`: passed in headless and Vulkan Forward+ rendered runs. It checks WASD, held left mouse movement, stop/start, direction changes, attack handoff and recovery, player defeat, adventure restart, two-hand grip and long-running pose stability. Maximum sampled torso deviation was 0.0419 rad; maximum sampled grip gap was about 0.0000014 world units.
- Existing `validate_dwarf_combat_playable.gd`, `validate_dwarf_playable.gd`, `validate_player_melee.gd`, `validate_health_damage_death.gd`, `validate_bidirectional_combat.gd`, `validate_combat_feel.gd` and `validate_first_adventure.gd`: passed.
- Rendered and visually inspected gameplay captures: `milestone_08g_idle.png`, `milestone_08g_walk.png`, `milestone_08g_stopped.png`, `milestone_08g_defeat.png`, plus `idle_close` and `walk_close` captures at temporary camera size 8. The real gameplay camera remains size 22.

Automated checks do not establish subjective animation approval. Manually run `res://scenes/world/AshenfallFirstAdventure.tscn` with F6; use WASD and click/hold on ground, reverse direction, approach an enemy and attack with Space or enemy selection, retreat during attack, then observe defeat and restart. Compare the original locomotion test scenes for the prior upper-body motion.

## Files

- Modified: `scripts/characters/DwarfPlayerPresentation.cs`, `scripts/characters/DwarfAxeAttackPresentation.cs`, `scenes/world/AshenfallDwarfPlayableDemo.tscn`.
- Added: `docs/validate_milestone_08g.gd`, this document, and six `docs/milestone_08g_*.png` validation captures.

Existing uncommitted work was preserved. No Git history or index operations were performed.
