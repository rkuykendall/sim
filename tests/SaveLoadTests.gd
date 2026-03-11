extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")
const _SaveService = preload("res://src/Core/SaveService.gd")


func run() -> void:
	print("  [SaveLoadTests]")
	run_test("SaveData_CanSerializeEmptySimulation", test_serialize_empty)
	run_test("SaveData_CanSerializeImpassableTiles", test_serialize_impassable)
	run_test("SaveData_CanSerializePawns", test_serialize_pawns)
	run_test("SaveData_CanSerializeBuildings", test_serialize_buildings)
	run_test("FromSaveData_RestoresPawnState", test_restore_pawn_state)
	run_test("FromSaveData_RestoresWorldTiles", test_restore_world_tiles)
	run_test("FromSaveData_RestoresBuildings", test_restore_buildings)
	run_test("FromSaveData_RestoresTime", test_restore_time)
	run_test("FromSaveData_RestoresEntityIds", test_restore_entity_ids)
	run_test("RoundTrip_ComplexScenario", test_roundtrip_complex)
	run_test("RoundTrip_WithResources", test_roundtrip_resources)
	run_test("RoundTrip_RestoredSimulationCanContinue", test_roundtrip_can_continue)


func test_serialize_empty() -> void:
	var builder = _Builder.new()
	builder.define_terrain("flat", true, "flat", false, true)
	var sim = builder.build()

	var data = _SaveService.to_dict(sim, "test-save")
	var json_str: String = JSON.stringify(data)
	var restored = JSON.parse_string(json_str)
	assert_true(restored is Dictionary, "Should deserialize to Dictionary")
	assert_eq(restored.get("name", ""), "test-save", "Name should be preserved")
	assert_eq(int(restored.get("seed", -1)), sim.sim_seed, "Seed should be preserved")
	assert_eq(int(restored.get("current_tick", -1)), sim.time.tick, "Tick should be preserved")


func test_serialize_impassable() -> void:
	var builder = _Builder.new()
	builder.define_terrain("flat", true, "flat", false, true)
	var wall_id := builder.define_terrain("wall", false)
	var sim = builder.build()

	sim.paint_terrain(Vector2i(2, 2), wall_id)

	var data = _SaveService.to_dict(sim, "test-save")
	var json_str: String = JSON.stringify(data)
	# Should not throw / should produce valid JSON
	assert_true(json_str.length() > 0, "JSON should not be empty")
	var restored = JSON.parse_string(json_str)
	assert_true(restored is Dictionary, "Should parse back to Dictionary")

	# Find the wall tile in restored world
	var world_data: Dictionary = restored.get("world", {})
	var tiles: Array = world_data.get("tiles", [])
	var found_wall := false
	for tile in tiles:
		if tile is Dictionary and tile.get("x", -1) == 2 and tile.get("y", -1) == 2:
			var cost: float = float(tile.get("walkability_cost", 1.0))
			# World.IMPASSABLE = 1e9; any value >= 1e9 is a wall
			assert_gt(cost, 100.0, "Wall tile walkability_cost should be impassable (>= 1e9)")
			found_wall = true
			break
	assert_true(found_wall, "Should find wall tile at (2,2) in saved data")


func test_serialize_pawns() -> void:
	var builder = _Builder.new()
	builder.define_terrain("flat", true, "flat", false, true)
	var hunger_id := builder.define_need("hunger", 0.02)
	builder.add_pawn("Alice", 1, 1, {hunger_id: 75.0})
	var sim = builder.build()

	var data = _SaveService.to_dict(sim, "test-save")
	var json_str: String = JSON.stringify(data)
	var restored = JSON.parse_string(json_str)
	assert_true(restored is Dictionary, "Should parse")

	var entities: Array = restored.get("entities", [])
	var pawn_data: Dictionary = {}
	for e in entities:
		if e is Dictionary and e.get("type", "") == "Pawn":
			pawn_data = e
			break
	assert_eq(pawn_data.get("name", ""), "Alice", "Pawn name should be Alice")
	assert_eq(int(pawn_data.get("x", -1)), 1, "Pawn x should be 1")
	assert_eq(int(pawn_data.get("y", -1)), 1, "Pawn y should be 1")
	var needs: Dictionary = pawn_data.get("needs", {})
	assert_approx(float(needs.get(str(hunger_id), 0.0)), 75.0, 0.1, "Hunger should be ~75")


