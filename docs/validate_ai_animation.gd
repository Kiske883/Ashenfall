extends "res://docs/validate_enemy_pursuit.gd"

var events := []
var driver: Node
var animator: AnimationPlayer
var sync_ok := true
var attachments_ok := true
var shield_bind: Transform3D
var previous_rotations := {}
var previous_frame := -1
var continuous := true
func capture(label: String) -> void:
    if "--capture" not in OS.get_cmdline_user_args(): return
    await RenderingServer.frame_post_draw
    check(root.get_texture().get_image().save_png("res://docs/milestone_05_ai_animation_"+label+".png") == OK,"capture integrated "+label)
func animation_step() -> void:
    await steps(1)
    if enemy.State in [4,5]:
        var fraction: float = clampf(1.0-enemy.AttackPhaseRemaining/enemy.AttackPhaseDuration,0,1)
        var expected := fraction*0.6 if enemy.State == 4 else 0.6+fraction
        sync_ok = sync_ok and driver.ActiveClip == "Combat/Attack" and absf(animator.current_animation_position-expected) < 0.0001
    var wrist: Node3D = enemy.find_child("RightWristPivot",true,false)
    var sword: Node3D = enemy.find_child("Sword",true,false)
    var shield: Node3D = enemy.find_child("Shield",true,false)
    var forearm: Node3D = enemy.find_child("LeftElbowPivot",true,false)
    attachments_ok = attachments_ok and sword.to_global(Vector3(0.007,-0.032,-0.005)).distance_to(wrist.to_global(Vector3(0.006,-0.084,-0.028))) < 0.0001
    attachments_ok = attachments_ok and shield_bind.is_equal_approx(forearm.global_transform.affine_inverse()*shield.global_transform)
    var frame := Engine.get_physics_frames()
    for node: Node3D in enemy.find_children("*","Node3D",true,false):
        var rotation := node.basis.get_rotation_quaternion()
        if frame == previous_frame+1 and previous_rotations.has(node):
            continuous = continuous and previous_rotations[node].angle_to(rotation) < 0.7
        previous_rotations[node] = rotation
    previous_frame = frame

func run_checks() -> void:
    world = load("res://scenes/world/World.tscn").instantiate()
    root.add_child(world); current_scene = world
    player = world.get_node("FutureActors/Player"); player.InputEnabled = false
    await steps(60)
    var map := world.get_world_3d().navigation_map
    for i in 300:
        if NavigationServer3D.map_get_iteration_id(map) > 0: break
        OS.delay_msec(1); await steps(1)
    place_player(Vector3(0,0,8))
    await spawn(Vector3.ZERO)
    driver = enemy.get_node("AnimationController")
    animator = enemy.get_node("Visual/AnimationPlayer")
    var shield: Node3D = enemy.find_child("Shield",true,false)
    var forearm: Node3D = enemy.find_child("LeftElbowPivot",true,false)
    shield_bind = forearm.global_transform.affine_inverse()*shield.global_transform
    check(driver.ActiveClip == "Idle", "stationary enemy starts Idle")
    await capture("idle")
    check(animator.callback_mode_process == AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL,"presentation uses one manual physics clock")
    check(enemy.collision_layer == 2 and enemy.collision_mask == 11 and is_equal_approx(enemy.get_node("CollisionShape3D").shape.radius,0.32),"original actor collision configuration")
    check(is_equal_approx(enemy.get_node("NavigationAgent3D").radius,0.32) and not enemy.get_node("NavigationAgent3D").avoidance_enabled,"original navigation agent configuration")
    place_player(Vector3(0,0,4))
    await steps(8)
    check(enemy.State == 1 and driver.ActiveClip == "Walk","chasing plays Walk")
    await capture("walk")
    enemy.MovementSpeed = 0
    await steps(6)
    check(enemy.State == 1 and driver.ActiveClip == "Idle","stationary blocked/debug chase does not walk in place")
    enemy.MovementSpeed = 3
    await steps(6)
    check(driver.ActiveClip == "Walk","actual movement resumes Walk")
    check(await wait_state(2),"arrives in legacy Hold with attacks disabled")
    check(driver.ActiveClip == "Idle","Hold uses Idle")
    enemy.connect("AttackExecuted",func(target: Node3D,sequence: int): events.append([target,sequence,Engine.get_physics_frames()]))
    enemy.AttacksEnabled = true
    check(await wait_state(4),"AI starts windup")
    var starts: int = driver.AttackPlaybackStarts
    var cycle: int = enemy.AttackCycleId
    for i in 12: await animation_step()
    check(driver.AttackPlaybackStarts == starts and events.is_empty(),"windup does not restart every frame or emit early events")
    await capture("windup")
    for i in 120:
        await animation_step()
        if enemy.State == 5: break
    check(enemy.State == 5 and events.size() == 1 and events[0][0] == player and events[0][1] == 1,"AI emits exactly one authoritative execution event")
    check(is_equal_approx(animator.current_animation_position,0.6),"execution transition lands at visual strike onset 0.6")
    await capture("strike")
    for i in 120:
        await animation_step()
        if enemy.AttackCycleId != cycle: break
    check(enemy.AttackCycleId == cycle+1 and driver.AttackPlaybackStarts == starts+1,"next AI cycle starts attack exactly once")
    place_player(Vector3(0,0,18))
    await animation_step()
    check(enemy.State == 3 and driver.ActiveClip != "Combat/Attack","leash cancels active windup immediately")
    await steps(4)
    check(driver.ActiveClip == "Walk","Return movement uses Walk")
    check(await wait_state(0),"returns to spawn and Idle")
    check(driver.ActiveClip == "Idle" and events.size() == 1,"no stale animation event after cancellation/return")
    # Non-default durations exercise independent phase mapping without changing gameplay timing.
    enemy.AttackWindupDuration = 0.3; enemy.AttackRecoveryDuration = 0.5
    place_player(Vector3(0,0,1.2))
    check(await wait_state(4),"reacquires for tuned-timing attack")
    var tuned_start := Engine.get_physics_frames()
    var before := events.size()
    for i in 90:
        await animation_step()
        if enemy.State == 5: break
    check(events.size() == before+1 and Engine.get_physics_frames()-tuned_start >= 17,"custom AI windup remains authoritative")
    for i in 15: await animation_step()
    check(enemy.State == 5 and animator.current_animation_position > 1.0,"custom recovery advances the remainder of approved clip")
    enemy.PlayerTarget = null
    await animation_step()
    check(enemy.State in [0,3] and driver.ActiveClip != "Combat/Attack","target removal cancels recovery presentation")
    check(sync_ok,"attack phase clock matches visuals throughout sampled cycles")
    check(attachments_ok,"sword and shield remain attached during integration")
    check(continuous,"no angular discontinuity over consecutive sampled physics frames")
    var collider_transform: Transform3D = enemy.get_node("CollisionShape3D").transform
    enemy.set_physics_process(false); driver.set_physics_process(false)
    var root_position := enemy.global_position
    animator.play("Combat/Attack"); animator.advance(1.0)
    check(enemy.global_position.is_equal_approx(root_position) and enemy.get_node("CollisionShape3D").transform == collider_transform,"animation cannot move physics root/collider")
    print("AI animation integration checks: ",failures," failures")
    quit(0 if failures == 0 else 1)
