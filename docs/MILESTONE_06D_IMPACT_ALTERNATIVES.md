# Milestone 06D — Static impact alternatives for visual review

## Open the comparison

Open `res://scenes/art/DwarfAxeBladeOrientationPreview.tscn` in Godot 4.7.2
Mono and press **F6**. Choose **GUARD**, **WINDUP**, **IMPACT A** or **IMPACT B**
to compare held poses immediately. **GUARD B** shows the complementary guard
stance for Alternative B. Front, Side and Isometric buttons change the view.
The green arrow is a temporary cutting-edge direction marker present only in
this preview. No attack transitions or gameplay actions play here.

## Why the earlier impact could read as a flat-side hit

The axe generator forms the blade in a plane spanned by the shaft direction
`normalize((0.92, 1.05, 0))` and the outward cutting direction
`(shaft.y, -shaft.x, 0)`. The broad faces have normals near `+Z` and `-Z`;
the focused test found **18 vertices** on the actual forged blade face with
these normals. In the earlier impact, the hilt and torso inclination left some
broad-face exposure toward the enemy. Checking only the hilt's forward axis or
the arrow direction missed this visual concern. The isometric camera also sees
the blade obliquely, so it can display part of the broad face even when the
enemy directly in front would meet the edge.

## Alternative A — Lowered Guard Impact (preferred for review)

Starts from the original Guard controls, preserving its exact `90°` shaft-axis
blade correction and zero weapon rotation. The planted pelvis lowers `0.07`
world units; the torso leans forward `0.07 rad`; the held axe shifts a further
`0.03` down and `0.035` forward relative to the torso. A small head/beard
response completes the pose. The actual cutting tip finishes **0.162 world
units lower** and **0.078 world units forward** of Guard in model space.
Both elbows remain bent (left `136.5°`, right `100.3°` between upper arm and
forearm), and the local wrist rotations remain below `11°`. A is visibly a
low, committed contact pose while keeping the existing Guard/Windup source
poses unchanged.

## Alternative B — Exchange Guard and Impact

**Impact B** is the original Guard posture used verbatim as impact. Its blade
plane is exact edge-on to the forward target, and its grips and wrist angles
match Guard. It shows less downward weight transfer than A, which is a visual
limitation to judge manually.

**Guard B** reuses the previous impact's body, arm, beard and axe attachment
posture. Only the axe mesh's static handle-axis roll changes from `90°` to
`109.5°` to remove the previous broad-face exposure. The grip markers stay on
the same shaft; both hands retain their anchors. This alternative places the
axe low in the waiting stance, while the impact itself returns to the higher
original Guard. It may read as a weaker strike, and no transition between
these poses has been authored.

## Measurements and checks

The target direction is the dwarf's model-space `-Z`; the intended contact
direction for this static evaluation is forward and downward. `edge_forward`
is the cutting-edge normal's dot product with forward. `flat_exposure` is the
absolute broad-face normal's dot product with forward; zero means the broad
face is edge-on to the enemy.

| Pose | Edge forward | Flat exposure | Edge vs. down-forward |
| --- | ---: | ---: | ---: |
| Guard | 1.000 | 0.000 | 0.707 |
| Windup | 0.519 | 0.332 | -0.237 (blade is raised) |
| Impact A | 0.998 | 0.046 | 0.755 |
| Impact B | 1.000 | 0.000 | 0.707 |
| Guard B | 0.993 | 0.000 | 0.735 |

Both impact choices therefore put the narrow cutting edge toward the enemy,
with little or no broad-face exposure. These are held-pose orientation checks;
they do **not** prove that a future animated path will contact edge-first.
That requires a later transition test once one option is approved.

`dotnet build` passed with **0 errors and 0 warnings**. Godot headless import
and direct preview startup passed. `docs/validate_dwarf_impact_alternatives.gd`
passed with **0 failures**: mesh/material identity, actual blade-face normals,
cutting and broad-face directions, down-forward contact orientation, both
grips, elbow and wrist ranges, grounded soles, Impact A displacement, exact
Guard-to-Impact B pose reuse, and old Impact-to-Guard B body pose reuse. The
previous blade-orientation test, original overhead-attack test, dwarf animation
test and definitive playable-dwarf test were also run as regressions.

Screenshots were rendered with Vulkan Forward+ and visually inspected:

| View | Alternative A: Guard / Windup / Impact A | Alternative B: Guard B / Windup / Impact B |
| --- | --- | --- |
| Front | [A front](milestone_06d_impact_a_front.png) | [B front](milestone_06d_impact_b_front.png) |
| Side | [A side](milestone_06d_impact_a_side.png) | [B side](milestone_06d_impact_b_side.png) |
| Isometric | [A isometric](milestone_06d_impact_a_isometric.png) | [B isometric](milestone_06d_impact_b_isometric.png) |

The broad axe face is visible from the *side* camera by design. The front
camera confirms that the face is narrow toward a target standing in front.
The isometric camera has an oblique view and should be assessed alongside the
front and side sheets.

## Files changed in this iteration

- Modified: `scripts/art/DwarfAxeBladePoseDemo.cs` (five held-pose controls)
- Modified: `scripts/characters/DwarfAxeBladePosePresentation.cs` (pose-local
  static roll setting for Guard B)
- Added: `docs/validate_dwarf_impact_alternatives.gd` and its generated `.uid`
- Added: six `docs/milestone_06d_impact_{a,b}_{front,side,isometric}.png` images
- Added: `docs/MILESTONE_06D_IMPACT_ALTERNATIVES.md`

The character model, approved Idle/Walk/grounded locomotion, existing full
attack resources, world and gameplay scenes remain unchanged. All files are
local and uncommitted on `feature/milestone-05-animation` at HEAD `2735828`.
Stop for manual visual approval before creating animation transitions.
