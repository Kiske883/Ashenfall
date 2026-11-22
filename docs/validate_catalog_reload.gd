extends SceneTree

var failures := 0

func check(condition: bool, label: String) -> void:
    if condition: print("PASS: ", label)
    else:
        failures += 1
        push_error("FAIL: " + label)

func press_key(code: Key, shifted := false) -> void:
    var key := InputEventKey.new()
    key.keycode = code
    key.physical_keycode = code
    key.shift_pressed = shifted
    key.pressed = true
    Input.parse_input_event(key)
    var released := InputEventKey.new()
    released.keycode = code
    released.physical_keycode = code
    released.shift_pressed = shifted
    released.pressed = false
    Input.parse_input_event(released)

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    var expect_ready := OS.get_environment("ASHENFALL_EXPECT_CATALOG_READY") != "0"
    var scene: Node = load("res://scenes/world/AshenfallFirstAdventure.tscn").instantiate()
    root.add_child(scene)
    current_scene = scene
    var runtime: Node = root.get_node("CatalogRuntime")
    for i in 420: await process_frame
    check(runtime.IsReady == expect_ready, "Catalog startup reaches the expected readiness before reload")
    var requests_before: int = runtime.ReloadRequestCount
    press_key(KEY_F9)
    await process_frame
    check(runtime.ReloadRequestCount == requests_before,
        "Injected F9 cannot request catalog reload (the editor owns physical F9)")
    var was_paused := paused
    var time_scale := Engine.time_scale
    press_key(KEY_R, true)
    await process_frame
    check(runtime.ReloadRequestCount == requests_before + 1,
        "One Shift+R press requests exactly one reload")
    check(paused == was_paused && Engine.time_scale == time_scale,
        "Reload input does not change pause state or time scale")
    press_key(KEY_R, true)
    await process_frame
    check(runtime.ReloadRequestCount == requests_before + 2,
        "Second Shift+R press requests one further reload")
    var start := Time.get_ticks_msec()
    for i in 12: press_key(KEY_R, true)
    await process_frame
    var elapsed := Time.get_ticks_msec() - start
    check(runtime.ReloadRequestCount == requests_before + 14,
        "Rapid Shift+R presses each reach input handling")
    check(paused == was_paused && Engine.time_scale == time_scale,
        "Rapid reload input preserves pause state and time scale")
    check(elapsed < 300, "Repeated reload input leaves the next frame responsive")
    var last_tick := Time.get_ticks_msec()
    var maximum_gap := 0
    for i in 420:
        await process_frame
        var now := Time.get_ticks_msec()
        maximum_gap = maxi(maximum_gap, now - last_tick)
        last_tick = now
    check(maximum_gap < 300, "Reload completion does not stall later frames")
    check(runtime.IsReady == expect_ready,
        "Repeated reload retains the expected catalog availability")
    print("Catalog Shift+R checks: ", failures, " failures; first-frame latency=", elapsed,
        " ms; max reload-frame gap=", maximum_gap, " ms")
    quit(0 if failures == 0 else 1)
