extends "res://docs/validate_enemy_pursuit.gd"

func run_checks() -> void:
    var baseline: CharacterBody3D = load("res://scenes/enemies/FallenGuardian.tscn").instantiate()
    check(is_equal_approx(baseline.MovementSpeed,1.0) and is_equal_approx(baseline.ReturnSpeed,0.7),"actual enemy defaults to 1 m/s Chase and 0.7 m/s Return")
    baseline.free()
    for trial in [["EnemySlowPursuitTest",0.7],["EnemySlowPursuitAlternativeTest",1.0]]:
        var scenario: Node3D = load("res://scenes/world/"+trial[0]+".tscn").instantiate()
        root.add_child(scenario); current_scene = scenario
        world = scenario.get_node("World")
        player = world.get_node("FutureActors/Player"); player.InputEnabled = false
        enemy = scenario.get_node("FallenGuardian")
        await steps(60)
        # Each inherited world publishes its navigation map asynchronously, especially
        # when two worlds are replaced rapidly by the accelerated headless runner.
        for attempt in 300:
            var map: RID = enemy.get_node("NavigationAgent3D").get_navigation_map()
            if NavigationServer3D.map_get_iteration_id(map) > 0 and not NavigationServer3D.map_get_path(map,enemy.global_position,enemy.SpawnPosition+Vector3(0,0,3.5),true).is_empty(): break
            OS.delay_msec(1)
            await steps(1)
        var driver: Node = enemy.get_node("AnimationController")
        var speed: float = trial[1]
        check(is_equal_approx(enemy.MovementSpeed,speed) and is_equal_approx(enemy.ReturnSpeed,speed),trial[0]+" preserves uniform scene-only comparison speed")
        check(enemy.State == 0 and driver.ActiveClip == "Idle",trial[0]+" starts Idle")
        check(is_equal_approx(enemy.DetectionRadius,6) and is_equal_approx(enemy.AttackRange,1.45) and is_equal_approx(enemy.AttackWindupDuration,0.6) and is_equal_approx(enemy.AttackRecoveryDuration,1.0),"detection/attack settings unchanged")
        var events := []
        enemy.connect("AttackExecuted",func(target: Node3D,sequence: int): events.append([target,sequence,Engine.get_physics_frames()]))
        place_player(enemy.SpawnPosition+Vector3(0,0,3.5))
        await steps(60)
        check(enemy.State == 1 and driver.ActiveClip == "Walk","slow enemy approaches using Walk")
        check(absf(driver.ActualHorizontalSpeed-speed)<0.02 and absf(driver.RequestedWalkPlaybackRate-speed/driver.WalkReferenceSpeed)<0.1,"Walk rate comes from measured displacement at "+str(speed))
        print("SLOW_TRIAL speed=",speed," actual=",driver.ActualHorizontalSpeed," requested=",driver.RequestedWalkPlaybackRate," used=",driver.WalkPlaybackRate)
        check(await wait_state(4,900),"slow enemy reaches range and prepares attack")
        var start := Engine.get_physics_frames()
        check(await wait_state(5),"slow enemy executes attack")
        check(events.size()==1 and events[0][0] == player and events[0][1] == 1 and Engine.get_physics_frames()-start>=35,"authoritative windup and single execution unchanged")
        for i in 150:
            await steps(1)
            if events.size()>=2: break
        check(events.size()==2 and events[1][2]-events[0][2]>=96,"repeated attack cycle timing unchanged")
        place_player(player.global_position+Vector3(0,0,2))
        check(await wait_state(1,150),"retreat resumes pursuit after recovery")
        await steps(8)
        check(driver.ActiveClip == "Walk" and driver.ActualHorizontalSpeed>0.1,"resumed pursuit visibly uses Walk")
        place_player(enemy.SpawnPosition+Vector3(0,0,18))
        await steps(3)
        check(enemy.State == 3 and driver.ActiveClip == "Walk","leash initiates slow return")
        check(await wait_state(0,900),"slow enemy returns to original spawn")
        check(distance(enemy.global_position,enemy.SpawnPosition)<=enemy.ArrivalTolerance and driver.ActiveClip=="Idle","return completes in Idle within existing tolerance")
        scenario.queue_free(); await steps(4)
    print("Slow pursuit checks: ",failures," failures")
    quit(0 if failures == 0 else 1)
