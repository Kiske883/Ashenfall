# Milestone 05A.3.2 - V3 visual locomotion demonstration

> Current pose: the relaxed low guard in the final section supersedes both earlier raised-guard iterations below. Locomotion and shield remain unchanged.

Isolated demo: **`res://scenes/art/FallenGuardianWalkDemo.tscn`**. Open in Godot 4.7.2 .NET and press **F6**. It starts in WALK; its only controls are IDLE and WALK. No joint UI, slider, locomotion integration, attacks or damage.

Branch remains `feature/milestone-05-animation`, HEAD `e4ec9ab`. All 164 pre-existing files under scripts/scenes/resources/docs were checked by SHA256 and remain unchanged, including uncommitted 05A.2/05A.3.1 sources, libraries, documentation and screenshots. No staging, commit, merge, push, reset, clean, stash or branch switch.

## Architecture and framing

`FallenGuardianV3GuardPresentation.tscn` instances the existing V3 presentation and adds AnimationPlayer using the new native `FallenGuardianV3GuardLocomotion.tres` AnimationLibrary. The old generators and presentations are reused unchanged; no mesh, material, shader or equipment geometry is rewritten. The new presentation has no AI or physics controller. The two-button demo plays clips with a local 0.18-second blend; default WALK is selected in Ready after the procedural hierarchy exists.

The runtime camera is fixed at 45-degree isometric inclination with **orthographic size 22**. The guardian is oriented -150 degrees around Y, a slight three-quarter turn for weapon and knee visibility. Character scale remains one. To satisfy both size 22 and a large inspection image without scaling the character, an isolated 2048-square SubViewport renders the scene and a TextureRect magnifies its central 384-square region. This is inspection magnification, NOT a claim that the character occupies this much screen space at ordinary gameplay framing. It incurs an extra offscreen render and can still soften thin details; it is not intended as a gameplay rendering architecture.

Ground, key/fill lights and shadow settings are local to this demo. The original world camera, atmosphere and main scene remain unchanged.

## Guard pose

Both states hold the same combat guard. Shoulders have -8 degrees X with only +/-2 degrees lateral abduction, limiting motion below the combined armor. Left elbow is approximately 45 degrees and carries the defensive shield. Right elbow is approximately 126 degrees with a small cyclic variation; right wrist has a fixed 24-degree flexion, without twisting around the forearm. The original sword remains attached to the right hand, and the shield remains attached to the left forearm. No weapon-only rotation or translation breaks the grip.

The stronger right elbow fold is deliberate: merely bending the previous downward grip slightly still projected the blade downward from the isometric camera. The final blade projects forward/up and its tip stays above the guard throughout both clips. This is a deeply flexed right elbow, rather than the requested slight elbow bend; it is a limitation of retaining the original grip/model exactly and should be judged visually. No claim of an approved production guard is made.

PelvisPivot has a constant -0.035-unit rest offset to allow both legs to flex while keeping the feet on the ground. There is no cyclic root translation, scaling or exaggerated bounce. PoseRoot and the presentation root never move. Torso counter-yaw and head compensation are small; shoulder swings stay within about two degrees of the guard during walking.

## Clips and gait design

- **Idle:** 3.0-second loop. Subtle torso/head and asymmetric arm movement, fixed leg guard and level feet.
- **Walk:** 1.4-second loop. Left/right leg phases differ by half a cycle. Each leg has 60% support and 40% swing, including brief double support.

During support, the ankle target moves backward through a 0.28-unit stride at constant height. During swing, it returns forward with a smoothstep horizontal path and a sin(pi*t)^1.5 lift reaching 0.12 units. This is an in-place treadmill gait: backward ground-relative foot movement during support is inherent when the root stays fixed. It avoids erratic sliding rather than pretending the support foot is stationary in world space.

Hip and knee keys were calculated from the original asymmetric thigh/shin vectors with a planar two-segment solve. Original thigh/shin lengths, joint positions and initial angular offsets are retained. Ankles counter-rotate by the negative sum of hip and knee offsets, keeping feet level instead of tipping detached toe clusters through the ground. The solve is baked into 41 reusable animation keys per clip; there is no runtime IK or per-frame procedural pose controller. AnimationPlayer evaluates the curves.

