# Milestone 02 - First playable hero and mouse navigation

Updated 2026-11-11 on `feature/milestone-02-player`. Baseline is commit `4a82e77` plus the existing uncommitted Milestone 02 implementation. Existing work was preserved. No commits, pushes, branch changes, merges or rebases were performed.

## Current behavior

Left-click navigable ground to order movement. Outside the terrain but inside the game viewport, mouse rays fall back to a horizontal plane and the requested point projects to the nearest safe navigable destination. Hold the left button to update the destination as the pointer or following camera moves, including past terrain edges. Releasing preserves the last valid destination; another valid click replaces it. Invalid clicks preserve the previous valid order. The humanoid follows a NavigationAgent3D path, turns toward travel and slows near arrival. Gravity, MoveAndSlide, physical obstacle collision, and the existing smooth fixed isometric camera remain active.

WASD remains a camera-relative debug control. Pressing a movement key cancels the mouse destination and held-mouse updates until a new mouse press. Both input modes are contained within the terrain.

User feedback before this update confirmed smooth WASD movement and camera tracking and working rock collision. This update preserves that camera implementation.

## Files changed by the mouse/collision update

Modified:
- `scripts/characters/PlayerController.cs`: mouse event handling, physics ray picking, destination validation, agent steering, arrival braking, blocked-path timeout and keyboard override.
- `scenes/characters/Player.tscn`: NavigationAgent3D child and path tolerances.
- `scripts/environment/EnvironmentGenerator.cs`: identifies navigable ground and creates four invisible physical perimeter barriers matching TerrainSize.
- `scenes/environment/AshPine.tscn`: trunk radius matches the visible trunk base; explicit collision layer/mask.
- `scenes/world/World.tscn`: navigation region, geometry reference, navigation resource and updated input hint.
- `project.godot`: left-click InputMap action and navigation map voxel resolution.
- `docs/validate_milestone_02.gd`: extends the existing regression suite to check every generated trunk.
- `docs/MILESTONE_02.md`: current behavior, architecture, findings, results and manual checklist.

Created:
- `scripts/world/WorldNavigation.cs`: one startup navigation bake from generated physics geometry.
- `resources/WorldNavigationMesh.tres`: configurable baking parameters.
- `docs/validate_mouse_navigation.gd`: focused input, navigation, collision-boundary and camera integration checks.
- Godot-generated `.uid` sidecars for the new scripts.

Existing rock and gate scene edits, IsometricCamera.cs, hero presentation, original documentation and preview files were preserved. The generator's random calls, seed, object counts, placements, materials and lights remain intact.

## Player architecture and input

Player.tscn is a reusable CharacterBody3D, with a 1.9-unit capsule of radius 0.32, Visual subtree and NavigationAgent3D. The primitive humanoid has armored torso/arms, head, legs, red cape and a gold marker facing negative Z. Visual rotates independently of the capsule.

World injects MovementCamera as an exported reference. A standalone instance can fall back to its viewport camera. No global player singleton or manager is used. Per-instance InputEnabled and action-name properties remain available for future actors; multiplayer is not implemented.

| Action | Input | Purpose |
| --- | --- | --- |
| move_to_cursor | Left mouse button | Primary destination control |
| move_up / move_down | Physical W / S | Debug screen-relative forward/back |
| move_left / move_right | Physical A / D | Debug screen-relative left/right |

Unhandled mouse presses queue a pick; release is observed even over GUI and does not clear the goal. Physics updates perform raycasts from the camera using ProjectRayOrigin/ProjectRayNormal. A valid upward hit in `navigable_ground` uses the original terrain-picking path. If there is no valid terrain hit, a reusable acquisition helper intersects the ray with a configured horizontal plane and requests safe navigation projection. Perimeter walls remain outside the picking layer. Out-of-viewport positions and invalid plane intersections are ignored. Continuous picks are throttled to HoldUpdateInterval (0.12 seconds), and changes smaller than 0.25 units do not restart the path.

