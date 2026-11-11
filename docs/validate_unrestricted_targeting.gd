extends SceneTree

# Viewport mouse targeting beyond the terrain, using the real controller and nav map.
var failures := 0
var size := 40.0
var height := 0.0
var rate := 60
var world: Node3D
var player: CharacterBody3D
var camera: Camera3D
func _initialize() -> void:
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--size="): size = float(arg.get_slice("=", 1))
        if arg.begins_with("--height="): height = float(arg.get_slice("=", 1))
        if arg.begins_with("--physics-rate="): rate = int(arg.get_slice("=", 1))
    Engine.physics_ticks_per_second = rate
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
func horizontal(a: Vector3, b: Vector3) -> float:
    return Vector2(a.x - b.x, a.z - b.z).length()
func mouse(pressed: bool, position: Vector2) -> void:
    var event := InputEventMouseButton.new()
    event.button_index = MOUSE_BUTTON_LEFT
    event.pressed = pressed
    event.position = position
    event.global_position = position
    root.push_input(event, true)
func motion(position: Vector2) -> void:
    var event := InputEventMouseMotion.new()
    event.position = position
    event.global_position = position
    event.button_mask = MOUSE_BUTTON_MASK_LEFT
    root.push_input(event, true)
func reset(point: Vector3) -> void:
    mouse(false, Vector2.ZERO)
    Input.action_release("move_left")
    player.CancelDestination()
    player.global_position = point
    player.velocity = Vector3.ZERO
    player.reset_physics_interpolation()
    await steps(rate)
func contained() -> bool:
    var local := world.to_local(player.global_position)
    var capsule: CapsuleShape3D = player.get_node("CollisionShape3D").shape
    return abs(local.x) <= size / 2 - capsule.radius + 0.002 and abs(local.z) <= size / 2 - capsule.radius + 0.002 and local.y > -0.01
