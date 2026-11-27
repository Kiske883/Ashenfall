extends SceneTree

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    var scene: Node = load("res://scenes/world/CharacterPersistencePreview.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    var found := false
    for node in scene.get_children():
        if node is CanvasLayer:
            for child in node.get_children():
                if child is Label and child.text.begins_with("Character unavailable:") and child.mouse_filter == Control.MOUSE_FILTER_IGNORE:
                    found = true
    print("Character preview missing-config check: ", "PASS" if found else "FAIL")
    quit(0 if found else 1)