Measured knee angle span across the sampled states/cycle is **53.80 degrees**. Peak ankle lift is **0.120 units**; minimum ankle Y is **0.09494**, compared with its original 0.095. Knees rotate relative to their thighs, not just with the hips. Original heel/toe geometry remains attached through AnklePivot; no scaling or independent foot translation is used.

## Visual validation and honest limits

The complete cycle was sampled numerically at 81 phases per clip. Real Vulkan Forward+ rendering captured six phases spanning one cycle from each of isometric, front and side views. The resulting phase sheet was opened and visually inspected, along with Idle and Walk full frames. This is rendered phase review, not a claim of manual interactive full-speed playback approval.

Evidence:

- `docs/milestone_05_v3_guard_idle.png`
- `docs/milestone_05_v3_guard_walk.png`
- `docs/milestone_05_v3_guard_cycle.png`: rows are isometric / front / side; columns are phases 0, 1/6, 2/6, 3/6, 4/6, 5/6.

The first visual iteration failed the weapon-readability goal despite a world-height test passing: foreshortening still made the blade appear downward. Guard angles were revised and a projected upward-blade assertion added. Initial low-resolution inspection was also soft; the stage resolution was doubled. The final phase sheet shows knee folding and foot lift in side/isometric views, with blade carried upward and shield forward. Front-on knee flexion remains less readable because of depth projection and shield occlusion. The final three-quarter view balances weapon and knee readability.

Remaining limitations:

- This remains a rigid segmented approximation. Original bone ends/rótula can overlap or reveal small joint discontinuities at deeper flexion, especially from the side. No cosmetic fillers or mesh redesign were added. The pose is not certified intersection-free across every possible viewing angle.
- The combined shoulder armor does not deform with the arms. Small shoulder amplitudes reduce, but cannot eliminate, contact issues; deep right elbow flexion is an explicit visual compromise.
- Feet are rigid, level pieces without heel/toe roll. The gait can still feel mechanical and has no lateral weight transfer. It is not a production-quality motion capture gait.
- Shield size hides parts of the far leg and arm. Magnified thin bone/weapon details remain somewhat soft; the offscreen inspection render also costs more than the normal gameplay view. No crowd/frame-rate benchmark was performed.
- No gameplay movement, travel-speed synchronization, attacks, combat or damage. User motion/pose approval remains pending; technical passing tests are not visual approval.

## Executed validation

- dotnet build: passed with zero warnings/errors.
- Godot headless editor import: passed.
- `validate_v3_walk_demo.gd`: final headless and actual Vulkan capture runs passed with zero failures/errors. Checks automatic WALK, exactly two buttons, size 22, unchanged mesh/material arrays, unit scales, no root motion, ankle compensation, upward sword projection, bounded wrist angle, cycle seams and knee/lift measurements.
- Existing V3 geometry/articulation, earlier Idle/Walk, enemy pursuit, enemy attack and player mouse/navigation suites: zero failures each. These also load the unchanged world and verify navigation/physics behavior.
- An initial capture-sheet assembly emitted image-format mismatch errors; conversion to RGBA8 fixed it. Final capture run completed without those errors. No unresolved build/test warning or failure was observed in the final runs.

