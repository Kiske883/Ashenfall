extends SceneTree

var failures := 0
var enemy: CharacterBody3D
var player: CharacterBody3D
var world: Node3D

func _initialize() -> void:
    call_deferred("run_checks")

func check(ok: bool, label: String) -> void:
    if ok: print("PASS: ", label)
    else:
        failures += 1
        push_error("FAIL: " + label)

func steps(count: int) -> void:
    for i in count:
        await physics_frame
        await process_frame

func distance(a: Vector3, b: Vector3) -> float:
    return Vector2(a.x-b.x, a.z-b.z).length()

func place_player(point: Vector3) -> void:
    player.global_position = point
    player.velocity = Vector3.ZERO
    player.reset_physics_interpolation()

func spawn(point: Vector3) -> void:
    if is_instance_valid(enemy):
        enemy.queue_free()
        await steps(2)
    enemy = load("res://scenes/enemies/FallenGuardian.tscn").instantiate()
    enemy.AttacksEnabled = false # Isolate the original 04B movement/state contract.
    enemy.position = point
    enemy.PlayerTarget = player
    world.add_child(enemy)
    await steps(4)

func wait_state(state: int, ticks: int = 600) -> bool:
    for i in ticks:
        if enemy.State == state: return true
        await steps(1)
    print("STATE_DIAGNOSTIC state=", enemy.State, " pos=", enemy.global_position, " player=", player.global_position)
    return false

func run_checks() -> void:
    world = load("res://scenes/world/World.tscn").instantiate()
    root.add_child(world)
    current_scene = world
    player = world.get_node("FutureActors/Player")
    player.InputEnabled = false
    await steps(60)
    var map := world.get_world_3d().navigation_map
    for i in 300:
        if NavigationServer3D.map_get_iteration_id(map) > 0: break
        OS.delay_msec(1)
        await steps(1)
    var mesh: NavigationMesh = world.get_node("WorldNavigation").navigation_mesh
    var polygons := mesh.get_polygon_count()
    place_player(Vector3(0,0,8))
    await spawn(Vector3(0,0,0))
    await steps(60)
    check(enemy.State == 0 and distance(enemy.global_position, enemy.SpawnPosition) < 0.03, "idle outside detection radius")
    check(enemy.get_node("Visual/Presentation/PoseRoot/PelvisPivot/TorsoPivot/HeadPivot/Head") != null, "approved V3 anatomy loads under actor")
    check(enemy.get_node("Visual/Presentation/PoseRoot/PelvisPivot/TorsoPivot/HeadPivot/CursedEyes") != null, "V3 cursed details preserved")
    place_player(Vector3(0,0,4))
    await steps(2)
    check(enemy.State == 1, "Idle to Chase on detection")
    check(await wait_state(2), "Chase to Hold")
    check(abs(distance(enemy.global_position, player.global_position) - enemy.StoppingDistance) < 0.08, "stops at configured distance without overlapping player")
    var held := enemy.global_position
    var updates: int = enemy.TargetUpdateCount
    await steps(90)
    check(enemy.State == 2 and distance(held, enemy.global_position) < 0.02, "Hold is stable")
    check(enemy.TargetUpdateCount == updates, "Hold does not repath")
    place_player(player.global_position + Vector3(0,0,0.15))
    await steps(10)
    check(enemy.State == 2, "hysteresis prevents oscillation")
    place_player(Vector3(0,0,6))
    await steps(2)
    check(enemy.State == 1, "Hold resumes Chase beyond hysteresis")
    await steps(30)
    check(enemy.TargetUpdateCount - updates <= 5, "path updates throttled")
    place_player(Vector3(0,0,16))
    await steps(2)
    check(enemy.State == 3, "spawn leash disengages")
    place_player(enemy.global_position + Vector3(0,0,2))
    await steps(10)
    check(enemy.State == 3, "Return ignores nearby target")
    check(await wait_state(0), "Return reaches spawn and becomes Idle")
    check(distance(enemy.global_position, enemy.SpawnPosition) <= enemy.ArrivalTolerance, "return arrival tolerance")
    place_player(Vector3(0,0,8))
    await steps(90)
    check(enemy.State == 0, "no infinite pursuit after leash return")

    # Short disengagement independent of the spawn leash.
    enemy.DetectionRadius = 2.0
    enemy.DisengagementRadius = 3.0
    place_player(Vector3(0,0,1.8))
    await steps(2)
    check(enemy.State == 1, "reacquires after completed Return")
    place_player(Vector3(0,0,6))
    await steps(2)
    check(enemy.State == 3, "enemy-to-player disengagement radius")
    place_player(Vector3(0,0,18))
    await steps(90)

    # Actual world tree: endpoints on opposite sides of its baked footprint.
    var tree: Node3D = world.get_node("EnvironmentGenerator/Tree_02")
    var a := NavigationServer3D.map_get_closest_point(map, tree.global_position + Vector3(-2,0,0))
    var b := NavigationServer3D.map_get_closest_point(map, tree.global_position + Vector3(2,0,0))
    place_player(b)
    await spawn(a)
    check(await wait_state(2, 900), "pursuit routes around actual tree")
    check(distance(enemy.global_position, b) <= enemy.StoppingDistance + 0.1, "obstacle pursuit reaches stop range")
    check(abs(enemy.global_position.y) < 0.1, "gravity and ground contact preserved")
    enemy.PlayerTarget = null
    await steps(2)
    check(enemy.State == 3, "missing target safely enters Return")
    check(await wait_state(0, 900), "returns around tree without a target")

    # Out-of-navigation request inside detection range is rejected rather than walked to.
    place_player(a + Vector3(0,3,1))
    player.set_physics_process(false)
    enemy.PlayerTarget = player
    await steps(65)
    await steps(2)
    check(enemy.State != 1 and not enemy.HasPath, "off-mesh target does not trigger unsafe movement")
    enemy.PlayerTarget = null
    await steps(60)
    check(world.get_node("WorldNavigation").navigation_mesh == mesh and mesh.get_polygon_count() == polygons, "navigation mesh preserved across actor lifecycles")
    print("Enemy pursuit checks: ", failures, " failures")
    quit(0 if failures == 0 else 1)
