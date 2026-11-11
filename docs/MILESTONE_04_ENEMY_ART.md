# Milestone 04A - Skeleton Warrior: Fallen Guardian

> Iteration 02 is complete for visual review. The original V1 record below is preserved; the appended Iteration 02 section documents V2 and current validation.

Isolated first-enemy visual prototype on `feature/milestone-04-first-enemy`, based on commit `52cb6f1`. No AI, pursuit, combat, health, damage, navigation, physics actor or world placement has been implemented. Art approval is the next step.

## Visual design

Fallen Guardian is an adult human skeleton wearing remnants of medieval equipment, intended for the future cursed forest. The bone palette is aged ivory/brown rather than pure white. All geometry and surface variation are original procedural Godot content; there are no downloaded assets or dependencies.

Anatomy includes:
- An open-front cranial vault, brow ridges, separate zygomatic arches, nasal opening, deep dark orbital bowls, a U-shaped mandible and individually modeled upper/lower teeth with a missing tooth. A restrained old fracture crosses the vault. Eye sockets are recessed geometry, not glowing eyeballs or black decals on a solid sphere.
- Eight curved rib pairs, sternum, clavicles, scapular plates and a segmented spine with rear processes.
- Pelvic wings, sacrum, pubic arch and open obturator rings.
- Tapered long bones with enlarged ends rather than uniform capsule limbs. Forearms and lower legs contain paired bones, with separate fingers and toes.
- A slightly uneven resting stance and a small head tilt, keeping the silhouette recognizable from the fixed isometric angle.

The right hand grips a worn, rusty medieval sword with a tapered ridged blade, quillons, wrapped handle and pommel. The left hand supports a kite-shaped shield made from three separate weathered boards. Splits, a missing edge chunk and an interrupted iron rim are actual geometry. A small boss and rivets reinforce the silhouette. A broken angular shoulder plate, narrow leather harness and torn rear cloth complete the fallen-warrior appearance without hiding the rib cage.

The procedural surface shader provides subtle bone discoloration, rust patches, long wood grain and rough leather using local noise and vertex variation. High roughness and restrained specular/metal values avoid plastic-looking white bones. The recessed cavities use a separate dark matte material. There is no transparency or emissive eye effect.

## Scene structure

Reusable scene: **`res://scenes/enemies/SkeletonWarrior.tscn`**.

The root is Node3D, with one `Visual` container. The C# tool script builds ten logical visual parts once when the scene enters the tree:

- Head
- RibcageAndSpine
- Pelvis
- LeftArm / RightArm
- LeftLeg / RightLeg
- Sword
- Shield
- LeatherAndRags

Each part has its own local pivot and one indexed ArrayMesh. Materials are reused across its surfaces and the other parts. Generated nodes have no Owner, so editor previews do not serialize duplicate generated geometry into the source scene. Default Seed=401 reproduces the same static asset. Parameter/material changes apply after reopening the scene or restarting execution.

The root can later be placed under a movement actor, and named parts make later attachment/rigging work easier. This is not a skinned skeleton, animation rig, CharacterBody3D or enemy controller. In particular, arms/legs are static posed mesh parts; articulated animation will need a deliberate rig or further mesh segmentation later. No AnimationPlayer or Skeleton3D was added in this phase.

## Geometry and material counts

Measured from the actual mesh surface arrays after generation:

| Metric | Result |
| --- | ---: |
| Triangles | **9,911** |
| Indexed mesh parts | **10** |
| Mesh surfaces | **15** |
| Distinct shared material resources | **5** |
| Bounds, X / Y / Z | approximately **1.45 / 1.95 / 0.59** units |

The wide X bound includes sword/shield. The top is approximately 1.965 units above ground, comparable to the existing 1.9-unit player reference. Five materials cover bone, dark recesses, worn iron/rust, wood and leather/cloth. Four materials reuse one new shader program; recesses use a StandardMaterial3D.

Fifteen surfaces are an explicit prototype tradeoff for named body parts and separately readable equipment. They are not a claim of fifteen total GPU draw calls: shadow and renderer passes add work. The count is moderate for an individual prototype, but multiple-instance performance has not been benchmarked. A future production pass could cache/bake mesh resources and evaluate batching or LOD after the appearance is approved.

## Isolated preview

Preview scene: **`res://scenes/art/EnemyArtPreview.tscn`**.

