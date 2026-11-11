extends SceneTree

# Read-only independent scene checks. No ResourceSaver, capture, or asset modification.
var failures := 0
func _initialize() -> void: call_deferred("run")
func check(condition: bool, message: String) -> void:
    if condition: print("PASS: ", message)
    else:
        failures += 1
        push_error("FAIL: " + message)
func run() -> void:
    var previous_materials: Array[Material] = []
    var definitions := [
        ["Ancient Needle Pine", "res://scenes/environment/CursedPineV1.tscn", 28642],
        ["Shadow Crown Pine", "res://scenes/environment/CursedPine.tscn", 24642]
    ]
    for definition in definitions:
        var scene: PackedScene = load(definition[1])
        check(scene != null, definition[0] + " scene resolves")
        if scene == null: continue
        var pine: Node3D = scene.instantiate()
        root.add_child(pine)
        # _Ready performs the existing normal V2 load; no recipe/seed changes or baking.
        await process_frame
        var triangles := 0
        var materials: Array[Material] = []
        var fingerprint := HashingContext.new()
        fingerprint.start(HashingContext.HASH_SHA256)
        for batch_name in ["ProceduralBark", "ProceduralNeedles"]:
            var batch: MeshInstance3D = pine.get_node_or_null(batch_name)
            check(batch != null and batch.mesh != null, definition[0] + " " + batch_name + " resolves")
            if batch == null or batch.mesh == null: continue
            check(batch.mesh.get_surface_count() == 1, definition[0] + " single opaque surface per batch")
            var arrays := batch.mesh.surface_get_arrays(0)
            triangles += arrays[Mesh.ARRAY_INDEX].size() / 3
            fingerprint.update(var_to_bytes(arrays))
            var material: Material = batch.material_override
            check(material != null, definition[0] + " material resolves")
            if material == null: continue
            materials.append(material)
            if material is ShaderMaterial:
                check(material.shader != null and not material.shader.code.is_empty(), definition[0] + " shader source resolves")
                fingerprint.update(material.shader.code.to_utf8_buffer())
            else:
                check(material is StandardMaterial3D, definition[0] + " native foliage material")
                fingerprint.update(var_to_bytes([material.vertex_color_use_as_albedo,
                    material.cull_mode, material.roughness, material.metallic_specular]))
            if definition[0] == "Ancient Needle Pine":
                check(batch.mesh.resource_path.begins_with("res://resources/art/CursedPineV1_"), "V1 uses preserved external mesh resource")
        check(triangles == definition[2], definition[0] + " approved triangle count preserved")
        check(materials.size() == 2 and materials[0] != materials[1], definition[0] + " two separate materials")
        for material in materials:
            check(not previous_materials.has(material), "species do not share mutable material resources")
        check(pine.find_children("*", "CollisionObject3D", true, false).is_empty()
            and pine.find_children("*", "CollisionShape3D", true, false).is_empty(), definition[0] + " collision absent as documented")
        check(pine.find_children("*", "NavigationRegion3D", true, false).is_empty()
            and pine.find_children("*", "NavigationObstacle3D", true, false).is_empty(), definition[0] + " no navigation integration")
        print("SPECIES_FINGERPRINT ", definition[0], " ", fingerprint.finish().hex_encode(),
            " triangles=", triangles, " surfaces=2 materials=", materials.size())
        previous_materials.append_array(materials)
        root.remove_child(pine)
        pine.free()
    print("Species checks: ", failures, " failures")
    quit(0 if failures == 0 else 1)
