# Milestone 06D — Playable dwarf combat presentation

## Launch and controls

Open `res://scenes/world/AshenfallDwarfPlayableDemo.tscn` in Godot 4.7.2 Mono and press **F6**. **WASD** or **left-click / hold left mouse** moves the player as before; WASD cancels the mouse destination. **Space** plays one visual two-handed axe attack. A repeated Space press during the 1.70 s clip does not restart it. A small scene-local hint shows the attack key. The player can continue moving during the visual strike; after it ends the presentation selects Combat Walk if moving, or Guard B if stopped.

The attack has no hit detection, damage, health change, target selection, stamina or cooldown. The Fallen Guardian's own AI and `AttackExecuted` signal are unchanged. The default F5 scene remains `res://scenes/world/World.tscn`.

## Production integration

`AshenfallDwarfPlayableDemo.tscn` retains its 2.5 m/s `PlayerController`, navigation, physical collider, camera and enemy. It now instantiates `DwarfCombatGuardImpactC.tscn` as its visual presentation. `DwarfPlayerPresentation.cs` has an optional `CombatMode` enabled only in this scene. It still measures actual horizontal displacement after player physics, calibrates Combat Walk using the existing **1.5151515 m/s** reference and **2×** cap, and rotates the visual toward real movement. Combat Walk enters at a near-planted support frame and blends with Guard B over 0.18 s. The 1.70 s Attack C one-shot has priority over locomotion and always runs at 1×; completion returns to Guard B, then the next physics tick selects Walk if appropriate. Both hands stay on one rigid axe via the approved IK rig.

The attack input uses `DwarfPlayerPresentation._UnhandledInput` for **Space**. It does not consume left-clicks, alter `PlayerController`, or add an input action to the shared project. The key is exported in the scene. The hint label ignores mouse input.

## Inventory and dependencies

At preparation, the branch was `feature/milestone-05-animation`, HEAD `2735828`; all prior 06D work was uncommitted. No prior files were deleted, moved or staged.
At final validation, `git status --short` reported **7 modified tracked paths and 83 untracked entries** (including Godot `.uid` sidecars and the Python cache directory). The seven modified paths are `docs/MILESTONE_06C_DWARF_PLAYABLE_INTEGRATION.md`, `docs/validate_dwarf_playable.gd`, `scenes/world/AshenfallDwarfPlayableDemo.tscn`, the two older dwarf world comparison scenes, `scripts/characters/DwarfPlayerPresentation.cs`, and `scripts/characters/DwarfBerserkerPresentation.cs`. The table below inventories the untracked 06D scenes, resources, scripts, documentation, validation tools and screenshot groups by role; none are staged.

| Role | Assets | Dependency / retention |
| --- | --- | --- |
| Final playable entry | `scenes/world/AshenfallDwarfPlayableDemo.tscn`, `scripts/characters/DwarfPlayerPresentation.cs` | Scene-local CombatMode, Space input and actual-speed adapter. |
| Final combat visual | `scenes/characters/DwarfCombatGuardImpactC.tscn`, `resources/animations/DwarfCombatGuardImpactC.tres` | Inherits `DwarfBerserkerApprovedAxeAttack.tscn`, its presentation script and the approved grounded rig. These are **production dependencies**, even though the A clip is replaced in the child scene. |
| Shared approved rig | `scripts/characters/DwarfBerserkerPresentation.cs`, approved Dwarf Berserker mesh/material scenes | A tiny wrist-endpoint correction removes numerical hand/grip drift under nested transforms; geometry and animation curves stay unchanged. |
| Current visual comparison | `scenes/art/DwarfCombatGuardComparison.tscn`, `scripts/art/DwarfCombatGuardComparison.cs` | Keeps Attack A and C immediately comparable; retain through visual review. |
| Previous approved-pose reference | `DwarfApprovedAxeAttack.tres`, `DwarfBerserkerApprovedAxeAttack.tscn`, `DwarfApprovedAxeAttackPresentation.cs`, `DwarfApprovedAxeAttackDemo.tscn` | Parent of the final C scene and comparison reference. Do not remove without first flattening or repointing those dependencies. |
| Older rejected attack experiments | `DwarfAxeAttack.tres`, `DwarfBerserkerAxeAttack.tscn`, `DwarfBerserkerAttackDemo.tscn`; `DwarfAxeOverhead.tres`, `DwarfBerserkerOverheadAttack.tscn`, `DwarfBerserkerOverheadAttackDemo.tscn` | Historical comparisons; separate validation scripts and docs load them. No playable runtime reference. |
| Static orientation/impact experiment | `DwarfBerserkerBladePose.tscn`, `DwarfAxeBladeOrientationPreview.tscn`, corresponding scripts | Used by approved A regression to verify its pose anchors; also retains the blade-edge explanation. |
| Authoring source | `docs/author_dwarf_overhead_attack.py`, `docs/author_dwarf_approved_axe_attack.py`, `docs/author_dwarf_combat_guard_variant.py` | C author imports A author, which imports overhead author. Keep together for deterministic regeneration. |
| Validation | Existing `validate_dwarf_{axe_attack,overhead_attack,blade_orientation,impact_alternatives,approved_axe_attack,combat_guard_variant}.gd` and new `validate_dwarf_combat_playable.gd` | The first five validate historical or approved art; the latter two validate the selected C variant and integration. |
| Evidence and notes | All `docs/milestone_06d_*.png` and `docs/MILESTONE_06D_*.md` | Earlier screenshots and milestone notes are retained unchanged; this task adds `milestone_06d_playable_{guard,walk,windup,impact}.png` and this report. |
| Older playable comparisons | `AshenfallDwarfLocomotionExperiment.tscn`, `AshenfallDwarfFiveMeterComparison.tscn` | Explicitly override CombatMode=false because they inherit the definitive scene and keep their original 2.5/5.0 m/s locomotion presentations. |

