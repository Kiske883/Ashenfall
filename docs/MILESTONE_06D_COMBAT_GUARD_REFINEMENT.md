# Milestone 06D — Combat guard locomotion and Impact C candidate

## Open the isolated comparison

Open `res://scenes/art/DwarfCombatGuardComparison.tscn` in Godot and press **F6**. The buttons are **COMBAT IDLE**, **COMBAT WALK**, **ATTACK A**, **ATTACK C**, and held **IMPACT A/C** poses. Choose Front, Side, or Isometric; toggle 0.35× slow motion; choose Inspection (orthographic size 4) or Gameplay (size 22). The scene is visual only. It does not move the character through the world or change the playable demo.

## Design

The approved grounded Walk is a full-body clip: torso and weapon tracks would replace GUARD B. `docs/author_dwarf_combat_guard_variant.py` therefore authors an isolated `Combat/CombatWalk` using the **exact** grounded foot target and ankle key values and timings (201 keys, 0.6 s). Its pelvis motion is a delta around GUARD B. The upper body uses GUARD B quaternion and rigid two-hand weapon grip, with only 0.007 radian torso yaw and 0.004 radian beard motion. The blade retains GUARD B's 109.5° handle-axis roll. No approved locomotion resource is edited. The preview uses the measured 1.51515 m/s stride reference: 2.5 / 1.51515 ≈ **1.65×** Walk playback. It enters the cycle at 0.165 s, when one foot is planted and the other is lifting, then uses a 0.18 s blend in and out of Walk.

`IMPACT C` starts from the approved `IMPACT A`. Configurable authoring offsets lower the whole axe an additional **0.025 world units**, pitch the torso forward an additional **0.025 radians**, and lower the pelvis an additional **0.008 world units**. The existing IK keeps both grips on the same rigid handle. The same 90° cutting-edge roll is retained at impact. The candidate attack keeps the prior 1.70 s timing and all Guard B, Windup and clearance knots. Only the Impact A endpoint and its hold become Impact C; torso and axe orientations still use normalized quaternion interpolation. The prior A scene/resource remain available for one-button comparison.

## Files added

- `docs/author_dwarf_combat_guard_variant.py` — isolated resource authoring and conservative impact offsets.
- `resources/animations/DwarfCombatGuardImpactC.tres` — Guard B, Combat Walk and 1.70 s Attack C clips.
- `scenes/characters/DwarfCombatGuardImpactC.tscn` — reusable preview presentation inheriting the approved rig and geometry.
- `scenes/art/DwarfCombatGuardComparison.tscn` and `scripts/art/DwarfCombatGuardComparison.cs` — preview controls and two reversible presentation instances.
- `docs/validate_dwarf_combat_guard_variant.gd` — focused motion, grip, ground, edge and clearance checks.
- `docs/milestone_06d_combat_guard_walk.png`, `docs/milestone_06d_impact_c_{front,side,isometric}.png` — rendered inspection sheets.

The original `DwarfGroundedExperiment.tres`, `DwarfApprovedAxeAttack.tres`, previous previews, original locomotion demo, player controller and playable world are unchanged.

## Validation

- `dotnet build --nologo`: succeeded, 0 warnings, 0 errors.
- Godot 4.7.2 Mono headless editor import: succeeded; isolated comparison startup: succeeded.
- Focused script: passed. Foot/ankle keys match original Walk exactly; both grips stayed within **0.000006 world units**; Walk sole span **−0.0000006 to 0.0700**; Idle→Walk→Idle maximum axe step **0.00308 world units per 60 Hz frame**. Attack C tip measured **0.03076 world units lower** than A in the focused sample; no crossed grips, deep head/torso proxy penetration, blade flip or physical foot penetration in 341 sampled frames. Minimum forward cutting-edge alignment was **0.527**; attack returns to Guard B.
- Existing `validate_dwarf_animation.gd` and `validate_dwarf_approved_axe_attack.gd`: passed with zero failures. `validate_dwarf_playable.gd` passed on repetition, including movement, navigation, camera, enemy pursuit, attacks and return. Its first run narrowly failed its pre-existing grip threshold (measured 0.0000521 versus required <0.00005 world units); the second measured 0.0000489 and passed. The new preview assets are not loaded by that playable scene, so this is a tolerance-sensitive baseline test rather than evidence of new gameplay integration.
- Vulkan Forward+ rendered the Walk and front/side/isometric Attack C sheets; these images were inspected. The Walk sheet shows the combat guard held across alternating leg positions. The side and isometric Attack C sheets show the raised axe, downward motion and return without a visible weapon flip.

## Limits and next review

Combat Walk is shown **in place** at the calibrated playback rate; real 2.5 m/s terrain motion and residual foot sliding still require gameplay visual testing if this variant is later integrated. The rigid arm/leg geometry and IK can show minor mechanical stiffness at some angles. The Impact C offset is deliberately small; its visual strength and cutting contact still require manual approval. Nothing from this experiment is integrated into the playable dwarf.
