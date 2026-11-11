# Milestone 03 / Art Prototype 03 - Approved cursed-forest species

## Prototype 03 - approval and preservation decision

The user has approved BOTH existing trees as independent environmental species for the future cursed forest biome:

| Display name | Approved variant | Exact reusable scene | Triangles | Surfaces / materials |
| --- | --- | --- | ---: | --- |
| Ancient Needle Pine | V1 | `res://scenes/environment/CursedPineV1.tscn` | 28,642 | 2 opaque / 2 |
| Shadow Crown Pine | V2 | `res://scenes/environment/CursedPine.tscn` | 24,642 | 2 opaque / 2 |

These are display names only. Existing scene, resource, script and node names are retained. Neither species replaces the other, and their appearances must not be merged. Original `res://scenes/environment/AshPine.tscn` remains a third, unchanged asset.

Inspection found both scenes already stable and independently instantiable. No scene consolidation, new mesh copies, asset renaming, parameter changes or art generation changes were necessary. This task changes documentation and adds a read-only validation utility only. No tree was baked, saved into a new mesh resource, or visually redesigned. Normal V2 scene loading still invokes its existing deterministic _Ready generator, which is inherent to opening the approved procedural scene.

### Species characteristics

Ancient Needle Pine has fine layered needles, pale/silvery gray-green highlights, visible branching and a prominent exposed root spread. Shadow Crown Pine has denser forest-green/olive crown masses, dark matte weathered bark, smaller soil-embedded roots and broken boughs. Both retain the gently curved pine silhouette and approximately 5.8 units above-ground height. The approved differences are intentional species characteristics, rather than a quality ranking or an obsolete/new replacement relationship.

### Exact dependencies and persistence

Ancient Needle Pine / V1:
- Scene: `scenes/environment/CursedPineV1.tscn`.
- Saved native wood mesh: `resources/art/CursedPineV1_ProceduralBark.res`.
- Saved native foliage mesh: `resources/art/CursedPineV1_ProceduralNeedles.res`.
- Independent bark shader: `resources/art/CursedBarkV1.gdshader`.
- Bark ShaderMaterial and foliage StandardMaterial3D are embedded in the scene. Needle colors/normals are stored in the native mesh.
- No generator script is needed to instantiate V1. The historical freeze utility is NOT a dependency and must not be run to preview it.

Shadow Crown Pine / V2:
- Scene: `scenes/environment/CursedPine.tscn`.
- Generator: `scripts/environment/CursedPineGenerator.cs` (Godot 4.7.2 .NET / net8.0).
- Independent bark shader: `resources/art/CursedBark.gdshader`.
- Independent foliage shader: `resources/art/CursedFoliage.gdshader`.
- Both ShaderMaterials are embedded in its own scene; Seed=1703 and Height=5.8 are the approved default recipe.
- Its two ArrayMeshes are generated on normal scene entry by the existing tool script. They are deliberately not serialized: generated children have no Owner. Closing the editor discards transient meshes; reopening after a C# build reconstructs the same geometry from the saved recipe and shader files. This is supported persistence, not a missing asset reference.

No mutable mesh or material resource is shared between V1 and V2. Editing V2's generator or shaders does not modify V1's frozen meshes/shader, and changing V1's own resources does not change V2. Resources can still be shared by multiple instances of the SAME species, as is normal in Godot; use Make Unique or a deliberate material variant before per-instance resource edits. Preserve approved source parameters and resources unless a later task explicitly authorizes an art variation.

Shared dependencies are the Godot engine/rendering facilities and this project setup. `scenes/art/TreeArtComparison.tscn` presents both species under shared ground/lights/camera and uses `scripts/art/TreeArtComparison.cs`; these are presentation dependencies only. The comparison's existing hero reference uses Player.tscn for scale, but neither individual tree scene references a player, the world generator, navigation, the comparison scene or camera/input logic. Keep existing `.uid` sidecars and all native mesh/shader files with their scenes.

### Collision and future integration status

