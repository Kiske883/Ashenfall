extends SceneTree

func _initialize() -> void:
    call_deferred("run")

func capture(label: String) -> void:
    await RenderingServer.frame_post_draw
    var image: Image = root.get_texture().get_image()
    var error := image.save_png("res://docs/milestone_07_" + label + ".png")
    if error != OK: push_error("Could not capture " + label)
    else: print("CAPTURE: ", label, " ", image.get_size())

func run() -> void:
    root.size = Vector2i(1280, 800)
    var scene: Node3D = load("res://scenes/world/AshenfallFirstAdventure.tscn").instantiate()
    root.add_child(scene)
    current_scene = scene
    for i in 75: await physics_frame
    await capture("start")

    var player: CharacterBody3D = scene.get_node("World/FutureActors/Player")
    player.InputEnabled = false
    player.global_position = Vector3(-5, 0, -11.5)
    player.velocity = Vector3.ZERO
    player.reset_physics_interpolation()
    scene.get_node("FallenGuardian").AttacksEnabled = false
    scene.get_node("GuardianEast").AttacksEnabled = false
    for i in 95: await physics_frame
    await capture("encounter")

    for name in ["FallenGuardian", "GuardianWest", "GuardianEast"]:
        scene.get_node(name + "/Health").ApplyDamage(100)
    player.global_position = scene.get_node("EasternRuinGoal").global_position
    for i in 8: await physics_frame
    await capture("victory")

    scene.get_node("EncounterReset").Restart()
    for i in 30:
        await process_frame
        if current_scene != null and current_scene != scene: break
    var fresh: Node3D = current_scene
    fresh.get_node("World/FutureActors/Player/Health").ApplyDamage(100)
    for i in 35: await physics_frame
    await capture("defeat")
    quit()
