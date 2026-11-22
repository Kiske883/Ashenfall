extends SceneTree

func _initialize() -> void:
    call_deferred("run")

func capture(label: String) -> void:
    await RenderingServer.frame_post_draw
    var image: Image = root.get_texture().get_image()
    var error: Error = image.save_png("res://docs/combat_03_3_hud_" + label + ".png")
    if error != OK: push_error("HUD capture failed: " + label)
    else: print("CAPTURE: ", label, " ", image.get_size())

func run() -> void:
    root.size = Vector2i(1280, 800)
    var demo: Node3D = load("res://scenes/world/AshenfallDwarfPlayableDemo.tscn").instantiate()
    root.add_child(demo)
    current_scene = demo
    var enemy: Node = demo.get_node("FallenGuardian")
    enemy.AttacksEnabled = false
    enemy.DetectionRadius = 0
    var health: Node = demo.get_node("World/FutureActors/Player/Health")
    for i in 75: await physics_frame
    await capture("100")
    health.ApplyDamage(10)
    await capture("90")
    DisplayServer.window_set_size(Vector2i(800, 600))
    for i in 5: await process_frame
    await capture("90_small")
    DisplayServer.window_set_size(Vector2i(1280, 800))
    for i in 5: await process_frame
    health.ApplyDamage(90)
    for i in 35: await physics_frame
    await capture("0")
    quit()
