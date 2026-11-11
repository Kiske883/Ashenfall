extends SceneTree

# Real-world mouse events, ray picks, NavigationAgent paths, and physical boundary checks.
var failures := 0
var rate := 60
var player: CharacterBody3D
var camera: Camera3D
var rig: Node3D
var world: Node3D

func _initialize() -> void:
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--physics-rate="): rate = int(arg.get_slice("=", 1))
    Engine.physics_ticks_per_second = rate
    call_deferred("run_checks")

func check(condition: bool, message: String) -> void:
    if condition: print("PASS: ", message)
    else:
        failures += 1
        push_error("FAIL: " + message)

func steps(count: int) -> void:
    for tick in range(count):
        await physics_frame
        await process_frame

func horizontal(a: Vector3, b: Vector3) -> float:
    return Vector2(a.x - b.x, a.z - b.z).length()

func mouse(pressed: bool, screen: Vector2) -> void:
    var event := InputEventMouseButton.new()
    event.button_index = MOUSE_BUTTON_LEFT
    event.position = screen
    event.global_position = screen
    event.pressed = pressed
    root.push_input(event, true)

func reset_actor(point: Vector3) -> void:
    mouse(false, Vector2.ZERO)
    for action in ["move_up", "move_down", "move_left", "move_right"]: Input.action_release(action)
    player.CancelDestination()
    player.global_position = point
    player.velocity = Vector3.ZERO
    player.reset_physics_interpolation()
    await steps(rate)

func wait_for_arrival(expected: Vector3, label: String) -> void:
    var arrived := false
    for tick in range(rate * 8):
        await steps(1)
        if not player.HasDestination:
            arrived = horizontal(player.global_position, expected) <= player.ArrivalDistance + 0.08
            break
    if not arrived:
        var agent = player.get_node("NavigationAgent3D")
        print("ARRIVAL_DIAGNOSTIC ", label, " pos=", player.global_position, " expected=", expected, " active=", player.HasDestination, " path=", agent.get_current_navigation_path(), " final=", agent.get_final_position())
    check(arrived, label)