func run() -> void:
    world = load("res://scenes/world/World.tscn").instantiate()
    world.position.y = height
    world.get_node("EnvironmentGenerator").TerrainSize = size
    root.add_child(world)
    current_scene = world
    player = world.get_node("FutureActors/Player")
    camera = world.get_node("CameraRig/Camera3D")
    var map: RID = world.get_world_3d().navigation_map
    for attempt in range(300):
        await steps(1)
        if NavigationServer3D.map_get_iteration_id(map) > 0 and not NavigationServer3D.map_get_path(map,
            world.to_global(Vector3(0, 0, 5)), world.to_global(Vector3.ZERO), true).is_empty(): break
        OS.delay_msec(1)
    var half := size / 2
    var directions := [Vector3.RIGHT, Vector3.LEFT, Vector3.BACK, Vector3.FORWARD,
        Vector3(1, 0, 1), Vector3(-1, 0, 1), Vector3(1, 0, -1), Vector3(-1, 0, -1)]
    for index in range(8):
        var direction: Vector3 = directions[index]
        var inside := NavigationServer3D.map_get_closest_point(map, world.to_global(direction * (half - 3)))
        inside.y = height + 0.12
        await reset(inside)
        var outside := world.to_global(direction * (half + 3))
        var screen := camera.unproject_position(outside)
        check(root.get_visible_rect().has_point(screen), "outside request remains inside viewport " + str(index))
        var origin := camera.project_ray_origin(screen)
        var ray := PhysicsRayQueryParameters3D.create(origin, origin + camera.project_ray_normal(screen) * camera.far, 1)
        var hit := world.get_world_3d().direct_space_state.intersect_ray(ray)
        check(hit.is_empty(), "outside ray has no terrain hit " + str(index))
        mouse(true, screen)
        await steps(1)
        mouse(false, screen)
        await steps(1)
        var expected := NavigationServer3D.map_get_closest_point(map, outside)
        check(player.HasDestination and horizontal(player.Destination, expected) < 0.05,
            "plane fallback selects nearest safe edge/corner " + str(index))
        var destination: Vector3 = player.Destination
        var arrived := false
        var safe := true
        for tick in range(rate * 8):
            await steps(1)
            safe = safe and contained()
            if not player.HasDestination:
                arrived = horizontal(player.global_position, destination) <= player.ArrivalDistance + 0.03
                break
        check(arrived and safe and player.is_on_floor(), "outside click arrives within physical boundaries " + str(index))
        check(player.Destination.is_equal_approx(destination), "release retains projected destination " + str(index))

        # Start inside, then drag a held button past the terrain border toward this edge/corner.
        await reset(inside)
        var first_goal := NavigationServer3D.map_get_closest_point(map, player.global_position - direction.normalized())
        first_goal.y = height
        var first := camera.unproject_position(first_goal)
        mouse(true, first)
        await steps(1)
        check(player.HasDestination, "held movement begins on navigable ground " + str(index))
        var before: Vector3 = player.Destination
        screen = camera.unproject_position(outside)
        motion(screen)
        await steps(rate / 3)
        if not player.HasDestination or horizontal(player.Destination, before) <= 0.5:
            print("HOLD_DIAGNOSTIC index=", index, " before=", before, " after=", player.Destination, " player=", player.global_position, " inside=", inside, " screen=", screen)
        check(player.HasDestination and horizontal(player.Destination, before) > 0.5,
            "held drag continues beyond terrain " + str(index))
        var held_destination: Vector3 = player.Destination
        safe = true
        for tick in range(rate * 3):
            await steps(1)
            safe = safe and contained()
        check(safe and player.is_on_floor(), "held outside request stays contained " + str(index))
        if index >= 4:
            check(horizontal(player.Destination, held_destination) < 0.01,
                "corner destination stable while camera follows " + str(index))
            var at_rest := player.global_position
            await steps(rate)
            check(horizontal(player.global_position, at_rest) < 0.01, "no corner arrival jitter " + str(index))
        mouse(false, screen)
        await steps(1)
        held_destination = player.Destination
        await steps(rate / 3)
        check(player.Destination.is_equal_approx(held_destination), "release freezes held outside request " + str(index))

    await reset(world.to_global(Vector3(0, 0.12, 5)))
    check(player.TrySetDestination(world.to_global(Vector3.ZERO)), "normal in-terrain order preserved")
    var saved: Vector3 = player.Destination
    check(not player.TrySetScreenDestination(Vector2(-1, -1)) and player.Destination.is_equal_approx(saved),
        "outside viewport ignored without losing valid goal")
    check(not player.TrySetScreenDestination(Vector2(INF, 0)), "non-finite screen request ignored")
    check(not player.TrySetProjectedDestination(Vector3(INF, height, 0)), "non-finite projected request ignored")
    mouse(true, camera.unproject_position(world.to_global(Vector3(2, 0, 3))))
    await steps(2)
    saved = player.Destination
    motion(Vector2(-10, -10))
    await steps(rate / 3)
    check(player.Destination.is_equal_approx(saved), "held cursor outside viewport preserves last valid goal")
    mouse(false, Vector2(-10, -10))
    var invalid_camera := Camera3D.new()
    invalid_camera.physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
    root.add_child(invalid_camera)
    invalid_camera.global_position = Vector3(half + 10, height + 10, half + 10)
    player.MovementCamera = invalid_camera
    var center := root.get_visible_rect().size / 2
    invalid_camera.look_at(invalid_camera.global_position + Vector3.FORWARD)
    check(not player.TrySetScreenDestination(center), "parallel plane ray ignored")
    invalid_camera.look_at(invalid_camera.global_position + Vector3(0, -0.00001, -1))
    check(not player.TrySetScreenDestination(center), "nearly parallel plane ray ignored")
    invalid_camera.look_at(invalid_camera.global_position + Vector3(0, 1, -1))
    check(not player.TrySetScreenDestination(center), "plane intersection behind ray ignored")
    check(player.Destination.is_equal_approx(saved), "invalid ray orientations retain previous goal")
    invalid_camera.look_at(invalid_camera.global_position + Vector3(0, -1, -1))
    check(player.TrySetScreenDestination(center), "downward ray positive control projects successfully")
    saved = player.Destination
    player.MaximumPlaneRayDistance = 1.0
    check(not player.TrySetScreenDestination(center), "excessive plane intersection distance ignored")
    player.MaximumPlaneRayDistance = 1000.0
    player.MovementCamera = camera
    invalid_camera.queue_free()
    check(player.Destination.is_equal_approx(saved), "invalid plane requests retain previous goal")
    mouse(true, camera.unproject_position(world.to_global(Vector3(2, 0, 3))))
    await steps(2)
    Input.action_press("move_left")
    await steps(2)
    check(not player.HasDestination, "WASD cancels projected mouse movement")
    Input.action_release("move_left")
    mouse(false, Vector2.ZERO)
    print("Unrestricted mouse checks: ", failures, " failures; size=", size, " height=", height, " Hz=", rate)
    quit(0 if failures == 0 else 1)
