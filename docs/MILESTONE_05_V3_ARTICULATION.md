# Milestone 05A.3.1 - V3 segmented limb foundation

Started from HEAD `e4ec9ab` on `feature/milestone-05-animation`. Existing untracked 05A.2 work was expected, inspected and preserved. This is a new isolated visual prototype, not approval of the earlier Idle/Walk motion. No original art, presentation, animation, player, enemy controller, gameplay scene, navigation, collision or world files were modified. No Git staging, history/branch mutations, reset, clean or stash.

## Scenes and generation

- New presentation: `res://scenes/enemies/FallenGuardianV3Presentation.tscn`.
- Comparison: `res://scenes/art/V3ArticulationComparison.tscn`.
- New recipe: `scripts/enemies/FallenGuardianV3Generator.cs`.

The V3 generator is an independent copy of the original anatomy recipe, with explicit segment boundaries inserted between existing primitive calls. It emits upper arm, forearm, hand, thigh, shin, kneecap and foot separately for each side. The original right shoulder remnant is also its own mesh. It does not classify or cut existing triangles. All original primitive calls retain their order, arguments and random-number consumption, including the generation of head/torso before limbs and equipment afterward. The duplicated recipe is deliberate isolation from approved V1/V2; future changes require maintaining and validating this separate V3 recipe.

Crucially, each new limb segment retains the old whole-limb mesh origin (shoulder for arm segments, hip for leg segments), not its new articulation origin. Pivot containers and compensating transforms provide elbow/wrist/knee/ankle movement without changing vertex coordinates sampled by the procedural shader. Normals and per-vertex colors are emitted by the unchanged primitive routines. Geometry grouping/indexing changes, but no cosmetic vertices, caps, fillers or new shapes were added.

The presentation reuses the unchanged V2 detail generator and existing V2 shader source. Embedded material configurations match V2. Anatomy generates first; a callback on the parent Ready signal builds the final hierarchy only after the V2 generator has created armor, cloth and eyes. This preserves the original head lookup/tilt used by eye generation. No external dependencies, Skeleton3D, skinning or animation clips.

## Hierarchy and attachments

```text
FallenGuardianV3Presentation (existing V2 detail generator)
├── Anatomy (new V3 recipe; original containers retained)
├── CursedDetails (original container retained)
└── PoseRoot
    └── PelvisPivot
        ├── Pelvis
        ├── TatteredCloth
        ├── LeftHipPivot / RightHipPivot
        │   ├── Thigh
        │   └── KneePivot
        │       ├── Shin
        │       ├── Kneecap
        │       └── AnklePivot
        │           └── Foot
        └── TorsoPivot
            ├── RibcageAndSpine
            ├── CorrodedArmor (combined V2 shoulders + chest)
            ├── LeatherAndRags
            ├── HeadPivot
            │   ├── Head (original tilt retained)
            │   └── CursedEyes
            └── LeftShoulderPivot / RightShoulderPivot
                ├── UpperArm
                ├── RightShoulderRemnant (right only)
                └── ElbowPivot
                    ├── Forearm
                    ├── ShieldAttachment -> Shield (left only)
                    └── WristPivot
                        ├── Hand
                        └── SwordAttachment -> Sword (right only)
```

Segment names have Left/Right prefixes in the actual scene. Each original anatomical part has a Geometry MeshInstance3D child. Generated nodes are reconstructed on entry, following the original no-Owner tool-generation convention; they are not serialized duplicate meshes.

Sword follows the right hand through WristPivot. Shield follows the left forearm through ElbowPivot and deliberately does not follow wrist rotation. Both equipment attachment transforms preserve their original placement. Eyes follow HeadPivot. Combined armor follows the torso, waist cloth the pelvis; the combined harness/rear-rags part follows the torso as in the earlier prototype.

## Joint coordinates

All values are model-space resting positions, before the comparison's orientation. Parent-local transforms compensate for nesting. No symmetry correction is applied.

| Joint | Left XYZ | Right XYZ |
| --- | --- | --- |
| Shoulder | (-0.279, 1.46, 0.012) | (0.279, 1.46, 0.012) |
| Elbow | (-0.377, 1.21, -0.034) | (0.377, 1.17, -0.034) |
| Wrist | (-0.426, 1.10, -0.216) | (0.426, 0.944, -0.216) |
| Hip | (-0.11, 0.878, 0.015) | (0.11, 0.878, 0.015) |
| Knee | (-0.14, 0.492, 0.035) | (0.14, 0.492, -0.025) |
| Ankle | (-0.16, 0.095, 0.07) | (0.16, 0.095, -0.015) |

Pelvis: (0, 0.94, 0); torso: (0, 1.19, 0); head: (0, 1.73, 0). Sword attachment: (0.438, 0.91, -0.23); shield attachment: (-0.45, 1.065, -0.30).

Pivots use model-aligned axes with compensated translations. The inspection uses local X for sagittal elbow/knee/wrist/ankle movement and local Z for conservative shoulder abduction. These are explicit prototype hinge tests, not a complete anatomical joint constraint solver or automatically derived bone-aligned skeleton.

## Geometry preservation results

| Metric | V2 | V3 |
| --- | ---: | ---: |
| Triangles | 10,170 | 10,170 |
| Mesh surfaces | 18 | 28 |
| Distinct materials | 7 | 7 |
| Mesh instances | 13 | 24 |

