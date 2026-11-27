extends SceneTree

var failures := 0

func check(value: bool, label: String) -> void:
    if value: print("PASS: ", label)
    else:
        failures += 1
        push_error("FAIL: " + label)

func ticks(n: int) -> void:
    for i in n: await physics_frame

func fixture(path: String) -> Dictionary:
    var scene: Node = load(path).instantiate()
    root.add_child(scene)
    current_scene = scene
    var world: Node = scene.get_node("Adventure") if scene.has_node("Adventure") else scene
    var player: CharacterBody3D = world.get_node("World/FutureActors/Player")
    var enemy: CharacterBody3D = world.get_node("FallenGuardian")
    enemy.AttacksEnabled = false
    var camera: Camera3D = world.get_node("World/CameraRig/Camera3D")
    var back: Vector3 = camera.global_basis.z
    back.y = 0
    player.global_position = enemy.global_position + back.normalized() * 1.2
    player.velocity = Vector3.ZERO
    player.reset_physics_interpolation()
    await ticks(80)
    enemy.AttacksEnabled = true
    return {"scene": scene, "world": world, "player": player, "enemy": enemy,
        "melee": world.get_node("PlayerMelee"), "health": player.get_node("Health"),
        "enemy_health": enemy.get_node("Health"), "damage": world.get_node("EnemyMeleeDamage")}

func run_cycle(data: Dictionary, number: int) -> bool:
    var enemy: CharacterBody3D = data.enemy
    var player: CharacterBody3D = data.player
    var melee: Node = data.melee
    for i in 240:
        if enemy.State == 4: break
        await physics_frame
    if enemy.State != 4: return false
    for i in 100:
        if enemy.State != 4 or enemy.AttackPhaseRemaining <= 0.65: break
        await physics_frame
    if enemy.State != 4: return false
    Input.action_press("move_down")
    for i in 45:
        await physics_frame
        var diff: Vector3 = player.global_position - enemy.global_position
        diff.y = 0
        if diff.length() > enemy.EffectiveAttackRange + 0.23: break
    Input.action_release("move_down")
    var before: int = data.health.CurrentHealth
    for i in 120:
        if enemy.State == 5 and data.damage.LastConsumedSequence == enemy.AttackCount: break
        await physics_frame
    if enemy.State != 5 or data.health.CurrentHealth != before or not melee.IssueAttack(enemy.get_node("MeleeTarget")):
        return false
    print("COUNTER command player=", player.global_position, " enemy=", enemy.global_position,
        " path=", player.HasDestination, " spawn=", enemy.SpawnPosition)
    var previous: int = data.enemy_health.CurrentHealth
    for i in 180:
        if data.enemy_health.CurrentHealth < previous: break
        await physics_frame
    print("COUNTER cycle=", number, " player_hp=", data.health.CurrentHealth,
        " enemy_hp=", data.enemy_health.CurrentHealth, " enemy_state=", enemy.State,
        " attacks=", enemy.AttackCount, " player=", player.global_position, " enemy=", enemy.global_position,
        " melee=", melee.State, " destination=", player.HasDestination)
    return data.health.CurrentHealth == before and data.enemy_health.CurrentHealth == previous - 25

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    var path := "res://scenes/world/CharacterPersistencePreview.tscn" if "--preview" in OS.get_cmdline_user_args() else "res://scenes/world/AshenfallDwarfPlayableDemo.tscn"
    for selected in [path]:
        var data: Dictionary = await fixture(selected)
        check(is_equal_approx(data.enemy.AttackWindupDuration, 1.2) and is_equal_approx(data.enemy.AttackRecoveryDuration, 1.0), selected + " timing")
        for i in 4:
            var ok: bool = await run_cycle(data, i + 1)
            check(ok, selected + " dodge and counter " + str(i + 1))
            if not ok: break
        check(data.enemy_health.CurrentHealth == 0 and data.health.CurrentHealth == 100, selected + " defeat without player damage")
        current_scene = null
        data.scene.queue_free()
        await data.scene.tree_exited
    print("Counterattack checks: ", failures, " failures")
    quit(1 if failures else 0)
