extends "res://docs/validate_dwarf_animation.gd"

const OPTIONS := ["GUARD", "WINDUP", "IMPACT A", "IMPACT B", "GUARD B"]

func run() -> void:
    root.size=Vector2i(1280,800)
    var preview: Node3D=load("res://scenes/art/DwarfAxeBladeOrientationPreview.tscn").instantiate()
    root.add_child(preview); current_scene=preview
    var actor: Node3D=preview.get_node("Dwarf")
    var model: Node3D=actor.get_node("Model")
    var axe: Node3D=find(model,"BattleAxe")
    var mesh: MeshInstance3D=axe.get_node("Geometry")
    var reference: Node3D=load("res://scenes/characters/DwarfBerserkerOverheadAttack.tscn").instantiate()
    root.add_child(reference); reference.set_process(false); reference.visible=false
    var reference_model: Node3D=reference.get_node("Model")
    check(not actor.is_processing(),"Five-option comparison samples held poses only")
    check(geometry(model).has("BattleAxe") and geometry(reference_model).has("BattleAxe"),"Original and candidate axe meshes resolve")
    var original_mesh: MeshInstance3D=find(reference_model,"BattleAxe").get_node("Geometry")
    check(mesh.mesh.get_surface_count()==original_mesh.mesh.get_surface_count(),"Original axe mesh surface count unchanged")
    for surface in mesh.mesh.get_surface_count():
        var a:=mesh.mesh.surface_get_arrays(surface); var b:=original_mesh.mesh.surface_get_arrays(surface)
        check(a[Mesh.ARRAY_VERTEX]==b[Mesh.ARRAY_VERTEX] and a[Mesh.ARRAY_NORMAL]==b[Mesh.ARRAY_NORMAL] and mesh.mesh.surface_get_material(surface)==original_mesh.mesh.surface_get_material(surface),"Axe geometry and material unchanged on surface "+str(surface))
    var shaft: Vector3=(Vector3(0.46,1.50,-0.43)-Vector3(-0.46,0.45,-0.43)).normalized()
    var edge:=Vector3(shaft.y,-shaft.x,0)
    # The forged broad faces occupy the shaft/edge plane; their normal is +/-Z.
    var face_normal:=Vector3.BACK
    check(absf(edge.dot(face_normal))<0.00001 and absf(shaft.dot(face_normal))<0.00001,"Measured blade plane and broad-face normal are orthogonal")
    var broad_face_vertices:=0
    for surface in original_mesh.mesh.get_surface_count():
        var arrays:=original_mesh.mesh.surface_get_arrays(surface)
        var vertices: PackedVector3Array=arrays[Mesh.ARRAY_VERTEX]
        var normals: PackedVector3Array=arrays[Mesh.ARRAY_NORMAL]
        for i in vertices.size():
            if vertices[i].dot(shaft)>0.70 and vertices[i].dot(edge)>0.10 and absf(normals[i].z)>0.95:
                broad_face_vertices+=1
    print("BLADE_PLANE broad_face_vertices=",broad_face_vertices)
    check(broad_face_vertices>12,"Actual blade mesh contains broad-face vertices with +/-Z normals")
    var butt:=Vector3(-0.46,0.45,-0.43)
    var end:=Vector3(0.46,1.50,-0.43)
    var tip: Vector3=butt.lerp(end,0.87)+edge*0.46+shaft*0.04-butt
    var guard_transforms:={}
    var guard_tip:=Vector3.ZERO
    var a_tip:=Vector3.ZERO
    var saved_b_guard:={}
    for option in OPTIONS:
        preview.ShowOption(option)
        var forward: Vector3=(model.global_basis*Vector3.FORWARD).normalized()
        var cutting: Vector3=(mesh.global_basis*edge).normalized()
        var broad: Vector3=(mesh.global_basis*face_normal).normalized()
        var edge_forward:=cutting.dot(forward)
        var flat_exposure:=absf(broad.dot(forward))
        var intended: Vector3=(model.global_basis*(Vector3.DOWN+Vector3.FORWARD).normalized()).normalized()
        print("IMPACT_OPTION ",option," edge_forward=",edge_forward," flat_exposure=",flat_exposure," edge_down_forward=",cutting.dot(intended))
        if option=="IMPACT A" or option=="IMPACT B":
            check(edge_forward>0.95 and flat_exposure<0.20,option+" contacts the forward target edge-first, with broad face edge-on")
            check(cutting.dot(intended)>0.55,option+" edge aligns with intended downward-forward contact")
        var sole_left:=bounds(model,find(model,"LeftBoot")).position.y
        var sole_right:=bounds(model,find(model,"RightBoot")).position.y
        check(absf(sole_left)<0.0001 and absf(sole_right)<0.0001,option+" keeps both soles grounded")
        for side in ["Left","Right"]:
            var anchor: Node3D=axe.get_node(side+"Grip")
            var hand: Node3D=find(model,side+"Gauntlet").get_node("GripPoint")
            check(hand.global_position.distance_to(anchor.global_position)<0.0001,option+" "+side+" grip remains on shaft")
            var shoulder: Vector3=find(model,side+"ShoulderPivot").global_position
            var elbow: Vector3=find(model,side+"ElbowPivot").global_position
            var wrist: Vector3=find(model,side+"WristPivot").global_position
            var bend:=rad_to_deg((shoulder-elbow).angle_to(wrist-elbow))
            var wrist_angle:=rad_to_deg(find(model,side+"WristPivot").quaternion.get_angle())
            print("ARM_ANGLES ",option," ",side," elbow=",bend," wrist=",wrist_angle)
            check(bend<170 and bend>35,option+" "+side+" elbow remains naturally flexed")
            check(wrist_angle<75,option+" "+side+" wrist avoids extreme local rotation")
        if option=="GUARD": guard_transforms=transforms(model); guard_tip=model.to_local(mesh.to_global(tip))
        if option=="IMPACT A": a_tip=model.to_local(mesh.to_global(tip))
        if option=="IMPACT B":
            var b_transforms:=transforms(model)
            var same:=true
            for key in guard_transforms: same=same and guard_transforms[key].is_equal_approx(b_transforms[key])
            check(same,"Impact B exactly reuses the original Guard posture")
        if option=="GUARD B": saved_b_guard=transforms(model)
    print("IMPACT_A_DELTA_MODEL ",a_tip-guard_tip)
    check(a_tip.y<guard_tip.y-0.05 and a_tip.z<guard_tip.z-0.02,"Impact A blade moves down and forward from Guard")
    reference.SampleAttack(0.96)
    var reference_impact:=transforms(reference_model)
    var b_body_same:=true
    for key in reference_impact:
        if key!="BattleAxe": b_body_same=b_body_same and saved_b_guard[key].is_equal_approx(reference_impact[key])
    check(b_body_same,"Guard B reuses the old Impact body posture without changing armor or limbs")

    if "--capture" in OS.get_cmdline_user_args():
        for view in ["Front","Side","Isometric"]:
            preview.SetView(view)
            for alternative in ["A","B"]:
                var sequence: Array=["GUARD","WINDUP","IMPACT A"] if alternative=="A" else ["GUARD B","WINDUP","IMPACT B"]
                var sheet:=Image.create(1800,500,false,Image.FORMAT_RGBA8)
                for i in sequence.size():
                    preview.ShowOption(sequence[i])
                    for n in 2: await process_frame
                    await RenderingServer.frame_post_draw
                    var image:=root.get_texture().get_image(); image.convert(Image.FORMAT_RGBA8)
                    sheet.blit_rect(image,Rect2i(340,180,600,500),Vector2i(i*600,0))
                check(sheet.save_png("res://docs/milestone_06d_impact_"+alternative.to_lower()+"_"+view.to_lower()+".png")==OK,alternative+" "+view+" static comparison captured")
    print("Dwarf impact alternative checks: ",failures," failures")
    quit(0 if failures==0 else 1)
