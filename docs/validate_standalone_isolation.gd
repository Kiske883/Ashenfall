extends SceneTree

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    var scene = load("res://scenes/world/AshenfallDwarfPlayableDemo.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    var health = scene.get_node("World/FutureActors/Player/Health")
    var damage = scene.get_node("MeleeDamage")
    var bar = scene.get_node("PlayerHealthHud/Panel/Bar")
    var label = scene.get_node("PlayerHealthHud/Panel/Value")
    if health.MaximumHealth != 100 or health.CurrentHealth != 100 or damage.Damage != 25 or bar.max_value != 100 or bar.value != 100 or label.text != "HP 100 / 100":
        push_error("Standalone initialization mismatch")
        quit(1)
        return
    print("Standalone initialized 100/100 HP and 25 damage without a catalog endpoint")
    quit(0)
