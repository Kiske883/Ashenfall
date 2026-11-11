# Milestone 05A.1 - Articulated resting presentation

## Visual review correction (current controls)

The first prototype was not visually approved. The original implementation record below describes its initial controls; this section supersedes those controls and the earlier shared-turntable behavior.

Diagnosis before changing files: pivots match the original model's head/shoulder/hip positions; no accidental scale or translation was present. The generic demo applied +20 degrees around local X to every non-head pivot. PelvisPivot is the ancestor of torso and both legs, so pitching it raised the rigid feet and shifted the projected silhouette, producing the reported growing-taller effect. Arms also pitched on X, putting much of their displacement along view depth rather than across the silhouette; the shield obscures its hand and forearm, and the sword demonstration lacked sufficient visible lateral separation. HeadPivot only offered positive Y rotation, so there was no independently reachable opposite direction. The old turntable rotated the reference as well as the prototype.

Corrected only the comparison demo (not the reusable pivot hierarchy or any art):

- Pelvis left/right: +/-25 degrees around local Y. This is a hip swivel of the existing rigid hierarchy; all vertex heights are unchanged. It is not a planted-foot/IK demonstration: torso and legs still follow the pelvis.
- Shield arm: -18 degrees around local Z; sword arm: +18 degrees around local Z. These lift the rigid arms outward into the silhouette rather than into camera depth. Both equipment attachments remain intact. Small controlled angles limit movement beneath the fixed combined shoulder armor; they are not a solution for extreme shoulder articulation.
- Head left/right: independently selectable +/-30 degrees around local Y. Eyes stay attached.
- Each joint button first restores the previous joint, then applies its named pose. Reset pose stops repeat, restores the selected transform and restores the prototype's front orientation. No scaling, vertical translation or geometry edits.
- Slow repeat checkbox: comparison-only six-second eased rest -> selected pose -> rest cycle. It updates transforms directly, with no animation clips, AnimationPlayer or gameplay state changes. Selecting another button switches the one repeated joint. Reset turns repetition off.
- Prototype view +/-45 and Restore front view affect only the articulated root. The original V2 remains stationary, including during repetition and view inspection. Reset restores equal orientations for rest comparison. Lighting and camera stay fixed; original close/gameplay zoom controls remain available.

The joint buttons, repetition control, reset, and selected joint axis/amplitude are displayed on screen. Comparison path remains `res://scenes/art/ArticulationComparison.tscn`; open and press F6.

Validation executed for this correction:

- dotnet build passed with zero warnings/errors.
- `validate_articulation_review.gd`: zero failures with real Vulkan rendering. All six poses restore correctly, every mesh/material/array stays unchanged, the reference remains stationary, both head signs are reachable, and repeat/reset work. Pelvis tests check every vertex for unchanged height. At the fixed close view, maximum projected displacements are approximately pelvis 13 px, shield 60 px, sword 96 px, head 15 px; these are geometric visibility checks rather than a claim of universal readability at gameplay zoom.
- New six-pose captures saved as `docs/milestone_05_review_pelvisleft.png`, `..._pelvisright.png`, `..._shield.png`, `..._sword.png`, `..._headleft.png`, `..._headright.png`. Both arms, both head directions and the final 25-degree pelvis-left capture were opened and visually inspected. All six captures are retained for user review.
- Existing articulation, enemy pursuit, enemy attack and mouse/navigation suites rerun: zero failures in each. Godot headless editor import passed. No gameplay or approved art files changed.
- No user approval is assumed; interactive visual acceptance remains pending.

Files changed relative to the previous local prototype: `scripts/art/ArticulationComparison.cs`, `scenes/art/ArticulationComparison.tscn`, this documentation. New: `docs/validate_articulation_review.gd`, its Godot `.uid`, and the six review PNGs with ignored import metadata. Earlier prototype files and screenshots are retained. Existing tracked art/gameplay files remain unchanged; no staging or commits.

Implemented on `feature/milestone-05-animation`, starting from clean HEAD `13b30a0`. All existing tracked files remain unchanged. No staging, commits, pushes, merges or branch switches were performed.

## Scenes and responsibility

- Reusable presentation: `res://scenes/enemies/FallenGuardianPresentation.tscn`.
- Isolated comparison: `res://scenes/art/ArticulationComparison.tscn`.

