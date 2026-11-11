extends SceneTree

# Measured terrain-edge regression: optional --size=60 and --radius=0.4.
var failures := 0
var terrain_size := 40.0
var requested_radius := -1.0
var player: CharacterBody3D
func _initialize() -> void:
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--size="): terrain_size = float(arg.get_slice("=", 1))
        if arg.begins_with("--radius="): requested_radius = float(arg.get_slice("=", 1))
    call_deferred("run")
func steps(count: int) -> void:
    for tick in range(count):
        await physics_frame
        await process_frame
func check(condition: bool, message: String) -> void:
    if condition: print("PASS: ", message)
    else:
        failures += 1
        push_error("FAIL: " + message)
func release_keys() -> void:
    for action in ["move_up", "move_down", "move_left", "move_right"]: Input.action_release(action)
func run() -> void:
    var world = load("res://scenes/world/World.tscn").instantiate()
    world.get_node("EnvironmentGenerator").TerrainSize = terrain_size
    player = world.get_node("FutureActors/Player")
    if requested_radius > 0:
        var shape: CapsuleShape3D = player.get_node("CollisionShape3D").shape.duplicate()
        shape.radius = requested_radius
        player.get_node("CollisionShape3D").shape = shape
    root.add_child(world)
    current_scene = world
    var camera: Camera3D = world.get_node("CameraRig/Camera3D")
    var region: NavigationRegion3D = world.get_node("WorldNavigation")
    var map: RID = world.get_world_3d().navigation_map
    var half := terrain_size / 2
    for attempt in range(300):
        await steps(1)
        if NavigationServer3D.map_get_iteration_id(map) > 0 and NavigationServer3D.map_get_closest_point(map, Vector3(half - 1, 0, 0)).x > half - 2: break
        OS.delay_msec(1)
    var radius: float = player.get_node("CollisionShape3D").shape.radius
    var safe_margin := radius + player.safe_margin
    var cell: float = region.navigation_mesh.cell_size
    var center_limit := half - safe_margin
    print("EDGE METRICS size=", terrain_size, " capsule=", radius, " safe_margin=", safe_margin,
        " baked_radius=", region.navigation_mesh.agent_radius, " cell=", cell)
    check(abs(float(region.SafeEdgeMargin) - safe_margin) < 0.0001, "navigation derives margin from physical capsule")
    for axis in [Vector3.RIGHT, Vector3.LEFT, Vector3.BACK, Vector3.FORWARD]:
        var extent := NavigationServer3D.map_get_closest_point(map, axis * half).dot(axis)
        var measured_margin := half - extent
        print("MEASURED ", axis, " extent=", extent, " margin=", measured_margin)
        check(measured_margin >= safe_margin - 0.001 and measured_margin <= safe_margin + 2 * cell + 0.002,
            "edge margin within two-cell raster allowance " + str(axis))

    var directions := [Vector3.RIGHT, Vector3.LEFT, Vector3.BACK, Vector3.FORWARD,
        Vector3(1, 0, 1), Vector3(-1, 0, 1), Vector3(1, 0, -1), Vector3(-1, 0, -1)]
    var right := camera.global_basis.x
    right.y = 0
    right = right.normalized()
    var back := camera.global_basis.z
    back.y = 0
    back = back.normalized()
    for index in range(directions.size()):
        var direction: Vector3 = directions[index]
        var target := direction * center_limit
        var start := NavigationServer3D.map_get_closest_point(map, target - direction.normalized() * 3)
        player.CancelDestination()
        release_keys()
        player.global_position = Vector3(start.x, 0.12, start.z)
        player.velocity = Vector3.ZERO
        player.reset_physics_interpolation()
        await steps(60)
        var screen := camera.unproject_position(target)
        var origin := camera.project_ray_origin(screen)
        var ray := PhysicsRayQueryParameters3D.create(origin, origin + camera.project_ray_normal(screen) * camera.far, 1)
        var hit: Dictionary = world.get_world_3d().direct_space_state.intersect_ray(ray)
        check(not hit.is_empty() and hit.collider.is_in_group("navigable_ground"), "boundary does not occlude ground ray " + str(index))
        check(player.TrySetScreenDestination(screen), "physically safe edge/corner mouse pick accepted " + str(index))
        var destination: Vector3 = player.Destination
        var arrived := false
        var stayed_safe := true
        for tick in range(480):
            await steps(1)
            stayed_safe = stayed_safe and abs(player.position.x) <= center_limit + 0.005 and abs(player.position.z) <= center_limit + 0.005 and player.position.y > -0.01
            if not player.HasDestination:
                var delta_position := player.global_position - destination
                delta_position.y = 0
                arrived = delta_position.length() <= player.ArrivalDistance + 0.02
                break
        check(arrived and stayed_safe and player.is_on_floor(), "agent reaches edge/corner safely " + str(index))
        # Continue with keyboard into the physical wall, including simultaneous corner contact.
        var movement := direction.normalized()
        var x := movement.dot(right)
        var y := movement.dot(back)
        Input.action_press("move_right", maxf(0, x))
        Input.action_press("move_left", maxf(0, -x))
        Input.action_press("move_down", maxf(0, y))
        Input.action_press("move_up", maxf(0, -y))
        await steps(90)
        release_keys()
        var constrained := true
        for axis in [Vector3.RIGHT, Vector3.BACK]:
            if abs(direction.dot(axis)) > 0.5:
                var margin: float = half - abs(player.global_position.dot(axis))
                constrained = constrained and margin >= radius - 0.005 and margin <= safe_margin + 0.01
        check(constrained and player.is_on_floor(), "physical wall stops keyboard at capsule limit " + str(index))
        check(player.TrySetDestination(target), "order remains possible from physical perimeter " + str(index))
        var saved: Vector3 = player.Destination
        check(not player.TrySetDestination(direction * (half + 1)) and player.Destination.is_equal_approx(saved),
            "outside click retains safe destination " + str(index))
        player.CancelDestination()
    print("Terrain-edge checks: ", failures, " failures; size=", terrain_size)
    quit(0 if failures == 0 else 1)
