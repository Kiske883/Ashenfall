extends SceneTree

# One-time native snapshot. Refuses to overwrite an existing baseline scene.
func _initialize() -> void: call_deferred("run")
func run() -> void:
    for path in ["res://scenes/environment/CursedPineV1.tscn",
            "res://resources/art/CursedPineV1_ProceduralBark.res",
            "res://resources/art/CursedPineV1_ProceduralNeedles.res"]:
        if FileAccess.file_exists(path):
            push_error("V1 snapshot already exists; preserve it")
            quit(1)
            return
    var pine = load("res://scenes/environment/CursedPine.tscn").instantiate()
    root.add_child(pine)
    if int(pine.get_meta("triangle_count", 0)) != 28642:
        push_error("Current generator is not the V1 baseline; refusing to freeze it as V1")
        quit(1)
        return
    for batch_name in ["ProceduralBark", "ProceduralNeedles"]:
        var batch: MeshInstance3D = pine.get_node(batch_name)
        batch.owner = pine
        var path := "res://resources/art/CursedPineV1_%s.res" % batch_name
        var result := ResourceSaver.save(batch.mesh, path, ResourceSaver.FLAG_CHANGE_PATH)
        if result != OK:
            push_error("Unable to preserve V1 mesh")
            quit(1)
            return
        batch.mesh = ResourceLoader.load(path, "ArrayMesh", ResourceLoader.CACHE_MODE_IGNORE)
        if batch_name == "ProceduralBark":
            var material: ShaderMaterial = batch.material_override.duplicate()
            material.shader = load("res://resources/art/CursedBarkV1.gdshader")
            batch.material_override = material
    pine.set_script(null)
    var packed := PackedScene.new()
    if packed.pack(pine) != OK or ResourceSaver.save(packed, "res://scenes/environment/CursedPineV1.tscn") != OK:
        push_error("Unable to preserve V1 scene")
        quit(1)
        return
    print("Preserved V1 as two native mesh resources and an independent scene")
    quit()
