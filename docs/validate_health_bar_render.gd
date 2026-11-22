extends SceneTree

var failures := 0

func _initialize() -> void:
    call_deferred("run")

func check(ok: bool, label: String) -> void:
    if ok: print("PASS: ", label)
    else:
        failures += 1
        push_error("FAIL: " + label)

func is_fill_pixel(color: Color, hp: int) -> bool:
    var green := color.g > 0.25 and color.g > color.r * 1.25 and color.g > color.b * 1.1
    var orange := color.r > 0.25 and color.r > color.g * 1.2 and color.g > color.b * 1.35
    var red := color.r > 0.2 and color.r > color.g * 1.65 and color.r > color.b * 1.55
    if hp > 50: return green
    if hp > 25: return orange
    if hp > 0: return red
    return green or orange or red # At zero no former fill color may remain.

func run() -> void:
    if DisplayServer.get_name() == "headless":
        print("Render check requires Vulkan Forward+; skipped in headless mode")
        quit()
        return
    root.size = Vector2i(1280, 800)
    var demo: Node3D = load("res://scenes/world/AshenfallDwarfPlayableDemo.tscn").instantiate()
    root.add_child(demo)
    current_scene = demo
    # Keep the 03.2 comparison captures focused on the pre-existing enemy bar.
    var player_hud: CanvasLayer = demo.get_node_or_null("PlayerHealthHud")
    if player_hud: player_hud.visible = false
    var enemy: CharacterBody3D = demo.get_node("FallenGuardian")
    enemy.AttacksEnabled = false
    enemy.DetectionRadius = 0
    var health: Node = enemy.get_node("Health")
    var bar: Node3D = enemy.get_node("HealthBar")
    var quad: MeshInstance3D = bar.get_node("Bar")
    var camera: Camera3D = demo.get_node("World/CameraRig/Camera3D")
    for i in 90: await physics_frame
    var first_center: Vector2 = camera.unproject_position(quad.global_position)
    var first_transform: Transform3D = quad.global_transform
    var baseline_left := -1
    var previous_count := 100000
    var capture_prefix := OS.get_environment("ASHENFALL_BAR_CAPTURE_PREFIX")
    if capture_prefix.is_empty(): capture_prefix = "res://docs/combat_03_2_fixed_"
    for hp in [100, 75, 50, 25, 0]:
        if hp < 100: health.ApplyDamage(25)
        await physics_frame
        await RenderingServer.frame_post_draw
        var image: Image = root.get_texture().get_image()
        check(image.save_png(capture_prefix + str(hp) + ".png") == OK,
            "Captured actual playable scene at " + str(hp) + " HP")
        var center: Vector2 = camera.unproject_position(quad.global_position)
        check(center.distance_to(first_center) < 0.5 and
            quad.global_transform.is_equal_approx(first_transform),
            "Bar screen anchor and world transform stay fixed at " + str(hp) + " HP")
        check(bar.get_child_count() == 1 and quad.mesh is QuadMesh,
            "Runtime scene contains one bar quad at " + str(hp) + " HP")
        check(bar.visible == (hp > 0),
            "Guardian bar visibility follows living health at " + str(hp) + " HP")
        var left := 100000
        var right := -1
        var colored := 0
        for y in range(maxi(0, roundi(center.y) - 6), mini(image.get_height(), roundi(center.y) + 7)):
            for x in range(maxi(0, roundi(center.x) - 28), mini(image.get_width(), roundi(center.x) + 29)):
                if is_fill_pixel(image.get_pixel(x, y), hp):
                    colored += 1
                    left = mini(left, x)
                    right = maxi(right, x)
        print("RENDER_BAR hp=", hp, " center=", center, " colored_pixels=", colored,
            " left=", left, " right=", right)
        if hp > 0:
            check(colored > 0 and colored < previous_count,
                "Rendered colored portion decreases at " + str(hp) + " HP")
            if baseline_left < 0: baseline_left = left
            else: check(absi(left - baseline_left) <= 2,
                "Rendered fill keeps its left screen edge at " + str(hp) + " HP")
        else:
            check(colored == 0, "No colored bar remains at zero HP")
        previous_count = colored
    print("Health bar render checks: ", failures, " failures")
    quit(0 if failures == 0 else 1)
