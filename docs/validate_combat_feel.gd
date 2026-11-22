extends SceneTree

var failures := 0
const SCENE := "res://scenes/world/AshenfallFirstAdventure.tscn"

func check(ok: bool, label: String) -> void:
    if ok: print("PASS: ", label)
    else:
        failures += 1
        push_error("FAIL: " + label)

func ticks(n: int) -> void:
    for i in n: await physics_frame

func fixture() -> Dictionary:
    var scene: Node3D = load(SCENE).instantiate()
    root.add_child(scene)
    current_scene = scene
    scene.get_node("GuardianWest").AttacksEnabled = false
    scene.get_node("GuardianEast").AttacksEnabled = false
    await ticks(75)
    return {"scene": scene, "player": scene.get_node("World/FutureActors/Player"),
        "enemy": scene.get_node("FallenGuardian"), "melee": scene.get_node("PlayerMelee"),
        "health": scene.get_node("World/FutureActors/Player/Health"),
        "enemy_health": scene.get_node("FallenGuardian/Health"),
        "damage": scene.get_node("EnemyMeleeDamage")}

func clear(data: Dictionary) -> void:
    current_scene = null
    data.scene.queue_free()
    await process_frame
    await process_frame

func wait_attack(melee: Node, limit: int = 700) -> bool:
    for i in limit:
        if melee.State == 2: return true
        await physics_frame
    return false

func wait_impact(melee: Node, sequence: int, limit: int = 120) -> bool:
    for i in limit:
        if melee.ImpactSequence == sequence: return true
        await physics_frame
    return false

func mouse_at(camera: Camera3D, point: Vector3, pressed: bool) -> void:
    var event := InputEventMouseButton.new()
    event.button_index = MOUSE_BUTTON_LEFT
    event.position = camera.unproject_position(point)
    event.global_position = event.position
    event.pressed = pressed
    root.push_input(event, true)

