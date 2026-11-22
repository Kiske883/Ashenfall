extends SceneTree

var failures := 0
var cues := []

func check(ok: bool, label: String) -> void:
    if ok: print("PASS: ", label)
    else:
        failures += 1
        push_error("FAIL: " + label)

func ticks(count: int) -> void:
    for i in count: await physics_frame

func grip_gap(model: Node3D, axe: Node3D) -> float:
    var gap := 0.0
    for side in ["Left", "Right"]:
        var hand: Node3D = model.find_child(side + "Gauntlet", true, false).get_node("GripPoint")
        gap = maxf(gap, hand.global_position.distance_to(axe.get_node(side + "Grip").global_position))
    return gap

func capture(label: String) -> void:
    if "--capture" not in OS.get_cmdline_user_args() or DisplayServer.get_name() == "headless": return
    await RenderingServer.frame_post_draw
    check(root.get_texture().get_image().save_png("res://docs/milestone_08_" + label + ".png") == OK,
        "Rendered " + label)

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    root.size = Vector2i(1280, 800)
    var scene: Node3D = load("res://scenes/world/AshenfallFirstAdventure.tscn").instantiate()
    root.add_child(scene); current_scene = scene
    var feedback: Node3D = scene.get_node("CombatFeedback")
    feedback.SoundCueRequested.connect(func(cue: StringName, _position: Vector3): cues.append(str(cue)))
    var actor: CharacterBody3D = scene.get_node("World/FutureActors/Player")
    var player_health: Node = actor.get_node("Health")
    var enemy: CharacterBody3D = scene.get_node("FallenGuardian")
    var enemy_health: Node = enemy.get_node("Health")
    var melee: Node = scene.get_node("PlayerMelee")
    var damage: Node = scene.get_node("MeleeDamage")
    var others := [scene.get_node("GuardianWest"), scene.get_node("GuardianEast")]
    for other in others: other.AttacksEnabled = false
    enemy.AttacksEnabled = false
    await ticks(75)
    var world_environment: WorldEnvironment = scene.get_node("World/WorldEnvironment")
    var atmosphere: Environment = world_environment.environment
    check(atmosphere != load("res://resources/AshenAtmosphere.tres") and atmosphere.fog_enabled and
        atmosphere.fog_density > 0.006,
        "Adventure has an isolated subtle atmosphere resource")
    check(feedback.FeedbackCount == 0 and feedback.ActiveNumberCount == 0,
        "No effects at full health or on startup")
    var animations := []
    for guardian in [enemy, others[0], others[1]]:
        var animation: Node = guardian.get_node("AnimationController")
        animations.append(animation)
        check(not guardian.has_node("Visual/MeleeWarning") and is_zero_approx(animation.AnticipationWeight),
            "Guardian starts without floating warning or residual anticipation")
    check(animations[0] != animations[1] and animations[1] != animations[2],
        "Each Guardian owns an independent attack presentation")
    if "--capture" in OS.get_cmdline_user_args():
        var no_fog: Environment = atmosphere.duplicate(true)
        no_fog.fog_enabled = false
        world_environment.environment = no_fog
        await process_frame
        await capture("review_atmosphere_disabled")
        world_environment.environment = atmosphere
        await process_frame
        await capture("review_atmosphere_enabled")

    check(melee.IssueAttack(enemy.get_node("MeleeTarget")), "Existing player approach command accepted")
    var struck := false
    for i in 700:
        await physics_frame
        if enemy_health.CurrentHealth == 75:
            struck = true
            break
    check(struck and damage.SuccessfulHits == 1 and feedback.FeedbackCount == 1,
        "Only a successfully applied player strike creates feedback")
    check(feedback.ActiveNumberCount == 1 and cues.has("GuardianImpact") and cues.has("AxeSwing"),
        "One enemy damage number and swing/impact audio hooks")
    await capture("guardian_hit")
    var camera: Camera3D = scene.get_node("World/CameraRig/Camera3D")
    camera.size = 8
    await capture("guardian_hit_close")
    camera.size = 22
    check(not damage.TryApplyImpact(enemy.get_node("MeleeTarget"), melee.ImpactSequence) and
        feedback.FeedbackCount == 1,
        "Duplicate player impact creates neither damage nor effects")
    await ticks(45)
    check(feedback.ActiveNumberCount == 0 and feedback.find_children("ImpactSparks", "Node3D", true, false).is_empty(),
        "Damage number and sparks are transient")

    # Use the validated encounter AI to enter a real attack preparation.
    melee.InterruptAttack()
    actor.CancelDestination()
    actor.InputEnabled = false
    actor.global_position = enemy.global_position + Vector3(0, 0, 1.25)
    actor.velocity = Vector3.ZERO
    enemy.AttacksEnabled = true
    var warned := false
    for i in 240:
        await physics_frame
        if enemy.State == 4:
            warned = true
            break
    await ticks(12)
    check(warned and animations[0].AnticipationWeight > 0.2 and
        is_zero_approx(animations[1].AnticipationWeight) and is_zero_approx(animations[2].AnticipationWeight),
        "Only the preparing Guardian adds its body-and-sword anticipation pose")
    await capture("polish_guardian_preparation")
    camera.size = 8
    await capture("polish_guardian_preparation_close")
    camera.size = 22
    enemy.AttacksEnabled = false
    await ticks(2)
    await process_frame
    check(is_zero_approx(animations[0].AnticipationWeight) and enemy.State != 4,
        "Cancelled windup clears the anticipation pose immediately")
    await capture("polish_guardian_cancelled")
    enemy.AttacksEnabled = true
    for i in 180:
        await physics_frame
        if enemy.State == 4: break
    await ticks(12)
    check(enemy.State == 4 and animations[0].AnticipationWeight > 0.2,
        "A later independent attack gets a fresh anticipation pose")
    var player_hit := false
    for i in 180:
        await physics_frame
        if player_health.CurrentHealth == 90:
            player_hit = true
            break
    check(player_hit and feedback.FeedbackCount == 2 and feedback.ActiveNumberCount == 1,
        "Successful Guardian damage creates one distinct player number")
    check(cues.has("GuardianSwing") and cues.has("PlayerImpact"),
        "Guardian swing and player impact cues emitted")
    check(is_zero_approx(animations[0].AnticipationWeight), "Anticipation ends after preparation")
    await capture("dwarf_hit")
    camera.size = 8
    await capture("dwarf_hit_close")
    camera.size = 22
    var old_count: int = feedback.FeedbackCount
    player_health.ApplyDamage(10)
    await physics_frame
    check(feedback.FeedbackCount == old_count,
        "Direct health changes do not masquerade as a confirmed combat impact")
    enemy_health.ApplyDamage(100)
    await ticks(50)
    check(enemy.State == 6 and is_zero_approx(animations[0].AnticipationWeight) and cues.has("GuardianDefeat"),
        "Guardian defeat clears anticipation and emits one audio hook")
    check(not enemy.get_node("AnimationController").is_physics_processing(),
        "Settled Guardian visual no longer recalculates every frame")
    await capture("guardian_defeat")
    camera.size = 8
    await capture("guardian_defeat_close")
    camera.size = 22
    # Keep the death sequence clear of the defeated Guardian's corpse.
    actor.global_position = Vector3(-3, 0, -5)
    actor.velocity = Vector3.ZERO
    actor.reset_physics_interpolation()
    await ticks(60)
    player_health.ApplyDamage(100)
    await physics_frame
    check(scene.get_node("Session").Outcome == 2 and not actor.InputEnabled and
        scene.get_node("AdventureHud").DefeatPresentationPending and
        not scene.get_node("AdventureHud/Overlay").visible,
        "Gameplay defeat is immediate while the covering UI waits")
    await ticks(7)
    var dwarf: Node3D = scene.get_node("DwarfPresentation").Presentation
    var model: Node3D = dwarf.get_node("Model")
    var axe: Node3D = model.find_child("BattleAxe", true, false)
    var reaction_pitch: float = dwarf.rotation.x
    check(reaction_pitch > 0.01 and grip_gap(model, axe) < 0.01,
        "Fatal reaction recoils and both hands remain on the axe")
    await capture("polish_dwarf_fatal_reaction")
    await ticks(24)
    check(dwarf.BodyOffset.y < -0.08 and dwarf.rotation.x < 0 and grip_gap(model, axe) < 0.01,
        "Knees buckle before the body falls and both grips remain coherent")
    await capture("polish_dwarf_knees_buckle")
    await ticks(14)
    check(not scene.get_node("AdventureHud/Overlay").visible,
        "Defeat UI leaves the falling body unobscured")
    check(grip_gap(model, axe) < 0.01 and dwarf.rotation.x < -0.2,
        "Mid-fall weapon remains gripped while the body tips forward")
    await capture("polish_dwarf_mid_fall")
    await ticks(55)
    check(dwarf.rotation.x < -1.4 and grip_gap(model, axe) < 0.01,
        "Dwarf reaches a grounded forward collapse with both hand grips attached")
    check(scene.get_node("DwarfPresentation").Defeated and cues.has("PlayerDefeat") and
        not scene.get_node("DwarfPresentation").is_physics_processing(),
        "Dwarf settles at defeat and stops pose updates")
    check(not scene.get_node("AdventureHud/Overlay").visible,
        "Complete fall and final pose are visible before defeat overlay")
    await capture("polish_dwarf_final_pose")
    camera.size = 8
    await capture("polish_dwarf_final_pose_close")
    camera.size = 22
    var settled_pose: Transform3D = dwarf.global_transform
    await ticks(6)
    check(dwarf.global_transform.is_equal_approx(settled_pose),
        "Final defeat pose remains stable after the authored clip")
    await ticks(45)
    check(scene.get_node("AdventureHud/Overlay").visible and
        not scene.get_node("AdventureHud").DefeatPresentationPending,
        "Defeat and restart UI appears after the configured presentation delay")
    await capture("dwarf_defeat_overlay")
    scene.get_node("EncounterReset").Restart()
    for i in 45:
        await process_frame
        if current_scene != null and current_scene != scene: break
    var fresh := current_scene
    check(fresh != scene and fresh.get_node("CombatFeedback").FeedbackCount == 0 and
        fresh.get_node("CombatFeedback").ActiveNumberCount == 0,
        "Restart releases every transient effect and resets feedback state")
    fresh.get_node("World/FutureActors/Player/Health").ApplyDamage(100)
    await physics_frame
    check(fresh.get_node("AdventureHud").DefeatPresentationPending,
        "A new defeat starts a fresh presentation timer")
    fresh.get_node("EncounterReset").Restart()
    for i in 45:
        await process_frame
        if current_scene != null and current_scene != fresh: break
    fresh = current_scene
    check(not fresh.get_node("AdventureHud/Overlay").visible and
        not fresh.get_node("AdventureHud").DefeatPresentationPending,
        "Restart during the delay leaves no stale outcome UI or timer")
    var fresh_actor: CharacterBody3D = fresh.get_node("World/FutureActors/Player")
    fresh_actor.InputEnabled = false
    fresh_actor.global_position = Vector3(0, 0, -9)
    fresh_actor.velocity = Vector3.ZERO
    for zone_name in ["RoadZone", "WestZone", "EastZone"]:
        fresh.get_node(zone_name).ActivationRadius = 8.0
    for guardian_name in ["FallenGuardian", "GuardianWest", "GuardianEast"]:
        fresh.get_node(guardian_name).AttacksEnabled = false
    await ticks(130)
    var all_active := true
    for zone_name in ["RoadZone", "WestZone", "EastZone"]:
        all_active = all_active and fresh.get_node(zone_name).Active
    check(all_active, "All three independent Guardians can be active together")
    await capture("three_active")
    print("Three active Guardians: reported FPS=", Engine.get_frames_per_second())
    print("Milestone 08 checks: ", failures, " failures; cues=", cues)
    quit(1 if failures else 0)
