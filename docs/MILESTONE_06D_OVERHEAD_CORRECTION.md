# Milestone 06D correction — overhead two-handed axe preview

## Review status and launch

The first 06D attack was visually rejected. Its resource and preview remain at
`res://resources/animations/DwarfAxeAttack.tres` and
`res://scenes/art/DwarfBerserkerAttackDemo.tscn` for comparison.

Open `res://scenes/art/DwarfBerserkerOverheadAttackDemo.tscn` in Godot 4.7.2
Mono and press **F6**. Use **ATTACK** to play the new 1.7 s sequence once; it
returns to Idle. **IDLE** and **WALK** reuse the approved clips. **Front**,
**Side**, **Isometric**, **Inspection**, and **Gameplay / 22** provide the
inspection views. The new preview is still awaiting manual visual approval.

## Diagnosed cause

The `BattleAxe` is one rigid mesh under `TorsoPivot`. The existing presentation
rotates it once about the midpoint of the two grip markers in torso-local space;
the arm solver then positions each hand against those markers. There are no
independent hand or axe tracks fighting each other. The torso transform is
inherited as intended. The 171 sampled poses have no Euler wrap or discontinuity.

The rejected clip authored a **3.1 radian (178 degree) rotation around the
model's Z axis**. The axe head is near the *upper end* of the shaft in the
approved generator (`t=0.87`), while the original animation swung that head
around the dwarf's frontal plane. This caused the visible lateral flourish and
forced the IK arms into very large wrist and elbow changes. The previous clip's
test reported a wrist orientation as large as **143 degrees**. Passing grip and
continuity checks did not make that motion visually credible.

## Replacement motion and architecture

The new `DwarfAxeOverhead.tres` is a separate `AnimationLibrary`. The new
character scene inherits the approved grounded dwarf and substitutes only the
`Combat` library. It reuses `DwarfAxeAttackPresentation.cs`, the same rigid
two-hand grip solver, mesh, materials, beard, original Idle and grounded Walk.
No player, enemy, collision, navigation, world, or gameplay resource changed.

The authoring script samples 171 poses at 10 ms intervals, producing native
Godot value tracks. One axe centre and one orientation determine both hand
targets. The axe does not spin around the dwarf. Its authored rotations stay
within **X -0.25 to +0.95 rad** and **Z -0.43 to +0.35 rad**, with **Y 0**.
The forearm twist weight rises to at most **0.5** during the overhead lift so
the hands can grip the same shaft without concentrating all roll at the wrists.
The torso, shoulders/elbows through the grip solver, hips/knees through a small
pelvis crouch, head, and beard participate. Both foot targets remain planted.

| Time | Motion |
| --- | --- |
| 0.00–0.18 s | Short anticipation from the approved neutral axe stance. |
| 0.18–0.67 s | Both hands lift the axe; the cutting head moves above and slightly behind the head, offset left to clear the skull. |
| 0.67–0.74 s | Brief held windup. |
| 0.74–0.96 s | Downward chop moves the cutting head forward and low. |
| 0.96–1.12 s | Grounded follow-through with torso lean and knee compression. |
| 1.12–1.70 s | Recovery to the original guard. |

The accepted geometry and approved locomotion curves are unchanged. The
original rejected clip and preview are unchanged and independently runnable.

## Validation actually performed

- `dotnet build`: passed with 0 errors and 0 warnings.
- Godot headless editor import and direct startup of the new preview: passed.
- `docs/validate_dwarf_overhead_attack.gd`: **0 failures** across 401 sampled
  poses, checking grip markers, hand and elbow order, real boot soles, limb
  lengths, fixed root transforms, geometry/material identity, continuous joints,
  constrained axe rotation, cutting-head trajectory, head/torso deep penetration
  proxies, recovery pose, native clip playback and Idle/Walk availability.
- Maximum measured hand-to-grip gap: **0.00000543 world units**. Soles stayed
  within about **0.000003 world units** of the floor. The cutting edge dropped
  **0.871 world units**. No deep skull/torso proxy penetration was detected.
- Maximum local wrist orientation was **51.7 degrees**; the forearm-to-grip
  direction reached **100.9 degrees**. These are geometric diagnostics, not
  proof that every arm pose is anatomically ideal.
- At the held windup, the measured cutting edge had `z=+0.180`, behind the
  head centre at `z=+0.035`; the axe mesh reached `y=1.645`.
- The original rejected 06D focused test, approved dwarf animation test,
  definitive player and locomotion tests, and Fallen Guardian attack test all
  passed with 0 failures. Existing gameplay scenes were not changed.
- Captured and inspected 8 phase frames each from front, side and isometric
  cameras, plus **16 evenly spaced isometric frames** over the full clip and a
  gameplay-size-22 frame. These are visual evidence for review, not user approval.