The preview has its own neutral matte ground, warm key/cool fill, color ambient light, local SSAO and 4x MSAA. These settings affect only this scene/viewport; the previous MSAA value is restored when it exits. Existing world lighting and atmosphere are untouched. Godot's color ambient mode is used explicitly, with no sky contribution; see the [Environment reference](https://docs.godotengine.org/en/stable/classes/class_environment.html).

The existing player scene is instanced only as a scale reference. Processing and input are disabled and its collision layer/mask are zero on this preview instance. The source Player scene and its controller are unchanged.

Two buttons affect only the preview Camera3D's orthographic size:
- **Inspection**: size 3.6, for anatomy/equipment detail.
- **Gameplay scale**: size 22, matching the existing World camera framing.

Both keep the same 45-degree downward inclination and 45-degree isometric azimuth. The enemy is oriented toward the camera for inspection; the reusable scene's canonical forward is negative Z. The close inspection view is intentionally magnified, while the gameplay view demonstrates actual small-screen readability rather than presenting the close-up as gameplay scale.

## Files created

Authored files:
- `scenes/enemies/SkeletonWarrior.tscn`
- `scripts/enemies/SkeletonWarriorGenerator.cs`
- `resources/art/enemies/FallenGuardianSurface.gdshader`
- `scenes/art/EnemyArtPreview.tscn`
- `scripts/art/EnemyArtPreview.cs`
- `docs/validate_enemy_art.gd` (test/capture utility only)
- `docs/MILESTONE_04_ENEMY_ART.md`

Rendered evidence:
- `docs/milestone_04_enemy_art.png`
- `docs/milestone_04_enemy_gameplay_scale.png`

Godot-generated metadata:
- `scripts/enemies/SkeletonWarriorGenerator.cs.uid`
- `resources/art/enemies/FallenGuardianSurface.gdshader.uid`
- `scripts/art/EnemyArtPreview.cs.uid`
- `docs/validate_enemy_art.gd.uid`
- `docs/milestone_04_enemy_art.png.import`
- `docs/milestone_04_enemy_gameplay_scale.png.import`

**Existing files modified: none.** No approved tree assets or earlier screenshots were modified. World.tscn, environment generation, player, navigation, camera/input code, project settings and all completed milestones remain unchanged. The main scene has no enemy reference.

## Open in Godot

1. Open `C:\Propio\Proyectos\Ashenfall\project.godot` using **Godot 4.7.2 Stable .NET**.
2. Build the C# solution if prompted, or run `dotnet build` from the project directory.
3. In FileSystem, double-click **`res://scenes/art/EnemyArtPreview.tscn`**.
4. Press **F6 / Run Current Scene**. Player scale reference appears left, Fallen Guardian right.
5. Use the **Inspection** and **Gameplay scale** buttons. Check skull/teeth, rib gaps, pelvis, hands/feet, damaged shield and sword silhouette.
6. For independent editor inspection, open `res://scenes/enemies/SkeletonWarrior.tscn`, select its root and frame it in the 3D viewport. Its tool script generates the visual geometry after compilation. The reusable scene has no camera/light setup; use the art preview for a lit runtime view.
7. **F5 still runs the original playable world**, without enemies.

Direct launch from the project directory in PowerShell:

```powershell
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan res://scenes/art/EnemyArtPreview.tscn
```

The explicit Vulkan flag uses Forward+ on the current GTX 650 without changing project or system settings.

## Validation results

- Inspected project rules, relevant existing scenes/scripts, branch, Git state and baseline before edits. Branch was `feature/milestone-04-first-enemy`, HEAD `52cb6f1`, and working tree was clean. No unexpected changes were present.
- `dotnet build`: passed, zero warnings and zero errors, with the existing Godot.NET.Sdk 4.7.2 and net8.0 target.
- Godot headless editor import: passed.
- Headless editor opening of SkeletonWarrior.tscn and EnemyArtPreview.tscn: passed, including tool-script generation.
- Focused validation loads the Skeleton Warrior independently, measures all geometry/materials, then loads the preview and repeats the checks. Both produce identical counts: 9,911 triangles / 15 surfaces / 5 materials / 10 parts. Scale and budget assertions pass; logical parts and material references resolve.
- Actual windowed Vulkan Forward+ rendering: passed, exit 0, no missing-resource/shader/runtime errors. Both inspection and gameplay-scale screenshots were saved under docs and opened for visual inspection. The skull, ribs, pelvis and equipment are visible in the isometric inspection view; sword/shield and bone silhouette remain identifiable at gameplay scale, while individual teeth are naturally subpixel at that distance.
- Preview gameplay-scale toggle is checked to equal the existing world's orthographic size 22. Preview buttons invoke only local presentation logic; no global InputMap actions were added.
- Main playable world headless startup for 180 frames: passed, exit 0. Existing seed 731, 10 trees, 16 rocks, terrain 40 and 2766-polygon navigation bake are unchanged, with no enemy generation in that scene.
- Existing mouse/navigation suite: zero failed assertions, including mouse press/hold/release, cancellation, paths around props, unreachable requests, containment and camera tracking.
- `git diff --exit-code` and `git diff --name-only` confirm no existing tracked file changes. All task files are new/untracked. No prohibited Git mutations were run.

