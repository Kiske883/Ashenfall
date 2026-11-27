extends SceneTree

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    var preview = load("res://scenes/world/CharacterPersistencePreview.tscn").instantiate()
    root.add_child(preview)
    for i in 300:
        await process_frame
        if preview.get_node_or_null("AshenfallDwarfPlayableDemo") != null: break
        await create_timer(0.01).timeout
    if preview.get_node_or_null("AshenfallDwarfPlayableDemo") == null:
        push_error("Snapshot was not accepted")
        quit(1)
        return
    var adventure = preview.get_node_or_null("AshenfallDwarfPlayableDemo")
    if adventure == null:
        push_error("Encounter missing")
        quit(1)
        return
    var health = adventure.get_node("World/FutureActors/Player/Health")
    var damage = adventure.get_node("MeleeDamage")
    if health.MaximumHealth != 105 or health.CurrentHealth != 105 or damage.Damage != 27 or not damage.UseFixedDamageOnly:
        push_error("Persisted statistics not applied before readiness")
        quit(1)
        return
    health.ApplyDamage(10)
    if health.CurrentHealth != 95 or health.MaximumHealth != 105:
        push_error("Health damage changed maximum")
        quit(1)
        return
    print("Persisted health/damage initialization passed")
    quit(0)