TrySetDestination retains bounded snapping for direct ground orders. TrySetProjectedDestination permits distant horizontal projection for plane requests. Both reject non-finite points, unsynchronized navigation, incompatible surface heights, and paths whose final point does not reach the safe destination. A small vertical allowance handles baked voxel height. XZ projection is bounded by two cell widths per axis (2 * sqrt(2) * map cell size radially), allowing near-edge ground clicks lost to rasterization to select a safe reachable point. Movement always follows the mesh; this never extends movement beyond its edge. Rejected orders retain the current valid goal. If physical progress stalls for 1.5 seconds, the order is canceled.

Exported movement settings: MovementSpeed=5, RotationSpeed=12, ArrivalDistance=0.2, SlowdownDistance=1, HoldUpdateInterval=0.12. The final path segment reduces speed toward the arrival radius; individual physics steps cannot overshoot the next waypoint. NavigationAgent PathDesiredDistance=0.3 accommodates the baked floor's voxel height above the physical floor. Keyboard input is normalized, camera-relative and immediate as before. MoveAndSlide applies physics time; gravity and exponential facing smoothing use delta.

## Tree collision findings and fix

Before editing, generated trees already inherited a StaticBody3D with a 3-unit cylinder, default layer/mask 1, and uniform parent scale. The player's mask 3 included their layer. There was no missing layer assignment. The cylinder radius was 0.32, while the visible trunk base radius was 0.36. Crowns had no collision.

The trunk cylinder now uses radius 0.36 and explicit environment layer 1 / player mask 2. It remains centered at y=1.5 and follows each generated instance's uniform scale and yaw. Only the trunk blocks movement; the much wider canopy remains traversable underneath.

Expanded testing confirmed capsule contact with all ten generated trunk colliders. Long straight approaches to some trees were redirected by other obstacles; Tree_03 is overlapped by Rock_05, which intercepts capsule sweeps first. The per-trunk diagnostic temporarily ignores other physics bodies during the sweep, restoring all exceptions immediately without advancing physics. The normal controller test still walks into a tree, rock and ruin in the unchanged actual world with all collisions enabled.

A missing-collider pass-through was not conclusively reproduced. The verified correction covers the visible trunk base and makes collision intent explicit; passing beneath foliage or sliding around a cylindrical trunk is expected. In manual retesting, distinguish the trunk from the canopy.

Player layer remains 2; its mask is now 11 (layers 1, 2 and 4: environment, actors and perimeter). Existing rock boxes and gate pillar/base/lintel collision shapes retain their geometry. Road slabs remain cosmetic over the ground.

## Navigation and terrain boundaries

WorldNavigation is a NavigationRegion3D with exported GeometryRoot and AgentCollider references, plus a native NavigationMesh resource. World composition injects the generated environment and the player capsule collider; the navigation component does not depend on the concrete generator or controller classes. After procedural generation finishes, one deferred startup operation parses StaticBody3D collision shapes on layer 1, adds projected collider footprints, then bakes and assigns a per-instance mesh. It does not parse visual foliage or road meshes and does not rebake while moving.

Projected footprints come from scaled/rotated cylinders and boxes, including their actual transforms. They exclude solid footprints and prevent obstacle tops becoming destinations. High lintels with enough headroom do not project a blocker through the doorway. The baked edge radius derives from the capsule radius plus CharacterBody3D.SafeMargin, rounded up to the next cell. With radius 0.32 and safe margin 0.001, the 0.321 requirement rounds to 0.35. The resource's 0.45 radius remains the desired obstacle clearance: projected obstacle footprints are expanded by the difference, preserving conservative paths around props. Cell size 0.05 and cell height 0.1 match the project's navigation map settings; agent height is 1.9 and maximum climb 0.1. Ledge-span filtering is disabled for the flat prototype; positive radius erosion and the independent physical perimeter still contain movement. The current default world produces 2766 polygons on the installed engine.

NavigationAgent3D supplies path waypoints around static obstacles. Physical collision remains independent. RVO avoidance is intentionally disabled: static navmesh pathfinding handles this prototype, with no dynamic crowd system.

