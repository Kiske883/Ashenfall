extends "res://docs/validate_dwarf_animation.gd"

func run() -> void:
    root.size=Vector2i(1280,800)
    var reference: Node3D=load("res://scenes/characters/DwarfBerserkerGroundedExperiment.tscn").instantiate()
    root.add_child(reference); reference.set_process(false); reference.visible=false
    var preview: Node3D=load("res://scenes/art/DwarfBerserkerAttackDemo.tscn").instantiate()
    root.add_child(preview); current_scene=preview
    var actor: Node3D=preview.get_node("Dwarf"); actor.set_process(false)
    var player: AnimationPlayer=actor.get_node("AnimationPlayer")
    var model: Node3D=actor.get_node("Model")
    var axe:=find(model,"BattleAxe")
    preview.SetView("Front")
    var attack:=player.get_animation("Combat/AxeAttack")
    check(attack!=null and is_equal_approx(attack.length,1.7) and attack.loop_mode==Animation.LOOP_NONE,"Attack is a separate non-looping 1.7-second native resource")
    check(player.get_animation("Idle")==reference.get_node("AnimationPlayer").get_animation("Idle") and player.get_animation("Walk")==reference.get_node("AnimationPlayer").get_animation("Walk"),"Approved Idle/Walk resource references unchanged")
    check(geometry(model)==geometry(reference),"Approved geometry, normals, vertex colors, indices and materials reused unchanged")
    for track in attack.get_track_count(): check(attack.track_get_type(track)==Animation.TYPE_VALUE,"Attack track is visual property only")
    var root_transform:=actor.transform
    var pose_transform: Transform3D=find(model,"PoseRoot").transform
    var bone_lengths:={}
    for side in ["Left","Right"]:
        for chain in [["ShoulderPivot","ElbowPivot","WristPivot"],["HipPivot","KneePivot","AnklePivot"]]:
            for j in 2: bone_lengths[side+chain[j]]=find(model,side+chain[j]).global_position.distance_to(find(model,side+chain[j+1]).global_position)
    var max_grip:=0.0; var min_sole:=100.0; var max_sole:=-100.0; var peak:=-100.0; var strike_low:=100.0
    var hip_min:=100.0; var hip_max:=-100.0; var knee_min:=100.0; var knee_max:=-100.0
    var wrist_max:=0.0; var elbow_max:=0.0; var shoulder_max:=0.0; var body_min:=100.0; var body_max:=-100.0
    var rigid:=true; var fixed_roots:=true; var unit_scale:=true; var max_step:=0.0; var previous_tip:=Vector3.ZERO
    var butt:=Vector3(-0.46,0.45,-0.43); var end:=Vector3(0.46,1.5,-0.43)
    var axis: Vector3=(end-butt).normalized(); var cross:=Vector3(axis.y,-axis.x,0)
    var tip_local: Vector3=butt.lerp(end,0.87)+cross*0.46+axis*0.04-butt
    var probes: PackedVector3Array=[]
    var axe_mesh: MeshInstance3D=axe.get_node("Geometry")
    for surface in axe_mesh.mesh.get_surface_count(): probes.append_array(axe_mesh.mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX])
    var head:=find(model,"BaldHead"); var torso:=find(model,"LeatherCuirass")
    var penetration_frames:=0
    var worst_grip_time:=0.0; var worst_grip_side:=""
    var prior_rotations:={}; var max_joint_step:=0.0
    var max_palm_angle:=0.0
    for i in 401:
        var t:=i/400.0*1.7
        actor.SampleAttack(t)
        fixed_roots=fixed_roots and actor.transform.is_equal_approx(root_transform) and find(model,"PoseRoot").transform.is_equal_approx(pose_transform)
        for node: Node3D in model.find_children("*","Node3D",true,false): unit_scale=unit_scale and node.scale.is_equal_approx(Vector3.ONE)
        for side in ["Left","Right"]:
            for joint in ["ShoulderPivot","ElbowPivot","WristPivot","HipPivot","KneePivot","AnklePivot"]:
                var name: String=side+joint
                var q:=find(model,name).global_basis.get_rotation_quaternion()
                if prior_rotations.has(name): max_joint_step=maxf(max_joint_step,q.angle_to(prior_rotations[name]))
                prior_rotations[name]=q
            var grip_error: float=find(model,side+"Gauntlet").get_node("GripPoint").global_position.distance_to(axe.get_node(side+"Grip").global_position)
            if grip_error>max_grip: max_grip=grip_error; worst_grip_time=t; worst_grip_side=side
            var wrist:=find(model,side+"WristPivot")
            var forearm: Vector3=(wrist.global_position-find(model,side+"ElbowPivot").global_position).normalized()
            var palm: Vector3=(find(model,side+"Gauntlet").get_node("GripPoint").global_position-wrist.global_position).normalized()
            max_palm_angle=maxf(max_palm_angle,forearm.angle_to(palm))
            var sole:=bounds(model,find(model,side+"Boot")).position.y
            min_sole=minf(min_sole,sole); max_sole=maxf(max_sole,sole)
            hip_min=minf(hip_min,find(model,side+"HipPivot").rotation.x); hip_max=maxf(hip_max,find(model,side+"HipPivot").rotation.x)
            knee_min=minf(knee_min,find(model,side+"KneePivot").rotation.x); knee_max=maxf(knee_max,find(model,side+"KneePivot").rotation.x)
            wrist_max=maxf(wrist_max,find(model,side+"WristPivot").quaternion.get_angle())
            elbow_max=maxf(elbow_max,find(model,side+"ElbowPivot").quaternion.get_angle())
            shoulder_max=maxf(shoulder_max,find(model,side+"ShoulderPivot").quaternion.get_angle())
            for chain in [["ShoulderPivot","ElbowPivot","WristPivot"],["HipPivot","KneePivot","AnklePivot"]]:
                for j in 2:
                    var length:=find(model,side+chain[j]).global_position.distance_to(find(model,side+chain[j+1]).global_position)
                    rigid=rigid and absf(length-bone_lengths[side+chain[j]])<0.00005
        var tip: Vector3=axe.to_global(tip_local)
        if t>=0.3 and t<=0.75: peak=maxf(peak,tip.y)
        if t>=0.9 and t<=1.2: strike_low=minf(strike_low,tip.y)
        if i>0: max_step=maxf(max_step,tip.distance_to(previous_tip))
        previous_tip=tip
        body_min=minf(body_min,actor.BodyOffset.y); body_max=maxf(body_max,actor.BodyOffset.y)
        var penetrates:=false
        var to_head: Transform3D=head.global_transform.affine_inverse()*axe_mesh.global_transform
        var to_torso: Transform3D=torso.global_transform.affine_inverse()*axe_mesh.global_transform
        for vertex in probes:
            var h: Vector3=(to_head*vertex)/Vector3(0.145,0.155,0.125)
            var b: Vector3=(to_torso*vertex-Vector3(0,0,0.035))/Vector3(0.31,0.235,0.195)
            if h.length_squared()<0.8*0.8 or b.length_squared()<0.8*0.8: penetrates=true; break
        if penetrates: penetration_frames+=1
    print("GRIP_DIAGNOSTIC side=",worst_grip_side," time=",worst_grip_time)
    check(max_grip<0.0001,"Both axe hand anchors remain attached throughout full attack")
    check(min_sole>-0.0001 and max_sole<0.0001,"Both actual boot soles remain grounded throughout attack")
    check(rigid and fixed_roots and unit_scale,"Bone lengths, root transforms and unit proportions preserved")
    check(peak-strike_low>0.8,"Cutting edge has a clearly descending strike arc")
    check(rad_to_deg(hip_max-hip_min)>5 and rad_to_deg(knee_max-knee_min)>15,"Hip and knee articulation participate in weight transfer")
    check(penetration_frames==0,"No axe vertices deeply inside skull/torso ellipsoid proxies")
    check(geometry(model)==geometry(reference),"Attack never mutates approved mesh or material resources")
    check(rad_to_deg(max_joint_step)<15,"No joint discontinuity above 15 degrees per 4.25 ms sample")
    print("JOINT_CONTINUITY max_step_deg=",rad_to_deg(max_joint_step))
    print("WRIST_REVIEW forearm_to_grip_axis_max_deg=",rad_to_deg(max_palm_angle))
    print("AXE_METRICS grip_error=",max_grip," sole_min=",min_sole," sole_max=",max_sole," peak_edge=",peak," strike_edge=",strike_low," drop=",peak-strike_low," max_4_25ms_edge_step=",max_step," hip_span_deg=",rad_to_deg(hip_max-hip_min)," knee_span_deg=",rad_to_deg(knee_max-knee_min)," max_wrist_deg=",rad_to_deg(wrist_max)," max_elbow_deg=",rad_to_deg(elbow_max)," max_shoulder_deg=",rad_to_deg(shoulder_max)," body_y_min=",body_min," body_y_max=",body_max," deep_penetration_frames=",penetration_frames)
    actor.SampleAttack(0); var first:=transforms(model)
    actor.SampleAttack(1.7); var last:=transforms(model)
    var restored:=true
    for key in first: restored=restored and first[key].is_equal_approx(last[key])
    check(restored,"Recovery returns to original stable guard without a pose jump")
    if "--capture" in OS.get_cmdline_user_args():
        for view in ["Front","Side","Isometric"]:
            preview.SetView(view)
            var sheet:=Image.create(2400,1000,false,Image.FORMAT_RGBA8)
            var times:=[0.0,0.18,0.58,0.72,0.84,0.94,1.08,1.7]
            for frame in times.size():
                actor.SampleAttack(times[frame])
                for n in 2: await process_frame
                await RenderingServer.frame_post_draw
                var pixels:=root.get_texture().get_image(); pixels.convert(Image.FORMAT_RGBA8)
                sheet.blit_rect(pixels,Rect2i(340,180,600,500),Vector2i(frame%4*600,frame/4*500))
            check(sheet.save_png("res://docs/milestone_06d_axe_"+view.to_lower()+".png")==OK,view+" attack phases captured")
        preview.SetView("Isometric"); preview.get_node("Camera3D").size=22; actor.SampleAttack(0.72)
        for n in 3: await process_frame
        await RenderingServer.frame_post_draw
        check(root.get_texture().get_image().save_png("res://docs/milestone_06d_axe_gameplay.png")==OK,"Gameplay isometric size 22 captured")
    # Exercise real playback and automatic completion, without test sampling or gameplay events.
    player.seek(1.69,true); actor._Process(0.05)
    actor.PlayClip("Idle"); actor._Process(0.25)
    actor.PlayClip("Attack"); actor._Process(0.1)
    var clock:=player.current_animation_position
    actor.PlayClip("Attack")
    check(is_equal_approx(clock,player.current_animation_position),"Repeated attack button does not restart active clip")
    for n in 120: actor._Process(1.0/60)
    check(not actor.IsAttacking and str(actor.PreviewClip)=="Idle" and player.current_animation=="Idle", "Attack plays once and automatically returns to Idle")
    actor.PlayClip("Walk"); actor._Process(0.25)
    check(player.current_animation=="Walk","Approved Walk remains available")
    check(actor.find_children("*","CollisionObject3D",true,false).is_empty(),"Preview contains no gameplay collision or combat controller")
    print("Dwarf axe attack checks: ",failures," failures")
    quit(0 if failures==0 else 1)
