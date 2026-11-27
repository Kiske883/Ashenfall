extends SceneTree

var failures := 0

func check(ok: bool, message: String) -> void:
    if ok: print("PASS: ", message)
    else:
        failures += 1
        push_error("FAIL: " + message)

func number(feedback: Node) -> Label3D:
    var labels := feedback.find_children("DamageNumber*", "Label3D", true, false)
    check(labels.size() == 1, "Exactly one number for the confirmed impact")
    return labels[0] if labels.size() == 1 else null

func capture(label: String) -> void:
    if "--capture" not in OS.get_cmdline_user_args() or DisplayServer.get_name() == "headless": return
    await RenderingServer.frame_post_draw
    var path := "user://milestone09c3_" + label + "_damage.png"
    check(root.get_texture().get_image().save_png(path) == OK, "Captured " + label)
    print("Capture: ", ProjectSettings.globalize_path(path))

func ticks(count: int) -> void:
    for i in count:
        await physics_frame
        await process_frame

func run() -> void:
    print("Isolated renderer validation: no API or PostgreSQL fixture required")
    root.size = Vector2i(1280, 800)
    var scene: Node3D = load("res://scenes/world/AshenfallFirstAdventure.tscn").instantiate()
    root.add_child(scene)
    current_scene = scene
    var player: CharacterBody3D = scene.get_node("World/FutureActors/Player")
    var enemy: CharacterBody3D = scene.get_node("FallenGuardian")
    var health: Node = enemy.get_node("Health")
    var melee: Node = scene.get_node("PlayerMelee")
    var damage: Node = scene.get_node("MeleeDamage")
    var feedback: Node = scene.get_node("CombatFeedback")
    enemy.AttacksEnabled = false
    enemy.DetectionRadius = 0
    damage.DevelopmentVariableDamage = true
    # Existing isolated debug-profile seam: equal final amounts prove styling uses the critical flag.
    damage.DevelopmentMinimumDamage = 30
    damage.DevelopmentMaximumDamage = 30
    damage.DevelopmentCriticalChance = 0.0
    damage.DevelopmentCriticalMultiplier = 1.5
    for i in 75: await physics_frame
    player.global_position = enemy.global_position + Vector3(0, 0, 1.2)
    player.velocity = Vector3.ZERO
    player.reset_physics_interpolation()
    for i in 3: await physics_frame
    check(melee.IssueAttack(enemy.get_node("MeleeTarget")), "Combat command accepted")
    for i in 210:
        await physics_frame
        if damage.SuccessfulHits > 0: break
    check(damage.SuccessfulHits == 1 and not damage.LastImpactCritical,
        "One deterministic normal hit resolved")
    check(health.CurrentHealth == 70, "Normal damage applies 30 once")
    check(feedback.FeedbackCount == 1, "One impact feedback event")
    var normal := number(feedback)
    if normal == null: quit(1); return
    check(normal.text == "30" and normal.modulate.is_equal_approx(Color(1, 0.92, 0.78, normal.modulate.a)),
        "Normal number retains ivory and numeric-only text")
    var normal_style := [normal.font, normal.font_size, normal.pixel_size, normal.outline_size,
        normal.outline_modulate, normal.billboard, normal.no_depth_test]
    var normal_rgb := Color(normal.modulate.r, normal.modulate.g, normal.modulate.b)
    check(normal.font_size == 60 and is_equal_approx(normal.pixel_size, 0.007) and
        is_equal_approx(feedback.NumberSeconds, 1.0), "Existing number size and duration remain unchanged")
    var normal_offset := normal.global_position - enemy.global_position
    var normal_start := normal.global_position
    var duration: float = feedback.NumberSeconds
    await capture("normal")
    await ticks(6)
    check(normal.global_position.y > normal_start.y and normal.modulate.a < 1.0,
        "Normal number rises and fades")
    await ticks(120)
    check(not is_instance_valid(normal) and feedback.ActiveNumberCount == 0, "Normal indicator cleans up")
    damage.DevelopmentMinimumDamage = 20
    damage.DevelopmentMaximumDamage = 20
    damage.DevelopmentCriticalChance = 1.0
    check(melee.IssueAttack(enemy.get_node("MeleeTarget")), "Second combat command accepted")
    for i in 210:
        await physics_frame
        if damage.SuccessfulHits > 1: break
    check(damage.SuccessfulHits == 2 and damage.LastImpactCritical and health.CurrentHealth == 40,
        "Critical modifier applies 30 once without changing impact timing")
    var critical := number(feedback)
    if critical == null: quit(1); return
    check(critical.text == "30" and critical.modulate.is_equal_approx(Color(1, 1, 0, critical.modulate.a)),
        "Same damage amount is yellow only when the critical result is true")
    check(not normal_rgb.is_equal_approx(Color(critical.modulate.r, critical.modulate.g, critical.modulate.b)),
        "Normal and critical colors are visibly distinct without inferring criticals from the amount")
    check(normal_style == [critical.font, critical.font_size, critical.pixel_size, critical.outline_size,
        critical.outline_modulate, critical.billboard, critical.no_depth_test],
        "Normal and critical use identical font, size, outline and billboard")
    check(critical.get_child_count() == 0 and feedback.ActiveNumberCount == 1 and
        is_equal_approx(feedback.NumberSeconds, duration) and
        (critical.global_position - enemy.global_position).distance_to(normal_offset) < 0.02,
        "Critical adds no UI elements and preserves position and duration")
    var critical_start := critical.global_position
    await capture("critical")
    await ticks(6)
    check(critical.global_position.y > critical_start.y and critical.modulate.a < 1.0,
        "Critical number uses the same upward fade")
    await ticks(120)
    check(not is_instance_valid(critical) and feedback.ActiveNumberCount == 0, "Critical indicator cleans up")
    var enemy_damage: Node = scene.get_node("EnemyMeleeDamage")
    enemy_damage.DevelopmentVariableDamage = true
    enemy_damage.DevelopmentMinimumDamage = 8
    enemy_damage.DevelopmentMaximumDamage = 8
    enemy_damage.DevelopmentCriticalChance = 0.0
    enemy_damage.DevelopmentCriticalMultiplier = 1.5
    var player_health: Node = player.get_node("Health")
    enemy.AttacksEnabled = true
    enemy.DetectionRadius = 10.0
    for i in 420:
        await physics_frame
        if enemy_damage.SuccessfulHits > 0: break
    check(enemy_damage.SuccessfulHits == 1 and not enemy_damage.LastImpactCritical and player_health.CurrentHealth == 92,
        "Normal Guardian impact preserves its configured damage")
    var received := number(feedback)
    if received == null: quit(1); return
    check(received.text == "8" and received.modulate.is_equal_approx(Color(0.9, 0.30, 0.27, received.modulate.a)),
        "Normal received damage retains crimson")
    enemy_damage.DevelopmentCriticalChance = 1.0
    for i in 420:
        await physics_frame
        if enemy_damage.SuccessfulHits > 1: break
    check(enemy_damage.SuccessfulHits == 2 and enemy_damage.LastImpactCritical and player_health.CurrentHealth == 80,
        "Guardian critical modifier applies 12 at its unchanged impact moment")
    var received_critical := number(feedback)
    if received_critical == null: quit(1); return
    check(received_critical.text == "12" and received_critical.modulate.is_equal_approx(Color(1, 1, 0, received_critical.modulate.a)) and
        absf((received_critical.global_position - player.global_position).y - 1.72) < 0.02,
        "Received critical uses yellow without moving the player's indicator")
    print("Variable damage checks: ", failures, " failures")
    quit(0 if failures == 0 else 1)

func _initialize() -> void:
    # Validation-only watchdog; never installed in a gameplay scene.
    create_timer(60.0).timeout.connect(func():
        push_error("Renderer validation exceeded its 60-second deadline")
        quit(1))
    call_deferred("run")
