extends SceneTree

# Headless checks; -- --capture writes only the new V2 comparison image.
func _initialize() -> void: call_deferred("run")
func fail(message: String) -> void:
    push_error(message)
    quit(1)
func run() -> void:
    # Headless windows may default to a square viewport; use the actual project framing.
    root.size = Vector2i(int(ProjectSettings.get_setting("display/window/size/viewport_width", 1280)),
        int(ProjectSettings.get_setting("display/window/size/viewport_height", 800)))
    var scene = load("res://scenes/art/TreeArtComparison.tscn").instantiate()
    root.add_child(scene)
    current_scene = scene
    for frame in range(16): await process_frame
    var camera: Camera3D = scene.get_node("Camera3D")
    for pine_name in ["CursedPineV1", "CursedPine"]:
        var pine: Node3D = scene.get_node(pine_name)
        var bounds := AABB()
        var first := true
        var triangles := 0
        var visible_vertices := 0
        var total_vertices := 0
        var materials := {}
        for batch_name in ["ProceduralBark", "ProceduralNeedles"]:
            var batch: MeshInstance3D = pine.get_node(batch_name)
            if batch.mesh == null or batch.mesh.get_surface_count() != 1:
                fail("Expected one surface in " + pine_name + "/" + batch_name)
                return
            if batch.material_override == null:
                fail("Missing comparison material")
                return
            materials[batch.material_override.get_instance_id()] = true
            var arrays := batch.mesh.surface_get_arrays(0)
            triangles += arrays[Mesh.ARRAY_INDEX].size() / 3
            bounds = batch.mesh.get_aabb() if first else bounds.merge(batch.mesh.get_aabb())
            first = false
            for vertex in arrays[Mesh.ARRAY_VERTEX]:
                var point: Vector3 = batch.to_global(vertex)
                total_vertices += 1
                if not camera.is_position_behind(point) and root.get_visible_rect().has_point(camera.unproject_position(point)):
                    visible_vertices += 1
        print("ART_METRICS ", pine_name, " triangles=", triangles, " bounds=", bounds,
            " surfaces=2 materials=", materials.size(), " visible_vertices=", visible_vertices, "/", total_vertices)
        if (pine_name == "CursedPineV1" and triangles != 28642) or triangles > 28642 or bounds.size.y > 6.3 or bounds.size.y < 5.4:
            fail("Art baseline, budget or scale check failed")
            return
        if materials.size() != 2:
            fail("Expected two distinct opaque materials")
            return
        if visible_vertices != total_vertices:
            fail("Comparison tree is cropped")
            return
    var original: Node3D = scene.get_node("OriginalPine")
    if not original.visible or not root.get_visible_rect().has_point(camera.unproject_position(original.global_position + Vector3(0, 3, 0))):
        fail("Original pine missing from camera")
        return
    if "--capture" in OS.get_cmdline_user_args():
        await RenderingServer.frame_post_draw
        var result := root.get_texture().get_image().save_png("res://docs/milestone_03_tree_comparison_v2.png")
        if result != OK:
            fail("Unable to save V2 comparison render")
            return
        print("Saved docs/milestone_03_tree_comparison_v2.png; original image preserved")
    quit()
