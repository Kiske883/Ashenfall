extends SceneTree

# Shared-scene regression: both generated gates, each direction, with optional yaw.
var failures := 0
var yaw := 0.0
func _initialize() -> void:
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--yaw="): yaw = float(arg.get_slice("=", 1))
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
func crosses_opening(gate: Node3D, path: PackedVector3Array) -> bool:
    for index in range(1, path.size()):
        var a := gate.to_local(path[index - 1])
        var b := gate.to_local(path[index])
        if a.z * b.z <= 0 and abs(b.z - a.z) > 0.0001:
            var x := lerpf(a.x, b.x, -a.z / (b.z - a.z))
            return abs(x) < 0.5
    return false
func run() -> void:
    var world = load("res://scenes/world/World.tscn").instantiate()
    root.add_child(world)
    current_scene = world
    # Runs before the one deferred startup bake; no production regeneration needed.
    for gate_name in ["WesternRuin", "EasternRuin"]:
        world.get_node("EnvironmentGenerator/" + gate_name).rotation.y = deg_to_rad(yaw)
    await steps(60)
    var map: RID = world.get_world_3d().navigation_map
    # Headless fixed-FPS can outrun the asynchronous initial navigation publication.
    for attempt in range(300):
        var ready_map: RID = world.get_world_3d().navigation_map
        if NavigationServer3D.map_get_iteration_id(ready_map) > 0 and not NavigationServer3D.map_get_path(ready_map, Vector3(0, 0, 5), Vector3.ZERO, true).is_empty(): break
        OS.delay_msec(1)
        await steps(1)
    var player: CharacterBody3D = world.get_node("FutureActors/Player")
    for gate_name in ["WesternRuin", "EasternRuin"]:
        var gate: Node3D = world.get_node("EnvironmentGenerator/" + gate_name)
        for side in [-1, 1]:
            var label := "%s yaw=%s side=%s" % [gate_name, yaw, side]
            var start := gate.to_global(Vector3(0, 0.12, side * 2))
            var goal := gate.to_global(Vector3(0, 0, -side * 2))
            var path := NavigationServer3D.map_get_path(map, start, goal, true)
            check(path.size() >= 2 and crosses_opening(gate, path), label + " path crosses central opening")
            var contact := KinematicCollision3D.new()
            check(not player.test_move(Transform3D(Basis.IDENTITY, start),
                gate.global_basis * Vector3(0, 0, -side * 4), contact), label + " capsule fits through opening")
            player.CancelDestination()
            player.global_position = start
            player.velocity = Vector3.ZERO
            player.reset_physics_interpolation()
            await steps(20)
            check(player.TrySetDestination(goal), label + " order accepted")
            var previous := gate.to_local(player.global_position)
            var crossed := false
            var side_contacts := 0
            for tick in range(240):
                await steps(1)
                var current := gate.to_local(player.global_position)
                if previous.z * current.z <= 0 and abs(current.x) < 0.5: crossed = true
                previous = current
                for index in range(player.get_slide_collision_count()):
                    var collider = player.get_slide_collision(index).get_collider()
                    if collider is Node and not collider.is_in_group("navigable_ground"): side_contacts += 1
                if not player.HasDestination: break
            var difference := player.global_position - goal
            difference.y = 0
            check(crossed and difference.length() <= player.ArrivalDistance + 0.08,
                label + " agent walks through opening and arrives")
            check(side_contacts == 0, label + " no stone/obstacle contacts on crossing")
        for part in ["LeftPillar", "RightPillar"]:
            var pillar: Node3D = gate.get_node(part)
            var local_start := Vector3(pillar.position.x, 0.5, 2)
            var contact := KinematicCollision3D.new()
            var blocked := player.test_move(Transform3D(Basis.IDENTITY, gate.to_global(local_start)),
                gate.global_basis * Vector3(0, 0, -4), contact)
            check(blocked and contact.get_collider() == pillar.get_node("Collision"),
                "%s yaw=%s %s stays solid" % [gate_name, yaw, part])
            var point := pillar.global_position
            point.y = 0
            check(not player.TrySetDestination(point), "%s %s excluded from navigation" % [gate_name, part])
    print("Gate navigation checks: ", failures, " failures at yaw ", yaw)
    quit(0 if failures == 0 else 1)
