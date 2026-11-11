extends SceneTree

# Read-only geometry/startup checks. -- --capture saves only new Milestone 04 evidence.
func _initialize() -> void: call_deferred("run")
func fail(message: String) -> void:
    push_error(message)
    quit(1)
func measure(enemy: Node3D) -> bool:
    var triangles := 0
    var surfaces := 0
    var materials := {}
    var bounds := AABB()
    var first := true
    var meshes := enemy.find_children("*", "MeshInstance3D", true, false)
    for mesh_instance in meshes:
        var mesh: ArrayMesh = mesh_instance.mesh
        if mesh == null:
            fail("Missing anatomy/equipment mesh")
            return false
        var transform: Transform3D = enemy.global_transform.affine_inverse() * mesh_instance.global_transform
        var box: AABB = transform * mesh.get_aabb()
        bounds = box if first else bounds.merge(box)
        first = false
        for index in range(mesh.get_surface_count()):
            surfaces += 1
            var arrays := mesh.surface_get_arrays(index)
            triangles += arrays[Mesh.ARRAY_INDEX].size() / 3
            var material := mesh.surface_get_material(index)
            if material == null:
                fail("Missing enemy material")
                return false
            materials[material.get_instance_id()] = true
    for part in ["Head", "RibcageAndSpine", "Pelvis", "LeftArm", "RightArm", "LeftLeg", "RightLeg", "Sword", "Shield", "LeatherAndRags"]:
        if not enemy.has_node("Visual/" + part):
            fail("Missing logical body/equipment part " + part)
            return false
    print("ENEMY_METRICS triangles=", triangles, " surfaces=", surfaces, " materials=", materials.size(),
        " mesh_parts=", meshes.size(), " bounds=", bounds)
    if triangles > 20000 or materials.size() > 6 or bounds.size.y < 1.7 or bounds.size.y > 2.2:
        fail("Enemy art budget/scale exceeded")
        return false
    if not enemy.find_children("*", "CollisionObject3D", true, false).is_empty() or not enemy.find_children("*", "NavigationAgent3D", true, false).is_empty():
        fail("Visual prototype must not contain gameplay actor systems")
        return false
    return true
func run() -> void:
    root.size = Vector2i(1280, 800)
    var standalone = load("res://scenes/enemies/SkeletonWarrior.tscn").instantiate()
    root.add_child(standalone)
    if not measure(standalone): return
    root.remove_child(standalone)
    standalone.free()
    var scene = load("res://scenes/art/EnemyArtPreview.tscn").instantiate()
    root.add_child(scene)
    current_scene = scene
    for frame in range(16): await process_frame
    if not measure(scene.get_node("FallenGuardian")): return
    if "--capture" in OS.get_cmdline_user_args():
        await RenderingServer.frame_post_draw
        if root.get_texture().get_image().save_png("res://docs/milestone_04_enemy_art.png") != OK:
            fail("Cannot save enemy preview")
            return
        scene.SetGameplayView(true)
        for frame in range(12): await process_frame
        await RenderingServer.frame_post_draw
        if root.get_texture().get_image().save_png("res://docs/milestone_04_enemy_gameplay_scale.png") != OK:
            fail("Cannot save gameplay-scale preview")
            return
        print("Saved both new Milestone 04 previews")
    else:
        scene.SetGameplayView(true)
        if not is_equal_approx(scene.get_node("Camera3D").size, 22.0):
            fail("Gameplay scale must match the existing world camera")
            return
    quit()
