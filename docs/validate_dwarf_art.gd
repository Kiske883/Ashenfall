extends SceneTree

var failures := 0
func _initialize() -> void: call_deferred("run")
func check(ok: bool,label: String) -> void:
    if ok: print("PASS: ",label)
    else:
        failures += 1
        push_error("FAIL: "+label)
func bounds(model: Node3D,part: Node3D) -> AABB:
    var first := true
    var result := AABB()
    var meshes := part.find_children("*","MeshInstance3D",true,false)
    if part is MeshInstance3D: meshes.append(part)
    for mesh: MeshInstance3D in meshes:
        for surface in mesh.mesh.get_surface_count():
            for vertex in mesh.mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]:
                var p: Vector3 = model.to_local(mesh.to_global(vertex))
                if first: result = AABB(p,Vector3.ZERO); first = false
                else: result = result.expand(p)
    return result
func run() -> void:
    var guardian: Node3D = load("res://scenes/enemies/FallenGuardianV3Presentation.tscn").instantiate()
    root.add_child(guardian)
    var head := bounds(guardian,guardian.find_child("Head",true,false))
    var feet := bounds(guardian,guardian.find_child("LeftFoot",true,false)).merge(bounds(guardian,guardian.find_child("RightFoot",true,false)))
    print("GUARDIAN_MEASURE top_head=",head.end.y," sole_min=",feet.position.y," sole_to_head=",head.end.y-feet.position.y," origin_ground_to_head=",head.end.y," target_75_percent=",head.end.y*0.75)
    var dwarf: Node3D = load("res://scenes/characters/DwarfBerserker.tscn").instantiate()
    root.add_child(dwarf)
    var crown := bounds(dwarf, dwarf.find_child("BaldHead", true, false))
    var boots := bounds(dwarf, dwarf.find_child("LeftBoot", true, false)).merge(bounds(dwarf, dwarf.find_child("RightBoot", true, false)))
    var height := crown.end.y - boots.position.y
    print("DWARF_MEASURE crown=", crown.end.y, " soles=", boots.position.y, " height=", height, " ratio_sole_to_crown=", height/(head.end.y-feet.position.y), " conservative_origin_ratio=", height/head.end.y)
    check(absf(boots.position.y) < 0.00001, "Flat boot soles contact y=0")
    check(height >= (head.end.y-feet.position.y)*0.75 and height >= head.end.y*0.75, "Dwarf exceeds 75% under both ground interpretations")
    check(absf(height-1.460) < 0.00001, "Measured bald crown height 1.460")
    for side in ["Left", "Right"]:
        var hand: Node3D = dwarf.find_child(side+"Gauntlet", true, false).get_node("GripPoint")
        var grip: Node3D = dwarf.find_child("BattleAxe", true, false).get_node(side+"Grip")
        check(hand.global_position.distance_to(grip.global_position) < 0.00001, side+" gripping hand aligns with axe shaft anchor")
        for joint in ["ShoulderPivot", "ElbowPivot", "WristPivot", "HipPivot", "KneePivot", "AnklePivot"]:
            check(dwarf.find_child(side+joint, true, false) != null, side+joint+" exists")
    check(dwarf.find_child("Beard", true, false).get_parent() == dwarf.find_child("HeadPivot", true, false), "Separate beard follows head pivot")
    check(dwarf.find_children("*", "CollisionObject3D", true, false).is_empty() and dwarf.find_children("*", "AnimationPlayer", true, false).is_empty(), "Static presentation contains no gameplay collision or animation")
    var materials := {}
    var triangle_count := 0
    var surface_count := 0
    var signature := []
    for mesh: MeshInstance3D in dwarf.find_children("*", "MeshInstance3D", true, false):
        check(mesh.scale.is_equal_approx(Vector3.ONE), str(mesh.get_parent().name)+" unit scale")
        for surface in mesh.mesh.get_surface_count():
            var arrays := mesh.mesh.surface_get_arrays(surface)
            var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
            check(vertices.size() == arrays[Mesh.ARRAY_NORMAL].size() and vertices.size() == arrays[Mesh.ARRAY_COLOR].size(), str(mesh.get_parent().name)+" normals/colors preserved")
            triangle_count += arrays[Mesh.ARRAY_INDEX].size()/3
            surface_count += 1
            var material: ShaderMaterial = mesh.mesh.surface_get_material(surface)
            check(material != null and material.shader != null, "Material and shader resolve")
            materials[material] = true
            signature.append(hash(vertices))
    var second: Node3D = load("res://scenes/characters/DwarfBerserker.tscn").instantiate()
    root.add_child(second)
    var repeated := []
    for mesh: MeshInstance3D in second.find_children("*", "MeshInstance3D", true, false):
        for surface in mesh.mesh.get_surface_count():
            repeated.append(hash(mesh.mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]))
    check(signature == repeated, "Fresh instance reproduces geometry deterministically")
    print("DWARF_BUDGET triangles=", triangle_count, " surfaces=", surface_count, " shared_materials=", materials.size())
    guardian.free(); dwarf.free(); second.free()
    var preview: Node3D = load("res://scenes/art/DwarfBerserkerPreview.tscn").instantiate()
    root.add_child(preview)
    current_scene = preview
    for view in ["Front", "Side", "Rear", "Isometric"]:
        preview.call("SetView", view)
        check(preview.get_node("Camera3D").current, view+" inspection camera active")
        if "--capture" in OS.get_cmdline_user_args():
            for frame in range(8): await process_frame
            await RenderingServer.frame_post_draw
            check(root.get_texture().get_image().save_png("res://docs/milestone_06a_dwarf_"+view.to_lower()+".png") == OK, view+" captured")
    if "--capture" in OS.get_cmdline_user_args():
        preview.get_node("Camera3D").size = 22
        for frame in range(8): await process_frame
        await RenderingServer.frame_post_draw
        check(root.get_texture().get_image().save_png("res://docs/milestone_06a_dwarf_gameplay.png") == OK, "Gameplay size 22 captured")
    print("DWARF_ART failures=", failures)
    quit(0 if failures == 0 else 1)
