extends "res://docs/validate_dwarf_animation.gd"

func run() -> void:
    root.size=Vector2i(1280,800)
    var static_preview: Node3D=load("res://scenes/art/DwarfAxeBladeOrientationPreview.tscn").instantiate()
    root.add_child(static_preview); static_preview.visible=false
    for layer: CanvasLayer in static_preview.find_children("*","CanvasLayer",true,false): layer.visible=false
    var static_actor: Node3D=static_preview.get_node("Dwarf")
    var demo: Node3D=load("res://scenes/art/DwarfApprovedAxeAttackDemo.tscn").instantiate()
    root.add_child(demo); current_scene=demo
    var actor: Node3D=demo.get_node("Dwarf"); actor.set_process(false)
    var model: Node3D=actor.get_node("Model")
    var axe: Node3D=find(model,"BattleAxe")
    var blade: MeshInstance3D=axe.get_node("Geometry")
    var player: AnimationPlayer=actor.get_node("AnimationPlayer")
    var attack: Animation=player.get_animation("Combat/AxeAttack")
    check(attack!=null and is_equal_approx(attack.length,1.7) and attack.loop_mode==Animation.LOOP_NONE,"Separate 1.7 s one-shot approved-pose clip loads")
    check(player.has_animation("Combat/GuardB") and player.get_animation("Combat/GuardB").loop_mode==Animation.LOOP_LINEAR,"Dedicated Guard B resting clip loads")
    check(player.current_animation=="Combat/GuardB","Demo starts in approved Guard B")
    var grounded: Node3D=load("res://scenes/characters/DwarfBerserkerGroundedExperiment.tscn").instantiate()
    root.add_child(grounded); grounded.set_process(false); grounded.visible=false
    check(player.get_animation("Idle")==grounded.get_node("AnimationPlayer").get_animation("Idle") and player.get_animation("Walk")==grounded.get_node("AnimationPlayer").get_animation("Walk"),"Approved Idle/Walk resource references unchanged")
    var quaternion_tracks:=0
    for track in attack.get_track_count():
        check(attack.track_get_type(track)==Animation.TYPE_VALUE,"Attack track is visual property only")
        var path:=str(attack.track_get_path(track))
        if path.contains("Orientation"):
            quaternion_tracks+=1
            var normalized:=true
            for key in attack.track_get_key_count(track):
                var value=attack.track_get_key_value(track,key)
                normalized=normalized and value is Quaternion and absf(value.length()-1)<0.0001
            check(normalized,"Rotation track "+path+" uses normalized quaternions")
    check(quaternion_tracks==2,"Torso and weapon orientations use quaternion tracks")
    var source_model: Node3D=grounded.get_node("Model")
    var source_geometry:=geometry(source_model)
    var new_geometry:=geometry(model)
    check(source_geometry==new_geometry,"Approved mesh vertices, normals and material resources unchanged")
    var anchors:=[["GUARD B",0.0],["WINDUP",0.70],["IMPACT A",0.94],["GUARD B",1.70]]
    for anchor in anchors:
        static_preview.ShowOption(anchor[0]); actor.SampleApprovedAttack(anchor[1])
        var expected:=transforms(static_actor.get_node("Model")); var actual:=transforms(model)
        var same:=true; var worst:=0.0
        for part in actual:
            if expected.has(part):
                var a: Transform3D=actual[part]; var b: Transform3D=expected[part]
                same=same and a.is_equal_approx(b)
                worst=maxf(worst,a.origin.distance_to(b.origin))
        print("ANCHOR_MATCH ",anchor[0]," at=",anchor[1]," max_origin_gap=",worst," blade_roll=",actor.BladeRollDegrees)
        check(same,anchor[0]+" matches the exact approved static visual transforms")

    var shaft: Vector3=(Vector3(0.46,1.50,-0.43)-Vector3(-0.46,0.45,-0.43)).normalized()
    var edge:=Vector3(shaft.y,-shaft.x,0)
    var face:=Vector3.BACK
    var butt:=Vector3(-0.46,0.45,-0.43)
    var end:=Vector3(0.46,1.50,-0.43)
    var tip_local: Vector3=butt.lerp(end,0.87)+edge*0.46+shaft*0.04-butt
    actor.SampleApprovedAttack(0.0); var guard_tip: Vector3=blade.to_global(tip_local)
    actor.SampleApprovedAttack(0.68); var windup_tip: Vector3=blade.to_global(tip_local)
    actor.SampleApprovedAttack(0.94); var impact_tip: Vector3=blade.to_global(tip_local)
    var rise_speed: float=(windup_tip.y-guard_tip.y)/0.68
    var strike_speed: float=(windup_tip.y-impact_tip.y)/0.18
    print("PHASE_SPEED rise=",rise_speed," strike=",strike_speed," ratio=",strike_speed/rise_speed)
    check(strike_speed>rise_speed*2,"Downward strike is substantially faster than preparation")
    var probes: PackedVector3Array=[]
    for surface in blade.mesh.get_surface_count(): probes.append_array(blade.mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX])
    var head:=find(model,"BaldHead"); var torso:=find(model,"LeatherCuirass")
    var grip_max:=0.0; var min_sole:=100.0; var max_sole:=-100.0
    var max_wrist:=0.0; var max_elbow:=0.0; var min_edge_forward:=1.0; var max_face_exposure:=0.0
    var no_cross:=true; var no_root_motion:=true; var no_scale:=true; var no_deep_penetration:=true
    var previous_tip:=Vector3.ZERO; var max_tip_step:=0.0
    var min_strike_leading:=1.0; var strike_peak:=-100.0; var strike_low:=100.0
    var first_penetration:=100.0; var last_penetration:=0.0; var head_hits:=0; var torso_hits:=0
    var prior_rotations:={}; var max_joint_step:=0.0; var min_roll:=1000.0; var max_roll:=-1000.0
    var root_rest: Transform3D=actor.transform; var pose_rest: Transform3D=find(model,"PoseRoot").transform
    for i in 341:
        var t:=i*1.7/340.0
        actor.SampleApprovedAttack(t)
        no_root_motion=no_root_motion and actor.transform.is_equal_approx(root_rest) and find(model,"PoseRoot").transform.is_equal_approx(pose_rest)
        min_roll=minf(min_roll,actor.BladeRollDegrees); max_roll=maxf(max_roll,actor.BladeRollDegrees)
        for joint in ["TorsoPivot","LeftShoulderPivot","RightShoulderPivot","LeftElbowPivot","RightElbowPivot","LeftWristPivot","RightWristPivot","LeftKneePivot","RightKneePivot"]:
            var rotation: Quaternion=find(model,joint).global_basis.get_rotation_quaternion()
            if prior_rotations.has(joint): max_joint_step=maxf(max_joint_step,rad_to_deg(rotation.angle_to(prior_rotations[joint])))
            prior_rotations[joint]=rotation
        for node: Node3D in model.find_children("*","Node3D",true,false): no_scale=no_scale and node.scale.is_equal_approx(Vector3.ONE)
        var left_grip: Vector3=model.to_local(axe.get_node("LeftGrip").global_position)
        var right_grip: Vector3=model.to_local(axe.get_node("RightGrip").global_position)
        var left_elbow: Vector3=model.to_local(find(model,"LeftElbowPivot").global_position)
        var right_elbow: Vector3=model.to_local(find(model,"RightElbowPivot").global_position)
        no_cross=no_cross and left_grip.x<right_grip.x and left_elbow.x<right_elbow.x
        for side in ["Left","Right"]:
            var wrist: Node3D=find(model,side+"WristPivot")
            var elbow: Node3D=find(model,side+"ElbowPivot")
            var shoulder: Node3D=find(model,side+"ShoulderPivot")
            var grip: Node3D=find(model,side+"Gauntlet").get_node("GripPoint")
            var anchor: Node3D=axe.get_node(side+"Grip")
            grip_max=maxf(grip_max,grip.global_position.distance_to(anchor.global_position))
            max_wrist=maxf(max_wrist,rad_to_deg(wrist.quaternion.get_angle()))
            var bend:=rad_to_deg((shoulder.global_position-elbow.global_position).angle_to(wrist.global_position-elbow.global_position))
            max_elbow=maxf(max_elbow,bend)
            var sole:=bounds(model,find(model,side+"Boot")).position.y
            min_sole=minf(min_sole,sole); max_sole=maxf(max_sole,sole)
        var forward: Vector3=(model.global_basis*Vector3.FORWARD).normalized()
        var edge_world: Vector3=(blade.global_basis*edge).normalized()
        var face_world: Vector3=(blade.global_basis*face).normalized()
        if t>=0.76 and t<=0.94:
            min_edge_forward=minf(min_edge_forward,edge_world.dot(forward))
            max_face_exposure=maxf(max_face_exposure,absf(face_world.dot(forward)))
        var tip: Vector3=blade.to_global(tip_local)
        if i>0: max_tip_step=maxf(max_tip_step,tip.distance_to(previous_tip))
        if t>=0.65 and t<=0.76: strike_peak=maxf(strike_peak,tip.y)
        if t>=0.94 and t<=1.10: strike_low=minf(strike_low,tip.y)
        if t>=0.82 and t<=0.94 and i>0 and tip.distance_to(previous_tip)>0.00001:
            min_strike_leading=minf(min_strike_leading,(tip-previous_tip).normalized().dot(edge_world))
        previous_tip=tip
        var to_head: Transform3D=head.global_transform.affine_inverse()*blade.global_transform
        var to_torso: Transform3D=torso.global_transform.affine_inverse()*blade.global_transform
        for vertex in probes:
            var h: Vector3=(to_head*vertex)/Vector3(0.145,0.155,0.125)
            var b: Vector3=(to_torso*vertex-Vector3(0,0,0.035))/Vector3(0.31,0.235,0.195)
            if h.length_squared()<0.8*0.8:
                no_deep_penetration=false; head_hits+=1
                first_penetration=minf(first_penetration,t); last_penetration=maxf(last_penetration,t)
                break
            if b.length_squared()<0.8*0.8:
                no_deep_penetration=false; torso_hits+=1
                first_penetration=minf(first_penetration,t); last_penetration=maxf(last_penetration,t)
                break
    print("PENETRATION_SPAN first=",first_penetration," last=",last_penetration," head=",head_hits," torso=",torso_hits)
    print("APPROVED_ATTACK_METRICS grip_max=",grip_max," sole=[",min_sole,",",max_sole,"] wrist_max_deg=",max_wrist," elbow_max_deg=",max_elbow," min_edge_forward=",min_edge_forward," max_face_exposure=",max_face_exposure," min_strike_leading=",min_strike_leading," blade_drop=",strike_peak-strike_low," max_5ms_tip_step=",max_tip_step," max_5ms_joint_deg=",max_joint_step," roll=[",min_roll,",",max_roll,"] no_deep_penetration=",no_deep_penetration)
    check(grip_max<0.0001 and no_cross,"Both hands stay on handle, with no crossed arms throughout 341 frames")
    check(max_wrist<75 and max_elbow<170,"Wrists and elbows avoid extreme intermediate poses")
    check(min_sole>-0.0001 and max_sole<0.0001 and no_root_motion and no_scale,"Feet, root and proportions stay grounded and fixed")
    check(min_edge_forward>0.3 and min_strike_leading>0,"Cutting edge stays toward target and leads the strike")
    check(strike_peak-strike_low>0.5 and max_tip_step<0.08,"Fast descending strike without a position jump")
    check(max_joint_step<10 and min_roll>=89.99 and max_roll<=109.51,"Joint motion and blade-axis roll remain continuous without a flip")
    check(no_deep_penetration,"Axe avoids deep skull/torso penetration")

    if "--capture" in OS.get_cmdline_user_args():
        for view in ["Front","Side","Isometric"]:
            demo.SetView(view)
            var sheet:=Image.create(2400,1000,false,Image.FORMAT_RGBA8)
            var times:=[0.0,0.18,0.48,0.70,0.82,0.90,0.94,1.70]
            for frame in times.size():
                actor.SampleApprovedAttack(times[frame])
                for n in 2: await process_frame
                await RenderingServer.frame_post_draw
                var image:=root.get_texture().get_image(); image.convert(Image.FORMAT_RGBA8)
                sheet.blit_rect(image,Rect2i(340,180,600,500),Vector2i(frame%4*600,frame/4*500))
            check(sheet.save_png("res://docs/milestone_06d_approved_attack_"+view.to_lower()+".png")==OK,view+" approved attack phases captured")
        demo.SetView("Isometric")
        var motion:=Image.create(2400,2000,false,Image.FORMAT_RGBA8)
        for frame in 16:
            actor.SampleApprovedAttack(frame*1.7/15.0)
            for n in 2: await process_frame
            await RenderingServer.frame_post_draw
            var image:=root.get_texture().get_image(); image.convert(Image.FORMAT_RGBA8)
            motion.blit_rect(image,Rect2i(340,180,600,500),Vector2i(frame%4*600,frame/4*500))
        check(motion.save_png("res://docs/milestone_06d_approved_attack_timeline.png")==OK,"16-frame approved attack timeline captured")
        demo.get_node("Camera3D").size=22
        var gameplay:=Image.create(2560,800,false,Image.FORMAT_RGBA8)
        for frame in 2:
            actor.SampleApprovedAttack(0.70 if frame==0 else 0.94)
            for n in 3: await process_frame
            await RenderingServer.frame_post_draw
            var image:=root.get_texture().get_image(); image.convert(Image.FORMAT_RGBA8)
            gameplay.blit_rect(image,Rect2i(0,0,1280,800),Vector2i(frame*1280,0))
        check(gameplay.save_png("res://docs/milestone_06d_approved_attack_gameplay.png")==OK,"Gameplay-size 22 Windup and Impact A captured")
    # Playback test uses a fresh actor because pose sampling intentionally holds the attack state.
    var playback: Node3D=load("res://scenes/characters/DwarfBerserkerApprovedAxeAttack.tscn").instantiate()
    root.add_child(playback); playback.set_process(false); playback.visible=false
    var playback_player: AnimationPlayer=playback.get_node("AnimationPlayer")
    playback.PlayApprovedAttack()
    var first_clock:=playback_player.current_animation_position
    playback.PlayApprovedAttack()
    check(is_equal_approx(first_clock,playback_player.current_animation_position),"Repeated attack request does not restart the active clip")
    for n in 110: playback._Process(1.0/60)
    check(not playback.IsAttacking and playback_player.current_animation=="Combat/GuardB","One-shot attack returns automatically to Guard B")
    static_preview.ShowOption("GUARD B")
    var resting:=transforms(static_actor.get_node("Model")); var played:=transforms(playback.get_node("Model"))
    var matching_guard:=true
    for part in played:
        if resting.has(part): matching_guard=matching_guard and resting[part].is_equal_approx(played[part])
    check(matching_guard,"Playback recovery restores the exact Guard B visual pose")
    playback.PlayClip("Walk")
    for n in 15: playback._Process(1.0/60)
    playback.PlayApprovedAttack()
    for n in 110: playback._Process(1.0/60)
    check(not playback.IsAttacking and playback_player.current_animation=="Combat/GuardB","Walk-to-attack and repeated attack return to Guard B")
    check(playback.find_children("*","CollisionObject3D",true,false).is_empty(),"Isolated preview has no gameplay collisions or hitboxes")
    print("Dwarf approved axe attack checks: ",failures," failures")
    quit(0 if failures==0 else 1)
