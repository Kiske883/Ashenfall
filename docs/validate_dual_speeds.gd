extends "res://docs/validate_enemy_pursuit.gd"

func run_checks() -> void:
    world = load("res://scenes/world/World.tscn").instantiate()
    root.add_child(world); current_scene = world
    player = world.get_node("FutureActors/Player"); player.InputEnabled = false
    await steps(60)
    place_player(Vector3(0,0,8))
    await spawn(Vector3.ZERO)
    var driver: Node = enemy.get_node("AnimationController")
    var animator: AnimationPlayer = enemy.get_node("Visual/AnimationPlayer")
    check(is_equal_approx(enemy.MovementSpeed,1) and is_equal_approx(enemy.ReturnSpeed,0.7),"actual prefab defaults: Chase 1.0 / Return 0.7")
    check(enemy.State == 0 and driver.ActiveClip == "Idle","default Idle preserved")
    place_player(Vector3(0,0,4))
    await steps(60)
    check(enemy.State == 1 and driver.ActiveClip == "Walk" and absf(driver.ActualHorizontalSpeed-1)<0.02,"detection and Chase use 1 m/s")
    var phase_before := animator.current_animation_position
    var previous := enemy.global_position
    enemy.PlayerTarget = null
    var continuous := true
    var poses := {}
    for i in 20:
        await steps(1)
        continuous = continuous and distance(previous,enemy.global_position) <= 0.7/60.0+0.0001
        previous = enemy.global_position
        for node: Node3D in enemy.find_children("*","Node3D",true,false):
            var rotation := node.basis.get_rotation_quaternion()
            if poses.has(node): continuous = continuous and poses[node].angle_to(rotation)<0.7
            poses[node] = rotation
    check(enemy.State == 3 and driver.ActiveClip == "Walk" and absf(driver.ActualHorizontalSpeed-0.7)<0.02,"target loss switches directly to 0.7 m/s Return")
    check(continuous,"Chase -> Return has bounded displacement and no angular discontinuity")
    check(not is_equal_approx(animator.current_animation_position,phase_before),"Walk keeps progressing across speed change")
    check(await wait_state(0),"return reaches spawn")
    check(driver.ActiveClip == "Idle","return arrival selects Idle")
    var events := []
    enemy.connect("AttackExecuted",func(target: Node3D,sequence: int): events.append([target,sequence,Engine.get_physics_frames()]))
    enemy.PlayerTarget = player; enemy.AttacksEnabled = true
    place_player(Vector3(0,0,3.5))
    check(await wait_state(4),"reacquisition approaches and starts windup")
    var start := Engine.get_physics_frames()
    check(await wait_state(5),"default attack executes")
    check(events.size()==1 and events[0][0]==player and events[0][1]==1 and Engine.get_physics_frames()-start>=35,"0.6 s windup and one authoritative AI event unchanged")
    for i in 150:
        await steps(1)
        if events.size()>=2: break
    check(events.size()==2 and events[1][2]-events[0][2]>=96,"repeated attacks retain 1 s recovery and single events")
    # Tune only Return; Chase's exported value must remain independent.
    enemy.ReturnSpeed = 0.5
    enemy.PlayerTarget = null
    await steps(12)
    check(enemy.State == 3 and is_equal_approx(enemy.MovementSpeed,1) and absf(driver.ActualHorizontalSpeed-0.5)<0.02,"Return export tunes independently of Chase")
    check(await wait_state(0),"custom Return speed still reaches spawn")
    check(events.size()==2 and driver.ActiveClip == "Idle","no stale attack after target loss/return")
    print("Dual movement speed checks: ",failures," failures")
    quit(0 if failures == 0 else 1)
