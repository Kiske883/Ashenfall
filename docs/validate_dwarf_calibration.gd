extends "res://docs/validate_dwarf_playable.gd"

var cached_vertices := {}
var trace_contacts := false
var contact_anchors := {}
var contact_phases := {}
var transient_contact_drift := 0.0

func steps(count: int) -> void:
    for n in count:
        await super.steps(1)
        if not trace_contacts or not is_instance_valid(dwarf): continue
        var phase: float=fposmod(dwarf.get_node("AnimationPlayer").current_animation_position/0.6,1)
        for side in ["Left","Right"]:
            var p:=fposmod(phase+(0.5 if side=="Right" else 0),1)
            var offset: Vector3=dwarf.LeftFootOffset if side=="Left" else dwarf.RightFootOffset
            if str(dwarf.ActiveClip)=="Walk" and p<=0.55 and offset.y<0.001:
                var point:=part(dwarf,side+"AnklePivot").global_position
                if not contact_anchors.has(side) or p<contact_phases.get(side,0.0): contact_anchors[side]=point
                transient_contact_drift=maxf(transient_contact_drift,point.distance_to(contact_anchors[side]))
                contact_phases[side]=p
            else:
                contact_anchors.erase(side); contact_phases.erase(side)

func floor_bounds(model: Node3D, part: Node3D) -> AABB:
    var id := part.get_instance_id()
    if not cached_vertices.has(id):
        var entries := []
        for mesh: MeshInstance3D in part.find_children("*", "MeshInstance3D", true, false):
            for surface in mesh.mesh.get_surface_count():
                entries.append([mesh, mesh.mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]])
        cached_vertices[id] = entries
    var result := AABB(); var first := true
    for entry in cached_vertices[id]:
        var transform: Transform3D = model.global_transform.affine_inverse()*entry[0].global_transform
        for vertex: Vector3 in entry[1]:
            var p := transform*vertex
            if first: result=AABB(p,Vector3.ZERO); first=false
            else: result=result.expand(p)
    return result

func part(model: Node, name: String) -> Node3D:
    return model.find_child(name,true,false)

func geometry(model: Node3D) -> Dictionary:
    var result := {}
    for mesh: MeshInstance3D in model.find_children("*","MeshInstance3D",true,false):
        var signature := []
        for surface in mesh.mesh.get_surface_count():
            var arrays := mesh.mesh.surface_get_arrays(surface)
            signature.append([hash(arrays[Mesh.ARRAY_VERTEX]),hash(arrays[Mesh.ARRAY_NORMAL]),hash(arrays[Mesh.ARRAY_COLOR]),hash(arrays[Mesh.ARRAY_INDEX]),mesh.mesh.surface_get_material(surface)])
        result[str(mesh.get_parent().name)] = signature
    return result

func capture(label: String) -> void:
    if "--capture" not in OS.get_cmdline_user_args(): return
    await RenderingServer.frame_post_draw
    check(root.get_texture().get_image().save_png("res://docs/milestone_06c1_"+label+".png")==OK,"experimental gameplay camera capture "+label)

