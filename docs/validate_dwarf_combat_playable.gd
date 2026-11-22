extends "res://docs/validate_dwarf_animation.gd"

func capture(label: String) -> void:
    if "--capture" not in OS.get_cmdline_user_args() or DisplayServer.get_name()=="headless": return
    await RenderingServer.frame_post_draw
    check(root.get_texture().get_image().save_png("res://docs/milestone_06d_playable_"+label+".png")==OK,"Gameplay camera captures "+label)

func run() -> void:
    root.size=Vector2i(1280,800)
    var demo: Node3D=load("res://scenes/world/AshenfallDwarfPlayableDemo.tscn").instantiate()
    root.add_child(demo); current_scene=demo
    var world: Node3D=demo.get_node("World")
    var player: CharacterBody3D=world.get_node("FutureActors/Player")
    var driver: Node=demo.get_node("DwarfPresentation")
    var dwarf: Node3D=player.get_node("Visual/Dwarf")
    var animator: AnimationPlayer=dwarf.get_node("AnimationPlayer")
    var axe: Node3D=find(dwarf.get_node("Model"),"BattleAxe")
    var enemy: Node3D=demo.get_node("FallenGuardian")
    for i in 60: await physics_frame
    check(driver.CombatMode and is_equal_approx(player.MovementSpeed,2.5),"Definitive demo selects combat mode and 2.5 m/s")
    check(animator.current_animation=="Combat/GuardB","Dwarf starts in approved Guard B")
    await capture("guard")
    check(is_equal_approx(animator.get_animation("Combat/AxeAttack").length,1.7),"Approved Impact C attack is 1.70 s")
    check(enemy.PlayerTarget==player and is_equal_approx(enemy.MovementSpeed,1) and is_equal_approx(enemy.ReturnSpeed,.7),"Guardian target and dual speeds unchanged")
    var max_grip:=0.0; var worst_context:=""
    var max_attack_grip:=0.0
    Input.action_press("move_right")
    for i in 36:
        await physics_frame
        for side in ["Left","Right"]:
            var hand: Node3D=find(dwarf.get_node("Model"),side+"Gauntlet").get_node("GripPoint")
            var grip: Node3D=axe.get_node(side+"Grip")
            var gap:=hand.global_position.distance_to(grip.global_position)
            if gap>max_grip: max_grip=gap; worst_context="walk "+side+" at "+str(animator.current_animation_position)
    check(animator.current_animation=="Combat/CombatWalk" and is_equal_approx(driver.ActualHorizontalSpeed,2.5),"Movement selects combat Walk using actual 2.5 m/s")
    check(absf(driver.WalkPlaybackRate-2.5/1.5151515)<.02,"Combat Walk keeps grounded stride calibration")
    await capture("walk")
    Input.action_release("move_right")
    for i in 3: await physics_frame
    # Press Space through the viewport to verify actual input routing.
    var key:=InputEventKey.new(); key.keycode=KEY_SPACE; key.physical_keycode=KEY_SPACE; key.pressed=true
    root.push_input(key,true)
    await physics_frame
    check(animator.current_animation=="Combat/AxeAttack" and dwarf.IsAttacking,"Space triggers visual Attack C")
    var clock:=animator.current_animation_position
    root.push_input(key,true)
    await physics_frame
    check(animator.current_animation=="Combat/AxeAttack" and animator.current_animation_position>clock,"Repeated Space does not restart the one-shot")
    var attack_scaled:=true
    for i in 90:
        await physics_frame
        if i==40: await capture("windup")
        if i==56: await capture("impact")
        attack_scaled=attack_scaled and is_equal_approx(animator.speed_scale,1)
        for side in ["Left","Right"]:
            var hand: Node3D=find(dwarf.get_node("Model"),side+"Gauntlet").get_node("GripPoint")
            var grip: Node3D=axe.get_node(side+"Grip")
            var gap:=hand.global_position.distance_to(grip.global_position)
            max_attack_grip=maxf(max_attack_grip,gap)
            if gap>max_grip: max_grip=gap; worst_context="attack "+side+" at "+str(animator.current_animation_position)
    check(attack_scaled,"Movement cannot speed up or replace the attack clip")
    for i in 24: await physics_frame
    check(not dwarf.IsAttacking and animator.current_animation=="Combat/GuardB","Uninterrupted attack recovers to Guard B")
    Input.action_press("move_right")
    for i in 12: await physics_frame
    check(animator.current_animation=="Combat/CombatWalk","Moving dwarf enters combat Walk after attack")
    Input.action_release("move_right")
    for i in 24: await physics_frame
    check(animator.current_animation=="Combat/GuardB" and is_equal_approx(animator.speed_scale,1),"Stopping restores combat idle in Guard B")
    check(driver.RequestAttack(),"Attack can be repeated after recovery")
    for i in 110: await physics_frame
    check(not dwarf.IsAttacking and animator.current_animation=="Combat/GuardB","Second attack returns to combat idle")
    # Mouse press still belongs to PlayerController; it must not trigger attack.
    var camera: Camera3D=world.get_node("CameraRig/Camera3D")
    var click:=InputEventMouseButton.new(); click.button_index=MOUSE_BUTTON_LEFT
    click.position=camera.unproject_position(Vector3(0,0,4)); click.global_position=click.position; click.pressed=true
    root.push_input(click,true)
    for i in 8: await physics_frame
    check(not dwarf.IsAttacking and player.HasDestination,"Left click retains navigation rather than attacking")
    click.pressed=false; root.push_input(click,true)
    print("COMBAT_GRIP max=",max_grip," during_attack=",max_attack_grip," worst=",worst_context)
    check(max_grip<.0001,"Axe grip remains within sub-millimetre visual tolerance")
    print("Dwarf combat playable checks: ",failures," failures")
    quit(0 if failures==0 else 1)
