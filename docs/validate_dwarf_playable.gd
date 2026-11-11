extends "res://docs/validate_playable_demo.gd"

var driver: Node
var dwarf: Node3D
var axe: Node3D
var max_grip_error := 0.0
var scale_preserved := true
var observed_walk := false
var observed_chase_speed := false
var observed_return_speed := false

func steps(count: int) -> void:
    for i in count:
        await physics_frame
        await process_frame
        if is_instance_valid(enemy):
            var speed := Vector2(enemy.velocity.x, enemy.velocity.z).length()
            observed_chase_speed = observed_chase_speed or (enemy.State == 1 and absf(speed-1)<0.01)
            observed_return_speed = observed_return_speed or (enemy.State == 3 and absf(speed-0.7)<0.01)
        if is_instance_valid(dwarf):
            scale_preserved = scale_preserved and dwarf.scale.is_equal_approx(Vector3.ONE)
            observed_walk = observed_walk or str(dwarf.ActiveClip) == "Walk"
            for side in ["Left", "Right"]:
                var hand: Node3D = dwarf.find_child(side+"Gauntlet", true, false).get_node("GripPoint")
                var grip: Node3D = axe.get_node(side+"Grip")
                max_grip_error = maxf(max_grip_error, hand.global_position.distance_to(grip.global_position))

func capture(label: String) -> void:
    if "--capture" not in OS.get_cmdline_user_args(): return
    check(is_equal_approx(camera.size, 22), "capture uses unmodified gameplay camera size 22")
    await RenderingServer.frame_post_draw
    var prefix := "milestone_06c_legacy_" if "--legacy-five-meter" in OS.get_cmdline_user_args() else "milestone_06c_final_"
    check(root.get_texture().get_image().save_png("res://docs/"+prefix+label+".png") == OK, "gameplay capture "+label)

func reset_actor(point: Vector3) -> void:
    for action in ["move_up", "move_down", "move_left", "move_right"]: Input.action_release(action)
    player.CancelDestination()
    place_player(point)
    driver.ResetMotionTracking()
    await steps(35)

func mouse(pressed: bool, point: Vector3) -> void:
    var event := InputEventMouseButton.new()
    event.button_index = MOUSE_BUTTON_LEFT
    event.position = camera.unproject_position(point); event.global_position = event.position
    event.pressed = pressed; root.push_input(event, true)

