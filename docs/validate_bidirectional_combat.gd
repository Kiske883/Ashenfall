extends SceneTree

var failures := 0
var demo: Node3D
var player: CharacterBody3D
var enemy: CharacterBody3D
var health: Node
var enemy_health: Node
var damage: Node
var melee: Node
var hud: CanvasLayer

func check(ok: bool, label: String) -> void:
    if ok: print("PASS: ", label)
    else:
        failures += 1
        push_error("FAIL: " + label)

func ticks(count: int) -> void:
    for i in count: await physics_frame

func fixture() -> void:
    demo = load("res://scenes/world/AshenfallDwarfPlayableDemo.tscn").instantiate()
    root.add_child(demo)
    current_scene = demo
    player = demo.get_node("World/FutureActors/Player")
    enemy = demo.get_node("FallenGuardian")
    health = player.get_node("Health")
    enemy_health = enemy.get_node("Health")
    damage = demo.get_node("EnemyMeleeDamage")
    melee = demo.get_node("PlayerMelee")
    hud = demo.get_node("PlayerHealthHud")
    player.InputEnabled = false
    player.global_position = enemy.global_position + Vector3(0, 0, 1.2)
    player.velocity = Vector3.ZERO
    player.reset_physics_interpolation()
    enemy.AttacksEnabled = false
    await ticks(75)
    enemy.AttacksEnabled = true

func clear_fixture() -> void:
    current_scene = null
    demo.queue_free()
    # process_frame is emitted before queued frees are processed. Wait for the
    # old scene to leave the tree before the next fixture is instantiated.
    await demo.tree_exited

func wait_execution(limit := 240) -> bool:
    for i in limit:
        if enemy.AttackCount > 0: return true
        await physics_frame
    return false

