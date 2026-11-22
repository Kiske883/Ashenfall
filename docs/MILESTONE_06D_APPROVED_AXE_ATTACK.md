# Milestone 06D — Approved-pose two-handed axe attack prototype

## Open the isolated preview

Open `res://scenes/art/DwarfApprovedAxeAttackDemo.tscn` in Godot 4.7.2 Mono
and press **F6**. It starts in **GUARD B**. Click **ATTACK** to play one 1.70 s
attack and return to GUARD B. The other controls are **GUARD B**, **WALK**,
Front, Side, Isometric, Inspection (orthographic size 4) and Gameplay / 22.
The preview has no player controller, collision, hitbox, damage or AI event.
It is not integrated into the playable demo.

## Approved endpoints and timing

The new attack copies the visual controls of the three approved static poses:

- **GUARD B** = the previous impact sample at 0.96 s from
  `DwarfAxeOverhead.tres`, with a 109.5° static blade roll.
- **WINDUP** = the 0.70 s overhead sample, with a 90° blade roll.
- **IMPACT A** = the lowered-guard static pose in
  `DwarfAxeBladePoseDemo.cs`, with its precise body, torso, head, beard,
  weapon offset, zero weapon rotation and 90° blade roll.

The full mesh transforms at clip times **0.00, 0.70, 0.94 and 1.70 s** matched
those static poses to within approximately `0.0000004` world units. The clip
does not use original GUARD or IMPACT B in their place.

| Time | Phase |
| --- | --- |
| 0.00–0.12 s | Restrained anticipation from Guard B. |
| 0.12–0.68 s | Deliberate overhead preparation via a shoulder-side clearance path. |
| 0.68–0.76 s | Brief high hold in Windup. |
| 0.76–0.94 s | Fast downward edge-leading strike to Impact A. |
| 0.94–1.10 s | Grounded follow-through held at Impact A. |
| 1.10–1.70 s | Recovery to the exact Guard B pose. |

The cutting edge rises at an average vertical speed of **1.44 world units/s**
and falls at **3.93 world units/s** during the authored preparation and strike
windows; the strike is **2.73 times faster** by that measure. There is no
root motion or scale animation.

## Rig and interpolation

`DwarfBerserkerApprovedAxeAttack.tscn` inherits the approved grounded dwarf
and adds only the `Combat` library from `DwarfApprovedAxeAttack.tres`. The
source mesh and materials, accepted Idle/Walk resources, character proportions,
beard and two-hand grip markers are reused. One rigid axe transform supplies
both hand targets through the existing two-bone arm solver. The blade's mesh
rolls modestly from **109.5° to 90°** around the cylindrical shaft, leaving
the grip markers fixed; this keeps the corrected cutting edge toward the
enemy without a sideways weapon flip.

The new clip has **normalized quaternion value tracks** for torso and weapon
orientation. The authoring tool uses shortest-arc slerp and samples them at
10 ms intervals. The presentation adapter converts each sampled quaternion
to the rig's YXZ Euler property immediately before the existing pose solver
runs. All selected rotations stay well short of 180°, so there is no wrap
across a representation boundary. Translation, beard/head response, foot
targets and blade roll remain separate visual-only value tracks. There are
no method, signal, collision, navigation or gameplay tracks.

A first straight interpolation from Guard B to Windup crossed the skull.
The final curve passes around the shoulder using an existing clear intermediate
pose from the isolated static investigation. That intermediate is a trajectory
guide; the three approved endpoints remain exact. The original rejected
`DwarfAxeAttack.tres` and earlier `DwarfAxeOverhead.tres` were not modified.

## Validation performed

- `dotnet build`: 0 errors, 0 warnings.
- Godot headless import and direct startup of the new demo: passed.
- `docs/validate_dwarf_approved_axe_attack.gd`: **0 failures**. It samples
  **341 frames** over the complete cycle and verifies approved endpoint mesh
  transforms, normalized quaternion tracks, both grip positions, hand/elbow
  order, wrist/elbow ranges, sole grounding, fixed roots and unit scale,
  cutting-edge direction, no deep skull/torso proxy penetration, joint and
  blade continuity, one-shot completion and repeated attacks.
- Maximum measured grip gap: **0.00000573 world units**. Maximum local wrist
  orientation: **51.7°**. Maximum elbow angle: **136.5°**. Maximum joint step
  over 5 ms: **5.4°**. The cutting edge led blade travel during the strike;
  minimum measured direction dot product was **0.398**.
- Approved pose comparison, previous blade-orientation and overhead tests,
  dwarf animation and definitive playable-dwarf tests were used as regressions.
- Captured and inspected front, side and isometric phase sheets, a 16-frame
  isometric timeline, and Windup/Impact at orthographic size 22. The lift and
  impact differ in silhouette at gameplay scale, though the dwarf is small
  enough that fine wrist detail requires Inspection mode.

Preview images: [front phases](milestone_06d_approved_attack_front.png),
[side phases](milestone_06d_approved_attack_side.png),
[isometric phases](milestone_06d_approved_attack_isometric.png),
[16-frame timeline](milestone_06d_approved_attack_timeline.png), and
[gameplay-scale pair](milestone_06d_approved_attack_gameplay.png).

The rigid armor and beard can still show small cosmetic overlaps at close
inspection; the deep skull/torso test is a proxy, not complete mesh collision.
The animation is a visual prototype and requires manual visual approval before
any combat or player integration.

## Files added in this phase

- `resources/animations/DwarfApprovedAxeAttack.tres`
- `scenes/characters/DwarfBerserkerApprovedAxeAttack.tscn`
- `scenes/art/DwarfApprovedAxeAttackDemo.tscn`
- `scripts/characters/DwarfApprovedAxeAttackPresentation.cs` and generated `.uid`
- `scripts/art/DwarfApprovedAxeAttackDemo.cs` and generated `.uid`
- `docs/author_dwarf_approved_axe_attack.py`
- `docs/validate_dwarf_approved_axe_attack.gd` and generated `.uid`
- `docs/milestone_06d_approved_attack_{front,side,isometric,timeline,gameplay}.png`
- `docs/MILESTONE_06D_APPROVED_AXE_ATTACK.md`

All earlier uncommitted Milestone 06D files were preserved. Branch:
`feature/milestone-05-animation`; HEAD: `2735828`. No stage or commit was made.
