extends "res://docs/validate_articulation.gd"

func run() -> void:
    var model: Node3D = load("res://scenes/enemies/FallenGuardianV3GuardPresentation.tscn").instantiate()
    root.add_child(model)
    var player: AnimationPlayer = model.get_node("AnimationPlayer")
    var hand_grip := Vector3(0.006,-0.084,-0.028)
    var sword_pivot := Vector3(0.438,0.91,-0.23)
    var pommel := Vector3(0.425,0.966,-0.21)-sword_pivot
    var guard := Vector3(0.465,0.79,-0.26)-sword_pivot
    var tip := Vector3(0.68,0.065,-0.39)-sword_pivot
    for clip in ["Idle","Walk"]:
        player.play(clip); player.advance(0); player.pause()
        var aligned := true
        var transverse := true
        var wrist_neutral := true
        var forward := true
        var chest_guard := true
        var tucked := true
        for i in 81:
            player.seek(i/80.0*player.get_animation(clip).length,true)
            var wrist := find(model,"RightWristPivot")
            var sword := find(model,"Sword")
            var midpoint := sword.to_global((pommel+guard)*0.5)
            aligned = aligned and midpoint.distance_to(wrist.to_global(hand_grip)) < 0.00001
            var shaft: Vector3 = wrist.global_basis.inverse()*(sword.to_global(guard)-sword.to_global(pommel)).normalized()
            transverse = transverse and shaft.dot(Vector3.RIGHT) > 0.9999
            wrist_neutral = wrist_neutral and absf(wrist.rotation.x) < 0.0001 and absf(wrist.rotation.y) < 0.0001 and absf(wrist.rotation.z) <= deg_to_rad(5.01)
            forward = forward and (sword.to_global(tip)-sword.to_global(guard)).z < 0
            var direction := (sword.to_global(tip)-sword.to_global(guard)).normalized()
            chest_guard = chest_guard and direction.y > 0.65 and midpoint.x > 0.25 and midpoint.x < 0.5 and midpoint.y > 1.0 and midpoint.y < 1.25
            var shoulder := find(model,"RightShoulderPivot")
            var elbow := find(model,"RightElbowPivot")
            tucked = tucked and (elbow.global_position-shoulder.global_position).normalized().dot(Vector3.DOWN) > 0.98 and absf(elbow.global_position.x-shoulder.global_position.x) < 0.05
            if i == 0:
                print("POSE ",clip," shoulder=",find(model,"RightShoulderPivot").rotation_degrees," elbow=",find(model,"RightElbowPivot").rotation_degrees," wrist_pos=",wrist.global_position," hand_grip=",midpoint," blade_axis=",(sword.to_global(tip)-sword.to_global(guard)).normalized())
        check(aligned,clip+" handle center stays within curled hand through full cycle")
        check(transverse,clip+" shaft crosses fingers on hand-local X")
        check(wrist_neutral,clip+" near-neutral wrist within 5 degrees, no longitudinal wrist twist")
        check(forward,clip+" blade points slightly forward")
        check(chest_guard,clip+" relaxed low guard beside right hip/lower ribs with diagonal upward blade")
        check(tucked,clip+" upper arm stays close to torso")
    if "--capture" in OS.get_cmdline_user_args():
        model.free()
        root.size = Vector2i(1280,800)
        var demo: Control = load("res://scenes/art/FallenGuardianWalkDemo.tscn").instantiate()
        root.add_child(demo); current_scene = demo
        var guardian: Node3D = demo.get_node("Stage/Guardian")
        var animator: AnimationPlayer = guardian.get_node("AnimationPlayer")
        var camera: Camera3D = demo.get_node("Stage/Camera3D")
        demo.PlayState("Idle"); animator.advance(0.3); animator.pause()
        var sheet := Image.create(2400,855,false,Image.FORMAT_RGBA8)
        for view in 3:
            if view > 0:
                guardian.rotation_degrees.y = 0
                camera.position = Vector3(0,5,-8) if view == 1 else Vector3(8,5,0)
                camera.look_at(Vector3(0,1.1,0))
            for phase in 6:
                animator.seek(phase/6.0*3.0,true)
                for frame in 4: await process_frame
                await RenderingServer.frame_post_draw
                var crop := root.get_texture().get_image().get_region(Rect2i(240,170,800,570))
                crop.convert(Image.FORMAT_RGBA8)
                crop.resize(400,285,Image.INTERPOLATE_LANCZOS)
                sheet.blit_rect(crop,Rect2i(0,0,400,285),Vector2i(phase*400,view*285))
        check(sheet.save_png("res://docs/milestone_05_v3_low_guard_idle_cycle.png") == OK,"Idle full-cycle phase sheet, isometric/front/side")
    print("Guard grip checks: ",failures," failures")
    quit(0 if failures == 0 else 1)
