extends SceneTree

var failures := 0

func check(ok: bool, message: String) -> void:
    if ok: print("PASS: ", message)
    else:
        failures += 1
        push_error("FAIL: " + message)

func close_to(actual: float, expected: float) -> bool:
    return absf(actual - expected) < 0.001

func press_reload() -> void:
    var down := InputEventKey.new()
    down.keycode = KEY_R
    down.physical_keycode = KEY_R
    down.shift_pressed = true
    down.pressed = true
    Input.parse_input_event(down)
    var up := InputEventKey.new()
    up.keycode = KEY_R
    up.physical_keycode = KEY_R
    up.shift_pressed = true
    Input.parse_input_event(up)

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    var ready_expected := OS.get_environment("ASHENFALL_EXPECT_CATALOG_READY") != "0"
    var scene: Node = load("res://scenes/world/AshenfallCatalogAdventure.tscn").instantiate()
    root.add_child(scene)
    current_scene = scene
    var runtime: Node = root.get_node("CatalogRuntime")
    var startup_deadline := Time.get_ticks_msec() + 12000
    while Time.get_ticks_msec() < startup_deadline:
        if scene.AdventureStarted or runtime.State == 3: break
        await process_frame
    if not ready_expected:
        check(not scene.AdventureStarted, "No actor or partial adventure starts without a valid catalog")
        var panel := scene.get_node("CatalogLoading/Center/StatusPanel")
        var box := panel.get_child(0)
        var retry: Button = box.get_child(1)
        var ui_deadline := Time.get_ticks_msec() + 3000
        while Time.get_ticks_msec() < ui_deadline:
            if not retry.disabled: break
            await process_frame
        check(retry.visible and not retry.disabled, "Failure provides an explicit retry control")
        retry.emit_signal("pressed")
        var retry_deadline := Time.get_ticks_msec() + 7000
        while Time.get_ticks_msec() < retry_deadline:
            if runtime.State == 3 and not retry.disabled: break
            await process_frame
        check(not scene.AdventureStarted, "Unavailable API retry cannot start an unvalidated adventure")
        print("Catalog gameplay checks: ", failures, " failures (API unavailable)")
        quit(0 if failures == 0 else 1)
        return

    check(scene.AdventureStarted, "Validated catalog starts the full first adventure")
    if not scene.AdventureStarted:
        quit(1)
        return
    var adventure := scene.get_node("AshenfallFirstAdventure")
    var player := adventure.get_node("World/FutureActors/Player")
    var health := player.get_node("Health")
    var melee := adventure.get_node("PlayerMelee")
    check(scene.PinnedVersionKey == "prototype-1", "Adventure pins published prototype-1")
    check(scene.PinnedContentSha256 == "a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17",
        "Adventure pins approved V1 content hash")
    check(health.MaximumHealth == 100 and health.CurrentHealth == 100, "Dwarf begins at catalog maximum health")
    check(close_to(player.MovementSpeed, 2.5), "Dwarf movement uses catalog speed")
    check(close_to(melee.MeleeRange, 1.45) and close_to(melee.AttackDuration, 1.7) and
        close_to(melee.ImpactFraction * melee.AttackDuration, 0.94),
        "Dwarf melee range, cycle and impact use catalog values")
    check(adventure.get_node("MeleeDamage").Damage == 25, "Dwarf fixed damage uses catalog value")
    for name in ["FallenGuardian", "GuardianWest", "GuardianEast"]:
        var guardian := adventure.get_node(name)
        var side: bool = name != "FallenGuardian"
        check(guardian.get_node("Health").MaximumHealth == 100 and
            guardian.get_node("Health").CurrentHealth == 100, name + " catalog health initialized before _Ready")
        check(close_to(guardian.AttackRange, 1.45) and close_to(guardian.AttackWindupDuration, 1.2) and
            close_to(guardian.AttackRecoveryDuration, 1.0), name + " catalog attack plus explicit 1.2s windup")
        check(close_to(guardian.MovementSpeed, 1.0) and close_to(guardian.ReturnSpeed, 0.7) and
            close_to(guardian.StoppingDistance, 1.3) and close_to(guardian.ResumeChaseDistance, 1.7),
            name + " catalog movement and pursuit distances")
        check(close_to(guardian.DetectionRadius, 5.5 if side else 6.0) and
            close_to(guardian.DisengagementRadius, 8.0 if side else 10.0) and
            close_to(guardian.MaximumDistanceFromSpawn, 8.0 if side else 12.0),
            name + " explicit overrides take precedence only where configured")
    for name in ["EnemyMeleeDamage", "WestDamage", "EastDamage"]:
        var damage := adventure.get_node(name)
        check(damage.Damage == 10 and close_to(damage.ImpactDelay, 0.1),
            name + " catalog fixed damage and post-execute impact")

    press_reload()
    for i in 240: await process_frame
    check(scene.AdventureStarted and scene.PinnedVersionKey == "prototype-1" and
        health.CurrentHealth == 100 and adventure.get_node("MeleeDamage").Damage == 25 and
        close_to(player.MovementSpeed, 2.5), "Shift+R does not rebind active adventure statistics")
    var reset := adventure.get_node("EncounterReset")
    check(reset.Restart(), "Adventure restart is available from the catalog entry scene")
    for i in 600:
        await process_frame
        if current_scene != scene and current_scene != null and current_scene.AdventureStarted: break
    check(current_scene != scene and current_scene != null and current_scene.AdventureStarted,
        "Restart creates a new adventure from the available validated snapshot")
    if current_scene != null and current_scene != scene and current_scene.AdventureStarted:
        check(current_scene.PinnedVersionKey == "prototype-1", "Restart pins the available version again")
        var restarted := current_scene.get_node("AshenfallFirstAdventure")
        for name in ["FallenGuardian", "GuardianWest", "GuardianEast"]:
            restarted.get_node(name + "/Health").ApplyDamage(100)
        await process_frame
        check(restarted.get_node("Objective").DefeatedCount == 3,
            "Catalog adventure retains three-Guardian objective accounting")
        for name in ["FallenGuardian", "GuardianWest", "GuardianEast"]:
            check(not restarted.get_node(name + "/HealthBar").visible,
                name + " health bar remains hidden after death")
        restarted.get_node("World/FutureActors/Player").global_position = \
            restarted.get_node("EasternRuinGoal").global_position
        for i in 10: await physics_frame
        check(restarted.get_node("Session").Outcome == 1,
            "Catalog adventure retains the victory condition")
        var victory_scene := current_scene
        check(restarted.get_node("EncounterReset").Restart(), "Victory scene can restart")
        for i in 600:
            await process_frame
            if current_scene != null and current_scene != scene and current_scene != victory_scene \
                and current_scene.AdventureStarted: break
        if current_scene != null and current_scene.AdventureStarted:
            var defeated_adventure := current_scene.get_node("AshenfallFirstAdventure")
            defeated_adventure.get_node("World/FutureActors/Player/Health").ApplyDamage(100)
            await process_frame
            check(defeated_adventure.get_node("Session").Outcome == 2,
                "Catalog adventure retains the defeat condition")
    print("Catalog gameplay checks: ", failures, " failures (API available)")
    quit(0 if failures == 0 else 1)