Reproduce:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_v3_walk_demo.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan --fixed-fps 60 --script res://docs/validate_v3_walk_demo.gd -- --capture
```

## New files only

- `resources/animations/FallenGuardianV3GuardLocomotion.tres`
- `scenes/enemies/FallenGuardianV3GuardPresentation.tscn`
- `scenes/art/FallenGuardianWalkDemo.tscn`
- `scripts/art/FallenGuardianWalkDemo.cs` and `.uid`
- `docs/validate_v3_walk_demo.gd` and `.uid`
- `docs/MILESTONE_05_V3_WALK_DEMO.md`
- The three new PNGs above and their ignored Godot `.import` sidecars.

No prior files changed. Stop here for user visual review before any gameplay integration or attack work.

## Focused correction - sword grip and right-arm guard

The walk has been accepted as recognizable and was preserved. This correction changes only the guard-specific sword binding and right Shoulder/Elbow/Wrist rotation key values in Idle and Walk. All other library content is identical, including key times, durations, left arm/shield, torso, hips, knees and ankles. No original art or previous prototype is changed.

### Root cause and binding

The sword already had the correct parent: SwordAttachment below RightWristPivot. However, its attachment preserved the old static art placement rather than defining a hand grip frame. The handle shaft ran from model-space pommel (0.425, 0.966, -0.21) to guard (0.465, 0.79, -0.26), nearly along negative Y. The four finger curls are distributed across local X and bend in the YZ plane. Thus the old handle ran mostly longitudinally beside the fingers rather than transversely through their curl. Their centers were also offset. The previous 126-degree elbow and 24-degree wrist-X pose compensated for weapon direction without correcting this binding.

The new guard-only `FallenGuardianGuardGrip.cs` binds the handle at its midpoint to model-space hand point (0.432, 0.86, -0.244), equivalently wrist-local (0.006, -0.084, -0.028). It rotates the shaft direction onto hand-local +X and compensates translation about the handle center, rather than spinning around the blade/pivot and pulling the grip out of the fingers. The child's sword mesh, original vertex coordinates, proportions and materials remain untouched. This binding is applied once, after V3's generated hierarchy exists, by a script on the guard presentation only.

### Final right-arm pose

- Shoulder base: X -10 degrees, Y 40 degrees, Z 0, retaining the small existing right-arm cyclic swing. Upper-arm direction stays close to vertical/down toward the torso, verified numerically.
- Elbow: 100-degree additional hinge rotation composed with 70-degree forearm pronation around the original elbow-to-wrist axis. This reduces the former 126-degree hinge compensation; raw Euler values in the native clip represent the composed transform and are not independent anatomical bend angles.
- Wrist: X/Y zero; Z -20 degrees of bounded deviation. There is no longitudinal wrist twist. This small hand pose is applied after correcting the binding; it is not used to conceal an incorrectly positioned attachment.
- At phase zero, handle center is approximately (0.217, 1.324, -0.333) in the demo's unrotated model frame, beside the right chest. Blade direction is approximately (0.665, 0.729, -0.163): diagonally upward and slightly forward (-Z).

The new scene script and only six key-value arrays (three right-arm tracks in each of two clips) provide the correction. Original sword geometry, shield pose, locomotion mechanics, 1.4-second Walk, 3-second Idle, 0.12-unit foot lift and knee/ankle compensation are unchanged. A normalized source comparison masks only those six arrays and confirms all remaining library text is identical to the pre-fix snapshot.

### Validation and visual review

- dotnet build: zero warnings/errors.
- Godot headless editor import: passed.
- New `validate_guard_grip.gd`: zero failures across 81 samples of each clip. Verifies handle center within the hand to 1e-5, shaft aligned with hand-local X, bounded wrist deviation, forward/up blade, hand beside chest and tucked upper arm.
- Existing V3 walk demo, V3 articulation/geometry, earlier Idle/Walk, enemy attack and player mouse/navigation tests: zero failures each. Knee span remains 53.795 degrees, maximum ankle lift 0.120, minimum ankle height 0.09494.
- Real Vulkan rendering and updated Idle/Walk/cycle captures: passed without shader/resource/runtime errors. Idle and the full Walk phase sheet were opened and inspected. The sheet covers six phases each from front, side and isometric views. Sword remains attached in both clips; handle alignment is also checked throughout the numerical cycle.
- Visual review used rendered sampled frames, not interactive human full-speed playback. Approval is still pending. Fingers are unchanged rigid stylized geometry, so no production-quality finger closure is claimed. Forearm pronation is substantial, and the shield still occludes portions of the weapon in a pure side view. Existing rigid elbow/armor contact limitations remain.

Modified existing local files:

- `resources/animations/FallenGuardianV3GuardLocomotion.tres`
- `scenes/enemies/FallenGuardianV3GuardPresentation.tscn`
- `docs/MILESTONE_05_V3_WALK_DEMO.md`
- `docs/milestone_05_v3_guard_idle.png`
- `docs/milestone_05_v3_guard_walk.png`
- `docs/milestone_05_v3_guard_cycle.png`

New files:

- `scripts/art/FallenGuardianGuardGrip.cs` and `.uid`
- `docs/validate_guard_grip.gd` and `.uid`

All unrelated uncommitted work is preserved. No tracked gameplay/art assets changed, and no staging, commits or Git history/branch mutations were performed. Reopen the same FallenGuardianWalkDemo.tscn with F6 and compare IDLE/WALK. Stop for visual approval.

## Final refinement - relaxed low combat guard

The raised guard was not approved. It required too much internal shoulder rotation (40 degrees), elbow hinge adjustment (100 degrees) and wrist deviation (20 degrees), placing the hand high and making the right arm appear forced. The grip binding itself is retained: the handle remains centered in the finger curl on hand-local X. Original rest axes are model-aligned, with asymmetric joint offsets; forearm rotation is composed around the actual elbow-to-wrist vector rather than interpreted from raw Euler components.

Only the right Shoulder/Elbow/Wrist key-value arrays in Idle and Walk change:

- Shoulder: base X -10, Y 5, Z -12 degrees. Negative Z brings the pre-existing outward-sloping upper arm inward. Internal rotation is now only 5 degrees. Small prior cyclic X variation remains.
- Elbow: 60-degree additional hinge rotation, composed with 65-degree forearm pronation around the original forearm axis. Raw Euler values encode this composition; they are not independent anatomical bend angles. The original neutral geometry already has some elbow bend.
- Wrist: X/Y zero, Z -5 degrees, near neutral. No longitudinal wrist twist or weapon-only compensation.
- At phase zero, the elbow is approximately (0.315, 1.118, 0.016) after the existing pelvis guard offset; the handle center is (0.371, 1.066, -0.353), near the right hip/lower ribs. Elbow X differs from shoulder X by about 0.036 units. Blade direction is approximately (0.692, 0.715, -0.099), diagonal upward and slightly forward.

The same low guard is used in both clips. A source comparison that masks only those six right-arm value arrays confirms the rest of the library is identical to the pre-refinement version: no change to legs, timing, foot lift, shield, torso/head motion or locomotion mechanics. Sword grip binding, mesh resources, materials and the original V1/V2/V3 art recipes are unchanged. No gameplay or camera changes.

Validation executed:

- dotnet build passed with zero warnings/errors.
- Updated focused grip tests passed across 81 samples of each complete clip. They check a low hand position, elbow lateral offset under 0.05, upper arm nearly vertical (down-axis dot >0.98), wrist within 5 degrees, handle/finger alignment and forward/up blade direction.
- Existing focused V3 walk tests passed: unchanged geometry/materials, no scaling/root motion, loop seams and identical gait metrics (knee span 53.795 degrees; peak ankle lift 0.120 units; minimum ankle Y 0.09494).
- Actual Vulkan capture runs passed with no reported errors. Updated Idle/Walk full frames and Walk phase sheet were produced; a new `docs/milestone_05_v3_low_guard_idle_cycle.png` covers the Idle cycle. Both phase sheets were opened for visual review across isometric, front and side views. Six phases span each full cycle; this is rendered phase review, not a claim of manual continuous playback approval.
- Capture crops were widened so the low-carried blade remains visible in the phase sheets. Only capture framing changed in the existing walk validation utility.

Remaining limitations: the fingers are a rigid stylized mesh, so perfect anatomical finger closure cannot be guaranteed. Forearm pronation remains substantial; original rigid elbow/armor contacts and side-view shield occlusion persist. No new geometry or extreme wrist compensation was introduced to hide those limitations. Final motion/pose acceptance remains the user's decision.

Modified in this refinement: `resources/animations/FallenGuardianV3GuardLocomotion.tres`, `docs/validate_guard_grip.gd`, `docs/validate_v3_walk_demo.gd` (capture crop only), this document, and the three existing guard evidence PNGs. New: `docs/milestone_05_v3_low_guard_idle_cycle.png` with ignored import metadata. All other local work is preserved. Same demo path and F6 instructions; stop for visual approval, without staging or committing.