The baked walkable area is eroded inside the terrain border. Separate four-unit-high physical perimeter barriers have their inner surfaces on the terrain edges, preventing even WASD from walking off the ground. Barriers derive from TerrainSize and remain physically solid on layer 4 (bit 8). Ground picking and navigation source parsing use layer 1, so these invisible walls neither occlude rays nor add a second navigation obstacle. No controller dependency on the generator is needed.

API references: [Godot navigation mesh baking](https://docs.godotengine.org/en/stable/tutorials/navigation/navigation_using_navigationmeshes.html), [projected source obstructions](https://docs.godotengine.org/en/stable/classes/class_navigationmeshsourcegeometrydata3d.html), [NavigationAgent3D](https://docs.godotengine.org/en/stable/classes/class_navigationagent3d.html).

## Camera

The existing CameraRig/IsometricCamera.cs is unchanged by this update: orthographic projection, 45-degree inclination and azimuth, configurable distance/zoom, World zoom 22, FollowOffset (0, 0.9, 0) and FollowSpeed 8. Physics interpolation and physics priority 10 keep camera updates after actor motion. The camera follows position without orbiting or rotating when the hero turns. Inspector settings apply on startup.

## Validation results

- Read project rules and inspected the current scenes/scripts and Git status. Stayed on the requested branch and preserved the existing uncommitted files.
- Godot 4.7.2 Stable .NET, Godot.NET.Sdk 4.7.2, net8.0 and installed .NET SDK 8.0.428 remain compatible.
- `dotnet build`: passed with zero warnings and zero errors.
- Bounded headless editor import and main-scene startup: passed. Runtime logs show the original seed 731, 10 trees, 16 rocks, terrain 40 and a single navigation bake.
- Existing WASD/camera regression suite passes, now including player-capsule sweeps against all ten generated trees. Physical rock and ruin tests still pass.
- New mouse/navigation suite passes at 30, 60 and 120 physics Hz: viewport mouse press/motion/release delivery, ground ray picking, click-to-move, held destination updates, release persistence, replacement clicks, arrival, no idle drift, keyboard cancellation, invalid/no-hit picks, excluded trunk footprint, full agent routes around tree/rock/ruin, all four terrain boundaries, unchanged camera angle, bounded follow lag and camera settling.
- Unreachable test adds a temporary disconnected navigation island and confirms the point really exists on navigation before verifying rejection and preservation of the last valid goal. The fixture uses synchronous map updates to avoid asynchronous test synchronization races; this is test-only and does not alter the production map or saved world.
- Map iteration remains unchanged during the movement tests, confirming no repeated rebaking.
- The same mouse/navigation suite passes with actual windowed Vulkan Forward+ rendering at 60 Hz, exit 0, without runtime errors. Events are injected into the viewport; this verifies the game's input/event path, raycasts, locomotion and tracking numerically. It is not a claim of an interactive visual review of continuous movement.
- Early testing exposed a waypoint tolerance smaller than the baked floor height, preventing path advancement; the final agent tolerance fixes this. Initial bake/map voxel mismatch warnings were resolved by matching their cell dimensions.
- Some unthrottled headless runs, including the final combined verification, emitted an intermittent Jolt worker-job capacity warning. All assertions still passed and processes exited 0; the windowed Vulkan navigation run did not emit it. No engine/global settings were changed.
- `git diff --check`: passed. No Git mutations were performed.

## Run and reproduce tests

From `C:\Propio\Proyectos\Ashenfall` in PowerShell:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --editor --import --quit-after 30
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --quit-after 120
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_milestone_02.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_mouse_navigation.gd
```

For 30/120 Hz, change --fixed-fps and append `-- --physics-rate=30` or `-- --physics-rate=120`. To run the navigation suite with rendering, replace --headless with `--rendering-driver vulkan`. Tests return exit 1 on a failed assertion. GDScript files are test utilities only; production logic uses C#.

## Manual checklist

1. Open project.godot with Godot 4.7.2 .NET, build if prompted, then press F5 and focus the game window. The environment and player should appear without editor construction.
2. Left-click a clear patch of ground; release immediately. Confirm the player reaches it and slows to a stable stop. Click somewhere else while moving to replace the order.
3. Hold the left mouse button and move the cursor around the player; confirm the destination follows the pointer. Release and verify the last valid destination remains active.
4. Click beyond a tree trunk, rock and gate pillar/base. Verify the player walks around each obstacle. With WASD, walk directly into the trunk itself; it should block, while walking under the outer crown remains allowed. Enable Debug > Visible Collision Shapes if needed to identify the trunk.
5. Click directly onto a solid collider, near blocked footprints, outside the terrain, and into empty background. No invalid destination should replace the previous valid order. Ground beyond an obstacle remains reachable via a path.
6. Press WASD during an active mouse order, including while holding the mouse button. Keyboard movement should take over, and the old goal should not resume until another click.
7. Walk toward each terrain edge using both input modes. The player should stay grounded inside the terrain. Clicks in the small rasterization fringe project to the closest safe navigation point. Clicks beyond the terrain now select the nearest safe navigation point through plane fallback; invalid intersections or unreachable projections retain the previous valid goal.
8. Change directions repeatedly and inspect camera smoothness, fixed angle and arrival behavior at different frame rates. The earlier WASD/camera manual feedback was positive; this mouse-driven update still requires subjective visual retesting.
9. Adjust ArrivalDistance, SlowdownDistance, speed or camera properties in the Inspector, stop/restart and compare.

Direct launch with the renderer known to work on this machine:

```powershell
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan
```

## Known limitations

- One synchronous bake at startup is suitable for this 40 x 40 prototype; much larger terrain or dynamic obstacles would need a revised baking/update strategy.
- Static pathfinding only; no crowd avoidance, jumping, moving platforms or runtime rebuilding. A physically blocked actor cancels its goal after a short stall timeout.
- The ground/navmesh is flat. Foliage has no collision; tree roots, capsule and rock boxes are approximate. Procedural props can overlap, including Tree_03/Rock_05.
- Road slabs are cosmetic, and feet may slightly intersect their thin surface. No destination marker, walking animation, camera occlusion fade, input-rebinding UI or runtime zoom controls are added.
- Camera tracking is not clamped at the terrain edge; it may show background outside the map while the player remains contained.
- Inspector bake settings and agent dimensions must remain compatible. In particular, waypoint tolerance must accommodate voxel floor height if navigation resolution is changed.
- The current GTX 650 needs Vulkan for Forward+; the existing Direct3D 12 project setting remains intact and can fall back automatically.
- No combat, networking, enemies or inventory is implemented. No user intervention is required for build/startup; the manual checklist covers remaining visual feel checks.
## Gate correction history - consistent ancient gate openings

The original issue was reproduced before changing production files. The western gate's path crossed the gate plane at local x=2.55 (outside the structure), whereas the eastern gate's path went through x=0. The nearest navigation point to the western opening center was 0.9 units away along Z; the eastern center was on the mesh. Direct capsule sweeps through BOTH centers reported no physical collision. The new regression failed four western-opening assertions before the fix, while eastern crossings and all pillar-collision assertions passed.

Both instances have identity rotation and unit scale, at (-5.2, 0, -7) and (5.2, 0, -7). The original shared scene placed bases 2.6 units apart, each 1.5 units wide, leaving only 1.10 units between them. The navigation radius consumes 0.90 units across that gap before voxel rasterization, ledge filtering and contour simplification. With 0.15-unit cells this left insufficient margin: different translations aligned the openings differently with the baking grid, closing the western corridor while leaving a very narrow eastern corridor. The lintel has ample overhead clearance and is already excluded from projected floor blockers. Nearby western trees do not occupy the opening; no physical center obstruction was found. This is a navigation-clearance/rasterization issue, not different instance settings or missing pillar collision.

The shared `scenes/environment/AncientGate.tscn` now places both pillars and bases at local x=-1.6/+1.6 instead of -1.3/+1.3. The gap between bases is 1.70 units. Pillar/base mesh and collision dimensions remain intact. The lintel mesh AND its BoxShape3D are extended from 3.9 to 4.5 units so the wider arch remains a coherent solid structure. No collision is disabled, no navigation clearance is reduced, and no per-instance exceptions or links are added. World.tscn, WorldNavigation.cs, WorldNavigationMesh.tres, controller, camera and terrain are unchanged by this correction.

Files changed for this correction:
- `scenes/environment/AncientGate.tscn`: shared geometry and matching lintel collision.
- `docs/validate_gate_navigation.gd`: new focused regression (plus generated `.uid`).
- `docs/MILESTONE_02.md`: root cause and validation evidence; current polygon count updated.

Validation:
- `dotnet build`: zero warnings, zero errors.
- Headless editor import and main-scene startup: exit 0, no runtime errors or warnings in this correction's validation runs. Default bake now contains 869 polygons.
- New gate regression passed at yaw 0, 22.5, 45, 90, 135, 180 and 270 degrees in separate fresh world runs. Tests rotate the generated instances before the single deferred startup bake; saved instance orientations are unchanged.
- BOTH gates passed in BOTH directions at each tested orientation: 28 actual NavigationAgent3D traversals. Paths and actual movement must cross the central opening, not just reach the other side by going around the arch. All arrivals succeeded, with no physical side contacts during crossing.
- Matching capsule sweeps confirm central clearance. All four pillars block the player capsule and remain invalid navigation destinations at every tested orientation (28 pillar sweeps and 28 destination-rejection checks).
- Existing `validate_milestone_02.gd` and `validate_mouse_navigation.gd` pass unchanged after the correction: WASD, input, camera, trees, rocks, ruins, boundary containment, reachable and unreachable destinations.
- Tests are automated numerical/physics checks. No new interactive or visual testing is claimed.
- Stayed on `feature/milestone-02-player`; preserved prior changes and performed no commit, push, merge, rebase or branch switch.

Run the focused test from the project directory:

```powershell
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_gate_navigation.gd
```

Append `-- --yaw=45` (or another angle) to validate a different orientation. For manual confirmation, press F5, click clear ground directly beyond the center of each arch from both sides, and verify passage through the opening. Use WASD to push into each pillar/base and confirm it stays solid. The shared gate is visibly wider; very small instance scales or altered agent/bake dimensions would require clearance validation again.
## Terrain-edge correction - root cause and current safe margin

Manual feedback confirmed that both arches now work, but the player met premature invisible restrictions near the perimeter. Inspection found TWO distinct causes before editing:

1. The old resource baked with radius 0.45 and 0.15-unit cells, with ledge-span filtering and the physical walls also included as static collision/navigation footprints. Measured center limits were +19.55 and -19.45 on X/Z for a terrain extending from -20 to +20: margins of 0.45 and 0.55. The capsule itself has radius 0.32 and CharacterBody3D.SafeMargin=0.001. The native NavigationAgent radius was not the cause: it controls RVO avoidance, which is disabled, rather than the baked path clearance. Terrain/region transforms and visible ground/collision extents aligned correctly.
2. The four-unit-high invisible walls were on the same layer as ground picking. At ground clicks near +X/+Z, the ray hit the wall's outer surface instead of the floor. For example, a ground target at x=19.6 hit TerrainBoundary at x=20.5, y=1.273. The controller correctly ignored the non-ground collider, making the apparent restriction much larger on camera-facing sides. Strict 0.02-unit destination validation additionally rejected ground clicks within the voxel fringe on all sides.

Current correction:
- Physical barriers remain in their original positions, with their inner faces at the terrain edges. Only their collision layer changes to layer 4, bit 8. The player mask changes from 3 to 11 to retain wall, actor and environment collisions. Mouse rays and navigation parsing remain on layer 1. No containment wall is removed or moved outward.
- WorldNavigation reads the actual capsule and actor SafeMargin through an exported collider reference. It derives and explicitly quantizes the required edge radius; no fixed map-size-dependent border offset is added.
- Cell size is 0.05 in both the NavigationMesh resource and default map. For this flat terrain, disabling the extra ledge-span filter avoids redundant border loss. Physical containment and positive radius erosion remain active.
- Solid obstacle footprints receive the extra clearance needed to retain the existing 0.45-unit desired obstacle radius, independently of the tighter floor-edge margin. Pillar, tree and rock physics geometry is unchanged, as is the corrected shared arch scene.
- Short off-mesh clicks project to a reachable navigation point within a tolerance derived from map voxel size (two cells per axis including diagonal rounding). For direct-ground orders, larger off-mesh requests are ignored. Mouse requests outside terrain now use unrestricted plane projection as described in the latest section below. No direct movement outside navigation is introduced.

Measured current limits on the 40-unit terrain:

| Quantity | Value |
| --- | --- |
| Physical capsule radius | 0.320 |
| Physical recovery margin | 0.001 |
| Minimum safe center inset | 0.321 |
| Baked safe inset, all four edges | 0.350 |
| Navigable X/Z center bounds | approximately -19.65 to +19.65 |
| Extra inset due to voxel rounding | 0.029 |
| Physical keyboard limit | approximately +/-19.679, with physics contact tolerance |
| Corner rule | same minimum inset on both axes |

The unchanged ArrivalDistance=0.2 remains an arrival tolerance around the destination, not a perimeter barrier: ordinary mouse movement may stop up to that distance before a selected point. The margin above describes the navigation geometry itself; WASD can approach the physical contact limit. Reduce the configurable arrival tolerance if finer stopping precision is desired. The collider footprint must remain inside the terrain even when the center looks short of the visible edge.

Files modified for the terrain-edge correction:
- `scripts/world/WorldNavigation.cs`: capsule-derived edge margin and independent obstacle clearance.
- `resources/WorldNavigationMesh.tres`: finer XZ voxel resolution and flat-floor filtering.
- `scripts/environment/EnvironmentGenerator.cs`: physics-only wall layer.
- `scenes/characters/Player.tscn`: physical perimeter bit in the collision mask.
- `scenes/world/World.tscn`: injects AgentCollider into the navigation component.
- `project.godot`: matching navigation map cell size.
- `scripts/characters/PlayerController.cs`: voxel-derived bounded destination projection.
- `docs/validate_gate_navigation.gd`, `docs/validate_milestone_02.gd`, `docs/validate_mouse_navigation.gd`: wait for asynchronous initial map publication before asserting steady-state results. The disconnected test fixture also inherits the map's cell resolution.
- `docs/MILESTONE_02.md`: current architecture, cause, measurements and validation.
- New `docs/inspect_terrain_edges.gd` and generated `.uid`: measured edge/corner regression, including picking, navigation and physical containment.

Validation for this correction:
- `dotnet build`: passed with zero warnings and zero errors.
- Godot 4.7.2 .NET headless editor import and 180-frame main-scene startup: exit 0, no runtime errors; one bake, 2766 polygons.
- New edge suite: all four sides and four corners accept physically safe ground picks, complete NavigationAgent movement, remain grounded, and reject farther-outside orders without losing the prior goal. Ground rays hit the floor rather than invisible walls. Subsequent keyboard pressure reaches the actual capsule-limited wall contact on each relevant axis and cannot leave the terrain.
- Repeated the full edge suite at TerrainSize=60, with the same measured 0.35 inset, and TerrainSize=32 with a larger radius-0.4 capsule, deriving a 0.401 physical requirement and 0.45 baked inset. Changes apply only to test instances. All three configurations pass, showing that margins derive from dimensions rather than the current 40-unit map.
- Both arches still pass in both directions at yaw 0, 22.5, 45, 90, 135, 180 and 270 degrees: 28 traversals, with solid pillar checks and no side contacts through the openings.
- Existing WASD, camera, tree, rock and ruin regression suite passes. Existing mouse/navigation suite passes at 30, 60 and 120 physics Hz, including hold/release/cancel behavior, blocked/unreachable orders, boundaries and no ongoing rebaking.
- Initial accelerated tests exposed an asynchronous startup-map publication race; the test harnesses now wait for a usable initial path rather than assuming a fixed frame count means navigation is ready. Production already rejects orders until the map is ready.
- No new rendered/interactive visual verification is claimed. Manual F5 testing should confirm edge-click feel, hold behavior and camera tracking.
- `git diff --check` passes. Stayed on feature/milestone-02-player and preserved all existing work; no commits, pushes, branch changes, merges or rebases.

Run the edge suite from the project directory:

```powershell
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/inspect_terrain_edges.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/inspect_terrain_edges.gd -- --size=60
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/inspect_terrain_edges.gd -- --size=32 --radius=0.4
```

Remaining limitations: the finer voxel grid costs more startup baking work; larger worlds should eventually bake tiles/chunks instead of rebuilding an entire level. The current bake remains once at startup. Collider-driven margins currently support upright capsules on the existing flat terrain. If multiple actors have different collider dimensions, bind the navigation component to the largest supported capsule or use separate size-appropriate maps. The map's cell size must match the resource. Obstacles at an edge can legitimately restrict its usable strip; invalid picks never bypass their navigation clearance or physical collision.

References: [NavigationMesh erosion and cell-size rounding](https://docs.godotengine.org/en/stable/classes/class_navigationmesh.html), [NavigationAgent radius and avoidance](https://docs.godotengine.org/en/stable/classes/class_navigationagent3d.html).
## Unrestricted mouse targeting

Root cause: mouse acquisition previously required a physical hit on the terrain collider. Rays into background returned immediately without setting a destination. The ordinary destination validator also only accepted a small voxel-sized projection error. Holding the mouse button could therefore keep polling but never produce a new goal once the cursor moved beyond the terrain. The character boundaries themselves were already correct.

The mouse request is now independent of the character's allowed movement area:

1. `MouseTargetAcquisition.TryAcquire` checks the camera viewport and finite screen/ray coordinates. A valid terrain hit follows the existing click-to-move path.
2. With no valid terrain hit, it calculates `t = (planeHeight - rayOrigin.Y) / rayDirection.Y`. An intersection in front of the camera returns the requested world point even outside the terrain. This helper has no actor, terrain-size or navigation dependencies.
3. PlayerController submits a fallback request through TrySetProjectedDestination. This uses the current navigation map's closest surface point without imposing the ordinary XZ snap-distance limit, then runs the same height and path-connectivity validation as a normal order. The accepted destination remains on the safe mesh; the existing physical perimeter remains a separate safeguard.
4. Invalid math, unsynchronized navigation, incompatible surface height or a disconnected target leaves the last valid order unchanged. No straight-line movement bypasses NavigationAgent3D or collision.

The world injects TargetPlaneAnchor as WorldNavigation (the generated floor's reference transform), so plane height follows the world height. TargetPlaneHeightOffset defaults to 0 and can adjust this reference without changing terrain geometry. A standalone player without an anchor uses world height zero plus the offset. MaximumPlaneRayDistance defaults to 1000 units and MinimumPlaneRayVerticalComponent to 0.001. Parallel/nearly parallel rays, behind-camera intersections and excessive/non-finite distances are rejected safely before an unstable target can be used. These parameters are exported on the player.

Hold updates continue through the same acquisition/validation flow at the existing 0.12-second interval. The existing 0.25-unit goal-change hysteresis avoids repeatedly assigning nearly identical corner targets. Releasing preserves the last valid goal; moving the cursor outside the viewport preserves that goal without updating it. WASD cancels the destination and held-mouse updates until a new press, as before. Scene composition, path following, camera smoothing and collider geometry are otherwise unchanged.

A ray hitting a prop instead of valid terrain can also use the plane beneath/behind it; the result projects to safe navigation clearance and routes around solids. Direct low-level TrySetDestination calls remain strict. Tests explicitly distinguish that API from mouse-driven projection: far direct-ground orders are rejected, while equivalent valid mouse-plane requests are accepted and constrained.

Files changed for this update:
- New `scripts/characters/MouseTargetAcquisition.cs`: reusable physics-hit/plane acquisition helper, plus generated `.uid`.
- `scripts/characters/PlayerController.cs`: exported plane settings/reference, acquisition delegation, projected-order API and shared destination validation.
- `scenes/world/World.tscn`: injects the plane anchor into the existing player instance.
- New `docs/validate_unrestricted_targeting.gd` and generated `.uid`: viewport click/drag, plane and containment integration checks.
- `docs/validate_mouse_navigation.gd`: expected no-hit behavior now verifies safe projection, checks solid clearance near a trunk, and verifies disconnected projected requests preserve the last goal.
- `docs/MILESTONE_02.md`: current behavior, parameters and validation.

No changes to terrain generation/geometry, navigation baking/resource, physical boundary placement/layers, camera implementation, tree/rock/gate scenes, or existing input mappings were made for this update.

Validation:
- `dotnet build`: passed, zero warnings and zero errors, with the existing Godot.NET.Sdk 4.7.2 / net8.0 setup.
- Bounded Godot headless editor import and 180-frame main-scene startup passed with exit 0. Terrain remains seed 731 / 10 trees / 16 rocks / size 40; the unchanged bake has 2766 polygons.
- New suite passed: clicks beyond all four sides and all four corners, with the projected cursor still in the viewport and the physics ray explicitly confirmed to have no terrain hit. Each order selects the nearest safe navigation destination and completes grounded inside the physical limits.
- New suite also injects real viewport mouse press/motion/release events: hold begins on valid ground, drag beyond each edge/corner keeps updating, release freezes the last destination, and a held cursor outside the viewport cannot overwrite it. Corner targets remain numerically stable while the camera follows; the stopped actor shows no measured horizontal jitter.
- Parallel/nearly parallel rays, intersections behind the camera, excessive distance, non-finite screen/world requests and out-of-viewport positions are rejected. A downward-ray positive control verifies that rejection fixtures are actually testing orientation/distance rather than a broken camera.
- Disconnected projected destination test passes using the existing isolated navigation island: it confirms rejection without losing the prior valid goal. Trunk projection remains outside solid clearance, and actual obstacle routes/collision checks still pass.
- New targeting suite passed for size 40 / height 0 at 60 Hz, size 32 / height 0 at 30 Hz, and size 60 / height 3 at 120 Hz. These are test-only world instances; no production terrain dimension or height is changed.
- Existing movement, camera and all-tree collision regression passes. Existing terrain-edge/corner containment suite passes. Existing mouse/navigation suite passes with updated no-hit expectations.
- Both ancient arches pass both directions at yaw 0, 22.5, 45, 90, 135, 180 and 270 degrees: all 28 central traversals and pillar-solid checks pass.
- The larger-map test initially placed the actor inside existing scatter geometry; its reset now chooses a navigable start before testing held updates. Temporary fixture cameras disable interpolation so ray-orientation tests inspect their explicitly assigned transforms; production camera interpolation is preserved.
- The previously documented intermittent Jolt worker-capacity warning appeared in some accelerated headless runs; assertions still passed and the affected successful runs exited 0. No engine/system configuration was changed.
- No new rendered or interactive visual verification is claimed. Manual feedback before this update confirmed all previous gameplay behavior was satisfactory; the new targeting feel still needs the checklist below.
- Stayed on feature/milestone-02-player and preserved all previous edits. No commit, push, branch switch, merge or rebase.

Reproduce targeting checks:

```powershell
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_unrestricted_targeting.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 30 --script res://docs/validate_unrestricted_targeting.gd -- --size=32 --physics-rate=30
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 120 --script res://docs/validate_unrestricted_targeting.gd -- --size=60 --height=3 --physics-rate=120
```

Manual checklist: press F5 and approach each side/corner; click the background beyond the visible terrain while keeping the cursor in the game viewport. Confirm movement toward a safe edge destination. Hold on ground, drag into background, sweep along the perimeter and across corners, then release. Confirm continued response, stable arrival, and no falling or stone penetration. Press WASD during a held order and confirm keyboard takeover. Move the held cursor outside the game viewport and back: only valid in-viewport requests should update the goal. Cross each arch and circle a tree/rock to confirm the established behavior remains satisfactory.

Remaining limitations: the fallback uses one horizontal plane, appropriate for this flat prototype; sloped or multi-floor terrain will need a surface-selection policy. The closest navigable point is validated for connectivity; a disconnected projection preserves the previous order rather than searching every reachable region for an alternative. Maximum ray distance is configurable for future scene scales. Existing arrival tolerance (0.2), collision safety margin, navigation clearance and static-obstacle limitations remain unchanged.