The presentation is a visual-only Node3D. It instances the approved V2 scene as Source. After its existing child generators finish their normal Ready initialization, the new parent adapter reparents their existing nodes with keep-global-transform enabled. The adapter never generates, rewrites, duplicates, splits or recenters vertex geometry. Instantiating the procedural source still performs its original, unchanged one-time mesh generation, exactly as an ordinary V2 instance does; articulation adds no mesh-generation pass.

EnemyController, FallenGuardian.tscn, EnemyPursuitTest, the main world, physics/navigation, player, camera controls, V1/V2 generators, scenes, shaders and materials are unchanged. This presentation is not integrated into the gameplay enemy. It contains no gameplay logic, animations, AnimationPlayer, Skeleton3D or skinning.

## Final runtime hierarchy

```text
FallenGuardianPresentation
├── Source (unchanged V2 instance; original containers retained)
└── PoseRoot
    └── PelvisPivot
        ├── Pelvis / Geometry
        ├── TatteredCloth
        ├── LeftLegPivot
        │   └── LeftLeg / Geometry
        ├── RightLegPivot
        │   └── RightLeg / Geometry
        └── TorsoPivot
            ├── RibcageAndSpine / Geometry
            ├── CorrodedArmor
            ├── LeatherAndRags / Geometry
            ├── HeadPivot
            │   ├── Head / Geometry
            │   └── CursedEyes
            ├── LeftArmPivot
            │   ├── LeftArm / Geometry
            │   └── ShieldAttachment
            │       └── Shield / Geometry
            └── RightArmPivot
                ├── RightArm / Geometry
                └── SwordAttachment
                    └── Sword / Geometry
```

Original mesh nodes and local vertex coordinates are preserved. Generated nodes have no serialized Owner, following the existing art convention; the adapter deterministically rebuilds the hierarchy on scene entry after compilation. No AnimationPlayer tracks depend on these paths yet.

## Pivots

Coordinates below are in the presentation's model space at rest, not parent-local coordinates. Nested pivots receive compensating local positions. Anatomical pivots are read directly from the generated part's existing Position, rather than maintained as duplicate constants. Equipment attachments use their existing item pivots; these are grip/equipment reference positions, not newly inferred wrist joints.

| Pivot | Model-space XYZ |
| --- | --- |
| PoseRoot | (0, 0, 0) |
| PelvisPivot | (0, 0.94, 0) |
| TorsoPivot | (0, 1.19, 0) |
| HeadPivot | (0, 1.73, 0) |
| LeftArmPivot | (-0.279, 1.46, 0.012) |
| RightArmPivot | (0.279, 1.46, 0.012) |
| LeftLegPivot | (-0.11, 0.878, 0.015) |
| RightLegPivot | (0.11, 0.878, 0.015) |
| SwordAttachment | (0.438, 0.91, -0.23) |
| ShieldAttachment | (-0.45, 1.065, -0.30) |

Head's original tilt remains on the original Head node. Eyes retain their initial model-space transform when attached under HeadPivot, so additional head-pivot rotation carries both together without applying the original tilt twice. Both weapons retain their exact resting placement and move with their corresponding complete arms.

## Geometry and appearance preservation

The presentation reuses all thirteen generated mesh instances with their original ArrayMesh and material references. It retains V2's 10,170 triangles, 18 surfaces and 7 materials. Adding pivots adds no mesh surfaces or shader variants. No mesh resource is changed during reparenting; the original vertex-space coordinates also preserve the procedural shader's grain/rust sampling.

The validation tool snapshots mesh references, material references and surface arrays immediately after Source.Ready but before the adapter's parent Ready. It compares them after adaptation, then compares every mesh's rest transform and complete array hashes against a separately loaded original V2. All checks pass. Global transform preservation is approximate to floating-point precision, not a claim of bit-identical matrices.

Actual Vulkan Forward+ captures were rendered and opened for inspection at the front, back and gameplay scale. The articulated presentation matches the original resting silhouette, head/eyes, bone color, equipment placement, armor and cloth. Side-by-side shadows can differ because of scene positions and overlapping projected shadows; equal model geometry does not imply identical screen pixels in two different locations.

New evidence only; previous milestone screenshots remain untouched:

- `docs/milestone_05_articulation_front.png`
- `docs/milestone_05_articulation_back.png`
- `docs/milestone_05_articulation_gameplay.png`

