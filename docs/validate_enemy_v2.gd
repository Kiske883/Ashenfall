extends SceneTree

# V2-specific test: preserves V1 sources and evidence; capture uses new filenames only.
var failures := 0
func _initialize() -> void: call_deferred("run")
func check(condition: bool, message: String) -> void:
    if condition: print("PASS: ", message)
    else:
        failures += 1
        push_error("FAIL: " + message)
func anatomy_fingerprint(visual: Node3D) -> String:
    var context := HashingContext.new()
    context.start(HashingContext.HASH_SHA256)
    for mesh in visual.find_children("*", "MeshInstance3D", true, false):
        context.update(var_to_bytes(mesh.transform))
        for surface in range(mesh.mesh.get_surface_count()):
            context.update(var_to_bytes(mesh.mesh.surface_get_arrays(surface)))
    return context.finish().hex_encode()
func measure(model: Node3D, label: String) -> Dictionary:
    var triangles := 0
    var surfaces := 0
    var materials := {}
    var material_resources: Array[Material] = []
    for instance in model.find_children("*", "MeshInstance3D", true, false):
        for surface in range(instance.mesh.get_surface_count()):
            surfaces += 1
            var arrays: Array = instance.mesh.surface_get_arrays(surface)
            triangles += arrays[Mesh.ARRAY_INDEX].size() / 3
            var material: Material = instance.mesh.surface_get_material(surface)
            check(material != null, label + " material resolves")
            if material != null and not materials.has(material.get_instance_id()):
                materials[material.get_instance_id()] = true
                material_resources.append(material)
    print("VERSION_METRICS ", label, " triangles=", triangles, " surfaces=", surfaces, " materials=", materials.size())
    check(model.find_children("*", "CollisionObject3D", true, false).is_empty()
        and model.find_children("*", "NavigationAgent3D", true, false).is_empty(), label + " remains visual only")
    return {"triangles": triangles, "surfaces": surfaces, "materials": material_resources}
func run() -> void:
    root.size = Vector2i(1280, 800)
    var v1 = load("res://scenes/enemies/SkeletonWarrior.tscn").instantiate()
    root.add_child(v1)
    var v1_stats := measure(v1, "V1 standalone")
    var fingerprint := anatomy_fingerprint(v1.get_node("Visual"))
    check(v1_stats.triangles == 9911 and v1_stats.surfaces == 15 and v1_stats.materials.size() == 5, "V1 approved metrics unchanged")
    var v2 = load("res://scenes/enemies/SkeletonWarriorV2.tscn").instantiate()
    root.add_child(v2)
    var v2_stats := measure(v2, "V2 standalone")
    check(anatomy_fingerprint(v2.get_node("Anatomy/Visual")) == fingerprint, "V2 preserves anatomical geometry without changing V1 recipe")
    check(anatomy_fingerprint(v1.get_node("Visual")) == fingerprint, "loading V2 does not mutate V1")
    for material in v2_stats.materials:
        check(not v1_stats.materials.has(material), "V2 material resource independent of V1")
        if material is ShaderMaterial:
            check(material.shader.resource_path == "res://resources/art/enemies/FallenGuardianV2Surface.gdshader", "V2 shader source independent of V1")
    check(v2_stats.triangles > v1_stats.triangles and v2_stats.triangles < 12000, "V2 stays near V1 geometry budget")
    check(v2_stats.surfaces <= 18 and v2_stats.materials.size() <= 7, "V2 bounded surface/material overhead")
    check(v2.find_children("*", "Light3D", true, false).is_empty(), "emissive eyes add no real-time lights")
    var eyes: StandardMaterial3D = v2.EyeMaterial
    check(eyes.emission_enabled and eyes.transparency == BaseMaterial3D.TRANSPARENCY_DISABLED, "eyes are opaque emissive material")
    v1.free()
    v2.free()
    var scene = load("res://scenes/art/EnemyArtComparison.tscn").instantiate()
    root.add_child(scene)
    current_scene = scene
    for frame in range(16): await process_frame
    var camera: Camera3D = scene.get_node("Camera3D")
    for name in ["FallenGuardianV1", "FallenGuardianV2", "PlayerScaleReference"]:
        var model: Node3D = scene.get_node(name)
        var visible := true
        for instance in model.find_children("*", "MeshInstance3D", true, false):
            for surface in range(instance.mesh.get_surface_count()):
                var arrays: Array = instance.mesh.surface_get_arrays(surface)
                for vertex in arrays[Mesh.ARRAY_VERTEX]:
                    var point: Vector3 = instance.to_global(vertex)
                    visible = visible and root.get_visible_rect().has_point(camera.unproject_position(point))
        check(visible, name + " fully in inspection frame")
    if "--capture" in OS.get_cmdline_user_args():
        await RenderingServer.frame_post_draw
        check(root.get_texture().get_image().save_png("res://docs/milestone_04_enemy_comparison_v2.png") == OK, "saved V2 inspection comparison")
    scene.SetGameplayView(true)
    for frame in range(12): await process_frame
    check(is_equal_approx(camera.size, 22.0), "gameplay comparison retains World size 22")
    check(is_equal_approx(rad_to_deg(camera.rotation.x), -45.0), "isometric inclination unchanged")
    if "--capture" in OS.get_cmdline_user_args():
        await RenderingServer.frame_post_draw
        check(root.get_texture().get_image().save_png("res://docs/milestone_04_enemy_comparison_v2_gameplay.png") == OK, "saved V2 gameplay comparison")
    print("V2 ART CHECKS: ", failures, " failures; V1 anatomy SHA256 ", fingerprint)
    quit(0 if failures == 0 else 1)
