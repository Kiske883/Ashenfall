extends "res://docs/validate_dwarf_animation.gd"

func run() -> void:
    root.size=Vector2i(1280,800)
    var demo: Node3D=load("res://scenes/art/DwarfCombatGuardComparison.tscn").instantiate()
    root.add_child(demo); current_scene=demo
    var actor: Node3D=demo.get_node("DwarfCandidate")
    actor.set_process(false)
    var player: AnimationPlayer=actor.get_node("AnimationPlayer")
    var model: Node3D=actor.get_node("Model")
    var axe: Node3D=find(model,"BattleAxe")
    var blade: MeshInstance3D=axe.get_node("Geometry")
    var walk: Animation=player.get_animation("Combat/CombatWalk")
    var source: Node3D=load("res://scenes/characters/DwarfBerserkerGroundedExperiment.tscn").instantiate()
    root.add_child(source); source.set_process(false); source.visible=false
    var source_walk: Animation=source.get_node("AnimationPlayer").get_animation("Walk")
    check(walk!=null and is_equal_approx(walk.length,source_walk.length) and walk.loop_mode==Animation.LOOP_LINEAR,"Combat Walk uses the approved 0.6 s grounded cycle")
    var lower_paths=["LeftFootOffset","RightFootOffset","LeftFootPitch","RightFootPitch"]
    var lower_same:=true
    for name in lower_paths:
        var ci:=walk.find_track(NodePath(".:"+name),Animation.TYPE_VALUE)
        var si:=source_walk.find_track(NodePath(".:"+name),Animation.TYPE_VALUE)
        lower_same=lower_same and ci>=0 and si>=0 and walk.track_get_key_count(ci)==source_walk.track_get_key_count(si)
        for i in walk.track_get_key_count(ci):
            lower_same=lower_same and walk.track_get_key_time(ci,i)==source_walk.track_get_key_time(si,i) and walk.track_get_key_value(ci,i)==source_walk.track_get_key_value(si,i)
    check(lower_same,"Foot targets and ankle curves exactly copy approved grounded Walk")
    check(player.get_animation("Idle")==source.get_node("AnimationPlayer").get_animation("Idle") and player.get_animation("Walk")==source_walk,"Original Idle and Walk resources remain shared and unchanged")
    var max_grip:=0.0; var min_sole:=100.0; var max_sole:=-100.0
    var max_blade_wobble:=0.0; var guard_blade_basis: Basis
    actor.ShowGuardB(); guard_blade_basis=model.global_basis.inverse()*blade.global_basis
    var guard_foot=[]
    for side in ["Left","Right"]: guard_foot.append(find(model,side+"Boot").global_position)
    var hand_cross:=false
    for i in 121:
        var t:=i*.6/120.0
        player.play("Combat/CombatWalk",0); player.seek(t,true); actor.ApplyPose(); actor.SetBladeRollDegrees(actor.BladeRollDegrees)
        for side in ["Left","Right"]:
            var hand: Node3D=find(model,side+"Gauntlet").get_node("GripPoint")
            var grip: Node3D=axe.get_node(side+"Grip")
            max_grip=maxf(max_grip,hand.global_position.distance_to(grip.global_position))
            var sole:=bounds(model,find(model,side+"Boot")).position.y
            min_sole=minf(min_sole,sole); max_sole=maxf(max_sole,sole)
        var left: Node3D=axe.get_node("LeftGrip"); var right: Node3D=axe.get_node("RightGrip")
        hand_cross=hand_cross or model.to_local(left.global_position).x>=model.to_local(right.global_position).x
        max_blade_wobble=maxf(max_blade_wobble,rad_to_deg((model.global_basis.inverse()*blade.global_basis).get_rotation_quaternion().angle_to(guard_blade_basis.get_rotation_quaternion())))
    print("COMBAT_WALK grip=",max_grip," sole=[",min_sole,",",max_sole,"] blade_sway_deg=",max_blade_wobble)
    check(max_grip<0.001 and not hand_cross,"Both hands retain separate grips throughout combat Walk")
    check(min_sole>-.001 and max_sole<.13,"Combat Walk soles remain grounded or lift within approved range")
    check(max_blade_wobble<8,"Combat guard has only subtle upper-body sway")
    actor.ShowGuardB()
    player.play("Combat/CombatWalk",.18); player.seek(.165,false); player.speed_scale=2.5/1.51515
    var previous_axe:=axe.global_position; var transition_step:=0.0
    for i in 30:
        actor._Process(1.0/60)
        transition_step=maxf(transition_step,axe.global_position.distance_to(previous_axe))
        previous_axe=axe.global_position
    player.play("Combat/GuardB",.18); player.speed_scale=1
    for i in 18:
        actor._Process(1.0/60)
        transition_step=maxf(transition_step,axe.global_position.distance_to(previous_axe))
        previous_axe=axe.global_position
    print("IDLE_WALK_IDLE max_axe_step_per_frame=",transition_step)
    check(transition_step<.08,"Idle-Walk-Idle has no abrupt axe jump at 60 fps")
    var attack: Animation=player.get_animation("Combat/AxeAttack")
    check(is_equal_approx(attack.length,1.7) and attack.loop_mode==Animation.LOOP_NONE,"Impact C retains 1.70 s one-shot attack")
    var previous: Node3D=demo.get_node("Dwarf"); previous.set_process(false)
    var previous_player: AnimationPlayer=previous.get_node("AnimationPlayer")
    var original_blade: MeshInstance3D=find(previous.get_node("Model"),"BattleAxe").get_node("Geometry")
    var tip_local:=Vector3(.4,1.4,-.43)
    actor.SampleApprovedAttack(.94); previous.SampleApprovedAttack(.94)
    var c_tip:=blade.to_global(tip_local)
    var a_tip:=original_blade.to_global(tip_local)
    print("IMPACT_TIP A=",a_tip," C=",c_tip," vertical_drop=",a_tip.y-c_tip.y)
    check(c_tip.y<a_tip.y-.01,"Impact C cuts lower than approved Impact A")
    check(actor.TorsoRotation.x<previous.TorsoRotation.x-.01,"Impact C torso leans farther forward")
    var attack_grip:=0.0; var no_cross:=true; var min_sole_attack:=100.0
    var max_frame_step:=0.0; var old_tip:=Vector3.ZERO
    var edge_axis: Vector3=(Vector3(.46,1.5,-.43)-Vector3(-.46,.45,-.43)).normalized()
    var edge_local:=Vector3(edge_axis.y,-edge_axis.x,0)
    var minimum_edge_forward:=1.0; var deep_penetration:=false
    var head: Node3D=find(model,"BaldHead")
    var torso: Node3D=find(model,"LeatherCuirass")
    var blade_vertices: PackedVector3Array=[]
    for surface in blade.mesh.get_surface_count(): blade_vertices.append_array(blade.mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX])
    for i in 341:
        var t:=i*1.7/340.0
        actor.SampleApprovedAttack(t)
        var tip:=blade.to_global(tip_local)
        if i>0: max_frame_step=maxf(max_frame_step,tip.distance_to(old_tip))
        old_tip=tip
        for side in ["Left","Right"]:
            var hand: Node3D=find(model,side+"Gauntlet").get_node("GripPoint")
            attack_grip=maxf(attack_grip,hand.global_position.distance_to(axe.get_node(side+"Grip").global_position))
            min_sole_attack=minf(min_sole_attack,bounds(model,find(model,side+"Boot")).position.y)
        no_cross=no_cross and model.to_local(axe.get_node("LeftGrip").global_position).x<model.to_local(axe.get_node("RightGrip").global_position).x
        if t>=.76 and t<=.94:
            minimum_edge_forward=minf(minimum_edge_forward,(blade.global_basis*edge_local).normalized().dot((model.global_basis*Vector3.FORWARD).normalized()))
        var blade_to_head: Transform3D=head.global_transform.affine_inverse()*blade.global_transform
        var blade_to_torso: Transform3D=torso.global_transform.affine_inverse()*blade.global_transform
        for vertex in blade_vertices:
            var h: Vector3=(blade_to_head*vertex)/Vector3(.145,.155,.125)
            var b: Vector3=(blade_to_torso*vertex-Vector3(0,0,.035))/Vector3(.31,.235,.195)
            if h.length_squared()<.64 or b.length_squared()<.64:
                deep_penetration=true; break
    print("ATTACK_C grip=",attack_grip," min_sole=",min_sole_attack," max_5ms_tip_step=",max_frame_step," edge_forward=",minimum_edge_forward," deep_penetration=",deep_penetration)
    check(attack_grip<.001 and no_cross,"Both hands hold the rigid axe throughout Attack C")
    check(min_sole_attack>-.001,"Attack C feet remain grounded")
    check(max_frame_step<.09,"Attack C has no large intermediate weapon flip")
    check(minimum_edge_forward>.35,"Cutting edge stays oriented toward the enemy during the strike")
    check(not deep_penetration,"Axe avoids deep head and torso penetration in sampled frames")
    var playback: Node3D=load("res://scenes/characters/DwarfCombatGuardImpactC.tscn").instantiate()
    root.add_child(playback); playback.set_process(false); playback.visible=false
    playback.PlayApprovedAttack()
    for i in 110: playback._Process(1.0/60)
    check(not playback.IsAttacking and playback.get_node("AnimationPlayer").current_animation=="Combat/GuardB","Attack C returns to Guard B")
    if DisplayServer.get_name()!="headless":
        demo.SetView("Isometric")
        var walk_sheet:=Image.create(2400,1000,false,Image.FORMAT_RGBA8)
        for frame in 8:
            player.play("Combat/CombatWalk",0); player.seek(frame*.6/8.0,true); actor.ApplyPose(); actor.SetBladeRollDegrees(actor.BladeRollDegrees)
            for n in 2: await process_frame
            await RenderingServer.frame_post_draw
            var image:=root.get_texture().get_image(); image.convert(Image.FORMAT_RGBA8)
            walk_sheet.blit_rect(image,Rect2i(340,180,600,500),Vector2i(frame%4*600,frame/4*500))
        check(walk_sheet.save_png("res://docs/milestone_06d_combat_guard_walk.png")==OK,"Combat Guard Walk timeline captured")
        for view in ["Front","Side","Isometric"]:
            demo.SetView(view)
            var sheet:=Image.create(2400,1500,false,Image.FORMAT_RGBA8)
            for frame in 12:
                actor.SampleApprovedAttack(frame*1.7/11.0)
                for n in 2: await process_frame
                await RenderingServer.frame_post_draw
                var image:=root.get_texture().get_image(); image.convert(Image.FORMAT_RGBA8)
                sheet.blit_rect(image,Rect2i(340,180,600,500),Vector2i(frame%4*600,frame/4*500))
            check(sheet.save_png("res://docs/milestone_06d_impact_c_"+view.to_lower()+".png")==OK,view+" Impact C timeline captured")
    print("Combat guard variant checks: ",failures," failures")
    quit(0 if failures==0 else 1)