func capture(label: String) -> void:
    if "--capture" not in OS.get_cmdline_user_args() or DisplayServer.get_name() == "headless": return
    await RenderingServer.frame_post_draw
    check(root.get_texture().get_image().save_png("res://docs/milestone_07_1_" + label + ".png") == OK,
        "Rendered " + label)

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    root.size = Vector2i(1280, 800)
    var data := await fixture()
    var enemy: CharacterBody3D = data.enemy
    var melee: Node = data.melee
    var player: CharacterBody3D = data.player
    var executions := []
    enemy.connect("AttackExecuted", func(_target: Node3D, _sequence: int):
        executions.append(Engine.get_physics_frames()))
    check(is_equal_approx(enemy.AttackWindupDuration, 1.2) and
        is_equal_approx(enemy.AttackRecoveryDuration, 1.0) and
        is_equal_approx(data.damage.ImpactDelay, 0.1),
        "Adventure-only anticipation is 1.20 s; impact 1.30 s; recovery 1.00 s")
    check(melee.IssueAttack(enemy.get_node("MeleeTarget")),
        "Dwarf issues a real approach command from the original spawn")
    check(await wait_attack(melee), "Navigation approach reaches one committed attack")
    var dwarf_start: int = Engine.get_physics_frames()
    var enemy_state_at_dwarf_start: int = enemy.State
    var sequence: int = melee.AttackSequence
    check(await wait_impact(melee, sequence), "Dwarf reaches its 0.94 s logical impact")
    var impact_frame: int = Engine.get_physics_frames()
    print("TIMELINE approach dwarf_start=", dwarf_start, " enemy_state=", enemy_state_at_dwarf_start,
        " dwarf_impact=", impact_frame, " guardian_state=", enemy.State,
        " guardian_attacks=", enemy.AttackCount, " player_hp=", data.health.CurrentHealth)
    check(data.enemy_health.CurrentHealth == 75 and data.health.CurrentHealth == 100,
        "Successful 25 HP hit precedes Guardian damage")
    await capture("anticipation")
    Input.action_press("move_down")
    var position_at_retreat: Vector3 = player.global_position
    await ticks(24)
    Input.action_release("move_down")
    check(player.global_position.distance_to(position_at_retreat) > 0.35 and
        melee.State == 0 and not player.MovementLocked,
        "WASD retreat interrupts recovery and actually moves the dwarf")
    await ticks(15)
    print("TIMELINE retreat hp=", data.health.CurrentHealth, " guardian_attacks=", enemy.AttackCount,
        " hits=", data.damage.SuccessfulHits, " consumed=", data.damage.LastConsumedSequence,
        " guardian_execution_frame=", executions[0] if not executions.is_empty() else -1,
        " dwarf_to_guardian_execution_frames=", executions[0] - impact_frame if not executions.is_empty() else -1)
    check(data.health.CurrentHealth == 100 and enemy.AttackCount >= 1 and
        data.damage.SuccessfulHits == 0,
        "Timely post-hit retreat makes the visible Guardian strike miss")
    await capture("dodge")
    var attacks_after_miss: int = enemy.AttackCount
    await ticks(25)
    check(enemy.AttackCount == attacks_after_miss,
        "A missed Guardian swing still has a recovery opportunity")
    await clear(data)

    data = await fixture()
    enemy = data.enemy
    melee = data.melee
    player = data.player
    check(melee.IssueAttack(enemy.get_node("MeleeTarget")) and await wait_attack(melee),
        "Late-retreat case reaches a committed attack")
    check(await wait_impact(melee, melee.AttackSequence),
        "Late-retreat case lands the dwarf hit")
    var damage_arrived := false
    for i in 150:
        await physics_frame
        if data.health.CurrentHealth < 100:
            damage_arrived = true
            break
    check(damage_arrived and data.health.CurrentHealth == 90 and data.damage.SuccessfulHits == 1,
        "Staying in range through the Guardian impact costs 10 HP")
    var enemy_clip: AnimationPlayer = enemy.get_node("Visual/AnimationPlayer")
    check(enemy_clip.current_animation == "Combat/Attack" and
        enemy_clip.current_animation_position >= 0.6 and
        enemy_clip.current_animation_position <= 0.9,
        "Logical damage lands during the visible sword strike")
    var spent_sequence: int = enemy.AttackCount
    enemy.emit_signal("AttackExecuted", player, spent_sequence)
    Input.action_press("move_down")
    var late_position: Vector3 = player.global_position
    await ticks(12)
    Input.action_release("move_down")
    check(player.global_position.distance_to(late_position) > 0.1 and
        data.health.CurrentHealth == 90 and data.damage.SuccessfulHits == 1,
        "Late movement works but cannot undo damage or duplicate the attack")
    await clear(data)

    data = await fixture()
    enemy = data.enemy
    melee = data.melee
    player = data.player
    check(melee.IssueAttack(enemy.get_node("MeleeTarget")) and await wait_attack(melee),
        "Pre-impact cancel case reaches attack state")
    enemy.AttacksEnabled = false
    await ticks(10)
    var before_cancel: Vector3 = player.global_position
    Input.action_press("move_down")
    await ticks(12)
    Input.action_release("move_down")
    await ticks(75)
    check(melee.State == 0 and melee.ImpactSequence == 0 and
        data.enemy_health.CurrentHealth == 100 and
        player.global_position.distance_to(before_cancel) > 0.1,
        "WASD before dwarf impact cancels its damage and moves immediately")
    await clear(data)

    data = await fixture()
    enemy = data.enemy
    melee = data.melee
    player = data.player
    check(melee.IssueAttack(enemy.get_node("MeleeTarget")) and await wait_attack(melee),
        "Mouse cancel case reaches attack state")
    enemy.AttacksEnabled = false
    var ground: Vector3 = player.global_position + Vector3(0, 0, 4)
    mouse_at(data.scene.get_node("World/CameraRig/Camera3D"), ground, true)
    await ticks(3)
    mouse_at(data.scene.get_node("World/CameraRig/Camera3D"), ground, false)
    check(melee.State == 0 and not player.MovementLocked and player.HasDestination and
        data.enemy_health.CurrentHealth == 100,
        "Ground click interrupts before impact and starts navigation")
    await clear(data)
    print("Combat feel checks: ", failures, " failures")
    quit(1 if failures else 0)
