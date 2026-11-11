extends "res://docs/validate_articulation.gd"

func run() -> void:
    root.size = Vector2i(1280,800)
    var scene: Control = load("res://scenes/art/FallenGuardianWalkDemo.tscn").instantiate()
    root.add_child(scene); current_scene = scene
    for i in 10: await process_frame
    var model: Node3D = scene.get_node("Stage/Guardian")
    var player: AnimationPlayer = model.get_node("AnimationPlayer")
    var camera: Camera3D = scene.get_node("Stage/Camera3D")
    check(player.current_animation == "Walk" and player.is_playing(),"starts in WALK")
    check(scene.find_children("*","Button",true,false).size() == 2,"only IDLE and WALK controls")
    check(camera.size == 22,"orthographic size 22; portrait magnifies image only")
    var root_rest := model.transform
    var pose_rest: Transform3D = find(model,"PoseRoot").transform
    var resources := snapshot(model)
    var min_knee := 100.0
    var max_knee := -100.0
    var max_lift := 0.0
    var min_ankle_height := 100.0
    for state in ["Idle","Walk"]:
        scene.PlayState(state)
        player.advance(0.3)
        player.pause()
        var length := player.get_animation(state).length
        var preserved := true
        var no_root := true
        var feet_level := true
        var sword_up := true
        var guard_readable := true
        var wrist_bounded := true
        for i in 81:
            player.seek(i/80.0*length,true)
            no_root = no_root and model.transform.is_equal_approx(root_rest) and find(model,"PoseRoot").transform.is_equal_approx(pose_rest)
            for node: Node3D in model.find_children("*","Node3D",true,false): preserved = preserved and node.scale.is_equal_approx(Vector3.ONE)
            for side in ["Left","Right"]:
                var knee := find(model,side+"KneePivot")
                min_knee = minf(min_knee,knee.rotation.x)
                max_knee = maxf(max_knee,knee.rotation.x)
                var ankle := find(model,side+"AnklePivot")
                max_lift = maxf(max_lift,ankle.global_position.y-0.095)
                min_ankle_height = minf(min_ankle_height,ankle.global_position.y)
                feet_level = feet_level and absf(ankle.global_basis.y.dot(Vector3.UP)-1.0) < 0.0005
            var sword := find(model,"Sword")
            var tip := sword.to_global(Vector3(0.68,0.065,-0.39)-Vector3(0.438,0.91,-0.23))
            var guard := sword.to_global(Vector3(0.465,0.79,-0.26)-Vector3(0.438,0.91,-0.23))
            sword_up = sword_up and tip.y > guard.y+0.05
            guard_readable = guard_readable and camera.unproject_position(tip).y < camera.unproject_position(guard).y-10
            wrist_bounded = wrist_bounded and absf(find(model,"RightWristPivot").rotation.x) < deg_to_rad(30)
        var after := snapshot(model)
        for k in resources: preserved = preserved and resources[k].mesh == after[k].mesh and resources[k].arrays == after[k].arrays and resources[k].materials == after[k].materials
        check(preserved,state+" unchanged geometry/materials, no scaling")
        check(no_root,state+" no root motion")
        check(feet_level,state+" ankle compensation keeps feet level")
        check(sword_up and guard_readable,state+" sword projects upward in the isometric view")
        check(wrist_bounded,state+" wrist flexion remains below 30 degrees")
        player.seek(0,true)
        var first := snapshot(model)
        player.seek(length,true)
        var last := snapshot(model)
        var seam := true
        for k in first: seam = seam and first[k].transform.is_equal_approx(last[k].transform)
        check(seam,state+" closed cycle seam")
    print("GAIT_METRICS knee_span_degrees=",rad_to_deg(max_knee-min_knee)," max_ankle_lift=",max_lift," min_ankle_y=",min_ankle_height)
    check(rad_to_deg(max_knee-min_knee) > 25,"visible knee flexion range")
    check(max_lift > 0.09 and min_ankle_height > 0.085,"feet lift and return without ankle floor penetration")
    if "--capture" in OS.get_cmdline_user_args():
        scene.PlayState("Idle"); player.advance(0.3); player.seek(0.7,true); player.pause()
        for i in 6: await process_frame
        await RenderingServer.frame_post_draw
        check(root.get_texture().get_image().save_png("res://docs/milestone_05_v3_guard_idle.png") == OK,"capture Idle")
        scene.PlayState("Walk"); player.advance(0.3); player.pause()
        var sheet := Image.create(2400,855,false,Image.FORMAT_RGBA8)
        for view in 3:
            if view > 0:
                model.rotation_degrees.y = 0
                camera.position = Vector3(0,5,-8) if view == 1 else Vector3(8,5,0)
                camera.look_at(Vector3(0,1.1,0))
            for phase in 6:
                player.seek(phase/6.0*1.4,true)
                for i in 4: await process_frame
                await RenderingServer.frame_post_draw
                var frame := root.get_texture().get_image()
                if view == 0 and phase == 5: frame.save_png("res://docs/milestone_05_v3_guard_walk.png")
                var crop := frame.get_region(Rect2i(240,170,800,570))
                crop.convert(Image.FORMAT_RGBA8)
                crop.resize(400,285,Image.INTERPOLATE_LANCZOS)
                sheet.blit_rect(crop,Rect2i(0,0,400,285),Vector2i(phase*400,view*285))
        check(sheet.save_png("res://docs/milestone_05_v3_guard_cycle.png") == OK,"capture cycle: isometric, front and side views")
    print("V3 walk demo checks: ",failures," failures")
    quit(0 if failures == 0 else 1)
