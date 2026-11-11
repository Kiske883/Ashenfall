extends "res://docs/validate_articulation.gd"

func run() -> void:
    root.size = Vector2i(1280,800)
    var scene: Node3D = load("res://scenes/art/LocomotionComparison.tscn").instantiate()
    root.add_child(scene)
    current_scene = scene
    for i in 16: await process_frame
    var model: Node3D = scene.get_node("Articulated")
    var player: AnimationPlayer = model.get_node("AnimationPlayer")
    var rest := snapshot(model)
    var original := snapshot(scene.get_node("Original"))
    var root_rest := model.transform
    var pose_rest: Transform3D = find(model,"PoseRoot").transform
    for key in rest:
        check(rest[key].arrays == original[key].arrays and rest[key].transform.is_equal_approx(original[key].transform), key+" approved resting appearance")
    for clip in ["Idle","Walk"]:
        var animation := player.get_animation(clip)
        check(animation != null and animation.loop_mode == Animation.LOOP_LINEAR, clip+" reusable looping resource")
        var paths_valid := true
        for track in animation.get_track_count():
            var path := animation.track_get_path(track)
            var target := model.get_node_or_null(NodePath(str(path).get_slice(":",0)))
            paths_valid = paths_valid and target != null and path.get_subname(0) == "rotation"
        check(paths_valid, clip+" rotation-only tracks resolve")
        scene.SampleClip(clip,0.0)
        var beginning := snapshot(model)
        scene.SampleClip(clip,1.0)
        var end := snapshot(model)
        var seam := true
        for key in beginning:
            seam = seam and beginning[key].transform.is_equal_approx(end[key].transform)
        check(seam, clip+" loop seam matches")
        var preserved := true
        var feet_stable := true
        var no_root := true
        for i in range(25):
            scene.SampleClip(clip,i/24.0)
            var sample := snapshot(model)
            no_root = no_root and model.transform.is_equal_approx(root_rest) and find(model,"PoseRoot").transform.is_equal_approx(pose_rest)
            for key in rest:
                preserved = preserved and sample[key].mesh == rest[key].mesh and sample[key].arrays == rest[key].arrays and sample[key].materials == rest[key].materials
            for node: Node3D in model.find_children("*","Node3D",true,false):
                preserved = preserved and node.scale.is_equal_approx(Vector3.ONE)
            for leg in ["LeftLeg","RightLeg"]:
                feet_stable = feet_stable and sample[leg].transform.is_equal_approx(rest[leg].transform)
        check(preserved, clip+" resources unchanged and no scaling")
        check(no_root, clip+" no root motion")
        if clip == "Idle": check(feet_stable, "Idle feet and legs remain fixed")
        scene.SampleClip(clip,0.25)
        if clip == "Walk":
            var left := find(model,"LeftLegPivot").rotation.x
            var right := find(model,"RightLegPivot").rotation.x
            check(left*right < 0 and left*find(model,"LeftArmPivot").rotation.x < 0, "Walk alternates legs with opposing arm swing")
        if "--capture" in OS.get_cmdline_user_args():
            for i in 10: await process_frame
            await RenderingServer.frame_post_draw
            check(root.get_texture().get_image().save_png("res://docs/milestone_05_locomotion_"+clip.to_lower()+".png") == OK,"capture "+clip)
        scene.PlayClip(clip)
        var time := player.current_animation_position
        for i in 10: await process_frame
        check(player.is_playing() and player.current_animation_position != time, clip+" advances during playback")
    scene.ResetPose()
    var reset := snapshot(model)
    var reset_ok := true
    for key in rest: reset_ok = reset_ok and reset[key].transform.is_equal_approx(rest[key].transform)
    check(reset_ok and not player.is_playing(), "Reset restores complete resting pose")
    scene.SetGameplayView(true)
    check(scene.get_node("Camera3D").size == 22,"gameplay view size 22")
    check(snapshot(scene.get_node("Original")) == original,"original stays stationary")
    print("Locomotion checks: ",failures," failures")
    quit(0 if failures == 0 else 1)
