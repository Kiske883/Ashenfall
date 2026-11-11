extends "res://docs/validate_articulation.gd"

var material_keys := {}
func vec(v: Vector3) -> String:
    return "%d,%d,%d" % [roundi(v.x*100000),roundi(v.y*100000),roundi(v.z*100000)]
func material_key(material: Material) -> String:
    if material_keys.has(material): return material_keys[material]
    var values := []
    if material is ShaderMaterial:
        values.append(material.shader.resource_path)
        for uniform in material.shader.get_shader_uniform_list():
            values.append([uniform.name, material.get_shader_parameter(uniform.name)])
    else:
        for property in material.get_property_list():
            if property.usage & PROPERTY_USAGE_STORAGE and not property.name.begins_with("resource_") and property.name != "script":
                values.append([property.name,material.get(property.name)])
    var key := var_to_bytes(values).hex_encode().sha256_text()
    material_keys[material] = key
    return key
func triangles(model: Node3D) -> Dictionary:
    var result := {}
    var surfaces := 0
    var count := 0
    var materials := {}
    for mesh: MeshInstance3D in model.find_children("*","MeshInstance3D",true,false):
        var transform: Transform3D = model.global_transform.affine_inverse()*mesh.global_transform
        for surface in mesh.mesh.get_surface_count():
            surfaces += 1
            var arrays := mesh.mesh.surface_get_arrays(surface)
            var material := material_key(mesh.mesh.surface_get_material(surface))
            materials[material] = true
            var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
            for index in range(0,indices.size(),3):
                var corners: Array[String] = []
                for corner in 3:
                    var i := indices[index+corner]
                    var position: Vector3 = arrays[Mesh.ARRAY_VERTEX][i]
                    var normal: Vector3 = arrays[Mesh.ARRAY_NORMAL][i]
                    var color: Color = arrays[Mesh.ARRAY_COLOR][i]
                    # Includes original mesh-local shader sampling coordinates, not just model positions.
                    corners.append(vec(transform*position)+"/"+vec(transform.basis*normal)+"/"+vec(position)+"/"+var_to_bytes(color).hex_encode())
                var cycles := [corners[0]+corners[1]+corners[2],corners[1]+corners[2]+corners[0],corners[2]+corners[0]+corners[1]]
                cycles.sort()
                var key: String = (material+cycles[0]).sha256_text()
                result[key] = result.get(key,0)+1
                count += 1
    print("GEOMETRY triangles=",count," surfaces=",surfaces," materials=",materials.size()," meshes=",model.find_children("*","MeshInstance3D",true,false).size())
    return result
func run() -> void:
    root.size = Vector2i(1280,800)
    var original: Node3D = load("res://scenes/enemies/SkeletonWarriorV2.tscn").instantiate()
    var model: Node3D = load("res://scenes/enemies/FallenGuardianV3Presentation.tscn").instantiate()
    root.add_child(original)
    root.add_child(model)
    check(model.has_node("PoseRoot"),"V3 builds after V2 detail generator Ready")
    check(triangles(original) == triangles(model),"oriented triangle multiset matches V2: model positions/normals, local shader coordinates, colors, materials (1e-5 coordinate tolerance)")
    check(model.find_children("*","AnimationPlayer",true,false).is_empty(),"no animation clips")
    check(model.find_children("*","CollisionObject3D",true,false).is_empty() and model.find_children("*","NavigationAgent3D",true,false).is_empty(),"visual-only presentation")
    var rest := snapshot(model)
    for side in ["Left","Right"]:
        var s := -1.0 if side == "Left" else 1.0
        var right: bool = side == "Right"
        var positions := {
            "Shoulder":Vector3(s*0.279,1.46,0.012),
            "Elbow":Vector3(s*0.377,1.17 if right else 1.21,-0.034),
            "Wrist":Vector3(s*0.426,0.944 if right else 1.1,-0.216),
            "Hip":Vector3(s*0.11,0.878,0.015),
            "Knee":Vector3(s*0.14,0.492,-0.025 if right else 0.035),
            "Ankle":Vector3(s*0.16,0.095,-0.015 if right else 0.07)}
        for joint in positions:
            var pivot := find(model,side+joint+"Pivot")
            check(pivot.global_position.is_equal_approx(positions[joint]),side+joint+" exact asymmetric joint position")
            var transform := pivot.transform
            var attachments := {}
            for mesh: MeshInstance3D in pivot.find_children("*","MeshInstance3D",true,false):
                attachments[mesh] = pivot.global_transform.affine_inverse()*mesh.global_transform
            pivot.rotate_x(0.35)
            var rigid := true
            for mesh in attachments: rigid = rigid and attachments[mesh].is_equal_approx(pivot.global_transform.affine_inverse()*mesh.global_transform)
            check(rigid and pivot.scale.is_equal_approx(Vector3.ONE),side+joint+" reversible rigid attachments without scale")
            pivot.transform = transform
    check(find(model,"RightWristPivot").is_ancestor_of(find(model,"Sword")),"sword follows right hand/wrist")
    check(find(model,"LeftElbowPivot").is_ancestor_of(find(model,"Shield")) and not find(model,"LeftWristPivot").is_ancestor_of(find(model,"Shield")),"shield follows forearm independently of wrist")
    check(find(model,"HeadPivot").is_ancestor_of(find(model,"CursedEyes")),"eyes follow head")
    var reset := snapshot(model)
    var restored := true
    for k in rest: restored = restored and rest[k].transform.is_equal_approx(reset[k].transform)
    check(restored,"all joint tests restore rest")
    original.free(); model.free()
    var scene: Node3D = load("res://scenes/art/V3ArticulationComparison.tscn").instantiate()
    root.add_child(scene); current_scene = scene
    for i in 12: await process_frame
    for pose in ["Rest","RightElbowPivot","LeftKneePivot","RightWristPivot","RightAnklePivot","RightShoulderPivot","Back","Gameplay"]:
        scene.ResetPose()
        if pose.ends_with("Pivot"): scene.ShowJoint(pose)
        if pose == "Back": scene.Orbit(180.0)
        if pose == "Gameplay":
            scene.Orbit(-180.0)
            scene.SetGameplayView(true)
            check(scene.get_node("Camera3D").size == 22,"gameplay size 22")
        if "--capture" in OS.get_cmdline_user_args():
            for i in 8: await process_frame
            await RenderingServer.frame_post_draw
            check(root.get_texture().get_image().save_png("res://docs/milestone_05_v3_"+pose.to_lower()+".png") == OK,"capture "+pose)
    print("V3 articulation checks: ",failures," failures")
    quit(0 if failures == 0 else 1)
