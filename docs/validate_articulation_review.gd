extends "res://docs/validate_articulation.gd"

func run() -> void:
    root.size = Vector2i(1280,800)
    var scene: Node3D = load("res://scenes/art/ArticulationComparison.tscn").instantiate()
    root.add_child(scene)
    current_scene = scene
    for i in 16: await process_frame
    var reference: Node3D = scene.get_node("Original")
    var model: Node3D = scene.get_node("Articulated")
    var camera: Camera3D = scene.get_node("Camera3D")
    var ref_rest := snapshot(reference)
    var rest := snapshot(model)
    var ref_transform := reference.global_transform
    var capture := "--capture" in OS.get_cmdline_user_args()
    var head_angles := []
    for mode in ["PelvisLeft", "PelvisRight", "Shield", "Sword", "HeadLeft", "HeadRight"]:
        scene.ShowDemo(mode)
        var posed := snapshot(model)
        var key: String = "Head" if mode.begins_with("Head") else (mode if mode in ["Shield","Sword"] else "Pelvis")
        var max_screen_motion := 0.0
        var max_height_motion := 0.0
        for mesh: MeshInstance3D in model.find_children("*", "MeshInstance3D", true, false):
            var mesh_key: String = mesh.get_parent().name if mesh.name == "Geometry" else mesh.name
            for surface in mesh.mesh.get_surface_count():
                for vertex in mesh.mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]:
                    var old: Vector3 = model.to_global(rest[mesh_key].transform * vertex)
                    var now: Vector3 = mesh.to_global(vertex)
                    max_height_motion = maxf(max_height_motion, absf(now.y-old.y))
                    if mesh_key == key:
                        max_screen_motion = maxf(max_screen_motion, camera.unproject_position(old).distance_to(camera.unproject_position(now)))
        check(max_screen_motion > (8 if mode.begins_with("Head") else (10 if mode.begins_with("Pelvis") else 15)), mode+" visible projected motion px="+str(max_screen_motion))
        if mode.begins_with("Pelvis"):
            check(max_height_motion < 0.00001, mode+" no vertex rises or grows")
        if mode.begins_with("Head"):
            head_angles.append(find(model,"HeadPivot").rotation.y)
            check(find(model,"HeadPivot").is_ancestor_of(find(model,"CursedEyes")), mode+" eyes attached")
        for name in ["PelvisPivot","HeadPivot","LeftArmPivot","RightArmPivot"]:
            check(find(model,name).scale.is_equal_approx(Vector3.ONE), mode+" no scale at "+name)
        check(reference.global_transform.is_equal_approx(ref_transform) and snapshot(reference) == ref_rest, mode+" reference unchanged")
        for k in rest:
            check(posed[k].mesh == rest[k].mesh and posed[k].arrays == rest[k].arrays and posed[k].materials == rest[k].materials, mode+" preserves "+k+" resources")
        if capture:
            for i in 8: await process_frame
            await RenderingServer.frame_post_draw
            check(root.get_texture().get_image().save_png("res://docs/milestone_05_review_"+mode.to_lower()+".png") == OK, "capture "+mode)
        scene.ResetPose()
        var reset := snapshot(model)
        for k in rest:
            check(reset[k].transform.is_equal_approx(rest[k].transform), mode+" reset "+k)
    check(head_angles[0] > 0 and head_angles[1] < 0, "head directions independently reachable")
    scene.ShowDemo("Sword")
    scene.SetAutomatic(true)
    var pose_a: Transform3D = find(model,"RightArmPivot").transform
    for i in 90: await process_frame
    check(not pose_a.is_equal_approx(find(model,"RightArmPivot").transform), "slow repeat changes selected pivot")
    scene.ResetPose()
    for i in 20: await process_frame
    check(find(model,"RightArmPivot").rotation.is_zero_approx(), "reset stops repeat")
    scene.Turn(45.0)
    check(reference.global_transform.is_equal_approx(ref_transform), "view control leaves original stationary")
    scene.ResetPose()
    check(model.global_rotation.is_equal_approx(reference.global_rotation), "reset restores equivalent orientation")
    print("Articulation review checks: ", failures, " failures")
    quit(0 if failures == 0 else 1)
