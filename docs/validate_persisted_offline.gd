extends SceneTree

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    var preview = load("res://scenes/world/CharacterPersistencePreview.tscn").instantiate()
    root.add_child(preview)
    await create_timer(1.5).timeout
    if preview.get_node_or_null("AshenfallDwarfPlayableDemo") != null:
        push_error("Encounter started without a snapshot")
        quit(1)
        return
    print("Offline persistence encounter remains blocked")
    quit(0)