func run_checks() -> void:
    root.size = Vector2i(1280,800)
    var legacy := "--legacy-five-meter" in OS.get_cmdline_user_args()
    var speed := 5.0 if legacy else 2.5
    var reference := 0.14/(1.4*0.6) if legacy else 0.5/(0.6*0.55)
    var cycle := 1.4 if legacy else 0.6
    var expected_rate := 2.0 if legacy else 1.65
    var path := "res://scenes/world/AshenfallDwarfFiveMeterComparison.tscn" if legacy else "res://scenes/world/AshenfallDwarfPlayableDemo.tscn"
    demo = load(path).instantiate()
    root.add_child(demo); current_scene = demo
    world = demo.get_node("World"); player = world.get_node("FutureActors/Player")
    enemy = demo.get_node("FallenGuardian"); driver = demo.get_node("DwarfPresentation")
    dwarf = player.get_node("Visual/Dwarf"); axe = dwarf.find_child("BattleAxe", true, false)
    rig = world.get_node("CameraRig"); camera = rig.get_node("Camera3D")
    await steps(60)
    var map := world.get_world_3d().navigation_map
    for i in 300:
        if NavigationServer3D.map_get_iteration_id(map)>0 and not NavigationServer3D.map_get_path(map,player.global_position,enemy.global_position,true).is_empty(): break
        OS.delay_msec(1); await steps(1)
    var baseline: CharacterBody3D = load("res://scenes/characters/Player.tscn").instantiate()
    check(player.get_script() == baseline.get_script() and is_equal_approx(player.MovementSpeed,speed), "Original PlayerController with scene-selected speed: "+str(speed))
    check(is_equal_approx(baseline.MovementSpeed,5), "Original player prefab default remains 5 m/s")
    check(is_equal_approx(driver.WalkReferenceSpeed,reference) and is_equal_approx(dwarf.get_node("AnimationPlayer").get_animation("Walk").length,cycle), "Correct approved animation and calibrated reference selected")
    check(player.get_node("CollisionShape3D").shape == baseline.get_node("CollisionShape3D").shape and player.collision_mask == baseline.collision_mask and player.collision_layer == baseline.collision_layer, "Original player collision resource and layers/masks retained")
    var agent: NavigationAgent3D = player.get_node("NavigationAgent3D")
    check(is_equal_approx(agent.radius,0.32) and is_equal_approx(agent.height,1.9) and not agent.avoidance_enabled, "Original navigation capsule configuration retained")
    baseline.free()
    var hidden := true
    for child in player.get_node("Visual").get_children():
        if child != dwarf and child is Node3D: hidden = hidden and not child.visible
    check(hidden and dwarf.is_visible_in_tree(), "Only placeholder visual hidden; approved dwarf visible")
    check(not dwarf.is_processing() and driver.process_physics_priority > player.process_physics_priority, "One presentation clock samples after player physics")
    check(dwarf.find_child("Model",true,false).get_meta("triangle_count") == 10336, "Approved mesh triangle count retained")
    check(demo.find_children("*","CharacterBody3D",true,false).size()==2 and enemy.PlayerTarget==player, "Same single player and Guardian interaction target")
    check(enemy.State==0 and player.is_on_floor() and enemy.is_on_floor() and str(dwarf.ActiveClip)=="Idle", "Safe grounded spawn, player and enemy idle")
    check(is_equal_approx(enemy.MovementSpeed,1) and is_equal_approx(enemy.ReturnSpeed,0.7) and is_equal_approx(enemy.AttackWindupDuration,0.6) and is_equal_approx(enemy.AttackRecoveryDuration,1), "Enemy speeds and attack timing unchanged")
    check(rig.FollowTarget==player and is_equal_approx(camera.size,22), "Existing camera follows same actor at size 22")
    var orientation := camera.global_basis
    await capture("spawn")
    for action in ["move_up", "move_right", "move_down", "move_left"]:
        await reset_actor(Vector3(-0.8,0.12,5))
        var start := player.global_position
        Input.action_press(action); await steps(30)
        check(absf(distance(player.global_position,start)-speed*0.5)<0.15, action+" moves at configured speed")
        check(str(dwarf.ActiveClip)=="Walk" and absf(driver.ActualHorizontalSpeed-speed)<0.03, action+" selects Walk from real displacement")
        var facing: Vector3 = -player.get_node("Visual").global_basis.z
        var velocity: Vector3 = driver.ActualHorizontalVelocity
        check(facing.dot(velocity.normalized())>0.995, action+" dwarf faces actual movement using local -Z")
        check(absf(driver.RequestedWalkPlaybackRate-speed/reference)<0.01 and driver.WalkRateLimited==legacy and absf(driver.WalkPlaybackRate-expected_rate)<0.03, "Playback follows actual speed with correct saturation policy")
        if action == "move_down": await capture("walk")
        var animator: AnimationPlayer = dwarf.get_node("AnimationPlayer")
        var phase := animator.current_animation_position
        await steps(1)
        var advance := fposmod(animator.current_animation_position-phase,cycle)
        check(absf(advance-driver.WalkPlaybackRate/60)<0.001, "Walk advances once per physics tick at synchronized rate")
        Input.action_release(action); await steps(25)
        check(str(dwarf.ActiveClip)=="Idle" and driver.WalkPlaybackRate==0 and is_equal_approx(animator.speed_scale,1), "Stopping selects unscaled Idle")
    check(camera.global_basis.is_equal_approx(orientation) and rig.global_position.distance_to(player.global_position+rig.FollowOffset)<0.15, "Camera orientation and smooth following preserved")
    # Unsaturated calibration proves the clock responds to actual speed, not a fixed cap.
    await reset_actor(Vector3(-0.8,0.12,5))
    player.MovementSpeed=0.2
    Input.action_press("move_right"); await steps(50)
    check(absf(driver.ActualHorizontalSpeed-0.2)<0.005 and absf(driver.WalkPlaybackRate-0.2/reference)<0.005 and not driver.WalkRateLimited, "Low-speed calibration uses measured motion without saturation")
    Input.action_release("move_right"); player.MovementSpeed=speed; await steps(20)
    # Real boundary contact: actual movement differs from the requested diagonal.
    await reset_actor(Vector3(19.67,0.12,0))
    Input.action_press("move_down"); await steps(35)
    var sliding: Vector3 = driver.ActualHorizontalVelocity
    check(absf(sliding.x)<0.05 and sliding.z>speed*0.6 and (-player.get_node("Visual").global_basis.z).dot(sliding.normalized())>0.99, "Wall-slide facing follows actual direction rather than desired diagonal")
    Input.action_press("move_right"); await steps(35)
    check(driver.ActualHorizontalSpeed<0.02 and str(dwarf.ActiveClip)=="Idle", "Blocked movement selects Idle despite held input")
    check(player.global_position.x<19.7 and player.is_on_floor(), "Original physical boundary keeps dwarf inside terrain")
    Input.action_release("move_right"); Input.action_release("move_down")
    await reset_actor(Vector3(-0.8,0.12,5))
    mouse(true, Vector3(2,0,5)); await steps(10)
    var saved: Vector3 = player.Destination
    var motion := InputEventMouseMotion.new()
    motion.position = camera.unproject_position(Vector3(3,0,3)); motion.global_position = motion.position
    motion.button_mask = MOUSE_BUTTON_MASK_LEFT; root.push_input(motion,true)
    await steps(12)
    check(distance(saved,player.Destination)>1, "Hold-to-move updates mouse destination")
    mouse(false, Vector3(3,0,3)); await steps(1); saved=player.Destination
    await steps(5)
    check(player.Destination.is_equal_approx(saved), "Release retains last mouse destination")
    Input.action_press("move_left"); await steps(2)
    check(not player.HasDestination, "WASD cancellation of mouse movement preserved")
    Input.action_release("move_left")
    await reset_actor(Vector3(-0.8,0.12,5))
    enemy.connect("AttackExecuted",func(target: Node3D,sequence: int): events.append([target,sequence,Engine.get_physics_frames()]))
    var approach := Vector3(0,0,-1.5)
    await click_ground(approach)
    check(player.HasDestination and await settled(approach), "Real ground click navigates dwarf to encounter")
    check(await wait_state(1,120), "Guardian detects and chases dwarf")
    await steps(12)
    check(enemy.get_node("AnimationController").ActiveClip=="Walk", "Guardian retains animated pursuit")
    check(await attack_cycles(2), "Guardian attacks repeatedly against same player target")
    check(events.size()>=2 and events[0][0]==player and events[0][1]==1 and events[1][1]==2 and events[1][2]-events[0][2]>=96, "Single authoritative AI attack events and timing preserved")
    await capture("encounter")
    var retreat := Vector3(0,0,8)
    await click_ground(retreat)
    check(await settled(retreat), "Dwarf retreats with mouse navigation")
    check(await wait_state(0,900), "Guardian returns to spawn and Idle")
    check(distance(enemy.global_position,enemy.SpawnPosition)<=enemy.ArrivalTolerance, "Guardian reaches original spawn")
    await capture("return")
    var first_count := events.size()
    await steps(30); check(events.size()==first_count, "No stale attack during return/idle")
    await click_ground(approach)
    check(await settled(approach) and await attack_cycles(first_count+1), "Second encounter works without scene restart")
    check(sync_ok and camera.global_basis.is_equal_approx(orientation), "Enemy phase synchronization and camera preserved across encounters")
    check(observed_chase_speed and observed_return_speed, "Observed Guardian moving at Chase 1.0 and Return 0.7 m/s")
    check(observed_walk and scale_preserved and max_grip_error<0.00005, "Scale and both axe grips preserved throughout movement, turning and encounters")
    check(str(ProjectSettings.get_setting("application/run/main_scene"))=="res://scenes/world/World.tscn", "Default F5 scene unchanged")
    print("DWARF_PLAYABLE max_grip_error=",max_grip_error," default_speed=",player.MovementSpeed," reference_speed=",driver.WalkReferenceSpeed)
    print("Dwarf playable checks: ",failures," failures")
    quit(0 if failures == 0 else 1)
