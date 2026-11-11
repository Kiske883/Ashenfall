extends SceneTree

# Integration checks against the real world/controller. No external test dependencies.
# Optional --render-preview saves a separate image without replacing Milestone 01 evidence.
var failures := 0
var rate := 60
var player: CharacterBody3D
var rig: Node3D
var camera: Camera3D

func _initialize() -> void:
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--physics-rate="):
            rate = int(arg.get_slice("=", 1))
    Engine.physics_ticks_per_second = rate
    call_deferred("run_checks")

func check(condition: bool, description: String) -> void:
    if condition:
        print("PASS: ", description)
    else:
        failures += 1
        push_error("FAIL: " + description)

func steps(count: int) -> void:
    for step in range(count):
        await physics_frame
        await process_frame

func release_input() -> void:
    for action in ["move_up", "move_down", "move_left", "move_right"]:
        Input.action_release(action)

func reset_player(point: Vector3) -> void:
    release_input()
    player.global_position = point
    player.velocity = Vector3.ZERO
    player.reset_physics_interpolation()
    await steps(rate / 3)

func run_checks() -> void:
    var world = load("res://scenes/world/World.tscn").instantiate()
    root.add_child(world)
    current_scene = world
    player = world.get_node("FutureActors/Player")
    rig = world.get_node("CameraRig")
    camera = rig.get_node("Camera3D")
    var bindings := {"move_up": KEY_W, "move_down": KEY_S, "move_left": KEY_A, "move_right": KEY_D}
    for action in bindings:
        var events := InputMap.action_get_events(action)
        check(events.size() == 1 and events[0] is InputEventKey
            and events[0].physical_keycode == bindings[action], "physical key binding: " + action)
    await steps(rate / 2)
    # Headless fixed-FPS can outrun the asynchronous initial navigation publication.
    for attempt in range(300):
        var ready_map: RID = world.get_world_3d().navigation_map
        if NavigationServer3D.map_get_iteration_id(ready_map) > 0 and not NavigationServer3D.map_get_path(ready_map, Vector3(0, 0, 5), Vector3.ZERO, true).is_empty(): break
        OS.delay_msec(1)
        await steps(1)
    var environment = world.get_node("EnvironmentGenerator")
    var trees := 0
    var rocks := 0
    for child in environment.get_children():
        if str(child.name).begins_with("Tree_"): trees += 1
        if str(child.name).begins_with("Rock_"): rocks += 1
    check(trees == 10 and rocks == 16, "seeded environment counts preserved")
    check(player.is_on_floor() and abs(player.position.y) < 0.03, "spawn settles on ground")
    check(player.get_slide_collision_count() <= 1, "spawn has no side obstacle contacts")
    check(camera.projection == Camera3D.PROJECTION_ORTHOGONAL, "orthographic projection preserved")
    var original_basis := camera.global_basis
    var back := camera.global_basis.z
    back.y = 0
    back = back.normalized()
    var right := camera.global_basis.x
    right.y = 0
    right = right.normalized()
    var start := player.global_position
    Input.action_press("move_up")
    await steps(rate / 2)
    release_input()
    var displacement := player.global_position - start
    displacement.y = 0
    var straight_distance := displacement.length()
    check(abs(straight_distance - 2.5) < 0.12, "W covers 2.5 units in half a second")
    check(displacement.normalized().dot(-back) > 0.99, "W is relative to camera ground projection")
    check(rig.global_position.distance_to(player.global_position + Vector3(0, 0.9, 0)) < 0.8,
        "camera follows with bounded lag")
    var desired_angle := atan2(back.x, back.z)
    check(abs(angle_difference(player.get_node("Visual").rotation.y, desired_angle)) < 0.1,
        "visual turns toward movement")
    await reset_player(Vector3(-0.8, 0.12, 5))
    start = player.global_position
    Input.action_press("move_up")
    Input.action_press("move_right")
    await steps(rate / 2)
    release_input()
    displacement = player.global_position - start
    displacement.y = 0
    check(abs(displacement.length() - straight_distance) < 0.12, "diagonal speed is normalized")
    check(displacement.normalized().dot((-back + right).normalized()) > 0.99,
        "diagonal follows camera axes")
    start = player.global_position
    await steps(rate / 2)
    check(player.global_position.distance_to(start) < 0.03, "no idle drift")
    check(rig.global_position.distance_to(player.global_position + Vector3(0, 0.9, 0)) < 0.05,
        "camera settles near actor center")

    # Approach real props head-on; confirm MoveAndSlide reports their bodies and never crosses them.
    # Sweep the real player capsule against every generated trunk. Multiple approach
    # angles avoid unrelated overlapping props; a round trunk may legally slide a body around it.
    for index in range(10):
        var tree = environment.get_node("Tree_%02d" % index)
        var ignored: Array[PhysicsBody3D] = []
        for body in environment.find_children("*", "StaticBody3D", true, false):
            if not tree.is_ancestor_of(body):
                player.add_collision_exception_with(body)
                ignored.append(body)
        var detected := false
        for angle_index in range(16):
            var angle := angle_index * TAU / 16.0
            var offset := Vector3(cos(angle), 0, sin(angle))
            var from := Transform3D(Basis.IDENTITY, tree.global_position + offset * 1.2 + Vector3(0, 0.001, 0))
            var contact := KinematicCollision3D.new()
            if player.test_move(from, -offset * 1.2, contact):
                if tree.is_ancestor_of(contact.get_collider()): detected = true
            if detected: break
        for body in ignored: player.remove_collision_exception_with(body)
        check(detected, "player capsule sweep blocks at " + str(tree.name))

    # Exercise the controller against unobstructed approach paths in the actual scene.
    for prop in [environment.get_node("Tree_00"), environment.get_node("Rock_00"),
            environment.get_node("WesternRuin/LeftBase")]:
        var center: Vector3 = prop.global_position
        center.y = 0.12
        await reset_player(center + back * 3.0)
        Input.action_press("move_up")
        var contacted := false
        for tick in range(rate):
            await steps(1)
            for index in range(player.get_slide_collision_count()):
                var collider = player.get_slide_collision(index).get_collider()
                if collider != null and prop.is_ancestor_of(collider): contacted = true
        release_input()
        check(contacted, "collision with " + str(prop.name))
        check((player.global_position - center).dot(back) > 0.3, "cannot pass through " + str(prop.name))
    check(camera.global_basis.is_equal_approx(original_basis), "camera orientation stays fixed")

    # Non-default azimuth must still drive screen-relative input.
    camera.rotate_y(deg_to_rad(35))
    await reset_player(Vector3(0, 0.12, 5))
    right = camera.global_basis.x
    right.y = 0
    right = right.normalized()
    start = player.global_position
    Input.action_press("move_right")
    await steps(rate / 3)
    release_input()
    displacement = player.global_position - start
    displacement.y = 0
    check(displacement.normalized().dot(right) > 0.99, "movement respects a changed camera azimuth")
    camera.global_basis = original_basis
    await reset_player(Vector3(-0.8, 0.12, 5))
    await steps(rate)
    if "--render-preview" in OS.get_cmdline_user_args():
        await RenderingServer.frame_post_draw
        var error := root.get_texture().get_image().save_png("res://docs/milestone_02_preview.png")
        check(error == OK, "render preview saved")
    print("Milestone 02 integration checks: ", failures, " failures at ", rate, " physics Hz")
    quit(0 if failures == 0 else 1)
