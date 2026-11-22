extends SceneTree

var failures := 0
const SCENE := "res://scenes/world/AshenfallFirstAdventure.tscn"

func check(ok: bool, label: String) -> void:
    if ok: print("PASS: ", label)
    else:
        failures += 1
        push_error("FAIL: " + label)

func ticks(count: int) -> void:
    for i in count: await physics_frame

func adventure() -> Node3D:
    return current_scene

func enemies(scene: Node3D) -> Array:
    return [scene.get_node("FallenGuardian"), scene.get_node("GuardianWest"),
        scene.get_node("GuardianEast")]

func restart(scene: Node3D) -> Node3D:
    var overlay: Control = scene.get_node("AdventureHud/Overlay")
    if scene.get_node("AdventureHud").DefeatPresentationPending:
        await create_timer(scene.get_node("AdventureHud").DefeatPresentationDelay + 0.1).timeout
    if not overlay.visible: return scene
    var button: Button = scene.get_node("AdventureHud/Overlay/Panel/Contents/RestartButton")
    var point: Vector2 = button.get_global_rect().get_center()
    for pressed in [true, false]:
        var event := InputEventMouseButton.new()
        event.button_index = MOUSE_BUTTON_LEFT
        event.position = point
        event.global_position = point
        event.pressed = pressed
        root.push_input(event, true)
    for i in 30:
        await process_frame
        if current_scene != scene and current_scene != null: return current_scene
    return current_scene

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    root.size = Vector2i(1280, 800)
    var scene: Node3D = load(SCENE).instantiate()
    root.add_child(scene)
    current_scene = scene
    await ticks(75)
    var player: CharacterBody3D = scene.get_node("World/FutureActors/Player")
    var hp: Node = player.get_node("Health")
    var actors := enemies(scene)
    var objective: Node = scene.get_node("Objective")
    var session: Node = scene.get_node("Session")
    var ui: CanvasLayer = scene.get_node("AdventureHud")
    var zones := [scene.get_node("RoadZone"), scene.get_node("WestZone"),
        scene.get_node("EastZone")]
    var resolvers := [scene.get_node("EnemyMeleeDamage"), scene.get_node("WestDamage"),
        scene.get_node("EastDamage")]
    player.InputEnabled = false
    check(objective.GuardianCount == 3 and objective.DefeatedCount == 0 and
        hp.CurrentHealth == 100 and session.Outcome == 0,
        "Three required guardians and healthy player initialize")
    check(not zones[0].Active and not zones[1].Active and not zones[2].Active and
        actors[0].PlayerTarget == null and actors[1].PlayerTarget == null,
        "Remote encounters start dormant")
    var bars := []
    var healths := []
    for actor in actors:
        bars.append(actor.get_node("HealthBar/Bar").material_override.get_instance_id())
        healths.append(actor.get_node("Health"))
    check(bars[0] != bars[1] and bars[0] != bars[2] and bars[1] != bars[2] and
        healths[0] != healths[1] and healths[1] != healths[2],
        "Every guardian has distinct health and bar material")
    var map: RID = player.get_node("NavigationAgent3D").get_navigation_map()
    var reachable := true
    for actor in actors:
        if NavigationServer3D.map_get_closest_point(map, actor.global_position).distance_to(actor.global_position) > 0.5:
            reachable = false
    check(reachable, "All three guardians spawn on the navigation surface")

    for actor in actors: actor.AttacksEnabled = false
    player.global_position = Vector3(0, 0, -1)
    await ticks(4)
    check(zones[0].Active and not zones[1].Active and not zones[2].Active,
        "Road encounter activates without waking distant guardians")
    player.global_position = Vector3(-5, 0, -12)
    await ticks(4)
    check(zones[1].Active and not zones[2].Active,
        "Western zone activates independently")
    player.global_position = Vector3(0, 0, 10)
    await ticks(4)
    check(not zones[0].Active and not zones[1].Active and not zones[2].Active and
        actors[0].PlayerTarget == null and actors[1].PlayerTarget == null,
        "Leaving zones releases all targets for return")
    await ticks(100)
    check(actors[0].global_position.distance_to(actors[0].SpawnPosition) < 0.35 and
        actors[1].global_position.distance_to(actors[1].SpawnPosition) < 0.35,
        "Released guardians return to their own spawn areas")

    player.global_position = scene.get_node("EasternRuinGoal").global_position
    await ticks(3)
    check(not objective.IsComplete and session.Outcome == 0,
        "Reaching the ruin before defeating guardians cannot win")
    player.global_position = Vector3(0, 0, -8)
    await ticks(2)
    for i in 2:
        healths[i].ApplyDamage(100)
        await ticks(2)
        check(objective.DefeatedCount == i + 1 and not objective.IsComplete and
            not actors[i].get_node("MeleeTarget").CanBeTargeted and
            not actors[i].get_node("HealthBar").visible,
            "Independent guardian death advances objective once")
    healths[2].ApplyDamage(100)
    await ticks(3)
    check(objective.DefeatedCount == 3 and not objective.IsComplete and
        not actors[2].get_node("HealthBar").visible,
        "Three defeats hide all bars without completing the adventure")
    player.InputEnabled = true
    check(player.TrySetDestination(scene.get_node("EasternRuinGoal").global_position),
        "Eastern ruin is a reachable navigation destination")
    for i in 300:
        if objective.IsComplete: break
        await physics_frame
    check(objective.IsComplete and session.Outcome == 1,
        "Navigating to the ruin after three defeats completes adventure")
    check(ui.get_node("Overlay").visible and
        "3 GUARDIANS" in ui.get_node("Overlay/Panel/Contents/Result").text and
        not player.InputEnabled and scene.get_node("PlayerMelee").Defeated,
        "Victory overlay shows count and stops gameplay input")
    check(ui.get_node("Overlay").mouse_filter == Control.MOUSE_FILTER_STOP,
        "Victory overlay intercepts mouse input")

    scene = await restart(scene)
    check(scene != null and scene.name == "AshenfallFirstAdventure" and
        scene.get_node("World/FutureActors/Player/Health").CurrentHealth == 100 and
        scene.get_node("Objective").DefeatedCount == 0,
        "Restart recreates player, enemies, health and objective")
    await ticks(8)
    check(scene.get_children().filter(func(n): return n.name == "AdventureHud").size() == 1 and
        scene.get_children().filter(func(n): return n.name == "PlayerHealthHud").size() == 1 and
        scene.get_node("FallenGuardian/Health").CurrentHealth == 100 and
        scene.get_node("FallenGuardian/HealthBar").visible,
        "Restart has one HUD of each type and full enemy health")
    hp = scene.get_node("World/FutureActors/Player/Health")
    hp.ApplyDamage(100)
    await ticks(2)
    check(scene.get_node("Session").Outcome == 2 and
        "FALLEN" in scene.get_node("AdventureHud/Overlay/Panel/Contents/Result").text,
        "Player defeat prepares restartable defeat overlay")
    var defeated_scene: Node3D = scene
    scene = await restart(scene)
    check(scene != defeated_scene and scene.get_node("Session").Outcome == 0 and
        scene.get_node("World/FutureActors/Player/Health").CurrentHealth == 100 and
        scene.get_node("Objective").GuardianCount == 3,
        "Second restart restores an independent playable encounter")
    await ticks(8)
    check(scene.get_node("AdventureHud/Overlay").visible == false and
        scene.get_node("PlayerHealthHud/Panel/Value").text == "HP 100 / 100",
        "No stale overlay or HUD value remains after repeated restart")

    player = scene.get_node("World/FutureActors/Player")
    player.InputEnabled = false
    actors = enemies(scene)
    for i in [0, 2]: actors[i].AttacksEnabled = false
    player.global_position = actors[1].global_position + Vector3(0, 0, 1.2)
    player.velocity = Vector3.ZERO
    player.reset_physics_interpolation()
    var west_damage: Node = scene.get_node("WestDamage")
    var road_damage: Node = scene.get_node("EnemyMeleeDamage")
    var east_damage: Node = scene.get_node("EastDamage")
    var attacked := false
    for i in 240:
        await physics_frame
        if actors[1].AttackCount > 0:
            attacked = true
            break
    check(attacked, "Western guardian attacks inside its activated zone")
    await ticks(8)
    hp = player.get_node("Health")
    check(hp.CurrentHealth == 90 and west_damage.SuccessfulHits == 1 and
        road_damage.SuccessfulHits == 0 and east_damage.SuccessfulHits == 0,
        "Only the attacking guardian resolves 10 HP damage")
    actors[1].emit_signal("AttackExecuted", player, actors[1].AttackCount)
    await ticks(3)
    check(hp.CurrentHealth == 90, "Repeated western signal cannot duplicate damage")
    actors[1].AttacksEnabled = false
    player.InputEnabled = true
    var melee: Node = scene.get_node("PlayerMelee")
    check(melee.IssueAttack(actors[1].get_node("MeleeTarget")),
        "Dwarf can target one guardian in a multi-enemy encounter")
    var impact := false
    for i in 300:
        await physics_frame
        if melee.ImpactSequence > 0:
            impact = true
            break
    check(impact and actors[1].get_node("Health").CurrentHealth == 75 and
        actors[0].get_node("Health").CurrentHealth == 100 and
        actors[2].get_node("Health").CurrentHealth == 100,
        "A dwarf attack damages only its committed guardian")
    await ticks(60)
    player.InputEnabled = false
    player.global_position = actors[2].global_position + Vector3(0, 0, 1.2)
    player.velocity = Vector3.ZERO
    actors[2].AttacksEnabled = true
    var east_attacked := false
    for i in 240:
        await physics_frame
        if actors[2].AttackCount > 0:
            east_attacked = true
            break
    await ticks(8)
    check(east_attacked and hp.CurrentHealth == 80 and
        east_damage.SuccessfulHits == 1 and west_damage.SuccessfulHits == 1 and
        east_damage.LastConsumedSequence == 1 and west_damage.LastConsumedSequence == 1,
        "Two guardians independently use sequence one for separate 10 HP attacks")
    current_scene = null
    scene.queue_free()
    await process_frame
    await process_frame
    print("First adventure checks: ", failures, " failures")
    quit(1 if failures else 0)
