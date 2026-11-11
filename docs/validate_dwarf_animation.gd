extends "res://docs/validate_dwarf_art.gd"

var _bounds_cache := {}

# Cache immutable mesh arrays once. Repeated GPU readbacks would stall the rendered inspection.
func bounds(model: Node3D, part: Node3D) -> AABB:
    var id := part.get_instance_id()
    if not _bounds_cache.has(id):
        var entries := []
        for mesh: MeshInstance3D in part.find_children("*", "MeshInstance3D", true, false):
            for surface in mesh.mesh.get_surface_count():
                entries.append([mesh, mesh.mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]])
        _bounds_cache[id] = entries
    var first := true
    var result := AABB()
    for entry in _bounds_cache[id]:
        var transform: Transform3D = model.global_transform.affine_inverse() * entry[0].global_transform
        for vertex: Vector3 in entry[1]:
            var p := transform * vertex
            if first: result = AABB(p, Vector3.ZERO); first = false
            else: result = result.expand(p)
    return result

func find(model: Node, name: String) -> Node3D:
    return model.find_child(name, true, false)

func geometry(model: Node3D) -> Dictionary:
    var result := {}
    for mesh: MeshInstance3D in model.find_children("*", "MeshInstance3D", true, false):
        var signature := []
        for surface in mesh.mesh.get_surface_count():
            var arrays := mesh.mesh.surface_get_arrays(surface)
            signature.append([hash(arrays[Mesh.ARRAY_VERTEX]), hash(arrays[Mesh.ARRAY_NORMAL]), hash(arrays[Mesh.ARRAY_COLOR]), hash(arrays[Mesh.ARRAY_INDEX]), mesh.mesh.surface_get_material(surface)])
        result[str(mesh.get_parent().name)] = signature
    return result

func transforms(model: Node3D) -> Dictionary:
    var result := {}
    for part: Node3D in model.find_children("*", "MeshInstance3D", true, false):
        result[str(part.get_parent().name)] = model.global_transform.affine_inverse() * part.global_transform
    return result