`validate_v3_articulation.gd` compares a multiset of oriented triangles independently of surface grouping and mesh indexing. Each corner includes model-space position and normal, original mesh-local vertex coordinate (shader sampling), exact serialized vertex color, and a material signature covering shader path/uniforms or stored StandardMaterial properties. Coordinates/normals are quantized to 1e-5 for transform floating-point tolerance. The multisets match. This also checks that splitting did not shift procedural random colors on subsequent body parts.

No deliberate resting silhouette, proportion, armor, cloth, material or equipment change was made. Triangle equality is tolerance-based, not a claim that reorganized mesh buffers are bit-identical. The cost is ten additional surfaces and eleven mesh instances; actual GPU passes/draw calls depend on rendering/shadows. No crowd-performance guarantee or optimization is claimed.

## Open and inspect

1. Open project.godot in Godot 4.7.2 .NET and build.
2. Open `res://scenes/art/V3ArticulationComparison.tscn`, press F6.
3. V2 stays at rest on the left; V3 is on the right. Both share scale, orientation, lighting and camera. Inspection uses orthographic size 3.6; Gameplay scale uses size 22.
4. Choose Left/Right Elbow, Knee, Wrist or Ankle independently from the dropdown, then Show joint. Additional Shoulder and Hip tests are included.
5. Elbow test: +35 degrees X; knee: -25 X; wrist: +15 X; ankle: +12 X; shoulders: +/-4 Z; hips: +8 X. Conservative values are for inspection, not approved motion ranges.
6. Enable Slow repeat for a six-second eased rest -> selected pose -> rest preview. It is comparison-only transform sampling; no AnimationPlayer, clip, locomotion or gameplay animation is created.
7. Reset pose stops repeat and restores the last selected joint. Selecting/showing another joint restores the preceding joint before testing the next one.
8. Camera +/-15 orbits the camera around both stationary models. Camera reset restores its original transform. Reset pose and camera reset are intentionally separate.

## Visual inspection and limitations

Vulkan Forward+ captures were produced for rest, back, size-22 gameplay, right elbow, left knee, right wrist, right ankle and right shoulder. Rest and the joint captures were opened during development; final shoulder/back captures were also opened after UI label corrections. Resting silhouette and equipment match V2. Evidence uses new filenames only: `docs/milestone_05_v3_*.png`.

The original long-bone ends and primitive caps were designed for the original static pose. Flexion can expose end caps, spaces or overlap at elbows/knees; no new joint fillers were added. The kneecap follows the shin rigidly and can appear offset/intersecting at stronger flexion. Ankle articulation rotates the existing rigid heel/toe cluster; there is no heel-roll, toe articulation or planted-foot contact. The shoulder remnant moves with its upper arm, but both larger V2 shoulder plates remain combined with chest armor on the torso. Shoulder excursions are restricted to four degrees in the demo; large rotations remain unsupported. Cloth remains rigid and can intersect bent limbs.

These limitations are exposed for review rather than hidden by changing approved geometry. No cosmetic remodeling has been performed. Any future joint-shape correction or armor segmentation needs separate documented approval. Manual interactive approval and a production animation-range sweep are still pending; the static captures and automated transform checks do not constitute motion-art approval.

## Executed validation

- C# dotnet build: passed, zero warnings/errors.
- Godot headless editor import: passed.
- V3 focused suite, headless and real Vulkan capture: zero failures. Geometry comparison, all twelve asymmetric limb pivot positions, reversible descendant attachments/unit scales, eyes, sword, forearm-mounted shield, reset, visual-only structure and size 22 checked.
- Existing articulation suite: zero failures.
- Existing articulation-review suite: zero failures.
- Existing uncommitted Idle/Walk suite: zero failures; no captures overwritten.
- Existing enemy pursuit suite: zero failures.
- Existing enemy attack suite: zero failures. It emitted the previously observed Jolt job-capacity warning during accelerated headless execution; this is reported rather than suppressed. No engine or physics settings were changed.
- Existing player mouse/navigation suite: zero failures, also loading the original world and checking obstacles/boundaries/camera.
- SHA256 comparison of all 139 pre-existing files under scripts/scenes/resources/docs: no changes, including the local 05A.2 work. Final size-22 gameplay capture also opened for visual inspection.
- Compile and resource generation initially exposed a validation-script type-inference parse error; it was corrected before the successful runs. No unresolved test failure remains.

Reproduce:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_v3_articulation.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan --fixed-fps 60 --script res://docs/validate_v3_articulation.gd -- --capture
```

## Files and next step

New source/documentation only:

- `scripts/enemies/FallenGuardianV3Generator.cs` and `.uid`
- `scenes/enemies/FallenGuardianV3Presentation.tscn`
- `scripts/art/V3ArticulationComparison.cs` and `.uid`
- `scenes/art/V3ArticulationComparison.tscn`
- `docs/validate_v3_articulation.gd` and `.uid`
- `docs/MILESTONE_05_V3_ARTICULATION.md`

Eight new captures: suffixes `rest`, `rightelbowpivot`, `leftkneepivot`, `rightwristpivot`, `rightanklepivot`, `rightshoulderpivot`, `back`, `gameplay`, all prefixed `docs/milestone_05_v3_` and suffixed `.png`. Godot generates ignored `.png.import` metadata.

No existing file modifications. Existing local 05A.2 files remain uncommitted and preserved. Stop here for V3 rest/joint visual approval; do not overwrite the old clips, build new locomotion/attack clips or integrate V3 into gameplay in this phase.