func run_checks() -> void:
    root.size=Vector2i(1280,800)
    var original: Node3D=load("res://scenes/characters/DwarfBerserkerAnimated.tscn").instantiate()
    var experiment: Node3D=load("res://scenes/characters/DwarfBerserkerGroundedExperiment.tscn").instantiate()
    root.add_child(original); root.add_child(experiment)
    original.set_process(false); experiment.set_process(false)
    var thigh:=part(original,"LeftHipPivot").global_position.distance_to(part(original,"LeftKneePivot").global_position)
    var shin:=part(original,"LeftKneePivot").global_position.distance_to(part(original,"LeftAnklePivot").global_position)
    print("LEG_REACH thigh=",thigh," shin=",shin," combined=",thigh+shin)
    check(geometry(original)==geometry(experiment),"Experimental presentation reuses every approved mesh attribute and material")
    var original_library: AnimationPlayer=original.get_node("AnimationPlayer")
    var experimental_library: AnimationPlayer=experiment.get_node("AnimationPlayer")
    var idle_original:=original_library.get_animation("Idle")
    var idle_experimental:=experimental_library.get_animation("Idle")
    var idle_same:=is_equal_approx(idle_original.length,idle_experimental.length)
    for track in idle_original.get_track_count():
        for key in idle_original.track_get_key_count(track):
            idle_same=idle_same and idle_original.track_get_key_value(track,key)==idle_experimental.track_get_key_value(track,key)
    check(idle_same,"Experimental Idle duplicates approved Idle keys unchanged")
    original.SamplePose("Walk",0)
    var old_start:=part(original,"LeftAnklePivot").global_position
    original.SamplePose("Walk",1.4*0.6)
    var old_support:=part(original,"LeftAnklePivot").global_position.z-old_start.z
    original.SamplePose("Walk",1.4)
    check(part(original,"LeftAnklePivot").global_position.distance_to(old_start)<0.00001,"Original in-place clip has zero net foot displacement; equivalent travel is inferred from support")
    experiment.SamplePose("Walk",0)
    var new_start:=part(experiment,"LeftAnklePivot").global_position
    experiment.SamplePose("Walk",0.6*0.55)
    var new_support:=part(experiment,"LeftAnklePivot").global_position.z-new_start.z
    experiment.SamplePose("Walk",0.6)
    check(part(experiment,"LeftAnklePivot").global_position.distance_to(new_start)<0.00001,"Experimental in-place clip also closes without root motion")
    var old_reference:=old_support/(1.4*0.6)
    var new_reference:=new_support/(0.6*0.55)
    var required_rate:=2.5/new_reference
    check(absf(old_support-0.14)<0.00001 and absf(new_support-0.5)<0.00001,"Measured support travel is 0.14 m original / 0.50 m experimental")
    check(absf(required_rate-1.65)<0.0001,"2.5 m/s requires moderate 1.65x for new stride, versus 15x original")
    var original_drift:=5.0*(1.4*0.6/2)-old_support
    var reduced_only_drift:=2.5*(1.4*0.6/2)-old_support
    var new_drift:=2.5*(0.6*0.55/required_rate)-new_support
    print("CALIBRATION original_support=",old_support," original_equivalent_cycle=",old_support/0.6," original_ref=",old_reference," old5_rate=",5/old_reference," old2_5_rate=",2.5/old_reference," experiment_support=",new_support," experiment_equivalent_cycle=",new_support/0.55," experiment_ref=",new_reference," experiment_rate=",required_rate," effective_cycle=",0.6/required_rate," footfalls_per_s=",2*required_rate/0.6)
    print("CONTACT_DRIFT original5_capped=",original_drift," original2_5_capped=",reduced_only_drift," calibrated2_5=",new_drift)
    var min_sole:=100.0; var max_lift:=0.0; var min_crown:=100.0; var max_contact_drift:=0.0; var grips:=0.0
    var anchors:={}; var was_stance:={"Left":false,"Right":false}; var max_knee:=-100.0; var min_knee:=100.0
    var unscaled:=true; var root_fixed:=true
    for i in 401:
        var phase:=i/400.0
        experiment.position=Vector3(0,0,-2.5*(phase*0.6/required_rate))
        experiment.SamplePose("Walk",phase*0.6)
        root_fixed=root_fixed and part(experiment,"PoseRoot").position.is_zero_approx()
        for node: Node3D in experiment.find_children("*","Node3D",true,false): unscaled=unscaled and node.scale.is_equal_approx(Vector3.ONE)
        min_crown=minf(min_crown,floor_bounds(experiment,part(experiment,"BaldHead")).end.y)
        for side in ["Left","Right"]:
            var foot_phase:=fposmod(phase+(0.5 if side=="Right" else 0),1)
            var stance:=foot_phase<=0.55
            var foot:=part(experiment,side+"AnklePivot")
            if stance:
                if not was_stance[side] or foot_phase<0.003: anchors[side]=foot.global_position
                max_contact_drift=maxf(max_contact_drift,foot.global_position.distance_to(anchors[side]))
            was_stance[side]=stance
            var sole:=floor_bounds(experiment,part(experiment,side+"Boot")).position.y
            min_sole=minf(min_sole,sole); max_lift=maxf(max_lift,sole)
            grips=maxf(grips,part(experiment,side+"Gauntlet").get_node("GripPoint").global_position.distance_to(part(experiment,"BattleAxe").get_node(side+"Grip").global_position))
            min_knee=minf(min_knee,part(experiment,side+"KneePivot").rotation.x)
            max_knee=maxf(max_knee,part(experiment,side+"KneePivot").rotation.x)
    check(min_sole>-0.0001 and max_lift>0.065,"Real boot geometry plants on floor and lifts in swing without penetration")
    check(max_contact_drift<0.0001,"Full-cycle moving-body support foot stays planted, not merely accelerated")
    check(grips<0.00005,"Both axe grips remain attached across complete experimental cycle")
    check(geometry(original)==geometry(experiment),"Experimental motion changes no mesh, material or proportions")
    check(unscaled and root_fixed,"No scaling or animation-induced root offset")
    print("EXPERIMENT_POSE sole_min=",min_sole," sole_lift=",max_lift," crouched_crown_min=",min_crown," planted_drift=",max_contact_drift," grip_error=",grips," knee_span_deg=",rad_to_deg(max_knee-min_knee))
    if "--capture" in OS.get_cmdline_user_args():
        original.visible=false
        var preview: Node3D=load("res://scenes/art/DwarfBerserkerPreview.tscn").instantiate()
        root.add_child(preview)
        var reference_preview: Node3D=preview.get_node("Dwarf")
        reference_preview.visible=false
        experiment.reparent(preview)
        experiment.transform=reference_preview.transform
        for view in ["Side","Isometric"]:
            preview.SetView(view); experiment.transform=reference_preview.transform
            var sheet:=Image.create(1600,860,false,Image.FORMAT_RGBA8)
            for frame in 8:
                experiment.SamplePose("Walk",frame/8.0*0.6)
                for n in 2: await process_frame
                await RenderingServer.frame_post_draw
                var pixels:=root.get_texture().get_image(); pixels.convert(Image.FORMAT_RGBA8)
                sheet.blit_rect(pixels,Rect2i(230,210,400,430),Vector2i(frame%4*400,frame/4*430))
            check(sheet.save_png("res://docs/milestone_06c1_cycle_"+view.to_lower()+".png")==OK,"experimental "+view+" cycle rendered")
        experiment.reparent(root); preview.free()
    original.free(); experiment.free()
    if "--capture" in OS.get_cmdline_user_args():
        var baseline_demo: Node3D=load("res://scenes/world/AshenfallDwarfFiveMeterComparison.tscn").instantiate()
        root.add_child(baseline_demo)
        for n in 60: await steps(1)
        Input.action_press("move_down")
        for n in 30: await steps(1)
        await RenderingServer.frame_post_draw
        check(root.get_texture().get_image().save_png("res://docs/milestone_06c1_baseline_5.png")==OK,"Preserved 5 m/s baseline captured without overwriting 06C evidence")
        Input.action_release("move_down"); baseline_demo.free()
        await process_frame
    var demo_path := "res://scenes/world/AshenfallDwarfPlayableDemo.tscn" if "--definitive" in OS.get_cmdline_user_args() else "res://scenes/world/AshenfallDwarfLocomotionExperiment.tscn"
    demo=load(demo_path).instantiate()
    root.add_child(demo); current_scene=demo
    world=demo.get_node("World"); player=world.get_node("FutureActors/Player"); enemy=demo.get_node("FallenGuardian")
    driver=demo.get_node("DwarfPresentation"); dwarf=player.get_node("Visual/Dwarf"); axe=part(dwarf,"BattleAxe")
    rig=world.get_node("CameraRig"); camera=rig.get_node("Camera3D")
    await steps(60)
    var map:=world.get_world_3d().navigation_map
    for i in 300:
        if NavigationServer3D.map_get_iteration_id(map)>0: break
        OS.delay_msec(1); await steps(1)
    check(is_equal_approx(player.MovementSpeed,2.5) and is_equal_approx(camera.size,22),"Only experimental actor uses 2.5 m/s; camera unchanged")
    check(str(dwarf.ActiveClip)=="Idle" and player.is_on_floor(),"Experimental spawn is grounded in approved Idle")
    await capture("spawn")
    var camera_basis:=camera.global_basis
    var actual_contact_drift:=0.0
    for action in ["move_up","move_right","move_down","move_left"]:
        await reset_actor(Vector3(-0.8,0.12,5))
        Input.action_press(action); await steps(45)
        check(absf(driver.ActualHorizontalSpeed-2.5)<0.02 and absf(driver.WalkPlaybackRate-1.65)<0.001 and not driver.WalkRateLimited,action+" actual speed and stride-matched clock")
        check((-player.get_node("Visual").global_basis.z).dot(driver.ActualHorizontalVelocity.normalized())>0.995,action+" orientation follows movement")
        var planted:=false; var anchor:=Vector3.ZERO; var previous_phase:=0.0
        for i in 45:
            await steps(1)
            var phase:=fposmod(dwarf.get_node("AnimationPlayer").current_animation_position/0.6,1)
            var foot:=part(dwarf,"LeftAnklePivot")
            if phase<=0.55:
                if not planted or phase<previous_phase: anchor=foot.global_position
                actual_contact_drift=maxf(actual_contact_drift,foot.global_position.distance_to(anchor))
                planted=true
            else: planted=false
            previous_phase=phase
        if action=="move_down": await capture("walk")
        Input.action_release(action); await steps(25)
        check(str(dwarf.ActiveClip)=="Idle","Stop blends back to approved Idle")
    check(actual_contact_drift<0.01,"Actual world motion has less than 1 cm steady support drift")
    check(camera.global_basis.is_equal_approx(camera_basis),"Original camera orientation preserved")
    await reset_actor(Vector3(-0.8,0.12,5))
    trace_contacts=true
    Input.action_press("move_down"); await steps(20); Input.action_release("move_down")
    Input.action_press("move_left"); await steps(30); Input.action_release("move_left"); await steps(20)
    trace_contacts=false
    print("TURN_AND_STARTUP support_drift_max=",transient_contact_drift)
    check(str(dwarf.ActiveClip)=="Idle" and max_grip_error<0.0001,"Direction changes and transitions retain axe grips")
    await reset_actor(Vector3(-0.8,0.12,5))
    enemy.connect("AttackExecuted",func(target:Node3D,sequence:int): events.append([target,sequence,Engine.get_physics_frames()]))
    await click_ground(Vector3(0,0,-1.5))
    check(await settled(Vector3(0,0,-1.5)),"Click-to-move reaches encounter at experimental speed")
    check(await attack_cycles(2),"Guardian detects, pursues and attacks experiment")
    await capture("encounter")
    await click_ground(Vector3(0,0,8))
    check(await settled(Vector3(0,0,8)) and await wait_state(0,900),"Retreat and Guardian return remain functional")
    var count:=events.size()
    await click_ground(Vector3(0,0,-1.5))
    check(await settled(Vector3(0,0,-1.5)) and await attack_cycles(count+1),"Repeated encounter works without restarting")
    check(sync_ok and observed_chase_speed and observed_return_speed,"Guardian timing and 1.0/0.7 movement retained")
    check(max_grip_error<0.0001 and scale_preserved,"Integrated axe contacts and unit scale preserved")
    print("WORLD_CALIBRATION steady_foot_drift=",actual_contact_drift," max_grip_error=",max_grip_error)
    print("Dwarf calibration checks: ",failures," failures")
    quit(0 if failures==0 else 1)