Reproduce focused validation:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_enemy_art.gd
```

Regenerate only the new Milestone 04 evidence with a real renderer:

```powershell
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan --fixed-fps 60 --script res://docs/validate_enemy_art.gd -- --capture
```

## Known limitations / next approval

- Static anatomical approximation, not a scanned or production-sculpted model. Orbital anatomy, bone ends and finger/toe detail are deliberately simplified. Bone grain is procedural and not anatomically unwrapped.
- No rig, skinning, idle/walk animation, moving grip attachments or articulation. Future animation needs a separate approved phase; static part pivots alone are not a complete rig.
- No physics collision, enemy navigation, perception, AI, pursuit, combat, health, damage or gameplay integration. The new enemy must not be inserted into the playable world as a functioning actor in this phase.
- Fifteen surfaces and procedural shaders need profiling before large enemy groups. No LOD, mass-instance benchmark or runtime frame-rate target was validated.
- Fine skull/finger details cannot all survive the actual gameplay zoom; the close-up and gameplay views make that limitation explicit. No motion-based art review is claimed because there is no animation.
- Only visual validation and existing player/world regression tests were performed. Enemy behavior, combat and animated intersections are out of scope and were not tested.

Git status: expected feature branch, existing tracked files unchanged, new art/test/documentation/evidence files left untracked. No `git add`, commit, push, merge, reset, clean, rebase or branch switch was performed. Milestone 04A stops here for visual approval before any enemy behavior or combat work.

## Art iteration 02 - Fallen Guardian V2

V1 is the approved anatomical baseline and remains independently available at `res://scenes/enemies/SkeletonWarrior.tscn`. V2 is independently instantiable at **`res://scenes/enemies/SkeletonWarriorV2.tscn`**. Both are static visual assets, without collision, navigation, AI, combat or animation, and neither is placed in the playable world.

V2 adds asymmetric, angular shoulder armor with missing sections and ragged lower plates, small damaged collar/chest fragments, folded dark burgundy waist strips with uneven hems and a small opening, and two tiny flattened red emissive points recessed in the tilted skull's eye sockets. The skull and most ribs remain exposed. Dark iron and reddish rust reuse the procedural surface approach. Cloth is opaque geometry rather than alpha cutouts; eyes add no real-time lights or bloom.

### Preservation and dependencies

V2 composes the existing anatomical scene as a read-only `Anatomy` instance. Its new generator adds only `CursedDetails` armor, cloth and eyes. It does not modify or copy the large anatomical generator or mesh data. The normal deterministic V1 loading recipe still runs with its approved seed; no V1 asset was rebaked or rewritten.

All five V1 material properties are overridden on the V2 instance with independent embedded resources, including an independent shader source at `resources/art/enemies/FallenGuardianV2Surface.gdshader`. The new cloth and eye materials are also V2-only. The existing preview script is reused unchanged for the comparison buttons. The shared dependencies are the immutable V1 anatomical scene/generator and unchanged preview presentation script; future V2 edits must go in its own generator, scene and shader, not the V1 recipe. Altering the shared anatomical recipe deliberately would affect both versions, so it remains protected.

SHA256 checks confirmed all 75 pre-existing files under scenes/scripts/resources/docs, excluding this explicitly updated document, remained byte-identical, including V1 scenes, generator, shader, preview, screenshots and both approved tree species. Mesh-array and transform fingerprints also matched between V1 and V2 anatomy; loading V2 did not mutate an existing V1 instance. Every V2 material was checked to be a separate resource from V1.