func test_serialize_buildings() -> void:
	var builder = _Builder.new()
	builder.define_terrain("flat", true, "flat", false, true)
	var bed_id := builder.define_building("bed")
	builder.add_building(bed_id, 2, 2)
	var sim = builder.build()

	var data = _SaveService.to_dict(sim, "test-save")
	var json_str: String = JSON.stringify(data)
	var restored = JSON.parse_string(json_str)
	assert_true(restored is Dictionary, "Should parse")

	var entities: Array = restored.get("entities", [])
	var building_data: Dictionary = {}
	for e in entities:
		if e is Dictionary and e.get("type", "") == "Building":
			building_data = e
			break
	assert_eq(int(building_data.get("building_def_id", -1)), bed_id, "building_def_id should match")
	assert_eq(int(building_data.get("x", -1)), 2, "Building x should be 2")
	assert_eq(int(building_data.get("y", -1)), 2, "Building y should be 2")


func test_restore_pawn_state() -> void:
	var builder = _Builder.new()
	builder.define_terrain("flat", true, "flat", false, true)
	var hunger_id := builder.define_need("hunger", 0.02)
	builder.add_pawn("Bob", 2, 3, {hunger_id: 60.0})
	var original = builder.build()

	_run_ticks(original, 10)

	var data = _SaveService.to_dict(original, "test-save")
	var restored = _SaveService.from_dict(data, original.content)

	# Find Bob in restored simulation
	var restored_pawn_id := -1
	for pid in restored.entities.all_pawns():
		var pc = restored.entities.pawns.get(pid)
		if pc != null and pc.name == "Bob":
			restored_pawn_id = pid
			break
	assert_not_eq(restored_pawn_id, -1, "Bob should be restored")

	# Check position
	var orig_pawn_id := _get_pawn_by_name(original, "Bob")
	var orig_pos = original.entities.positions.get(orig_pawn_id)
	var rest_pos = restored.entities.positions.get(restored_pawn_id)
	if orig_pos != null and rest_pos != null:
		assert_eq(rest_pos.coord, orig_pos.coord, "Position should be restored")

	# Check needs (approximately)
	var orig_need_comp = original.entities.needs.get(orig_pawn_id)
	var rest_need_comp = restored.entities.needs.get(restored_pawn_id)
	if orig_need_comp != null and rest_need_comp != null:
		var orig_hunger: float = orig_need_comp.needs.get(hunger_id, 0.0)
		var rest_hunger: float = rest_need_comp.needs.get(hunger_id, 0.0)
		assert_approx(rest_hunger, orig_hunger, 0.1, "Hunger need should be restored accurately")


func test_restore_world_tiles() -> void:
	var builder = _Builder.new()
	builder.define_terrain("flat", true, "flat", false, true)
	var wall_id := builder.define_terrain("wall", false)
	var original = builder.build()

	original.paint_terrain(Vector2i(1, 1), wall_id)
	original.paint_terrain(Vector2i(2, 2), wall_id)

	var data = _SaveService.to_dict(original, "test-save")
	var restored = _SaveService.from_dict(data, original.content)

	assert_false(restored.world.is_walkable(Vector2i(1, 1)), "(1,1) should remain a wall after restore")
	assert_false(restored.world.is_walkable(Vector2i(2, 2)), "(2,2) should remain a wall after restore")
	assert_true(restored.world.is_walkable(Vector2i(0, 0)), "(0,0) should be walkable")


func test_restore_buildings() -> void:
	var builder = _Builder.new()
	builder.define_terrain("flat", true, "flat", false, true)
	var bed_id := builder.define_building("bed")
	builder.add_building(bed_id, 1, 1)
	var original = builder.build()

	var data = _SaveService.to_dict(original, "test-save")
	var restored = _SaveService.from_dict(data, original.content)

	var buildings = restored.entities.all_buildings()
	assert_eq(buildings.size(), 1, "Should have exactly one building")

	var building_id = buildings[0]
	var bc = restored.entities.buildings.get(building_id)
	assert_not_null(bc, "Building component should exist")
	assert_eq(bc.building_def_id, bed_id, "Building def ID should be restored")

	var pos = restored.entities.positions.get(building_id)
	assert_not_null(pos, "Position component should exist")
	assert_eq(pos.coord, Vector2i(1, 1), "Building position should be restored")


func test_restore_time() -> void:
	var builder = _Builder.new()
	builder.define_terrain("flat", true, "flat", false, true)
	var original = builder.build()

	_run_ticks(original, 100)
	var orig_tick = original.time.tick

	var data = _SaveService.to_dict(original, "test-save")
	var restored = _SaveService.from_dict(data, original.content)

	assert_eq(restored.time.tick, orig_tick, "Time tick should be restored")