Both species are visual-only Node3D assets. Inspection and runtime checks confirm there is no StaticBody3D/CollisionObject3D, CollisionShape3D, NavigationRegion3D or NavigationObstacle3D in either asset. They are reusable visuals, not yet solid gameplay obstacles. Dropping them into a future test scene will not automatically block player movement or carve their trunks out of navigation.

A later integration task will need a trunk-appropriate static collision structure, compatible collision layers/masks, and inclusion of those colliders in the existing navigation bake. Fit the trunk/root clearance to each species; do not make the entire crown solid or assume the old Ash Pine collider can be copied unchanged. This task adds no collider, navigation data, world placement, generator reference or biome-spawning system.

### Manual preview and future instantiation

1. Open project.godot with Godot 4.7.2 Stable .NET and build the C# solution.
2. Open `res://scenes/art/TreeArtComparison.tscn` and press F6. The existing labels still say V1/V2: center V1 is Ancient Needle Pine; right V2 is Shadow Crown Pine. Original Ash Pine remains left. Labels/layout/screenshots are intentionally unchanged in this preservation task.
3. To inspect independently in the editor, open either exact species scene from the table, use the 3D viewport and frame its root. V1 displays saved meshes; V2 previews through its existing tool script after compilation. The individual scenes have no camera/light rig, so use the comparison for a lit game-window preview.
4. For manual instantiation in a FUTURE scratch/world scene, create a Node3D root and drag the desired `.tscn` from FileSystem into its scene tree/3D viewport. Drag the individual species scene rather than the full comparison. Each can be instantiated without the other. Leave the current playable World.tscn and environment generator unchanged.
5. Future manual instances should retain the approved default seed/height/materials unless a separate variation is intended. Keep the saved mesh/shader dependencies available at their current paths.

Both species are intended for the cursed forest: Ancient Needle Pine can provide finer, spectral-looking stands and Shadow Crown Pine denser, darker canopy masses. Future snowy forests, graveyards and dungeons may have their own distinct assets or separately approved variants; this task does not recolor these trees for those regions or implement any biome logic.

### Prototype 03 validation

- Verified the requested feature branch and inspected/preserved all existing uncommitted art work before edits.
- `dotnet build`: passed, zero warnings and zero errors.
- Both species opened in fresh headless Godot editor processes, followed by fresh independent-instantiation validation. Scene, external mesh, embedded material and shader dependencies resolve. No references required repair.
- The new utility loads each species independently, confirms the exact approved triangle counts, two single-surface mesh batches, two distinct materials, lack of cross-species material sharing, and the documented lack of collision/navigation nodes.
- Geometry arrays (including vertex colors/normals/indices) and shader/native-material settings have identical SHA256 fingerprints before and after the fresh editor launches, on the installed engine:
  - Ancient Needle Pine: `b341adf310ae96dcad29f8c068808387bc18c4d40e453c731e2e6bebe68762b9`.
  - Shadow Crown Pine: `6f687fecccc2d180271ca945c12140c95c2458ae6d50182dd4ef7c92331bda66`.
- Existing comparison validation passed without `--capture`: exact counts/materials/scale and all three trees in frame.
- A normal Vulkan Forward+ comparison-scene startup passed, exit 0, with no shader/resource/runtime errors; no screenshot was saved or overwritten. This is a render-startup check, not a new interactive art review.
- The unchanged main playable world loaded for 180 frames, exit 0: seed 731, 10 original trees, 16 rocks, terrain 40 and the existing 2766-polygon navigation bake.
- Both approved art scenes, all existing source/resource files and both comparison screenshots were verified unchanged by file hashes. Screenshot capture flags and the historical freeze utility were not run.
- No interactive editor-session review, mass-forest benchmark, LOD assessment or new collision test was performed. There are no new colliders to validate; runtime inspection confirms their absence.

