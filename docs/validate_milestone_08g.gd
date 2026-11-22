extends SceneTree

var failures := 0

func check(ok: bool, label: String) -> void:
    if ok: print("PASS: ", label)
    else:
        failures += 1
        push_error("FAIL: " + label)

func ticks(count: int) -> void:
    for i in count: await physics_frame

func capture(label: String) -> void:
    if "--capture" not in OS.get_cmdline_user_args() or DisplayServer.get_name() == "headless": return
    await RenderingServer.frame_post_draw
    check(root.get_texture().get_image().save_png("res://docs/milestone_08g_" + label + ".png") == OK,
        "Rendered " + label)

func grip_gap(model: Node3D, axe: Node3D) -> float:
    var gap := 0.0
    for side in ["Left", "Right"]:
        var hand: Node3D = model.find_child(side + "Gauntlet", true, false).get_node("GripPoint")
        gap = maxf(gap, hand.global_position.distance_to(axe.get_node(side + "Grip").global_position))
    return gap

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    root.size = Vector2i(1280, 800)
    var scene: Node3D = load("res://scenes/world/AshenfallFirstAdventure.tscn").instantiate()
    root.add_child(scene); current_scene = scene
    var actor: CharacterBody3D = scene.get_node("World/FutureActors/Player")
    var driver: Node = scene.get_node("DwarfPresentation")
    var dwarf: Node3D = actor.get_node("Visual/Dwarf")
    var model: Node3D = dwarf.get_node("Model")
    var axe: Node3D = model.find_child("BattleAxe", true, false)
    var torso: Node3D = model.find_child("TorsoPivot", true, false)
    var animator: AnimationPlayer = dwarf.get_node("AnimationPlayer")
    var camera: Camera3D = scene.get_node("World/CameraRig/Camera3D")
    for enemy_name in ["FallenGuardian", "GuardianWest", "GuardianEast"]:
        scene.get_node(enemy_name).AttacksEnabled = false
    await ticks(75)
    check(driver.NaturalWalkMotion and is_equal_approx(actor.MovementSpeed, 2.5),
        "Adventure inherits 08G presentation and approved movement speed")
    await capture("idle")
    camera.size = 8
    await capture("idle_close")
    camera.size = 22
    var neutral_torso := torso.rotation
    var max_grip := 0.0
    var max_torso_delta := 0.0
    Input.action_press("move_right")
    for i in 45:
        await physics_frame
        max_grip = maxf(max_grip, grip_gap(model, axe))
        max_torso_delta = maxf(max_torso_delta, torso.rotation.distance_to(neutral_torso))
        if i == 22:
            await capture("walk")
            camera.size = 8
            await capture("walk_close")
            camera.size = 22
    check(animator.current_animation == "Combat/CombatWalk" and driver.WalkMotionWeight > .8,
        "WASD selects walk and secondary motion fades in")
    check(max_torso_delta > .015 and max_torso_delta < .15,
        "Torso counter-motion is visible but restrained")
    Input.action_release("move_right")
    await ticks(25)
    check(animator.current_animation == "Combat/GuardB" and driver.WalkMotionWeight < .2,
        "Stopping returns smoothly to combat guard")
    await capture("stopped")
    var click := InputEventMouseButton.new()
    click.button_index = MOUSE_BUTTON_LEFT
    click.position = camera.unproject_position(actor.global_position + Vector3(0, 0, -3))
    click.global_position = click.position; click.pressed = true
    root.push_input(click, true)
    await ticks(25)
    check(actor.HasDestination and animator.current_animation == "Combat/CombatWalk",
        "Held mouse movement still selects walk")
    click.pressed = false; root.push_input(click, true)
    await ticks(25)
    max_grip = maxf(max_grip, grip_gap(model, axe))
    # Explicit attack command checks presentation handoff without changing melee timing.
    actor.CancelDestination()
    await ticks(12)
    check(driver.RequestAttack(), "Attack can follow locomotion")
    await ticks(12)
    check(animator.current_animation == "Combat/AxeAttack" and driver.WalkMotionWeight < .2,
        "Attack owns its pose without locomotion overlay")
    for i in 90:
        await physics_frame
        max_grip = maxf(max_grip, grip_gap(model, axe))
    check(animator.current_animation == "Combat/GuardB", "Attack recovers to guard")
    # Long alternating movement checks deterministic pose reconstruction and turns.
    for direction in ["move_left", "move_right", "move_left", "move_right"]:
        Input.action_press(direction)
        for i in 150:
            await physics_frame
            max_grip = maxf(max_grip, grip_gap(model, axe))
        Input.action_release(direction)
    await ticks(30)
    check(max_grip < .0001, "Both hands remain on one axe handle after extended movement")
    check(dwarf.scale.is_equal_approx(Vector3.ONE) and model.scale.is_equal_approx(Vector3.ONE) and
        torso.rotation.distance_to(neutral_torso) < .01,
        "No root/model scaling or accumulated pose drift after long movement")
    scene.get_node("World/FutureActors/Player/Health").ApplyDamage(100)
    await ticks(25)
    check(driver.Defeated and not actor.InputEnabled and driver.WalkMotionWeight < .2,
        "Defeat disables movement and secondary walking motion")
    await capture("defeat")
    scene.get_node("EncounterReset").Restart()
    for i in 40:
        await process_frame
        if current_scene != null and current_scene != scene: break
    check(current_scene != scene and current_scene.get_node("DwarfPresentation").NaturalWalkMotion,
        "Adventure restart creates a fresh 08G presentation")
    print("08G max grip gap: ", max_grip, "; maximum torso delta: ", max_torso_delta)
    print("08G checks: ", failures, " failures")
    quit(1 if failures else 0)
