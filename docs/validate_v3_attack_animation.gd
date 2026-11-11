extends "res://docs/validate_articulation.gd"

func run() -> void:
    root.size = Vector2i(1280,800)
    var scene: Control = load("res://scenes/art/FallenGuardianAttackDemo.tscn").instantiate()
    root.add_child(scene); current_scene = scene
    for i in 8: await process_frame
    var model: Node3D = scene.get_node("Stage/Guardian")
    var player: AnimationPlayer = model.get_node("AnimationPlayer")
    var attack := player.get_animation("Combat/Attack")
    var locomotion: AnimationLibrary = load("res://resources/animations/FallenGuardianV3GuardLocomotion.tres")
    check(player.get_animation("Idle") == locomotion.get_animation("Idle") and player.get_animation("Walk") == locomotion.get_animation("Walk"),"original Idle/Walk resources reused unchanged")
    check(scene.find_children("*","Button",true,false).size() == 3,"only IDLE WALK ATTACK controls")
    check(attack.loop_mode == Animation.LOOP_NONE and is_equal_approx(attack.length,1.6),"one-shot 1.6 s attack")
    var transform_only := true
    for track in attack.get_track_count(): transform_only = transform_only and attack.track_get_type(track) == Animation.TYPE_VALUE
    check(transform_only,"no method/event/damage tracks")
    scene.PlayState("Idle"); player.advance(0.2); player.seek(0,true); player.pause()
    var rest := snapshot(model)
    var root_rest := model.transform
    var pose_rest: Transform3D = find(model,"PoseRoot").transform
    var shield := find(model,"Shield")
    var forearm := find(model,"LeftElbowPivot")
    var shield_bind := forearm.global_transform.affine_inverse()*shield.global_transform
    scene.PlayState("Attack"); player.advance(0.2); player.pause()
    var attachments := true
    var preserved := true
    var no_root := true
    var tip_heights := []
    var previous_bases := {}
    var continuous := true
    var old_grip := Vector3(0.445,0.878,-0.235)-Vector3(0.438,0.91,-0.23)
    for i in 161:
        player.seek(i/100.0,true)
        var wrist := find(model,"RightWristPivot")
        var sword := find(model,"Sword")
        attachments = attachments and sword.to_global(old_grip).distance_to(wrist.to_global(Vector3(0.006,-0.084,-0.028))) < 0.00001
        attachments = attachments and shield_bind.is_equal_approx(forearm.global_transform.affine_inverse()*shield.global_transform)
        no_root = no_root and model.transform.is_equal_approx(root_rest) and find(model,"PoseRoot").transform.is_equal_approx(pose_rest)
        for node: Node3D in model.find_children("*","Node3D",true,false):
            preserved = preserved and node.scale.is_equal_approx(Vector3.ONE)
            var rotation := node.basis.get_rotation_quaternion()
            if previous_bases.has(node): continuous = continuous and previous_bases[node].angle_to(rotation) < 0.35
            previous_bases[node] = rotation
        tip_heights.append(sword.to_global(Vector3(0.68,0.065,-0.39)-Vector3(0.438,0.91,-0.23)).y)
    var final := snapshot(model)
    var guard_return := true
    for key in rest:
        guard_return = guard_return and rest[key].transform.is_equal_approx(final[key].transform)
        preserved = preserved and rest[key].mesh == final[key].mesh and rest[key].materials == final[key].materials and rest[key].arrays == final[key].arrays
    check(attachments,"grip and shield stay rigidly attached through 161 phases")
    check(no_root and preserved,"geometry/materials preserved; no root motion or scaling")
    check(guard_return,"final pose exactly restores approved Idle phase-zero guard")
    check(continuous,"no angular discontinuity over 10 ms samples")
    var high: float = tip_heights.max()
    var low: float = tip_heights.slice(60,90).min()
    print("ATTACK_METRICS tip_peak_y=",high," strike_min_y=",low," drop=",high-low)
    check(high-low > 0.6,"sword has a significant downward strike arc")
    scene.PlayState("Idle"); player.advance(0.2)
    scene.PlayState("Attack")
    for i in 130:
        await physics_frame
        await process_frame
    check(player.current_animation == "Idle" and player.is_playing(),"attack finishes once and automatically resumes Idle")
    if "--capture" in OS.get_cmdline_user_args():
        var camera: Camera3D = scene.get_node("Stage/Camera3D")
        var times := [0.0,0.2,0.4,0.6,0.68,0.76,1.0,1.4]
        var sheet := Image.create(2640,765,false,Image.FORMAT_RGBA8)
        for view in 3:
            if view > 0:
                model.rotation_degrees.y = 0
                camera.position = Vector3(0,5,-8) if view == 1 else Vector3(8,5,0)
                camera.look_at(Vector3(0,1.1,0))
            scene.PlayState("Idle"); player.advance(0.2)
            scene.PlayState("Attack"); player.advance(0.2); player.pause()
            for phase in times.size():
                player.seek(times[phase],true)
                for i in 4: await process_frame
                await RenderingServer.frame_post_draw
                var frame := root.get_texture().get_image()
                if view == 0 and phase in [3,5]:
                    frame.save_png("res://docs/milestone_05_attack_"+("windup" if phase == 3 else "strike")+".png")
                var crop := frame.get_region(Rect2i(200,80,880,680))
                crop.convert(Image.FORMAT_RGBA8); crop.resize(330,255,Image.INTERPOLATE_LANCZOS)
                sheet.blit_rect(crop,Rect2i(0,0,330,255),Vector2i(phase*330,view*255))
        check(sheet.save_png("res://docs/milestone_05_attack_phases.png") == OK,"capture full sequence isometric/front/side")
    print("Attack animation checks: ",failures," failures")
    quit(0 if failures == 0 else 1)