Reproduce the read-only species validation:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_tree_species.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_tree_art.gd
```

Do not append `--capture` when preserving the approved screenshots. The new species validation utility has no save/capture operation.

### Files changed by Prototype 03

- Modified: `docs/MILESTONE_03_ART_PROTOTYPE.md` (approval, species names, dependencies, persistence, limitations and validation).
- Created: `docs/validate_tree_species.gd` and Godot-generated `docs/validate_tree_species.gd.uid`.
- All tree scenes, generators, meshes, shaders, comparison presentation and screenshots remain unchanged. No large mesh duplication was needed. No gameplay/world/camera/input changes, branch switches, commits, pushes, merges, resets or cleans were performed.

Remaining limitations: visual-only assets lack collision/navigation integration, wind, LOD/impostors and forest-scale profiling. V2 depends on its saved generator, seed and materials to reconstruct its approved geometry; future source or engine-version changes should rerun the validation before declaring equivalence. Both species are approved, independently reusable and ready for future VISUAL biome placement, with gameplay obstacle integration explicitly deferred.

## Prototype 02 art history

The following sections describe how the two existing looks were created; the approval/preservation decision above supersedes any earlier pending-review wording.

Art direction: **DARK FANTASY**, selected by the user. Isolated visual work on `feature/milestone-03-art-prototype`; the playable world is not changed. Prototype 01 is retained as a frozen baseline and as its original screenshot.

## Result

The comparison now shows, left to right:

1. Original Ash Pine, unchanged.
2. Cursed Pine V1, preserved geometry and materials.
3. Cursed Pine V2, the current procedural dark fantasy tree.

V2 uses deep forest green, muted olive and dark gray-green foliage, dark weathered brown/charcoal bark, five less prominent exposed roots, and two additional broken boughs. Its trunk retains the original gently twisted path and approximately 5.8-unit height. It remains an ancient pine rather than a monstrous silhouette.

The new capture is `docs/milestone_03_tree_comparison_v2.png`. It was rendered using Vulkan Forward+ and opened for visual inspection. All three trees are visible at unit scale, equal camera depth, under identical lights and ground material. The 45-degree orthographic camera, exposure/tonemapping, ambient settings, key light and fill light are unchanged from Prototype 01. The entire scene was not darkened to make V2 appear darker.

The original `docs/milestone_03_tree_comparison.png` was not overwritten. Its SHA256 before and after this iteration is:

`432AD5594E4B18DE59DB4B08029EBD59B4D4B5085B3CCF65717CD8FDD49CA970`

## Design decisions

### Bark and trunk

The existing procedural bark shader now uses a lower-value brown/charcoal palette, sparse gray weathering, longer coherent grain and more restrained normal perturbation. High roughness and low specular response prevent plastic or pale glossy bark. A height-dependent soil-contact tint blends exposed roots into the base of the trunk. The twisted centerline is preserved rather than exaggerating it into tentacles.

The procedural shader remains local to the tree. It does not alter the world lighting, terrain material or atmosphere. Noise is generated in Godot shaders with no texture downloads.

### Foliage and silhouette

Ancient Needle Pine (V1) retains bright contrasting needle tips and fine stems for a silvery, intricate needle texture; this appearance is approved as a distinct species. V2 uses a coherent color per spray instead of large color differences per needle. Forest greens are mixed with restrained olive variations; tips remain slightly lighter rather than white or silver. Fine stem tubes are omitted more often.

Each spray has four needle pairs instead of five. A small shallow six-sided opaque cushion connects their bases into a cohesive mass, leaving pointed tips around the edge. Smooth tuft normals and a subtle procedural foliage shader break up plain surfaces while reducing isolated needle sparkle. This is opaque geometry, not transparent leaf cards, particle foliage or a smooth cone.

Two selected boughs are bare and broken, in addition to the existing broken lower branches. Irregular tier heights, branch yaw, lengths and droop remain seeded. The silhouette stays asymmetrical, with open lower areas exposing wood and roots. There is no full LOD or wind system.

### Roots

Ancient Needle Pine retains eight prominent radial roots. Shadow Crown Pine uses five roots distributed at irregular angles, with varying lengths and thickness. They are lower, thinner and taper gradually beneath the soil. Root tips end below the ground plane; only a few exposed arcs remain visible. They share the darker bark shader and soil-contact tint instead of using a separate pale material.

### V1 preservation

Before modifying the generator, its two meshes were saved as native Godot ArrayMesh `.res` resources. `CursedPineV1.tscn` references these resources and a frozen copy of the old bark shader. Its original vertex colors, foliage material, geometry and 28,642-triangle count are preserved without maintaining a duplicate C# generator.

`docs/freeze_cursed_pine_v1.gd` is the historical one-time snapshot utility. Normal opening/testing does not run it. It refuses to overwrite an existing snapshot/mesh and refuses to freeze the current non-V1 generator as V1. The frozen scene is ready to load directly.

## Measured cost and scale

| Asset | Triangles | Surfaces | Materials | Mesh bounds X/Y/Z |
| --- | ---: | ---: | ---: | --- |
| Cursed Pine V1 | 28,642 | 2 opaque | 2 | 4.11 / 5.91 / 3.87 |
| Cursed Pine V2 | 24,642 | 2 opaque | 2 | 4.35 / 5.91 / 3.79 |

V2 has 4,000 fewer triangles, approximately 14% below V1. The bounds include embedded roots; height above ground remains approximately 5.80 units. The original Ash Pine is also 5.8 units tall. The existing 1.9-unit hero is included as an inert scale reference; it is not a new player implementation.

Both procedural materials remain opaque and are batched into one mesh per material. Two surfaces are not a claim of two total GPU draw calls: shadows and other renderer passes add work. The foliage shader adds restrained two-dimensional procedural noise; no expensive transparency effects are used. Real forest-scale performance has not been profiled.

## Architecture and parameters

`scenes/environment/CursedPine.tscn` remains the current reusable visual asset. `CursedPineGenerator.cs` generates its indexed ArrayMesh wood and foliage batches once on entry, with default Seed=1703 and Height=5.8. The tool script also previews the geometry when opened in the editor after compiling C#.

Primary controls:
- Seed and Height: exported on CursedPine; parameters apply when reopening/restarting the scene.
- Root count, height, taper and angle distribution: the root construction block in the generator.
- Layer count, bough placement and dead-branch selection: the tier loop and Bough method.
- Spray color palette, needle pairs, width and tuft footprint: Spray.
- Aged wood and contact tint: `CursedBark.gdshader`.
- Matte foliage variation: `CursedFoliage.gdshader`.

Generated children have no Owner and do not serialize back into the procedural scene. V1 instead uses its frozen mesh resources. The comparison's existing local 4x MSAA script remains unchanged and restores the prior viewport setting when exiting. The hero reference has processing disabled and collision layers zero only on its comparison instance.

## Modified files for Prototype 02

- `scenes/environment/CursedPine.tscn`: V2 foliage material now references its procedural shader.
- `scripts/environment/CursedPineGenerator.cs`: roots, broken branches, grouped foliage, colors and normals.
- `resources/art/CursedBark.gdshader`: dark aged bark and soil tint.
- `scenes/art/TreeArtComparison.tscn`: three-tree composition, V1 reference and labels. Shared lighting/camera/ground unchanged.
- `docs/validate_tree_art.gd`: checks both versions, budgets, materials and camera visibility; capture writes only the new V2 screenshot.
- `docs/MILESTONE_03_ART_PROTOTYPE.md`: this documentation.

## Created files for Prototype 02

- `scenes/environment/CursedPineV1.tscn`
- `resources/art/CursedPineV1_ProceduralBark.res`
- `resources/art/CursedPineV1_ProceduralNeedles.res`
- `resources/art/CursedBarkV1.gdshader`
- `resources/art/CursedFoliage.gdshader`
- `docs/freeze_cursed_pine_v1.gd`
- `docs/milestone_03_tree_comparison_v2.png`
- Generated sidecars: `resources/art/CursedBarkV1.gdshader.uid`, `resources/art/CursedFoliage.gdshader.uid`, `docs/freeze_cursed_pine_v1.gd.uid`, `docs/milestone_03_tree_comparison_v2.png.import`.

All prior uncommitted art files were inspected before edits. No changes were made to World.tscn, AshPine.tscn, EnvironmentGenerator.cs, player/navigation code, input handling, existing camera code, project settings, world lighting or atmosphere. No trees were replaced in gameplay. No external assets, plugins or dependencies were added. No branches were switched, commits created, pushes, merges, or destructive Git operations performed.

## Open the comparison in Godot

1. Open `C:\Propio\Proyectos\Ashenfall\project.godot` in **Godot 4.7.2 Stable .NET**.
2. Build the C# solution if prompted, or run `dotnet build` from the project directory.
3. Double-click **`res://scenes/art/TreeArtComparison.tscn`** in FileSystem.
4. Press **F6 / Run Current Scene**. The three labeled trees appear left to right: Ash Pine, Cursed Pine V1, Cursed Pine V2.
5. Compare foliage masses, bark and root silhouettes under the same lighting. The scene is static and has no movement controls.
6. **F5 still runs the untouched playable World scene.**