## Open and inspect

1. Open project.godot in Godot 4.7.2 .NET and build the C# solution.
2. Open `res://scenes/art/ArticulationComparison.tscn` and press F6.
3. Original V2 is left; articulated V2 is right. They have equal scale/orientation and share the same neutral ground, lights and camera.
4. Inspection uses orthographic size 3.6; Gameplay scale uses size 22. Inclination remains 45 degrees.
5. View -45 / +45 rotates both models equally under the same fixed lighting, allowing inspection from all sides.
6. Select a pivot, then Inspect pivot 20 degrees. This applies a single static offset to the articulated model only. Head turns around local Y; other selected pivots turn around local X. This is not a clip or an automatically advancing animation.
7. Reset pose restores the exact stored pivot transform. Selecting another pivot and inspecting also restores the previous one first. Reset before evaluating resting-pose equality.
8. The standalone presentation can also be opened in the 3D editor after compilation. Runtime pivot inspection controls are provided by the comparison, not the reusable visual asset.

## Validation results

- `dotnet build`: passed, zero warnings/errors (.NET 8 / Godot.NET.Sdk 4.7.2).
- Godot headless editor import: passed, including new scripts/scenes and evidence.
- `docs/validate_articulation.gd` with actual Vulkan rendering: zero failures. Both models and comparison loaded; all thirteen surface-array sets and resting transforms match; each existing mesh/material resource is unchanged before/after adaptation.
- Head, both arms, both legs, pelvis and torso pivots tested with reversible static rotations. Descendant mesh transforms remain rigidly attached. Explicit assertions verify eyes/head, sword/right arm, shield/left arm and hip positions.
- The comparison's inspect/reset controls were invoked programmatically; gameplay camera size 22 checked. Front/back/gameplay captures were opened and visually inspected. Interactive human button-click testing is not claimed.
- Existing `validate_enemy_pursuit.gd`: zero failures.
- Existing `validate_enemy_attack.gd`: zero failures, including event count and timing.
- Existing `validate_mouse_navigation.gd`: zero failures, including obstacles, boundaries and camera tracking. These tests load the original world.
- Existing `validate_enemy_v2.gd`: zero failures, including original anatomy fingerprint, counts and material independence.
- `git diff --name-only` empty: no existing tracked art, gameplay or other files changed.

Reproduce from project directory:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_articulation.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan --fixed-fps 60 --script res://docs/validate_articulation.gd -- --capture
```

## Known limitations and next step

- Arms and legs remain single rigid pieces. There are no elbow, wrist, knee or ankle articulations. Existing mesh surfaces are material groups, not movable joints.
- Both V2 shoulders and chest fragments remain one combined armor mesh attached to TorsoPivot. Large independent arm rotations can reveal detachment/intersection; the static inspection offset deliberately exposes this limitation. Resting armor placement is unchanged. No triangle splitting was attempted.
- LeatherAndRags combines the diagonal harness with rear cloth and follows the torso. TatteredCloth follows the pelvis as a single piece. No cloth physics or strip articulation.
- No general skeleton retargeting, LOD, crowds benchmark or gameplay integration. Future generator structural changes require updating/validating this explicit adapter; it intentionally uses known source part names.
- Next recommended step after resting-pose approval: add an AnimationPlayer and a separate presentation driver for a small-amplitude Idle proof of concept. Resolve the accepted range of rigid shoulder motion before Walk/Attack. Retain controller authority over yaw, physics and AttackExecuted timing. This phase deliberately stops before creating any animation clips or modifying controller contracts.

## Complete file inventory

New authored files:

- `scenes/enemies/FallenGuardianPresentation.tscn`
- `scripts/enemies/FallenGuardianPresentation.cs`
- `scenes/art/ArticulationComparison.tscn`
- `scripts/art/ArticulationComparison.cs`
- `docs/validate_articulation.gd`
- `docs/MILESTONE_05_ANIMATION_FOUNDATION.md`

New evidence: the three PNGs listed above. Godot metadata: `.uid` sidecars for the two new C# scripts and the GDScript test; `.png.import` sidecars for the three captures (ignored under existing project rules).

Existing files modified: none. All additions remain local/untracked. Stop for visual approval before animations or gameplay replacement.
