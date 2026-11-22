extends SceneTree

var failures := 0

func _initialize() -> void:
    call_deferred("run")

func check(ok: bool, label: String) -> void:
    if ok: print("PASS: ", label)
    else:
        failures += 1
        push_error("FAIL: " + label)

func close_color(a: Color, b: Color) -> bool:
    return absf(a.r-b.r) < 0.01 and absf(a.g-b.g) < 0.01 and absf(a.b-b.b) < 0.01

func bar_pixels(bar: Node3D, texture: ImageTexture, fraction: float, fill_color: Color, background: Color) -> bool:
    if not is_equal_approx(bar.DisplayedFraction, fraction) or not close_color(bar.DisplayedColor, fill_color):
        return false
    # The headless renderer does not expose ImageTexture.update() through get_image().
    if DisplayServer.get_name() == "headless": return true
    var image: Image = texture.get_image()
    if image.get_width() != 128 or image.get_height() != 16: return false
    var filled: int = roundi(128 * fraction)
    for x in 128:
        if not close_color(image.get_pixel(x, 8), fill_color if x < filled else background): return false
    return true

func ticks(count: int) -> void:
    for i in count: await physics_frame

func click(camera: Camera3D, point: Vector3, pressed: bool) -> void:
    var event := InputEventMouseButton.new()
    event.button_index = MOUSE_BUTTON_LEFT
    event.position = camera.unproject_position(point)
    event.global_position = event.position
    event.pressed = pressed
    root.push_input(event, true)

func capture(label: String) -> void:
    if "--capture" not in OS.get_cmdline_user_args() or DisplayServer.get_name() == "headless": return
    await RenderingServer.frame_post_draw
    check(root.get_texture().get_image().save_png("res://docs/combat_03_2_" + label + ".png") == OK,
        "Rendered " + label)

func wait_attack(melee: Node, limit: int = 300) -> bool:
    for i in limit:
        if melee.State == 2: return true
        await physics_frame
    return false

func wait_impact(melee: Node, sequence: int, limit: int = 160) -> bool:
    for i in limit:
        if melee.ImpactSequence == sequence: return true
        await physics_frame
    return false