Direct launch from the project directory:

```powershell
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan res://scenes/art/TreeArtComparison.tscn
```

## Validation results

- Read project rules and inspected the existing scene, C# generator, shaders, presentation camera/lights and original screenshot before changes.
- `dotnet build`: passed, zero warnings and zero errors, with the existing Godot.NET.Sdk 4.7.2 / net8.0 configuration.
- Headless Godot editor import: passed, exit 0.
- Headless editor opening of the comparison scene: passed, including procedural tool-script generation.
- Comparison loaded and rendered in windowed Vulkan Forward+: passed, exit 0, no rendering errors or shader warnings. The final V2 screenshot was opened and visually inspected; all three trees, labels and reference hero are present.
- Automated comparison check: V1 remains exactly 28,642 triangles; V2 remains below V1 at 24,642. Both have two single-surface meshes and two distinct materials. All 77,564 stored V1 vertices and 60,506 stored V2 vertices project inside the project's 1280 x 800 comparison viewport. The utility explicitly sets the project resolution because headless windows can default to a square viewport. Heights remain within the original scale envelope.
- Original World main-scene startup for 180 frames: passed, exit 0. Existing environment counts and the 2766-polygon navigation bake remain unchanged, with no Cursed Pine generation in the world.
- Existing mouse/navigation integration suite: zero failed assertions, covering routes around trees/rocks/ruins, physical containment and camera behavior. The already-documented intermittent Jolt worker-capacity warning appeared in the accelerated headless test; it did not fail checks and is unrelated to the isolated art assets.
- Original screenshot SHA256 was verified unchanged. Git diff shows no tracked gameplay/world changes; the art work remains local and uncommitted.

Reproduce validation:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_tree_art.gd
```

Regenerate only the new screenshot with a real renderer:

```powershell
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan --fixed-fps 60 --script res://docs/validate_tree_art.gd -- --capture
```

The capture destination is exclusively `docs/milestone_03_tree_comparison_v2.png`; the original baseline is preserved.

## Limitations and review

This remains a visual proof of concept with no gameplay collision/navigation on the new trees, no wind, no LOD, no impostor and no forest-scale benchmark. Foliage tufts are still stylized geometric approximations of dense needle masses, and fine edges can shimmer in motion. The static render was inspected during Prototype 02. Both species now have user art approval; continuous-motion and performance reviews remain future work.

Both shaders use original procedural noise. Bark grain is biased toward the tree's local vertical axis, so horizontal branches do not receive perfect anatomical mapping. Embedded roots are tuned to a flat ground plane. Seed variations and other lighting setups still need art review. Frozen V1 meshes intentionally add approximately 2 MB of native baseline data to avoid duplicating the old generator.

The DARK FANTASY direction and both species are approved. Gameplay integration is a separate future task; no commit has been made.
