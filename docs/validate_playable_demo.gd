extends "res://docs/validate_enemy_pursuit.gd"

var camera: Camera3D
var rig: Node3D
var events := []
var demo: Node3D
var sync_ok := true

func capture(label: String) -> void:
    if "--capture" not in OS.get_cmdline_user_args(): return
    await RenderingServer.frame_post_draw
    check(root.get_texture().get_image().save_png("res://docs/milestone_05c_"+label+".png") == OK,"capture "+label)

func click_ground(point: Vector3) -> void:
    var screen := camera.unproject_position(point)
    check(root.get_visible_rect().has_point(screen),"encounter click is inside viewport")
    var event := InputEventMouseButton.new()
    event.button_index = MOUSE_BUTTON_LEFT; event.position = screen; event.global_position = screen; event.pressed = true
    root.push_input(event,true)
    await steps(1)
    event = InputEventMouseButton.new()
    event.button_index = MOUSE_BUTTON_LEFT; event.position = screen; event.global_position = screen; event.pressed = false
    root.push_input(event,true)

func settled(point: Vector3) -> bool:
    for i in 600:
        await steps(1)
        if not player.HasDestination and distance(player.global_position,point)<0.35: return true
    return false

func attack_cycles(count: int) -> bool:
    var animator: AnimationPlayer = enemy.get_node("Visual/AnimationPlayer")
    var driver: Node = enemy.get_node("AnimationController")
    for i in 900:
        await steps(1)
        if enemy.State in [4,5]:
            var fraction: float = clampf(1-enemy.AttackPhaseRemaining/enemy.AttackPhaseDuration,0,1)
            var expected := fraction*0.6 if enemy.State == 4 else 0.6+fraction
            sync_ok = sync_ok and driver.ActiveClip=="Combat/Attack" and absf(animator.current_animation_position-expected)<0.0001
        if events.size()>=count: return true
    return false

func run_checks() -> void:
    root.size = Vector2i(1280,800)
    demo = load("res://scenes/world/AshenfallPlayableDemo.tscn").instantiate()
    root.add_child(demo); current_scene = demo
    world = demo.get_node("World")
    player = world.get_node("FutureActors/Player")
    enemy = demo.get_node("FallenGuardian")
    rig = world.get_node("CameraRig"); camera = rig.get_node("Camera3D")
    await steps(60)
    var map := world.get_world_3d().navigation_map
    for i in 300:
        if NavigationServer3D.map_get_iteration_id(map)>0 and not NavigationServer3D.map_get_path(map,player.global_position,enemy.global_position,true).is_empty(): break
        OS.delay_msec(1); await steps(1)
    check(player.InputEnabled and enemy.PlayerTarget==player,"real player controls and explicit enemy target wired")
    check(demo.find_children("*","CharacterBody3D",true,false).size()==2,"exactly one player and one enemy")
    check(enemy.State==0 and enemy.get_node("AnimationController").ActiveClip=="Idle" and distance(player.global_position,enemy.global_position)>enemy.DetectionRadius,"safe initial spawn outside detection")
    check(player.is_on_floor() and enemy.is_on_floor(),"both actors grounded on original terrain")
    var player_floor := NavigationServer3D.map_get_closest_point(map,player.global_position)
    var enemy_floor := NavigationServer3D.map_get_closest_point(map,enemy.global_position)
    check(distance(player_floor,player.global_position)<0.1 and distance(enemy_floor,enemy.global_position)<0.1,"both spawns aligned with navigation surface")
    check((player.collision_mask & 9)==9 and (enemy.collision_mask & 9)==9,"static obstacle and terrain boundary collision masks retained")
    check(is_equal_approx(enemy.MovementSpeed,1) and is_equal_approx(enemy.ReturnSpeed,0.7),"approved dual enemy speeds")
    check(is_equal_approx(enemy.AttackWindupDuration,0.6) and is_equal_approx(enemy.AttackRecoveryDuration,1),"approved attack durations")
    check(not enemy.ShowAttackDebug,"no new gameplay/debug UI enabled")
    var environment = world.get_node("EnvironmentGenerator")
    check(environment.Seed==731 and environment.TreeCount==10 and environment.RockCount==16 and is_equal_approx(environment.TerrainSize,40),"original environment configuration preserved")
    check(rig.FollowTarget==player and is_equal_approx(camera.size,22),"original isometric camera follows player")
    var orientation := camera.global_basis
    var start := player.global_position
    var camera_start := rig.global_position
    Input.action_press("move_right"); await steps(12); Input.action_release("move_right")
    await steps(25)
    check(distance(player.global_position,start)>0.5,"WASD moves real player")
    check(distance(rig.global_position,camera_start)>0.4 and rig.global_position.distance_to(player.global_position+rig.FollowOffset)<0.15 and camera.global_basis.is_equal_approx(orientation),"camera tracks smoothly without rotating")
    await capture("start")
    enemy.connect("AttackExecuted",func(target: Node3D,sequence: int): events.append([target,sequence,Engine.get_physics_frames()]))
    var approach := Vector3(0,0,-1.5)
    await click_ground(approach)
    check(player.HasDestination,"mouse click creates real navigation order")
    check(await settled(approach),"player walks along path to encounter")
    check(await wait_state(1,120),"enemy detects and pursues player")
    await steps(12)
    check(enemy.get_node("AnimationController").ActiveClip=="Walk","enemy pursues using Walk")
    check(await attack_cycles(2),"first encounter produces repeated AI attacks")
    check(events.size()>=2 and events[0][0]==player and events[0][1]==1 and events[1][1]==2 and events[1][2]-events[0][2]>=96,"single authoritative events and cooldown semantics retained")
    await capture("encounter")
    var retreat := Vector3(0,0,8)
    await click_ground(retreat)
    check(await settled(retreat),"player retreats on existing terrain using mouse navigation")
    check(await wait_state(0,900),"enemy abandons and returns to spawn")
    check(distance(enemy.global_position,enemy.SpawnPosition)<=enemy.ArrivalTolerance and enemy.get_node("AnimationController").ActiveClip=="Idle","enemy returns home and idles")
    var first_count := events.size()
    await steps(30)
    check(events.size()==first_count,"no stale attack after return")
    await click_ground(approach)
    check(await settled(approach),"same player approaches again without restarting")
    check(await attack_cycles(first_count+1),"second encounter reacquires and attacks")
    check(sync_ok,"AI/animation phase synchronization holds in both encounters")
    check(camera.global_basis.is_equal_approx(orientation),"camera orientation remains unchanged through encounters")
    await capture("repeat")
    print("Playable demo checks: ",failures," failures")
    quit(0 if failures == 0 else 1)