The old experimental attack resources and screenshots can be **cleanup candidates after explicit approval**, with their preview scenes, validation scripts, docs and authoring chain reviewed together. The generated `docs/__pycache__/author_dwarf_combat_guard_variant.cpython-314.pyc` has no runtime dependency and is also a cleanup candidate. Nothing was deleted in this milestone. The A scene/resource/script and static preview currently have live dependencies and are not safe cleanup candidates.

## Grip tolerance investigation

The existing long playable regression intermittently exceeded its unchanged **0.00005 world-unit** hand/grip threshold: observed maxima included 0.000052, 0.000092 and 0.000144. Diagnostic output showed the worst sample could occur in Guard B or Combat Walk after movement and turning, while the demanded wrist target was well inside the arm's ~0.60-unit reach; this ruled out a genuinely unreachable IK target. The rig's nested float transforms left a small residual between its predicted wrist target and the actual hand marker. `DwarfBerserkerPresentation.ApplyPose()` now measures that residual after solving each arm and applies an endpoint-only correction when it is below **0.001 world units**. A larger residual remains visible for diagnosis. No test threshold was increased. The full playable regression then passed with a maximum of **0.00000582**; the focused combat integration measured **0.00000097**.

## Validation

- `dotnet build --nologo`: passed, **0 warnings / 0 errors**.
- Godot 4.7.2 Mono headless editor import and bounded startup of the definitive demo and retained 2.5/5.0 comparison scenes: passed.
- `validate_dwarf_combat_playable.gd`: passed. It tests actual Space input, duplicate attack prevention, unchanged 1× attack duration while moving, Walk/Idle recovery, left-click navigation, Guardian configuration and grips.
- `validate_dwarf_playable.gd`: passed with the original grip threshold unchanged; movement, camera, navigation, Guardian pursuit/attack/return and repeated encounters passed.
- `validate_dwarf_playable.gd -- --legacy-five-meter`: passed, preserving the historical comparison.
- `validate_dwarf_animation.gd`, `validate_dwarf_approved_axe_attack.gd`, and `validate_dwarf_combat_guard_variant.gd`: passed with zero failures.
- Vulkan Forward+ gameplay-camera captures of Guard B, Walk, Windup and Impact C were rendered and inspected. At orthographic size 22, the dwarf's fine hand/axe details are small; the isolated comparison remains the useful close view.

## Remaining limitations

The attack is presentation-only and does not interrupt physical player movement. An attack issued while running may therefore slide across the ground while the feet remain planted in the attack pose. The 2.5 m/s Combat Walk inherits the approved grounded stride and may still show mild foot sliding on turns or variable speed; this needs direct manual gameplay review. The visual contact and impact strength of Attack C need the user's in-game approval before any further combat work or cleanup.