func run() -> void:
    root.size = Vector2i(1280, 800)
    # Isolated component checks cover clamp and a single terminal notification.
    var isolated: Node = load("res://scripts/combat/HealthComponent.cs").new()
    isolated.MaximumHealth = 10
    root.add_child(isolated)
    var isolated_deaths := []
    isolated.connect("Died", func(): isolated_deaths.append(1))
    check(isolated.CurrentHealth == 10 and isolated.IsAlive, "Health initializes at configured maximum")
    check(not isolated.ApplyDamage(0) and not isolated.ApplyDamage(-3) and isolated.CurrentHealth == 10,
        "Nonpositive damage is rejected")
    isolated.MaximumHealth = 7
    check(isolated.CurrentHealth == 7 and isolated.MaximumHealth == 7,
        "Reducing maximum health clamps current health")
    check(isolated.ApplyDamage(99) and isolated.CurrentHealth == 0 and isolated_deaths.size() == 1,
        "Overkill clamps to zero and emits death once")
    check(not isolated.ApplyDamage(1) and isolated_deaths.size() == 1,
        "Dead health cannot take more damage or emit another death")
    isolated.queue_free()

    var demo: Node3D = load("res://scenes/world/AshenfallDwarfPlayableDemo.tscn").instantiate()
    root.add_child(demo)
    current_scene = demo
    var world: Node3D = demo.get_node("World")
    var player: CharacterBody3D = world.get_node("FutureActors/Player")
    var enemy: CharacterBody3D = demo.get_node("FallenGuardian")
    var marker: Node = enemy.get_node("MeleeTarget")
    var health: Node = enemy.get_node("Health")
    var bar: Node3D = enemy.get_node("HealthBar")
    var bar_mesh: MeshInstance3D = bar.get_node("Bar")
    var melee: Node = demo.get_node("PlayerMelee")
    var resolver: Node = demo.get_node("MeleeDamage")
    var camera: Camera3D = world.get_node("CameraRig/Camera3D")
    var deaths := []
    var enemy_attacks := []
    health.connect("Died", func(): deaths.append(1))
    enemy.connect("AttackExecuted", func(_target: Node3D, _sequence: int): enemy_attacks.append(1))
    enemy.AttacksEnabled = false
    enemy.DetectionRadius = 0
    await ticks(75)
    var mesh_id: int = bar_mesh.get_instance_id()
    var quad_id: int = bar_mesh.mesh.get_instance_id()
    var material: StandardMaterial3D = bar_mesh.material_override
    var material_id: int = material.get_instance_id()
    var texture: ImageTexture = material.albedo_texture
    var texture_id: int = texture.get_instance_id()
    var fixed_transform: Transform3D = bar_mesh.global_transform
    check(health.MaximumHealth == 100 and health.CurrentHealth == 100 and marker.CanBeTargeted,
        "Guardian starts alive with 100 HP and a targetable collider")
    check(bar.get_child_count() == 1 and bar.visible and bar_pixels(bar, texture, 1.0,
        bar.HealthyColor, bar.BackgroundColor), "100 HP is one full green bar")
    check(resolver.Damage == 25 and absf(melee.ImpactFraction * melee.AttackDuration - 0.94) < 0.001,
        "Damage is 25 and logical impact is at 0.94 s of 1.70 s")
    await capture("full")
    player.global_position = enemy.global_position + Vector3(0, 0, 1.2)
    player.velocity = Vector3.ZERO
    player.reset_physics_interpolation()
    await ticks(2)

    # Commit one target; another enemy click during the swing cannot retarget it.
    check(melee.IssueAttack(marker) and await wait_attack(melee), "First command reaches attack state")
    var first: int = melee.AttackSequence
    var start_frame: int = Engine.get_physics_frames()
    check(health.CurrentHealth == 100 and melee.CommittedTarget == marker,
        "Attack start causes no damage and stores its original target")
    click(camera, enemy.global_position + Vector3.UP * 0.9, true)
    await ticks(2)
    click(camera, enemy.global_position + Vector3.UP * 0.9, false)
    check(melee.CommittedTarget == marker and melee.State == 2 and melee.AttackSequence == first,
        "Enemy click cannot retarget or duplicate a committed swing")
    check(await wait_impact(melee, first), "First attack reaches its logical impact")
    var impact_frames: int = Engine.get_physics_frames() - start_frame
    check(impact_frames >= 55 and impact_frames <= 59,
        "Impact is scheduled near 0.94 seconds of the 1.70-second logical cycle")
    check(health.CurrentHealth == 75 and resolver.SuccessfulHits == 1 and
        bar_pixels(bar, texture, 0.75, bar.HealthyColor, bar.BackgroundColor),
        "75 HP is one 75% green bar")
    await capture("damaged")
    check(not resolver.TryApplyImpact(marker, first) and health.CurrentHealth == 75,
        "Duplicate impact for the same sequence is rejected")
    melee.emit_signal("AttackImpact", marker, first)
    check(health.CurrentHealth == 75 and resolver.SuccessfulHits == 1,
        "Duplicate emitted impact event cannot apply damage again")
    await ticks(110)
    player.CancelDestination()
    player.global_position = enemy.global_position + Vector3(0, 0, 1.2)
    player.velocity = Vector3.ZERO
    await ticks(2)

    # The committed swing misses if the target is out of range at impact.
    check(melee.IssueAttack(marker) and await wait_attack(melee), "Second unique attack starts")
    var second: int = melee.AttackSequence
    player.global_position = enemy.global_position + Vector3(0, 0, 5)
    player.velocity = Vector3.ZERO
    check(await wait_impact(melee, second), "Out-of-range attack reaches impact")
    check(health.CurrentHealth == 75 and resolver.LastResolvedSequence == second,
        "Out-of-range impact is consumed without damage")
    check(not resolver.TryApplyImpact(marker, second), "Missed impact cannot become a delayed hit")
    await ticks(110)
    player.global_position = enemy.global_position + Vector3(0, 0, 1.2)
    player.velocity = Vector3.ZERO
    await ticks(2)

    # Insert solid geometry only after windup begins, so the approach can start.
    check(melee.IssueAttack(marker) and await wait_attack(melee), "Third unique attack starts")
    var third: int = melee.AttackSequence
    var wall := StaticBody3D.new()
    wall.collision_layer = 1
    wall.collision_mask = 0
    wall.position = (player.global_position + enemy.global_position) * 0.5
    var wall_shape := CollisionShape3D.new()
    var box := BoxShape3D.new()
    box.size = Vector3(0.5, 2, 0.5)
    wall_shape.shape = box
    wall.add_child(wall_shape)
    demo.add_child(wall)
    check(await wait_impact(melee, third), "Obstructed attack reaches impact")
    check(health.CurrentHealth == 75 and resolver.LastResolvedSequence == third,
        "Line-of-sight obstruction consumes impact without damage")
    wall.queue_free()
    await ticks(110)
    player.global_position = enemy.global_position + Vector3(0, 0, 1.2)
    player.velocity = Vector3.ZERO
    await ticks(2)

    for expected in [50, 25]:
        check(melee.IssueAttack(marker) and await wait_attack(melee), "Fresh command starts another attack")
        var sequence: int = melee.AttackSequence
        check(sequence > third and await wait_impact(melee, sequence), "Every attack has a distinct impact sequence")
        var expected_color: Color = bar.WoundedColor if expected == 50 else bar.CriticalColor
        check(health.CurrentHealth == expected and
            bar_pixels(bar, texture, float(expected) / 100.0, expected_color, bar.BackgroundColor),
            str(expected) + " HP has the expected width and color")
        await capture(str(expected) + "hp")
        check(bar.get_child_count() == 1 and bar_mesh.get_instance_id() == mesh_id and
            bar_mesh.mesh.get_instance_id() == quad_id and
            material.get_instance_id() == material_id and texture.get_instance_id() == texture_id and
            bar_mesh.global_transform.is_equal_approx(fixed_transform),
            "Health changes reuse one fixed quad, material and texture")
        await ticks(110)

    # A long enemy windup must be interrupted by the fourth valid dwarf hit.
    enemy.AttacksEnabled = true
    enemy.AttackWindupDuration = 2.5
    enemy.DetectionRadius = 6
    for i in 180:
        if enemy.State == 4: break
        await physics_frame
    check(enemy.State == 4, "Guardian enters an active attack windup before defeat")
    check(melee.IssueAttack(marker) and await wait_attack(melee), "Lethal attack starts during enemy windup")
    var lethal: int = melee.AttackSequence
    check(await wait_impact(melee, lethal), "Lethal attack reaches impact")
    await ticks(2)
    check(health.CurrentHealth == 0 and deaths.size() == 1 and enemy.State == 6 and enemy.PlayerTarget == null,
        "Fourth hit defeats Guardian exactly once")
    check(not marker.CanBeTargeted and enemy.get_node("CollisionShape3D").disabled and
        enemy.collision_layer == 0 and enemy.collision_mask == 0,
        "Defeated Guardian loses targetability and collision safely")
    check(not resolver.TryApplyImpact(marker, lethal) and not health.ApplyDamage(25) and deaths.size() == 1,
        "Repeated impact and postmortem damage are both rejected")
    await ticks(150)
    check(enemy.State == 6 and enemy_attacks.is_empty() and is_instance_valid(enemy),
        "Death cancels enemy attack and AI permanently without deleting node")
    check(not bar.visible and bar_pixels(bar, texture, 0.0, bar.CriticalColor, bar.BackgroundColor),
        "Zero HP hides the persistent bar immediately")
    check(bar.get_child_count() == 1 and bar_mesh.get_instance_id() == mesh_id and
        bar_mesh.mesh.get_instance_id() == quad_id and
        material.get_instance_id() == material_id and texture.get_instance_id() == texture_id and
        bar_mesh.global_transform.is_equal_approx(fixed_transform),
        "Defeat does not accumulate geometry or move the bar")
    await capture("defeated")

    # Finish the dwarf's active animation, then click the former enemy location.
    var before: int = melee.AttackSequence
    click(camera, enemy.global_position + Vector3(0, 0.9, 0), true)
    await ticks(2)
    click(camera, enemy.global_position + Vector3(0, 0.9, 0), false)
    check(melee.AttackSequence == before and melee.SelectedTarget == null,
        "Click on defeated Guardian does not select or attack it")
    player.CancelDestination()
    player.global_position = enemy.global_position + Vector3(0, 0, 2.2)
    player.velocity = Vector3.ZERO
    await ticks(2)
    check(player.TrySetDestination(enemy.global_position + Vector3(0, 0, -2.2)),
        "Navigation accepts a route through the defeated enemy position")
    var closest := 100.0
    var crossed := false
    for i in 270:
        await physics_frame
        var horizontal := Vector2(player.global_position.x-enemy.global_position.x,
            player.global_position.z-enemy.global_position.z).length()
        closest = minf(closest, horizontal)
        if player.global_position.z < enemy.global_position.z - 0.7:
            crossed = true
            break
    check(crossed and closest < 0.33, "Dwarf physically passes through former Guardian position")

    # A second actor dies externally after its attack was committed but before impact.
    var second_enemy: CharacterBody3D = load("res://scenes/enemies/FallenGuardian.tscn").instantiate()
    second_enemy.position = enemy.position
    second_enemy.PlayerTarget = player
    second_enemy.AttacksEnabled = false
    second_enemy.DetectionRadius = 0
    demo.add_child(second_enemy)
    await ticks(3)
    var second_bar: MeshInstance3D = second_enemy.get_node("HealthBar/Bar")
    check(second_bar.material_override.get_instance_id() != material_id and
        second_bar.material_override.albedo_texture.get_instance_id() != texture_id,
        "Each enemy has its own persistent material and texture")
    player.CancelDestination()
    player.global_position = second_enemy.global_position + Vector3(0, 0, 1.2)
    player.velocity = Vector3.ZERO
    await ticks(2)
    var second_marker: Node = second_enemy.get_node("MeleeTarget")
    var second_health: Node = second_enemy.get_node("Health")
    var second_deaths := []
    second_health.connect("Died", func(): second_deaths.append(1))
    check(melee.IssueAttack(second_marker) and await wait_attack(melee),
        "Attack can commit to another independently instanced target")
    var pending: int = melee.AttackSequence
    var hits_before: int = resolver.SuccessfulHits
    check(second_health.ApplyDamage(100) and second_deaths.size() == 1,
        "Target can die while a dwarf attack is still active")
    check(not second_enemy.get_node("HealthBar").visible,
        "Independently instanced Guardian hides its bar on lethal damage")
    check(await wait_impact(melee, pending) and resolver.SuccessfulHits == hits_before and
        second_health.CurrentHealth == 0 and second_deaths.size() == 1,
        "Committed impact on a newly dead target does no damage or duplicate death")
    await ticks(55)
    second_enemy.queue_free()
    await process_frame
    print("Health/damage/death checks: ", failures, " failures; closest corpse passage=", closest)
    quit(0 if failures == 0 else 1)
