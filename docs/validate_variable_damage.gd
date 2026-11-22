extends SceneTree

var failures := 0

func check(ok: bool, message: String) -> void:
    if ok: print("PASS: ", message)
    else:
        failures += 1
        push_error("FAIL: " + message)

func run() -> void:
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
    damage.DevelopmentMinimumDamage = 20
    damage.DevelopmentMaximumDamage = 20
    damage.DevelopmentCriticalChance = 1.0
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
    check(damage.SuccessfulHits == 1 and damage.LastImpactCritical,
        "One debug-profile critical hit resolved")
    check(health.CurrentHealth == 70, "Inclusive 20 damage at 1.5x applies 30 once")
    check(feedback.FeedbackCount == 1, "One impact feedback event")
    var found_critical := false
    for child in feedback.get_children():
        if child is Label3D and child.text == "CRIT -30": found_critical = true
    check(found_critical, "Critical hit has a distinct readable number")
    var enemy_damage: Node = scene.get_node("EnemyMeleeDamage")
    enemy_damage.DevelopmentVariableDamage = true
    enemy_damage.DevelopmentMinimumDamage = 8
    enemy_damage.DevelopmentMaximumDamage = 8
    enemy_damage.DevelopmentCriticalChance = 1.0
    enemy_damage.DevelopmentCriticalMultiplier = 1.5
    var player_health: Node = player.get_node("Health")
    enemy.AttacksEnabled = true
    enemy.DetectionRadius = 10.0
    for i in 420:
        await physics_frame
        if enemy_damage.SuccessfulHits > 0: break
    check(enemy_damage.SuccessfulHits >= 1 and enemy_damage.LastImpactCritical,
        "Guardian uses the same debug-profile calculator")
    check(player_health.CurrentHealth <= 88,
        "Guardian 8 damage at 1.5x applies 12 at its unchanged impact moment")
    print("Variable damage checks: ", failures, " failures")
    quit(0 if failures == 0 else 1)

func _initialize() -> void:
    call_deferred("run")
