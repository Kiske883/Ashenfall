extends "res://docs/validate_dwarf_animation.gd"

func capture(label: String) -> void:
    if "--capture" not in OS.get_cmdline_user_args() or DisplayServer.get_name()=="headless": return
    await RenderingServer.frame_post_draw
    check(root.get_texture().get_image().save_png("res://docs/combat_03_1_"+label+".png")==OK,"Gameplay camera capture "+label)

func ticks(count: int) -> void:
    for i in count: await physics_frame

func mouse_at(camera: Camera3D, point: Vector3, pressed: bool) -> void:
    var event:=InputEventMouseButton.new()
    event.button_index=MOUSE_BUTTON_LEFT
    event.position=camera.unproject_position(point)
    event.global_position=event.position
    event.pressed=pressed
    root.push_input(event,true)

func run() -> void:
    root.size=Vector2i(1280,800)
    var demo: Node3D=load("res://scenes/world/AshenfallDwarfPlayableDemo.tscn").instantiate()
    root.add_child(demo); current_scene=demo
    var world: Node3D=demo.get_node("World")
    var actor: CharacterBody3D=world.get_node("FutureActors/Player")
    var camera: Camera3D=world.get_node("CameraRig/Camera3D")
    var enemy: CharacterBody3D=demo.get_node("FallenGuardian")
    var marker: Node=enemy.get_node("MeleeTarget")
    var melee: Node=demo.get_node("PlayerMelee")
    var dwarf: Node3D=actor.get_node("Visual/Dwarf")
    var animator: AnimationPlayer=dwarf.get_node("AnimationPlayer")
    await ticks(75)
    check(melee.State==0 and marker.Targetable and melee.AttackSequence==0,"Passive target and idle command state load")
    check(is_equal_approx(melee.AttackDuration,1.7) and is_equal_approx(actor.MovementSpeed,2.5),"Configurable 1.70 s attack and original 2.5 m/s movement")
    # Ground press, drag and release still use the existing acquisition path.
    mouse_at(camera,Vector3(2,0,4),true); await ticks(8)
    var first: Vector3=actor.Destination
    var motion:=InputEventMouseMotion.new()
    motion.position=camera.unproject_position(Vector3(3,0,3)); motion.global_position=motion.position
    motion.button_mask=MOUSE_BUTTON_MASK_LEFT; root.push_input(motion,true)
    await ticks(12)
    check(actor.HasDestination and first.distance_to(actor.Destination)>1 and melee.State==0,"Held ground click still updates destination")
    mouse_at(camera,Vector3(3,0,3),false); await ticks(1)
    var released: Vector3=actor.Destination
    await ticks(5)
    check(actor.Destination.is_equal_approx(released),"Mouse release preserves last ground destination")
    # A click on the enemy collider is consumed before any ground ray runs.
    mouse_at(camera,enemy.global_position+Vector3.UP*.9,true); await ticks(1)
    mouse_at(camera,enemy.global_position+Vector3.UP*.9,false)
    check(melee.SelectedTarget==marker and melee.State==1 and not actor.HasDestination,"Enemy click replaces ground order without a simultaneous terrain target")
    await capture("approach")
    var approached:=false
    for i in 700:
        await physics_frame
        if melee.State==2:
            approached=true; break
    check(approached and melee.AttackSequence==1 and actor.MovementLocked,"Navigation approach ends in one locked logical melee attack")
    check(animator.current_animation=="Combat/AxeAttack","Presentation observes logical attack and plays Impact C")
    await ticks(42)
    await capture("windup")
    var center_distance:=Vector2(actor.global_position.x-enemy.global_position.x,actor.global_position.z-enemy.global_position.z).length()
    check(center_distance<=melee.MeleeRange+.08,"Attack begins inside configured center-based melee range")
    mouse_at(camera,enemy.global_position+Vector3.UP*.9,true); await ticks(1)
    mouse_at(camera,enemy.global_position+Vector3.UP*.9,false)
    check(melee.AttackSequence==1 and not actor.HasDestination,"Enemy click during attack cannot duplicate the swing or issue ground movement")
    var frozen:=actor.global_position
    Input.action_press("move_right")
    await ticks(8)
    Input.action_release("move_right")
    check(Vector2(actor.global_position.x-frozen.x,actor.global_position.z-frozen.z).length()>.05 and melee.State==0 and not actor.MovementLocked,
        "WASD intentionally interrupts the attack and moves in the same physics window")
    var prior:int=melee.AttackSequence
    await ticks(110)
    check(melee.State==0 and not actor.MovementLocked and melee.AttackSequence==prior,"One command yields one attack, then releases movement lock without auto-repeat")
    check(animator.current_animation=="Combat/GuardB","Visual recovery returns to Guard B")
    await capture("recovery")
    # Commands work without mouse input and do not know the dwarf model.
    check(melee.IssueAttack(marker),"Input-independent command entry accepts a valid target")
    var again:=false
    for i in 700:
        await physics_frame
        if melee.AttackSequence==prior+1:
            again=true; break
    check(again,"A second explicit command can approach and attack once")
    await ticks(110)
    check(melee.AttackSequence==prior+1 and not actor.MovementLocked,"Second order does not loop or leave movement locked")
    check(melee.IssueAttack(marker),"A fresh target order can be issued after recovery")
    Input.action_press("move_up"); await ticks(2); Input.action_release("move_up")
    check(melee.State==0 and not actor.MovementLocked,"Manual WASD cancels a pending target approach")
    check(melee.IssueAttack(marker),"Target can be selected again after WASD cancellation")
    mouse_at(camera,Vector3(-.8,0,5),true); await ticks(1)
    mouse_at(camera,Vector3(-.8,0,5),false)
    check(melee.State==0 and melee.SelectedTarget==null and actor.HasDestination,"Ground click cancels target order and keeps original movement semantics")
    actor.CancelDestination()
    # Outside-mesh targets are rejected without a direct off-terrain move.
    var outside:=StaticBody3D.new(); outside.position=Vector3(30,0,0)
    var outside_marker:Node=load("res://scripts/combat/MeleeTarget.cs").new()
    outside.add_child(outside_marker); demo.add_child(outside)
    check(melee.IssueAttack(outside_marker),"Command accepts model-independent marker")
    await ticks(12)
    check(melee.State==0 and not actor.HasDestination and not actor.MovementLocked,"Unreachable target cancels safely at navigation boundary")
    outside.queue_free()
    await process_frame
    melee.AttackDuration=1.2
    check(melee.IssueAttack(null),"Configurable-duration untargeted Space-style command remains available")
    await ticks(1)
    check(is_equal_approx(animator.speed_scale,1.7/1.2) and actor.MovementLocked,"Visual clip rate follows the independent logical attack duration")
    var lock_position: Vector3=actor.global_position
    mouse_at(camera,Vector3(-.8,0,5),true); await ticks(8)
    mouse_at(camera,Vector3(-.8,0,5),false)
    check(actor.HasDestination and not actor.MovementLocked and melee.State==0 and
        Vector2(actor.global_position.x-lock_position.x,actor.global_position.z-lock_position.z).length()>.05,
        "Ground click interrupts the attack and begins immediate navigation")
    await ticks(78)
    check(not actor.MovementLocked and Vector2(actor.global_position.x-lock_position.x,actor.global_position.z-lock_position.z).length()>.05,"Navigation continues after interrupting the attack")
    print("Melee foundation checks: ",failures," failures")
    quit(0 if failures==0 else 1)