func run() -> void:
    root.size = Vector2i(1280, 800)
    var reference: Node3D = load("res://scenes/characters/DwarfBerserker.tscn").instantiate()
    root.add_child(reference)
    var animated: Node3D = load("res://scenes/characters/DwarfBerserkerAnimated.tscn").instantiate()
    root.add_child(animated); animated.set_process(false)
    var model: Node3D = animated.get_node("Model")
    var player: AnimationPlayer = animated.get_node("AnimationPlayer")
    check(player.has_animation("Idle") and player.has_animation("Walk"), "Reusable Idle/Walk resources resolve")
    check(is_equal_approx(player.get_animation("Idle").length, 3.2) and is_equal_approx(player.get_animation("Walk").length, 1.4), "Idle 3.2 s / Walk 1.4 s")
    check(geometry(reference) == geometry(model), "All approved 06A vertices, normals, colors, indices and material resources reused unchanged")
    var reference_transforms := transforms(reference)
    for property in ["BodyOffset", "TorsoRotation", "HeadRotation", "WeaponOffset", "WeaponRotation", "BeardRotation", "LeftFootOffset", "RightFootOffset"]:
        animated.set(property, Vector3.ZERO)
    animated.LeftFootPitch = 0; animated.RightFootPitch = 0; animated.ApplyPose()
    var neutral := transforms(model)
    var rest_matches := true
    for part in neutral: rest_matches = rest_matches and neutral[part].is_equal_approx(reference_transforms[part])
    check(rest_matches, "Zero controls restore exact approved resting transforms after attachment binding and IK")
    var originals := geometry(model)
    var root_rest := animated.transform
    var model_rest := model.transform
    var pose_rest: Transform3D = find(model, "PoseRoot").transform
    var axe := find(model, "BattleAxe")
    check(axe.get_parent() == find(model, "TorsoPivot"), "Stable axe attachment belongs to torso")
    check(find(model, "Beard").get_parent() == find(model, "HeadPivot"), "Separate beard follows head")
    var lengths := {}
    for side in ["Left", "Right"]:
        for chain in [["ShoulderPivot", "ElbowPivot", "WristPivot"], ["HipPivot", "KneePivot", "AnklePivot"]]:
            for i in 2:
                lengths[side+chain[i]] = find(model, side+chain[i]).global_position.distance_to(find(model, side+chain[i+1]).global_position)
    var max_grip := 0.0
    var min_sole := 100.0
    var min_crown := 100.0
    var max_lift := 0.0
    var min_knee := 100.0
    var max_knee := -100.0
    var max_shoulder := 0.0
    var max_wrist := 0.0
    var rigid_lengths := true
    var unchanged_roots := true
    var unit_scale := true
    var idle_feet := true
    var stance_error := 0.0
    for state in ["Idle", "Walk"]:
        var length := player.get_animation(state).length
        var property_tracks := true
        for track in player.get_animation(state).get_track_count():
            property_tracks = property_tracks and player.get_animation(state).track_get_type(track) == Animation.TYPE_VALUE
        check(property_tracks, state+" tracks are visual properties only (no method/event tracks)")
        for i in 241:
            var phase := i/240.0
            animated.SamplePose(state, phase*length)
            unchanged_roots = unchanged_roots and animated.transform.is_equal_approx(root_rest) and model.transform.is_equal_approx(model_rest) and find(model, "PoseRoot").transform.is_equal_approx(pose_rest)
            min_crown = minf(min_crown, bounds(model, find(model, "BaldHead")).end.y)
            for node: Node3D in model.find_children("*", "Node3D", true, false): unit_scale = unit_scale and node.scale.is_equal_approx(Vector3.ONE)
            for side in ["Left", "Right"]:
                var hand := find(model, side+"Gauntlet").get_node("GripPoint")
                var anchor := axe.get_node(side+"Grip")
                max_grip = maxf(max_grip, hand.global_position.distance_to(anchor.global_position))
                var sole := bounds(model, find(model, side+"Boot")).position.y
                min_sole = minf(min_sole, sole)
                if state == "Walk":
                    max_lift = maxf(max_lift, sole)
                    min_knee = minf(min_knee, find(model, side+"KneePivot").rotation.x)
                    max_knee = maxf(max_knee, find(model, side+"KneePivot").rotation.x)
                else:
                    var foot := find(model, side+"AnklePivot")
                    var original := find(reference, side+"AnklePivot")
                    idle_feet = idle_feet and foot.global_position.distance_to(original.global_position) < 0.00002
                max_shoulder = maxf(max_shoulder, find(model, side+"ShoulderPivot").quaternion.get_angle())
                max_wrist = maxf(max_wrist, find(model, side+"WristPivot").quaternion.get_angle())
                for chain in [["ShoulderPivot", "ElbowPivot", "WristPivot"], ["HipPivot", "KneePivot", "AnklePivot"]]:
                    for j in 2:
                        var current := find(model, side+chain[j]).global_position.distance_to(find(model, side+chain[j+1]).global_position)
                        rigid_lengths = rigid_lengths and absf(current-lengths[side+chain[j]]) < 0.00002
            if state == "Walk" and phase <= 0.6:
                var z := find(model, "LeftAnklePivot").global_position.z
                stance_error = maxf(stance_error, absf(z-(0.015-0.070+0.14*phase/0.6)))
        check(geometry(model) == originals, state+" does not mutate meshes, attributes or material resources")
        animated.SamplePose(state, 0); var first := transforms(model)
        animated.SamplePose(state, length); var last := transforms(model)
        var seamless := true
        for part in first: seamless = seamless and first[part].is_equal_approx(last[part])
        check(seamless, state+" loop endpoints match without a snap")
    check(unchanged_roots and unit_scale, "No root motion, model displacement or scaling")
    check(rigid_lengths, "All four limbs maintain original segment lengths")
    check(max_grip < 0.00002, "Both hand anchors stay on shaft throughout both complete cycles")
    check(idle_feet, "Idle ankle/foot positions remain planted through weight shifts")
    check(min_sole > -0.0001, "Actual boot geometry never penetrates floor")
    check(min_crown >= 1.9309104681015*0.75, "Minimum animated crown stays above 75% of Guardian standing height")
    check(max_lift > 0.05 and rad_to_deg(max_knee-min_knee) > 25, "Swing has visible sole lift and knee flexion")
    check(stance_error < 0.00002, "Support foot has constant backward speed, matching virtual forward travel")
    check(rad_to_deg(max_shoulder) < 15 and rad_to_deg(max_wrist) < 35, "Grip constraints avoid extreme shoulder and wrist rotations")
    print("DWARF_GAIT max_grip_error=", max_grip, " min_sole=", min_sole, " min_crown=", min_crown, " max_lift=", max_lift, " knee_span_deg=", rad_to_deg(max_knee-min_knee), " max_shoulder_deg=", rad_to_deg(max_shoulder), " max_wrist_deg=", rad_to_deg(max_wrist), " stance_error=", stance_error)
    var transition_step := 0.0
    var blended_grip := true
    var blended_clearance := true
    for state in ["Idle", "Walk", "Idle", "Walk", "Idle"]:
        animated.PlayState(state)
        var previous: Vector3 = find(model, "LeftAnklePivot").global_position
        for i in 40:
            animated._Process(1.0/60)
            blended_clearance = blended_clearance and bounds(model, find(model, "BaldHead")).end.y >= 1.9309104681015*0.75
            var current: Vector3 = find(model, "LeftAnklePivot").global_position
            transition_step = maxf(transition_step, previous.distance_to(current)); previous = current
            for side in ["Left", "Right"]:
                blended_grip = blended_grip and find(model, side+"Gauntlet").get_node("GripPoint").global_position.distance_to(axe.get_node(side+"Grip").global_position) < 0.00002
                blended_clearance = blended_clearance and bounds(model, find(model, side+"Boot")).position.y > -0.0001
    check(blended_grip, "Grip retained while blending between Idle and Walk repeatedly")
    check(blended_clearance, "Transitions preserve floor contact tolerance and minimum height")
    check(transition_step < 0.025, "Repeated Idle/Walk transitions blend without foot teleport")
    var clock := player.current_animation_position
    animated.PlayState("Idle")
    check(is_equal_approx(player.current_animation_position, clock), "Selecting same clip does not restart it")
    animated.PlayState("Attack")
    check(str(animated.ActiveClip) == "Idle", "Unsupported gameplay/attack state ignored")
    print("DWARF_TRANSITION max_60hz_foot_step=", transition_step)
    var oriented_grip := true
    var oriented_floor := true
    for angle in [0.0, 60.0, -135.0, 180.0]:
        animated.position = Vector3(3, 0, -2); animated.rotation.y = deg_to_rad(angle)
        for phase in [0.0, 0.23, 0.55, 0.83]:
            animated.SamplePose("Walk", phase*1.4)
            for side in ["Left", "Right"]:
                oriented_grip = oriented_grip and find(model, side+"Gauntlet").get_node("GripPoint").global_position.distance_to(axe.get_node(side+"Grip").global_position) < 0.00002
                oriented_floor = oriented_floor and bounds(model, find(model, side+"Boot")).position.y > -0.0001
    check(oriented_grip and oriented_floor, "Bindings and ground compensation work after translation and rotation")
    reference.free(); animated.free()
    var preview: Node3D = load("res://scenes/art/DwarfBerserkerPreview.tscn").instantiate()
    root.add_child(preview); current_scene = preview
    var actor: Node3D = preview.get_node("Dwarf"); actor.set_process(false)
    check(str(actor.ActiveClip) == "Idle", "Preview starts in Idle")
    preview.PlayState("Walk")
    check(str(actor.ActiveClip) == "Walk", "Preview Walk control selects Walk")
    if "--capture" in OS.get_cmdline_user_args():
        preview.PlayState("Idle"); preview.SetView("Isometric"); actor.SamplePose("Idle", 1.0)
        for i in 5: await process_frame
        await RenderingServer.frame_post_draw
        check(root.get_texture().get_image().save_png("res://docs/milestone_06b_idle_isometric.png") == OK, "Idle captured")
        var idle_sheet := Image.create(800, 860, false, Image.FORMAT_RGBA8)
        for frame in 4:
            actor.SamplePose("Idle", frame/4.0*3.2)
            for i in 2: await process_frame
            await RenderingServer.frame_post_draw
            var idle_frame := root.get_texture().get_image()
            idle_frame.convert(Image.FORMAT_RGBA8)
            idle_sheet.blit_rect(idle_frame, Rect2i(230, 210, 400, 430), Vector2i(frame%2*400, frame/2*430))
        check(idle_sheet.get_used_rect().size.x == 800 and idle_sheet.save_png("res://docs/milestone_06b_idle_cycle.png") == OK, "Four-phase Idle cycle captured")
        preview.PlayState("Walk")
        for view in ["Front", "Side", "Rear", "Isometric"]:
            preview.SetView(view)
            var sheet := Image.create(1600, 860, false, Image.FORMAT_RGBA8)
            for frame in 8:
                actor.SamplePose("Walk", frame/8.0*1.4)
                for i in 2: await process_frame
                await RenderingServer.frame_post_draw
                var capture := root.get_texture().get_image()
                capture.convert(Image.FORMAT_RGBA8)
                sheet.blit_rect(capture, Rect2i(230, 210, 400, 430), Vector2i(frame%4*400, frame/4*430))
            check(sheet.get_used_rect().size.x == 1600, view+" contact sheet contains rendered pixels")
            check(sheet.save_png("res://docs/milestone_06b_walk_"+view.to_lower()+".png") == OK, view+" complete 8-phase Walk inspection captured")
        preview.SetView("Isometric"); preview.get_node("Camera3D").size = 22
        actor.SamplePose("Walk", 0.35)
        for i in 3: await process_frame
        await RenderingServer.frame_post_draw
        check(root.get_texture().get_image().save_png("res://docs/milestone_06b_walk_gameplay.png") == OK, "Walk gameplay size 22 captured")
    check(preview.get_node("Guardian").find_children("*", "AnimationPlayer", true, false).is_empty(), "Guardian reference remains static")
    print("Dwarf animation checks: ", failures, " failures")
    quit(0 if failures == 0 else 1)
