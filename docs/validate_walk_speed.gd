extends "res://docs/validate_enemy_pursuit.gd"

func run_checks() -> void:
    world = load("res://scenes/world/World.tscn").instantiate()
    root.add_child(world); current_scene = world
    player = world.get_node("FutureActors/Player"); player.InputEnabled = false
    await steps(60)
    # Measure the approved unit-scale clip itself, not the AI's desired velocity.
    var visual: Node3D = load("res://scenes/enemies/FallenGuardianV3AttackPresentation.tscn").instantiate()
    world.add_child(visual)
    var animation: AnimationPlayer = visual.get_node("AnimationPlayer")
    animation.play("Walk"); animation.advance(0); animation.pause()
    var clip := animation.get_animation("Walk")
    var ankle: Node3D = visual.find_child("LeftAnklePivot",true,false)
    animation.seek(0,true)
    var contact_start := ankle.global_position
    animation.seek(clip.length*0.6,true)
    var contact_end := ankle.global_position
    var stance_travel := horizontal_distance(contact_start,contact_end)
    var calibrated := stance_travel/(clip.length*0.6)
    print("CALIBRATION scale=",visual.scale," cycle=",clip.length," stance_s=",clip.length*0.6," travel=",stance_travel," reference_mps=",calibrated)
    check(absf(stance_travel-0.28) < 0.0001 and absf(contact_start.y-contact_end.y)<0.0001,"measured 0.28 m ground-level support over 60 percent of 1.4 s")
    visual.free()
    place_player(Vector3(0,0,4))
    await spawn(Vector3.ZERO)
    var driver: Node = enemy.get_node("AnimationController")
    check(absf(driver.WalkReferenceSpeed-calibrated)<0.0001,"reference matches measured stance speed at unit scale")
    enemy.MovementSpeed = calibrated
    await steps(100)
    check(driver.ActiveClip == "Walk" and absf(driver.WalkPlaybackRate-1)<0.02,"calibrated speed converges to 1x")
    check(absf(driver.ActualHorizontalSpeed-calibrated)<0.01,"actual displacement speed measured independently of desired target")
    place_player(Vector3(0,0,10))
    enemy.MovementSpeed = 3
    var previous_rate: float = driver.WalkPlaybackRate
    await steps(1)
    check(driver.WalkPlaybackRate > previous_rate and driver.WalkPlaybackRate < driver.MaximumWalkPlaybackRate,"acceleration is smoothed instead of jumping rate")
    await steps(60)
    print("NORMAL_PURSUIT actual_mps=",driver.ActualHorizontalSpeed," requested=",driver.RequestedWalkPlaybackRate," used=",driver.WalkPlaybackRate)
    check(absf(driver.ActualHorizontalSpeed-3)<0.02 and absf(driver.RequestedWalkPlaybackRate-9)<0.1,"normal pursuit requests mathematically calibrated 9x")
    check(driver.WalkRateLimited and absf(driver.WalkPlaybackRate-2)<0.01,"reasonable cadence cap makes residual mismatch explicit")
    enemy.MovementSpeed = calibrated*0.5
    previous_rate = driver.WalkPlaybackRate
    await steps(1)
    check(driver.WalkPlaybackRate < previous_rate and driver.WalkPlaybackRate > 0.5,"deceleration is smoothed")
    await steps(60)
    check(absf(driver.WalkPlaybackRate-0.5)<0.01,"half-reference displacement converges to 0.5x")
    # Freeze physical movement while leaving desired speed and AI state unchanged.
    enemy.MovementSpeed = 3
    enemy.set_physics_process(false)
    await steps(2)
    check(driver.ActiveClip == "Idle" and driver.ActualHorizontalSpeed < 0.001 and driver.WalkPlaybackRate == 0,"no movement selects Idle even if desired movement speed is 3")
    enemy.set_physics_process(true)
    place_player(Vector3(0,0,18))
    await steps(15)
    check(enemy.State == 3 and driver.ActiveClip == "Walk" and absf(driver.RequestedWalkPlaybackRate-enemy.ReturnSpeed/driver.WalkReferenceSpeed)<0.1,"return movement uses its independent configured speed calibration")
    check(await wait_state(0),"return finishes unchanged")
    check(driver.ActiveClip == "Idle" and driver.WalkPlaybackRate == 0,"arrival clears locomotion rate")
    print("Walk speed checks: ",failures," failures")
    quit(0 if failures == 0 else 1)

func horizontal_distance(a: Vector3,b: Vector3) -> float:
    return Vector2(a.x-b.x,a.z-b.z).length()
