extends SceneTree

var failures := 0
func _initialize() -> void: call_deferred("run")
func check(ok: bool, label: String) -> void:
    if ok: print("PASS: ", label)
    else:
        failures += 1
        push_error("FAIL: " + label)
func snapshot(model: Node3D) -> Dictionary:
    var result := {}
    for node: MeshInstance3D in model.find_children("*", "MeshInstance3D", true, false):
        var key: String = node.get_parent().name if node.name == "Geometry" else node.name
        var arrays := []
        var materials := []
        for surface in node.mesh.get_surface_count():
            arrays.append(var_to_bytes(node.mesh.surface_get_arrays(surface)).hex_encode().sha256_text())
            materials.append(node.mesh.surface_get_material(surface))
        result[key] = {"mesh": node.mesh, "arrays": arrays, "materials": materials,
            "transform": model.global_transform.affine_inverse() * node.global_transform}
    return result
func find(model: Node, name: String) -> Node3D:
    return model.find_child(name, true, false)
func run() -> void:
    root.size = Vector2i(1280,800)
    var original: Node3D = load("res://scenes/enemies/SkeletonWarriorV2.tscn").instantiate()
    root.add_child(original)
    var expected := snapshot(original)
    var model: Node3D = load("res://scenes/enemies/FallenGuardianPresentation.tscn").instantiate()
    var before := {}
    # Source Ready fires after both original generators, before the adapter's parent Ready.
    model.get_node("Source").ready.connect(func(): before.merge(snapshot(model)))
    root.add_child(model)
    var after := snapshot(model)
    check(expected.size() == 13 and after.size() == 13, "same thirteen mesh instances")
    for key in expected:
        check(after[key].arrays == expected[key].arrays, key + " exact vertex/index/normal/color arrays")
        check(after[key].transform.is_equal_approx(expected[key].transform), key + " original rest transform")
        check(after[key].mesh == before[key].mesh and after[key].materials == before[key].materials,
            key + " same resources before/after reparenting")
    check(model.find_children("*", "AnimationPlayer", true, false).is_empty(), "no animation clips or player introduced")
    check(model.find_children("*", "CollisionObject3D", true, false).is_empty() and
        model.find_children("*", "NavigationAgent3D", true, false).is_empty(), "presentation contains no physics/navigation")
    for pivot_name in ["HeadPivot", "LeftArmPivot", "RightArmPivot", "LeftLegPivot", "RightLegPivot", "PelvisPivot", "TorsoPivot"]:
        var pivot := find(model, pivot_name)
        var rest := pivot.transform
        var children := {}
        for child: Node3D in pivot.find_children("*", "MeshInstance3D", true, false):
            children[child] = pivot.global_transform.affine_inverse() * child.global_transform
        pivot.rotate_object_local(Vector3.RIGHT, 0.35)
        var rigid := true
        for child in children:
            rigid = rigid and children[child].is_equal_approx(pivot.global_transform.affine_inverse() * child.global_transform)
        check(rigid and not children.is_empty(), pivot_name + " carries all attached meshes rigidly")
        pivot.transform = rest
    check(find(model,"HeadPivot").is_ancestor_of(find(model,"CursedEyes")), "eyes follow head")
    check(find(model,"RightArmPivot").is_ancestor_of(find(model,"Sword")), "sword follows right arm")
    check(find(model,"LeftArmPivot").is_ancestor_of(find(model,"Shield")), "shield follows left arm")
    for side in ["Left", "Right"]:
        var sign_x := -1.0 if side == "Left" else 1.0
        check((model.to_local(find(model,side+"LegPivot").global_position)).is_equal_approx(Vector3(sign_x*0.11,0.878,0.015)), side+" hip pivot matches source")
    original.free()
    model.free()
    var scene: Node3D = load("res://scenes/art/ArticulationComparison.tscn").instantiate()
    root.add_child(scene)
    current_scene = scene
    for i in 16: await process_frame
    scene.InspectPivot()
    check(not find(scene.get_node("Articulated"),"HeadPivot").rotation.is_zero_approx(), "inspection control applies static pose")
    scene.ResetPose()
    check(find(scene.get_node("Articulated"),"HeadPivot").rotation.is_zero_approx(), "reset restores head pivot")
    var capture := "--capture" in OS.get_cmdline_user_args()
    for view in ["front", "back", "gameplay"]:
        if view == "back": scene.Turn(180.0)
        if view == "gameplay":
            scene.Turn(-180.0)
            scene.SetGameplayView(true)
            check(scene.get_node("Camera3D").size == 22, "gameplay size 22")
        for i in 12: await process_frame
        if capture:
            await RenderingServer.frame_post_draw
            check(root.get_texture().get_image().save_png("res://docs/milestone_05_articulation_"+view+".png") == OK, "capture "+view)
    print("Articulation checks: ", failures, " failures")
    quit(0 if failures == 0 else 1)
