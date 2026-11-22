extends "res://docs/validate_dwarf_animation.gd"

func run() -> void:
    root.size=Vector2i(1280,800)
    var original: Node3D=load("res://scenes/characters/DwarfBerserkerOverheadAttack.tscn").instantiate()
    root.add_child(original); original.set_process(false); original.visible=false
    var corrected: Node3D=load("res://scenes/characters/DwarfBerserkerBladePose.tscn").instantiate()
    root.add_child(corrected); corrected.set_process(false); corrected.visible=false
    var original_model: Node3D=original.get_node("Model")
    var corrected_model: Node3D=corrected.get_node("Model")
    var original_axe: Node3D=find(original_model,"BattleAxe")
    var corrected_axe: Node3D=find(corrected_model,"BattleAxe")
    var original_mesh: MeshInstance3D=original_axe.get_node("Geometry")
    var corrected_mesh: MeshInstance3D=corrected_axe.get_node("Geometry")
    var shaft: Vector3=(Vector3(0.46,1.50,-0.43)-Vector3(-0.46,0.45,-0.43)).normalized()
    var edge:=Vector3(shaft.y,-shaft.x,0)
    var forward:=Vector3.FORWARD
    check(absf(edge.dot(forward))<0.00001,"Original blade-edge vector is perpendicular to enemy-facing -Z")
    check(geometry(original_model)==geometry(corrected_model),"Approved vertices, normals, materials and indices unchanged")
    var angle_results:=[]
    var static_poses:=[["GUARD",0.0],["WINDUP",0.70],["IMPACT",0.96]]
    for pose in static_poses:
        original.SampleAttack(pose[1]); corrected.SampleAttack(pose[1])
        var edge_world: Vector3=(corrected_mesh.global_basis*edge).normalized()
        var target_forward: Vector3=(corrected_model.global_basis*forward).normalized()
        var facing:=edge_world.dot(target_forward)
        print("POSE_AXIS ",pose[0]," unrolled_edge_forward_dot=",(original_mesh.global_basis*edge).normalized().dot(target_forward)," rolled_edge_forward_dot=",facing)
        angle_results.append([pose[0],rad_to_deg(acos(clampf(facing,-1,1)))])
        check(facing>(0.99 if pose[0]=="GUARD" else (0.5 if pose[0]=="WINDUP" else 0.8)),pose[0]+" cutting edge faces target")
        check(original_axe.global_transform.is_equal_approx(corrected_axe.global_transform),pose[0]+" weapon attachment transform unchanged")
        for side in ["Left","Right"]:
            var left_anchor: Node3D=original_axe.get_node(side+"Grip")
            var right_anchor: Node3D=corrected_axe.get_node(side+"Grip")
            var hand: Node3D=find(corrected_model,side+"Gauntlet").get_node("GripPoint")
            check(left_anchor.global_position.distance_to(right_anchor.global_position)<0.00001 and hand.global_position.distance_to(right_anchor.global_position)<0.0001,pose[0]+" "+side+" hand and grip unchanged")
        var before:=transforms(original_model); var after:=transforms(corrected_model)
        var body_same:=true
        for key in before:
            if key!="BattleAxe": body_same=body_same and before[key].is_equal_approx(after[key])
        check(body_same,pose[0]+" body, limbs, beard and boots preserve original pose")
        var actual_roll: Basis=original_mesh.global_basis.inverse()*corrected_mesh.global_basis
        check((actual_roll*shaft).distance_to(shaft)<0.00001,pose[0]+" correction rolls only around shaft axis")
    print("EDGE_ANGLES_TO_FORWARD_DEG ",angle_results)
    var min_strike_facing:=1.0
    var butt:=Vector3(-0.46,0.45,-0.43)
    var end:=Vector3(0.46,1.50,-0.43)
    var cutting_tip: Vector3=butt.lerp(end,0.87)+edge*0.46+shaft*0.04-butt
    var prior_tip:=Vector3.ZERO
    var min_leading:=1.0
    for i in 45:
        var t:=0.74+i*(0.22/44.0)
        corrected.SampleAttack(t)
        var direction: Vector3=(corrected_mesh.global_basis*edge).normalized()
        min_strike_facing=minf(min_strike_facing,direction.dot(corrected_model.global_basis*forward))
        var tip: Vector3=corrected_mesh.to_global(cutting_tip)
        if i>0 and tip.distance_to(prior_tip)>0.00001:
            min_leading=minf(min_leading,(tip-prior_tip).normalized().dot(direction))
        prior_tip=tip
    check(min_strike_facing>0.5,"Cutting edge stays toward target throughout descending strike interval")
    print("STRIKE_MIN_EDGE_FORWARD_DOT ",min_strike_facing," MIN_LEADING_DOT ",min_leading)
    check(min_leading>0,"Cutting edge leads blade travel during descending strike interval")
    corrected.SampleAttack(0.86)
    var tip_before: Vector3=corrected_mesh.to_global(cutting_tip)
    corrected.SampleAttack(0.96)
    var tip_at_impact: Vector3=corrected_mesh.to_global(cutting_tip)
    var travel: Vector3=(tip_at_impact-tip_before).normalized()
    var edge_at_impact: Vector3=(corrected_mesh.global_basis*edge).normalized()
    print("IMPACT_TRAVEL ",travel," EDGE ",edge_at_impact," DOT ",travel.dot(edge_at_impact))
    check(travel.dot(edge_at_impact)>0,"Blade edge leads the striking travel at impact")

    var preview: Node3D=load("res://scenes/art/DwarfAxeBladeOrientationPreview.tscn").instantiate()
    root.add_child(preview); current_scene=preview
    check(not preview.get_node("Dwarf").is_processing(),"Preview holds only static pose samples")
    if "--capture" in OS.get_cmdline_user_args():
        for view in ["Front","Side","Isometric"]:
            preview.SetView(view)
            var sheet:=Image.create(1800,500,false,Image.FORMAT_RGBA8)
            for i in static_poses.size():
                preview.ShowPose(static_poses[i][0],static_poses[i][1])
                for n in 2: await process_frame
                await RenderingServer.frame_post_draw
                var image:=root.get_texture().get_image(); image.convert(Image.FORMAT_RGBA8)
                sheet.blit_rect(image,Rect2i(340,180,600,500),Vector2i(i*600,0))
            check(sheet.save_png("res://docs/milestone_06d_blade_"+view.to_lower()+".png")==OK,view+" static cutting-edge poses captured")
    print("Dwarf blade orientation checks: ",failures," failures")
    quit(0 if failures==0 else 1)
