extends "res://docs/validate_enemy_pursuit.gd"

var events: Array = []

func on_attack(target: Node3D, sequence: int) -> void:
    events.append({"frame": Engine.get_physics_frames(), "target": target, "sequence": sequence})
    check(enemy.State == 5, "event observes committed Recovery")
    check(abs(enemy.velocity.x) + abs(enemy.velocity.z) < 0.001, "stationary at event")

func run_checks() -> void:
    Engine.physics_ticks_per_second = 60
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
    place_player(Vector3(0,0,8))
    await spawn(Vector3.ZERO)
    enemy.AttacksEnabled = true
    enemy.connect("AttackExecuted", on_attack)
    await steps(90)
    check(events.is_empty(), "no attacks outside range")
    place_player(Vector3(0,0,4))
    check(await wait_state(4), "Windup begins after pursuit")
    var start := Engine.get_physics_frames()
    var position_at_start := enemy.global_position
    var heading: Vector3 = -enemy.get_node("Visual").global_basis.z
    check(heading.dot((player.global_position-enemy.global_position).normalized()) > 0.97, "faces player before Windup")
    await steps(20)
    check(events.is_empty() and enemy.State == 4, "windup cannot execute early")
    check(distance(position_at_start, enemy.global_position) < 0.001, "windup movement stopped")
    # Tiny motion outside nominal range does not reset the committed cycle.
    place_player(enemy.global_position + Vector3(0,0,1.5))
    check(await wait_state(5), "range change preserves committed windup")
    check(events.size() == 1 and events[0].sequence == 1 and events[0].target == player, "one event with target and monotonic sequence")
    check(events[0].frame - start >= 35, "full configured windup elapsed")
    var executed: int = events[0].frame
    await steps(45)
    check(enemy.State == 5 and events.size() == 1, "recovery prevents extra attacks")
    check(distance(position_at_start, enemy.global_position) < 0.001, "recovery movement stopped")
    check(await wait_state(1), "out-of-range player resumes pursuit after recovery")
    check(Engine.get_physics_frames() - executed >= 59, "full recovery respected")
    place_player(enemy.global_position + Vector3(0,0,1.2))
    check(await wait_state(4), "next windup within range")
    # Keep player in range for repeated complete cycles.
    await steps(240)
    check(events.size() >= 3, "attacks repeat while conditions allow")
    var intervals_ok := true
    for i in range(1, events.size()):
        intervals_ok = intervals_ok and events[i].frame-events[i-1].frame >= 96 and events[i].sequence == i+1
    check(intervals_ok, "one event per windup plus recovery cycle")
    check(await wait_state(4), "windup available for cancellation check")
    var count_before := events.size()
    place_player(Vector3(0,0,18))
    await steps(2)
    check(enemy.State == 3 and events.size() == count_before, "leash immediately cancels Windup without event")
    check(await wait_state(0), "returns to spawn after attack cancellation")
    await steps(90)
    check(events.size() == count_before, "no stale attack after Return or Idle")
    place_player(Vector3(0,0,1.2))
    check(await wait_state(5), "reacquisition can execute new attack")
    count_before = events.size()
    enemy.PlayerTarget = null
    await steps(2)
    check(enemy.State == 3 or enemy.State == 0, "target removal during Recovery safely disengages")
    await steps(90)
    check(events.size() == count_before, "missing target never executes attack")
    enemy.PlayerTarget = player
    place_player(Vector3(0,0,1.2))
    check(await wait_state(4), "windup restarts for disable check")
    count_before = events.size()
    enemy.AttacksEnabled = false
    await steps(120)
    check(enemy.State == 2 and events.size() == count_before, "disabled attacks preserve legacy Hold")
    enemy.AttackRange = 0.1
    check(enemy.EffectiveAttackRange > enemy.StoppingDistance, "conflicting range safely covers stopping distance")
    print("Enemy attack checks: ", failures, " failures")
    quit(0 if failures == 0 else 1)
