# Milestone 08 — final combat presentation polish

## Visual causes and changes

- The floating `!` was a runtime `Label3D` created by `AdventureCombatFeedback`, separate from the Guardian's existing V3 sword attack. It was visually generic. The label is removed. `EnemyAnimationController` now gives the tracked torso and right shoulder a restrained temporary preparation accent during `AttackWindup`. The approved 1.6 s sword attack remains the base clip. The accent follows the AI's existing phase fraction, peaks halfway through preparation, and reaches zero before the strike. The first adventure's 1.20 s preparation override is unchanged. Every Guardian owns its own animation controller. Cancellation, death and the end of preparation clear the accent; no UI warning or collision area is created.
- The dwarf's previous death branch rotated and translated the whole presentation toward a sideways pose. It did not animate joints or the shared axe grip. `DwarfDefeatFall.tres` is a separate 1.55 s visual clip with a short recoil, knee buckling, forward loss of balance and a settled collapse. It drives the existing segmented rig controls and weapon attachment; the existing two-hand IK solves both grips. Only the first adventure loads this library; prior demos retain their original fallback. The approved Idle, Walk, AxeAttack and 08G resources remain unchanged. `VisualGrounding` still settles the model onto terrain. Gameplay death and control lock remain immediate.
- The adventure HUD's existing 2.0 s defeat presentation delay remains unchanged. The visual clip completes about 0.45 s before the overlay. Restart recreates the scene and releases the clip and effects.

## Validation

- `dotnet build --no-restore`: passed, zero warnings and errors.
- Godot 4.7.2 Mono headless import and first-adventure startup: passed.
- `validate_milestone_08.gd`: passed headlessly and in Vulkan Forward+ with real screenshot captures. Checks independent Guardian anticipation, cancellation, later attack, death, no punctuation node, staged dwarf defeat, axe hand grips, final pose stability, UI delay and restart.
- The 21-test movement, combat, animation and adventure regression batch ran. Twenty scripts exited successfully with zero failures. `validate_bidirectional_combat.gd` completed with zero failed assertions, but repeated runs intermittently produced Godot Mono's `ScriptManagerBridge.SwapGCHandleForType` / `Handle is not initialized` error while re-instantiating its fixture; it sometimes exited cleanly. Experimental changes to that test's cleanup did not make the error reliable and were removed. The cause of this intermittent failure is not confirmed; no gameplay assertion failed.
- Actual rendered images under `docs/milestone_08_polish_*.png` show Guardian preparation and cancellation, fatal reaction, buckling, mid-fall and final pose. The close captures temporarily use orthographic size 8; the playable scene remains at size 22.

## Limitations and owner review

The Guardian attack accent is a small visual pose offset layered over the approved animation; it does not author a new strike. The dwarf uses rigid segmented limbs and IK, so the fall is a staged visual animation rather than a soft-body or fully skinned collapse. Review the sequence in motion at the gameplay camera and check the silhouette from different approach directions. Final aesthetic acceptance belongs to the project owner.

Open `res://scenes/world/AshenfallFirstAdventure.tscn` and press **F6**. Approach a Guardian to see its preparation; dodge or retreat to cancel, then let the dwarf be defeated to inspect the fall and delayed restart screen. The separate [Fog of War design](FOG_OF_WAR_DESIGN.md) is future scope only.

## Files for this polish

Created: `resources/animations/DwarfDefeatFall.tres`, `docs/FOG_OF_WAR_DESIGN.md`, this document and the eight `docs/milestone_08_polish_*.png` captures.

Modified: `scripts/enemies/EnemyAnimationController.cs`, `scripts/world/AdventureCombatFeedback.cs`, `scripts/characters/DwarfPlayerPresentation.cs`, `scenes/world/AshenfallFirstAdventure.tscn`, `docs/validate_milestone_08.gd` and `docs/MILESTONE_08_RECOVERY.md`. Earlier uncommitted work is preserved. No Git write operation was performed.