func test_restore_entity_ids() -> void:
	var builder = _Builder.new()
	builder.define_terrain("flat", true, "flat", false, true)
	builder.add_pawn("Pawn1", 0, 0)
	builder.add_pawn("Pawn2", 1, 0)
	var original = builder.build()

	var orig_ids = original.entities.all_pawns().duplicate()
	orig_ids.sort()

	var data = _SaveService.to_dict(original, "test-save")
	var restored = _SaveService.from_dict(data, original.content)

	var rest_ids = restored.entities.all_pawns().duplicate()
	rest_ids.sort()

	assert_eq(rest_ids, orig_ids, "Entity IDs should be preserved across save/load")


func test_roundtrip_complex() -> void:
	var builder = _Builder.new()
	builder.with_world_bounds(9, 9)
	builder.define_terrain("flat", true, "flat", false, true)
	var wall_id := builder.define_terrain("wall", false)
	var hunger_id := builder.define_need("hunger", 0.02, 30.0, 35.0, -20.0)
	var energy_id := builder.define_need("energy", 0.01)
	var farm_id := builder.define_building(
		"farm", -1, 0.0, 100, 0.0, 0, [], 1, 10, 1.0, true, "food", 100.0
	)
	var bed_id := builder.define_building("bed", energy_id)
	builder.add_building(farm_id, 2, 2)
	builder.add_building(bed_id, 5, 5)
	builder.add_pawn("Worker", 0, 0, {hunger_id: 80.0, energy_id: 90.0})
	var original = builder.build()

	original.paint_terrain(Vector2i(4, 0), wall_id)
	original.paint_terrain(Vector2i(4, 1), wall_id)
	original.paint_terrain(Vector2i(4, 2), wall_id)

	_run_ticks(original, 50)

	var data = _SaveService.to_dict(original, "test-save")
	var restored = _SaveService.from_dict(data, original.content)

	assert_eq(restored.time.tick, original.time.tick, "Tick should match")
	assert_eq(restored.entities.all_pawns().size(), original.entities.all_pawns().size(), "Pawn count should match")
	assert_eq(restored.entities.all_buildings().size(), original.entities.all_buildings().size(), "Building count should match")
	assert_false(restored.world.is_walkable(Vector2i(4, 0)), "Wall at (4,0) should be preserved")
	assert_false(restored.world.is_walkable(Vector2i(4, 1)), "Wall at (4,1) should be preserved")


func test_roundtrip_resources() -> void:
	var builder = _Builder.new()
	builder.define_terrain("flat", true, "flat", false, true)
	var farm_id := builder.define_building(
		"farm", -1, 0.0, 100, 0.0, 0, [], 1, 10, 1.0, true, "food", 100.0
	)
	builder.add_building(farm_id, 2, 2)
	var original = builder.build()

	var data = _SaveService.to_dict(original, "test-save")
	var restored = _SaveService.from_dict(data, original.content)

	var building_id = restored.entities.all_buildings()[0]
	var rc = restored.entities.resources.get(building_id)
	assert_not_null(rc, "Resource component should be restored")
	assert_eq(rc.resource_type, "food", "Resource type should be 'food'")
	assert_approx(rc.max_amount, 100.0, 0.1, "Max resource amount should be 100")


func test_roundtrip_can_continue() -> void:
	var builder = _Builder.new()
	builder.define_terrain("flat", true, "flat", false, true)
	var hunger_id := builder.define_need("hunger", 0.02)
	builder.add_pawn("TestPawn", 2, 2, {hunger_id: 100.0})
	var original = builder.build()

	_run_ticks(original, 50)

	var data = _SaveService.to_dict(original, "test-save")
	var restored = _SaveService.from_dict(data, original.content)

	var tick_before = restored.time.tick
	_run_ticks(restored, 100)

	assert_eq(restored.time.tick, tick_before + 100, "Restored sim should advance 100 more ticks")

	var pawn_id = restored.entities.all_pawns()[0]
	var need_comp = restored.entities.needs.get(pawn_id)
	if need_comp != null:
		var hunger: float = need_comp.needs.get(hunger_id, 100.0)
		assert_lt(hunger, 100.0, "Hunger should have decayed after running ticks")


# -------------------------------------------------------------------------
# Helpers
# -------------------------------------------------------------------------

func _get_pawn_by_name(sim, name: String) -> int:
	for pawn_id in sim.entities.all_pawns():
		var pc = sim.entities.pawns.get(pawn_id)
		if pc != null and pc.name == name:
			return pawn_id
	return -1


func _run_ticks(sim, count: int) -> void:
	for _i in count:
		sim.tick()