Preview images: [isometric phases](milestone_06d_overhead_isometric.png),
[side phases](milestone_06d_overhead_side.png),
[front phases](milestone_06d_overhead_front.png),
[16-frame timeline](milestone_06d_overhead_timeline.png), and
[gameplay-scale frame](milestone_06d_overhead_gameplay.png).

To reproduce the authored clip and validation from the project root:

```powershell
python docs/author_dwarf_overhead_attack.py
dotnet build
& 'C:/Propio/tools/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan --resolution 1280x800 --fixed-fps 60 --script docs/validate_dwarf_overhead_attack.gd -- --capture
```

## Limitations and review points

The shoulders, elbows, armor and beard are rigid segments, so close-up
overlaps can remain even though the axe avoids deep penetration into the
skull/torso proxy. The raised arm partly occludes the face in the isometric
view. At orthographic size 22, the entire dwarf is small; use Inspection for
arm and grip review. The preview has no combat event, hitbox, damage, root
motion, gameplay input, or integration with the playable demo. A human should
watch repeated full cycles in F6 before approving the attack.

## Files added for this correction

- `docs/MILESTONE_06D_OVERHEAD_CORRECTION.md`
- `docs/author_dwarf_overhead_attack.py`
- `docs/validate_dwarf_overhead_attack.gd` and generated `.uid`
- `resources/animations/DwarfAxeOverhead.tres`
- `scenes/characters/DwarfBerserkerOverheadAttack.tscn`
- `scenes/art/DwarfBerserkerOverheadAttackDemo.tscn`
- `docs/milestone_06d_overhead_front.png`
- `docs/milestone_06d_overhead_side.png`
- `docs/milestone_06d_overhead_isometric.png`
- `docs/milestone_06d_overhead_gameplay.png`
- `docs/milestone_06d_overhead_timeline.png`

All pre-existing uncommitted 06D files are retained. No files were staged or
committed. Branch: `feature/milestone-05-animation`; HEAD: `2735828`.

## Blade-orientation review (static pose correction)

The three body poses were reviewed as almost acceptable, but the cutting edge
was sideways to the enemy. In the approved generator, the handle axis is
`normalize((0.92, 1.05, 0))` and the blade extends from the socket along
`(axis.y, -axis.x, 0)`. This outward cutting-edge direction is perpendicular
to the dwarf's forward `-Z`. The broad face, rather than the edge, would lead
an uncorrected forward hit.

An **isolated static preview** now rolls the entire axe `Geometry` child by
exactly **+90 degrees around its own handle axis**. This maps the cutting-edge
direction to `-Z` in guard. The cylindrical shaft remains on the same axis;
the two grip markers and the axe attachment stay in their approved positions.
The source mesh, materials, proportions, IK solver, body poses and original
attack clip remain unchanged. This correction is **not yet integrated into
the full attack animation**.

Open `res://scenes/art/DwarfAxeBladeOrientationPreview.tscn` and press **F6**.
Select **GUARD**, **WINDUP** or **IMPACT**. Front, Side and Isometric buttons
change the view. The temporary green arrow points out from the cutting edge;
it exists only in this preview. No clip plays automatically.

The focused static-pose test passed with **0 failures**. Measured edge angles
to the target-forward axis were **0.0 degrees in Guard**, **58.7 degrees in
Windup** (the edge also points upward while raised), and **20.7 degrees at
Impact**. The edge retains a forward component throughout the sampled
descending interval; its minimum forward dot product was **0.519**. The edge
also leads the actual blade-tip travel throughout that interval (minimum dot
**0.330**), with **0.676** for the late strike sample. Hand-to-grip anchors,
weapon attachment transform, body/limb/beard poses, and mesh/material data
matched the uncorrected presentation. The windup edge is still angled upward,
so its final visual readability needs manual review.

Captured and inspected: [front](milestone_06d_blade_front.png),
[side](milestone_06d_blade_side.png), and
[isometric](milestone_06d_blade_isometric.png) Guard/Windup/Impact sheets.

Files added for this isolated correction:

- `scripts/characters/DwarfAxeBladePosePresentation.cs` and generated `.uid`
- `scripts/art/DwarfAxeBladePoseDemo.cs` and generated `.uid`
- `scenes/characters/DwarfBerserkerBladePose.tscn`
- `scenes/art/DwarfAxeBladeOrientationPreview.tscn`
- `docs/validate_dwarf_blade_orientation.gd` and generated `.uid`
- `docs/milestone_06d_blade_front.png`
- `docs/milestone_06d_blade_side.png`
- `docs/milestone_06d_blade_isometric.png`

This documentation file was updated. All earlier uncommitted 06D files and
the accepted gameplay scenes remain untouched. Manual visual approval is
pending before the orientation is applied to any complete attack clip.
