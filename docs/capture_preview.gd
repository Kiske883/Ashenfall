extends SceneTree

# Run with the regular renderer, never --headless, to capture the actual scene.
func _initialize() -> void:
    call_deferred("capture")

func capture() -> void:
    var world = load("res://scenes/world/World.tscn").instantiate()
    root.add_child(world)
    current_scene = world
    for frame in range(12):
        await process_frame
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://docs/milestone_01_preview.png")
    if result != OK:
        push_error("Preview save failed: %s" % result)
        quit(1)
        return
    print("Rendered preview saved to docs/milestone_01_preview.png")
    quit()