### Measured cost

| Metric | V1 | V2 |
| --- | ---: | ---: |
| Triangles | 9,911 | 10,170 |
| Mesh surfaces | 15 | 18 |
| Distinct materials | 5 | 7 |
| Mesh parts | 10 | 13 |

The additions cost 259 triangles (2.6%), three opaque surfaces and two extra materials. The existing iron material is shared by V2 weapons and added armor. Surfaces are not a total GPU draw-call count because shadows and rendering passes add work. No group-performance benchmark, LOD or frame-rate guarantee is claimed.

### Open the comparison

1. Open `project.godot` in Godot 4.7.2 .NET and build the C# solution.
2. Open **`res://scenes/art/EnemyArtComparison.tscn`**.
3. Press **F6 / Run Current Scene**. Left to right: player reference, V1, V2.
4. Select **Inspection** (orthographic size 3.6) or **Gameplay scale** (size 22). Both variants use identical lighting, scale and 45-degree isometric camera settings. These settings are local to the comparison.
5. For independent asset inspection, open either enemy scene in the 3D editor after compilation. The original `EnemyArtPreview.tscn` remains unchanged. F5 still opens the original world.

New rendered evidence, actually opened and visually inspected:

- `docs/milestone_04_enemy_comparison_v2.png`
- `docs/milestone_04_enemy_comparison_v2_gameplay.png`

The inspection render shows the asymmetric shoulder silhouette, exposed anatomy, waist strips and restrained red socket points. At size 22, sword/shield and shoulder silhouettes remain readable, but the tiny eyes and fine cloth folds are barely perceptible. No enlarged eyes, extra lights or world-camera changes were used to disguise this limitation. Manual interactive art approval remains the user's next step; no manual gameplay or animation testing is claimed.

### Validation performed for iteration 02

- `dotnet build`: passed, zero warnings/errors.
- Godot headless editor import: passed; editor opening of V2 and the comparison scene passed, including C# tool generation.
- Independent V1/V2 loads, material/shader resolution, mesh statistics, matching anatomy fingerprints, material isolation and visual-only structure: passed, zero failures in `docs/validate_enemy_v2.gd`.
- Comparison loads with all three models inside the inspection viewport. Gameplay size 22 and camera inclination checks passed.
- Actual Vulkan Forward+ rendering and capture at both scales: passed; both images inspected. No resource, shader or runtime errors were reported.
- Original `docs/validate_enemy_art.gd`: passed without capture, preserving its original screenshots.
- Original playable world headless startup for 180 frames: passed; seed 731, 10 trees, 16 rocks and 2766 navigation polygons unchanged.
- Existing `docs/validate_mouse_navigation.gd`: passed, zero failures, covering click/hold/release, WASD cancellation, obstacles, boundaries, unreachable requests and camera tracking.
- No new dependencies, external assets, project settings, world lighting or gameplay changes.

Reproduce focused checks and create only the NEW comparison captures:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_enemy_v2.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan --fixed-fps 60 --script res://docs/validate_enemy_v2.gd -- --capture
```

### Complete iteration 02 file inventory and Git state

Updated existing untracked documentation: `docs/MILESTONE_04_ENEMY_ART.md` only. All pre-existing V1 art files and evidence remain unchanged.

New authored files:

- `scenes/enemies/SkeletonWarriorV2.tscn`
- `scripts/enemies/FallenGuardianV2Generator.cs`
- `resources/art/enemies/FallenGuardianV2Surface.gdshader`
- `scenes/art/EnemyArtComparison.tscn`
- `docs/validate_enemy_v2.gd`

New evidence: the two comparison PNGs listed above. New Godot metadata:

- `scripts/enemies/FallenGuardianV2Generator.cs.uid`
- `resources/art/enemies/FallenGuardianV2Surface.gdshader.uid`
- `docs/validate_enemy_v2.gd.uid`
- `docs/milestone_04_enemy_comparison_v2.png.import`
- `docs/milestone_04_enemy_comparison_v2_gameplay.png.import`

Branch remains `feature/milestone-04-first-enemy`. Pre-existing untracked V1 work was retained; the additions are also local/untracked (PNG import sidecars follow the existing ignore rules). No tracked file changed, and no staging, commits, pushes, merges, rebases, resets, cleans or branch switches were performed. Stop here for visual approval before gameplay integration.
