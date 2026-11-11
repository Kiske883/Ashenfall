# Milestone 05C - First integrated playable demo

Scene: **`res://scenes/world/AshenfallPlayableDemo.tscn`**.

Implemented from clean HEAD `6710bb4` on `feature/milestone-05-animation`. New files only; existing world, test scenes, gameplay scripts, approved animations, resources and project settings are unchanged. No staging, commits, pushes, merges, resets, stashes, cleans or branch switches.

## Launch and play

1. Open project.godot in Godot 4.7.2 .NET and build the C# solution.
2. Open `scenes/world/AshenfallPlayableDemo.tscn` and press **F6 / Run Current Scene**.
3. Move with WASD or left-click the ground; holding left mouse continuously updates the destination. The existing fixed isometric camera follows the player.
4. Explore the terrain, then follow the stone road toward the ruins. The Fallen Guardian waits on the clear central road before the arches.
5. Approach until it detects you. Stop and observe Walk pursuit, facing, windup, sword slash and recovery. It repeats attacks while you remain in range.
6. Retreat along the road, continuing past your original starting point until you leave its leash. It returns home more slowly and resumes Idle.
7. Approach again to repeat the encounter without restarting. There is no damage, health loss or death; this is an integrated movement/behavior/animation prototype.

F5 retains the original main scene, World.tscn. The integrated scene is deliberately opt-in through F6; no global startup setting changed. Direct PowerShell launch from the project directory:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan res://scenes/world/AshenfallPlayableDemo.tscn
```

## Minimal composition

```text
AshenfallPlayableDemo
├── World (existing World.tscn)
│   ├── EnvironmentGenerator / original terrain, path, props, ruins
│   ├── WorldNavigation
│   ├── WorldEnvironment / Moonlight
│   ├── CameraRig / Camera3D
│   └── FutureActors / Player
└── FallenGuardian (existing FallenGuardian.tscn)
```

The only encounter configuration is the enemy position and explicit PlayerTarget reference. No new production script, generator, AI, UI, camera controller or input action was needed. Existing title/control text is inherited from World; no new UI system or enemy debug label is enabled.

Player starts at the unchanged (-0.8, 0.12, 5) placement and settles onto the ground. Enemy starts at (0, 0, -5), centered in the road, with ground-level feet. Initial horizontal separation is approximately 10.03 units, beyond detection radius 6. Both placements resolve onto the shared navigation surface and are physically grounded. Trees/rocks occupy the original side bands; ruins remain at x +/-5.2, z -7, leaving this central approach clear. The map has room to retreat beyond the spawn leash (12 units); the focused test retreats to (0, 0, 8), still well inside the terrain.

World is reused with its original seed 731, terrain 40, 10 trees, 16 rocks, road, two ruins, lighting and atmosphere. Its existing deterministic one-time startup generation and 2766-polygon navigation bake run normally. No seed, content recipe, mesh resource or bake configuration was changed or rerolled for this demo.

The enemy prefab supplies the approved articulated V3, low guard, Idle/Walk/Attack, navigation and collision setup, detection/state machine and visual adapter. Chase remains 1.0 m/s, Return 0.7 m/s, windup 0.6 s and recovery 1.0 s. AttackExecuted remains AI-only. Animation does not control physics or create additional execution events. Only one player and one enemy are instantiated.

## Executed validation

- dotnet build: passed with zero warnings/errors.
- Godot headless editor import: passed.
- Direct headless startup of AshenfallPlayableDemo for 180 frames: passed.
- `docs/validate_playable_demo.gd`: zero failures headlessly and with actual Vulkan Forward+ rendering. It loads the actual new scene, confirms safe/grounded/navigation-aligned spawns, original configuration and camera, then uses real WASD input and mouse button events rather than teleporting the player through the encounter.
- Focused sequence: WASD exploration/camera tracking; click-to-move approach; enemy detection/Walk; two authoritative attacks; click retreat; leash return/Idle; second click approach and another attack without restarting. Attack phase sampling matches the existing AI clock, no stale attacks appear after return, and the camera keeps its original orientation throughout.
- Collision safety checks retain static-obstacle/boundary masks and shared ground contact. Existing player mouse/navigation and ancient-gate suites provide the prop/boundary/arch collision regressions.
- Existing enemy pursuit, enemy attack, AI/animation integration, dual movement speeds, player mouse/navigation, both ancient gates and approved attack-animation suites: all seven passed with zero failures. No final runtime warning/error was reported.
- Real render captures: `docs/milestone_05c_start.png`, `docs/milestone_05c_encounter.png`, `docs/milestone_05c_repeat.png`. Start and encounter images were opened for visual inspection. This is automated rendered validation; manual interactive gameplay approval is still pending.
- Git diff for tracked files remains empty: all earlier scenes/scripts/assets and settings are preserved.

Reproduce focused validation:

```powershell
dotnet build
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --fixed-fps 60 --script res://docs/validate_playable_demo.gd
& 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --rendering-driver vulkan --fixed-fps 60 --script res://docs/validate_playable_demo.gd -- --capture
```

## Known limitations

- No health, damage, hitboxes, combat effects, inventory, multiplayer or extra enemies. Sword strikes are visual/logical attempts and do not harm the player.
- Approved in-place Walk retains its known residual sliding: approximately 33.3% contact-speed mismatch during 1.0 m/s Chase and 4.8% during 0.7 m/s Return with the current cadence cap. No animation or movement tuning was altered here.
- Original rigid joints/combined armor, simplified player and original Milestone 01 environment assets remain. Later approved tree art prototypes are not substituted into the world.
- The enemy is small/dark at the unchanged gameplay camera size 22 and original world lighting. No studio magnification or lighting changes were used to improve appearance in this demo.
- Encounter placement is intentionally specific to the existing map. Future world changes need spawn/navigation revalidation. No crowd, long-duration soak or manual visual motion approval is claimed.

## Exact new files

- `scenes/world/AshenfallPlayableDemo.tscn`
- `docs/validate_playable_demo.gd`
- `docs/validate_playable_demo.gd.uid`
- `docs/MILESTONE_05C_PLAYABLE_DEMO.md`
- `docs/milestone_05c_start.png`
- `docs/milestone_05c_encounter.png`
- `docs/milestone_05c_repeat.png`

Godot also generates ignored `.png.import` metadata. Existing files modified: none. All additions remain local/untracked. Stop for manual gameplay approval before further work.