func run_checks() -> void:
    world = load("res://scenes/world/World.tscn").instantiate()
    root.add_child(world)
    current_scene = world
    player = world.get_node("FutureActors/Player")
    rig = world.get_node("CameraRig")
    camera = rig.get_node("Camera3D")
    await steps(rate)
    var map: RID = player.get_node("NavigationAgent3D").get_navigation_map()
    # Headless fixed-FPS can outrun the asynchronous initial navigation publication.
    for attempt in range(300):
        var ready_map: RID = world.get_world_3d().navigation_map
        if NavigationServer3D.map_get_iteration_id(ready_map) > 0 and not NavigationServer3D.map_get_path(ready_map, Vector3(0, 0, 5), Vector3.ZERO, true).is_empty(): break
        OS.delay_msec(1)
        await steps(1)
    var original_basis := camera.global_basis
    check(NavigationServer3D.map_get_iteration_id(map) > 0, "navigation map synchronized")
    var region: NavigationRegion3D = world.get_node("WorldNavigation")
    check(region.navigation_mesh.get_polygon_count() > 0, "generated navigation polygons exist")
    var initial_iteration := NavigationServer3D.map_get_iteration_id(map)
    var binding := InputMap.action_get_events("move_to_cursor")
    check(binding.size() == 1 and binding[0] is InputEventMouseButton and binding[0].button_index == MOUSE_BUTTON_LEFT,
        "left mouse InputMap action")

    var goal := Vector3(0, 0, 0)
    var screen := camera.unproject_position(goal)
    mouse(true, screen)
    await steps(1)
    mouse(false, screen)
    await steps(1)
    check(player.HasDestination and horizontal(player.Destination, goal) < 0.1, "left click raycasts ground and sets goal")
    var saved: Vector3 = player.Destination
    await steps(rate / 2)
    check(player.Destination.is_equal_approx(saved) and player.HasDestination, "release preserves the last goal")
    var camera_error := rig.global_position.distance_to(player.global_position + Vector3(0, 0.9, 0))
    check(camera_error < 0.8, "camera follows during mouse movement")
    await wait_for_arrival(saved, "click destination reached")
    var stopped := player.global_position
    await steps(rate / 2)
    check(horizontal(stopped, player.global_position) < 0.02, "arrival stops without jitter")

    await reset_actor(Vector3(-0.8, 0.12, 5))
    screen = camera.unproject_position(Vector3(0, 0, -1))
    mouse(true, screen)
    await steps(rate / 4)
    saved = player.Destination
    var motion := InputEventMouseMotion.new()
    motion.position = camera.unproject_position(Vector3(3, 0, 3))
    motion.global_position = motion.position
    motion.button_mask = MOUSE_BUTTON_MASK_LEFT
    root.push_input(motion, true)
    await steps(rate / 4)
    check(horizontal(player.Destination, saved) > 1, "held mouse updates the goal after cursor motion")
    mouse(false, motion.position)
    await steps(1)
    saved = player.Destination
    await steps(rate / 4)
    check(player.Destination.is_equal_approx(saved), "releasing hold freezes the last valid goal")
    screen = camera.unproject_position(Vector3(-2, 0, 4))
    mouse(true, screen)
    await steps(1)
    mouse(false, screen)
    await steps(1)
    check(horizontal(player.Destination, saved) > 1, "a later click replaces the previous goal")
    Input.action_press("move_left")
    await steps(2)
    check(not player.HasDestination, "WASD cancels the mouse goal")
    Input.action_release("move_left")
    await steps(2)
    check(not player.HasDestination, "old mouse goal does not resume after WASD")

    await reset_actor(Vector3(-0.8, 0.12, 5))
    check(player.TrySetDestination(Vector3(0, 0, 0)), "accept a reachable reference order")
    saved = player.Destination
    check(not player.TrySetDestination(Vector3(40, 0, 40)), "outside-terrain order rejected")
    check(not player.TrySetDestination(Vector3(0, 20, 0)), "off-surface order rejected")
    check(not player.TrySetScreenDestination(Vector2(-20, -20)), "off-viewport ray rejected")
    var empty_camera := Camera3D.new()
    empty_camera.physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
    root.add_child(empty_camera)
    empty_camera.global_position = Vector3(60, 40, 90)
    empty_camera.look_at(Vector3(60, 0, 60))
    empty_camera.force_update_transform()
    player.MovementCamera = empty_camera
    check(player.TrySetScreenDestination(root.get_visible_rect().size / 2), "ray without ground hit projects to safe navigation")
    player.MovementCamera = camera
    empty_camera.queue_free()
    check(player.TrySetDestination(Vector3(0, 0, 0)), "restore reference order after plane fallback")
    saved = player.Destination
    var tree = world.get_node("EnvironmentGenerator/Tree_02")
    var blocked: Vector3 = tree.global_position
    blocked.y = 0
    check(not player.TrySetDestination(blocked), "tree footprint is excluded from navigation")
    check(player.Destination.is_equal_approx(saved), "invalid orders retain the last valid destination")
    var trunk_screen := camera.unproject_position(tree.global_position + Vector3(0, 1, 0))
    var trunk_order: bool = player.TrySetScreenDestination(trunk_screen)
    if trunk_order:
        var trunk_radius: float = tree.get_node("Collision/TrunkShape").shape.radius * tree.global_basis.get_scale().x
        check(horizontal(player.Destination, tree.global_position) >= trunk_radius + 0.32,
            "plane fallback near a trunk stays outside solid geometry")
    else:
        check(player.Destination.is_equal_approx(saved), "invalid trunk pick preserves reference order")

    # Follow complete paths whose straight line crosses real collision geometry.
    for prop in [tree, world.get_node("EnvironmentGenerator/Rock_00"),
            world.get_node("EnvironmentGenerator/WesternRuin/LeftBase")]:
        var center: Vector3 = prop.global_position
        center.y = 0
        var from := NavigationServer3D.map_get_closest_point(map, center + Vector3(-2.2, 0, 0))
        var to := NavigationServer3D.map_get_closest_point(map, center + Vector3(2.2, 0, 0))
        await reset_actor(Vector3(from.x, 0.12, from.z))
        var path := NavigationServer3D.map_get_path(map, from, to, true)
        check(path.size() >= 3, "path bends around " + str(prop.name))
        check(player.TrySetDestination(to), "order accepted across " + str(prop.name))
        await wait_for_arrival(to, "walks around " + str(prop.name))
        check(player.is_on_floor(), "stays grounded around " + str(prop.name))

    # All four perimeter sides also stop secondary keyboard control.
    var axes := [Vector3.RIGHT, Vector3.LEFT, Vector3.BACK, Vector3.FORWARD]
    var action_pairs := [["move_right", "move_down"], ["move_left", "move_up"],
        ["move_left", "move_down"], ["move_right", "move_up"]]
    for index in range(4):
        await reset_actor(axes[index] * 18.8 + Vector3(0, 0.12, 0))
        for action in action_pairs[index]: Input.action_press(action)
        await steps(rate)
        for action in action_pairs[index]: Input.action_release(action)
        check(abs(player.global_position.x) <= 19.7 and abs(player.global_position.z) <= 19.7
            and player.is_on_floor(), "keyboard contained at boundary " + str(index))
    check(NavigationServer3D.map_get_iteration_id(map) == initial_iteration, "navigation not rebuilt while moving")
    check(camera.global_basis.is_equal_approx(original_basis), "mouse movement never rotates camera")
    await reset_actor(Vector3(-0.8, 0.12, 5))
    await steps(rate)
    check(rig.global_position.distance_to(player.global_position + Vector3(0, 0.9, 0)) < 0.02,
        "camera settles smoothly at the player")

    # Temporary disconnected test island: a point really on navigation, but unreachable
    # from the world. This fixture never changes the saved scene or terrain bake.
    NavigationServer3D.map_set_use_async_iterations(map, false)
    var island_mesh := NavigationMesh.new()
    island_mesh.cell_size = float(ProjectSettings.get_setting("navigation/3d/default_cell_size"))
    island_mesh.cell_height = 0.1
    island_mesh.vertices = PackedVector3Array([Vector3(25, 0, 25), Vector3(25, 0, 27),
        Vector3(27, 0, 27), Vector3(27, 0, 25)])
    island_mesh.add_polygon(PackedInt32Array([0, 1, 2, 3]))
    var island := NavigationRegion3D.new()
    island.navigation_mesh = island_mesh
    root.add_child(island)
    await steps(rate)
    NavigationServer3D.map_force_update(map)
    await steps(rate)
    check(player.TrySetDestination(Vector3(0, 0, 0)), "valid goal before disconnected request")
    saved = player.Destination
    check(NavigationServer3D.map_get_closest_point(map, Vector3(26, 0, 26)).distance_to(Vector3(26, 0, 26)) < 0.01,
        "unreachable fixture is a real navigation surface")
    check(not player.TrySetDestination(Vector3(26, 0, 26)) and player.Destination.is_equal_approx(saved),
        "disconnected destination is rejected without losing valid goal")
    check(not player.TrySetProjectedDestination(Vector3(26, 0, 26)) and player.Destination.is_equal_approx(saved),
        "disconnected plane projection is rejected without losing valid goal")
    island.queue_free()
    await steps(4)
    print("Mouse/navigation checks: ", failures, " failures at ", rate, " Hz")
    quit(0 if failures == 0 else 1)