func mouse_filters_ignored(node: Node) -> bool:
    if node is Control and node.mouse_filter != Control.MOUSE_FILTER_IGNORE: return false
    for child in node.get_children():
        if not mouse_filters_ignored(child): return false
    return true

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    root.size = Vector2i(1280, 800)
    await fixture()
    check(health.MaximumHealth == 100 and health.CurrentHealth == 100 and health.IsAlive,
        "Player starts with 100 HP")
    check(is_equal_approx(enemy.AttackWindupDuration, 1.2) and
        is_equal_approx(enemy.AttackRecoveryDuration, 1.0) and
        is_equal_approx(damage.ImpactDelay, 0.1) and damage.Damage == 10,
        "Dwarf demo anticipation 1.20 s; impact 1.30 s and damage 10")
    check(mouse_filters_ignored(hud), "HUD controls ignore all mouse input")
    var panel: Control = hud.get_node("Panel")
    var bar: ProgressBar = hud.get_node("Panel/Bar")
    var label: Label = hud.get_node("Panel/Value")
    await process_frame
    var position_before: Vector2 = panel.get_global_rect().position
    check(bar.value == 100 and bar.max_value == 100 and label.text == "HP 100 / 100",
        "HUD initializes at 100 / 100")
    var camera: Camera3D = demo.get_node("World/CameraRig/Camera3D")
    camera.position += Vector3(3, 0, 2)
    await process_frame
    check(panel.get_global_rect().position.is_equal_approx(position_before),
        "HUD remains fixed when camera moves")
    root.size = Vector2i(800, 600)
    await process_frame
    check(panel.anchor_top == 1 and panel.anchor_bottom == 1 and
        absf(panel.get_global_rect().position.x - 24) < 1,
        "HUD uses bottom-left anchors and fixed left margin")
    root.size = Vector2i(1280, 800)
    camera.position -= Vector3(3, 0, 2)
    check(await wait_execution(), "Guardian executes one real attack")
    var sequence: int = enemy.AttackCount
    check(damage.PendingSequence == sequence and health.CurrentHealth == 100,
        "Damage waits for configured impact delay")
    enemy.emit_signal("AttackExecuted", player, sequence)
    enemy.emit_signal("AttackExecuted", player, sequence)
    await ticks(8)
    check(health.CurrentHealth == 90 and damage.LastConsumedSequence == sequence and
        damage.SuccessfulHits == 1, "Repeated attack signals cause exactly one 10 HP impact")
    enemy.emit_signal("AttackExecuted", player, sequence)
    await ticks(2)
    check(health.CurrentHealth == 90, "Consumed sequence cannot damage twice")
    enemy.Defeat()
    await clear_fixture()

    await fixture()
    check(await wait_execution(), "Out-of-range case has committed attack")
    player.global_position += Vector3(0, 0, 4)
    await ticks(9)
    check(health.CurrentHealth == 100 and damage.PendingSequence == 0 and
        damage.LastConsumedSequence == 1, "Missed range check consumes sequence")
    await clear_fixture()

    await fixture()
    check(await wait_execution(), "Occlusion case has committed attack")
    var wall := StaticBody3D.new()
    wall.collision_layer = 1
    var shape := CollisionShape3D.new()
    var box := BoxShape3D.new()
    box.size = Vector3(1, 2, 0.15)
    shape.shape = box
    wall.add_child(shape)
    demo.add_child(wall)
    wall.global_position = (enemy.global_position + player.global_position) * 0.5 + Vector3.UP
    await ticks(9)
    check(health.CurrentHealth == 100 and damage.LastConsumedSequence == 1,
        "Obstacle blocks damage at logical impact")
    await clear_fixture()

    await fixture()
    check(await wait_execution(), "Guardian death case has pending impact")
    enemy_health.ApplyDamage(100)
    await ticks(9)
    check(health.CurrentHealth == 100 and damage.PendingSequence == 0 and
        enemy.State == 6, "Guardian death cancels pending damage")
    await clear_fixture()

    await fixture()
    check(await wait_execution(), "Interrupt case has pending impact")
    enemy.AttacksEnabled = false
    await ticks(9)
    check(health.CurrentHealth == 100 and damage.PendingSequence == 0 and
        damage.LastConsumedSequence == 1, "Interrupted recovery cancels pending impact")
    await clear_fixture()

    await fixture()
    check(await wait_execution(), "Player death case has pending impact")
    bar = hud.get_node("Panel/Bar")
    label = hud.get_node("Panel/Value")
    var deaths := []
    health.connect("Died", func(): deaths.append(1))
    health.ApplyDamage(100)
    await ticks(9)
    check(health.CurrentHealth == 0 and deaths.size() == 1 and not health.ApplyDamage(10),
        "Player death is terminal, clamped and signaled once")
    check(damage.PendingSequence == 0 and damage.SuccessfulHits == 0,
        "Player death cancels delayed enemy impact")
    check(demo.get_node("PlayerDefeat").IsDefeated and not player.InputEnabled and
        not player.HasDestination and player.MovementLocked and melee.Defeated and
        not melee.IssueAttack(null), "Defeat cancels commands and disables movement and attacks")
    check(enemy.State == 0 or enemy.State == 3,
        "Defeated player is no longer chased or attacked")
    check(bar.value == 0 and label.text == "HP 0 / 100", "HUD displays zero HP")
    await clear_fixture()

    await fixture()
    enemy.AttacksEnabled = false
    player.InputEnabled = true
    check(melee.IssueAttack(null) and melee.State == 2,
        "Player has active attack before defeat")
    health.ApplyDamage(100)
    await ticks(70)
    check(melee.State == 0 and melee.Defeated and enemy_health.CurrentHealth == 100 and
        not player.HasDestination and player.MovementLocked,
        "Player death cancels active attack before impact and queued movement")
    await clear_fixture()

    await fixture()
    enemy.Defeat()
    bar = hud.get_node("Panel/Bar")
    label = hud.get_node("Panel/Value")
    var expected := [90, 50, 10, 0]
    for value in expected:
        health.ApplyDamage(health.CurrentHealth - value)
        check(bar.value == value and label.text == "HP %d / 100" % value,
            "HUD updates to %d HP" % value)
    await clear_fixture()

    print("Bidirectional combat checks: ", failures, " failures")
    quit(1 if failures else 0)
