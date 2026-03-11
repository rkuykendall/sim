extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")


func run() -> void:
	print("  [PathfindingWallTests]")
	run_test("PawnCannotReachBedBehindWall", test_pawn_blocked_by_wall)
	run_test("PawnCanReachBedWithOpenUseArea", test_pawn_can_reach_with_gap)
	run_test("PawnCannotMoveOntoWallTile", test_pawn_never_on_wall)
	run_test("PawnCanReachBedDiagonalOpen", test_pawn_reach_diagonal)
	run_test("PawnCannotReachBedWithDiagonalWall", test_pawn_blocked_diagonal)


# 5x5 world, pawn at (1,2), building at (3,2), vertical wall at x=2 — pawn cannot reach.
func test_pawn_blocked_by_wall() -> void:
	var builder := _Builder.new()
	var restfulness_id := builder.define_need("Restfulness", 1.0, 15.0, 35.0, 0.0, -5.0)
	var bed_id := builder.define_building("Home", restfulness_id)
	builder.define_terrain("Floor", true)
	var wall_id := builder.define_terrain("Wall", false)
	builder.add_pawn("Alice", 1, 2, {restfulness_id: 0.0})
	builder.add_building(bed_id, 3, 2)
	var sim = builder.build()

	# Paint vertical wall at x=2, all y
	for y in 5:
		sim.paint_terrain(Vector2i(2, y), wall_id)

	var pawn_id := _get_pawn_by_name(sim, "Alice")
	assert_not_null(pawn_id)
	assert_not_eq(pawn_id, -1, "Pawn Alice should exist")

	_run_ticks(sim, 100)

	# Pawn should not have satisfied restfulness (still at 0 or very low, minus decay)
	var restfulness := _get_need_value(sim, pawn_id, restfulness_id)
	# Need started at 0 and has been decaying — should still be 0 (clamped)
	assert_approx(restfulness, 0.0, 5.0, "Pawn should not have satisfied restfulness behind wall")


# Partial wall at x=2 with gap at y=2 — pawn can reach building through gap.
func test_pawn_can_reach_with_gap() -> void:
	var builder := _Builder.new()
	var tiredness_id := builder.define_need("Tiredness", 0.5, 15.0, 35.0, 0.0, -5.0)
	var bed_id := builder.define_building("Home", tiredness_id, 50.0)
	builder.define_terrain("Floor", true)
	var wall_id := builder.define_terrain("Wall", false)
	builder.add_pawn("Bob", 1, 2, {tiredness_id: 100.0})
	builder.add_building(bed_id, 3, 2)
	var sim = builder.build()

	# Block (2,1) and (2,3) but leave (2,2) open
	sim.paint_terrain(Vector2i(2, 1), wall_id)
	sim.paint_terrain(Vector2i(2, 3), wall_id)

	var pawn_id := _get_pawn_by_name(sim, "Bob")
	assert_not_null(pawn_id)
	assert_not_eq(pawn_id, -1, "Pawn Bob should exist")

	_run_ticks(sim, 100)

	var tiredness := _get_need_value(sim, pawn_id, tiredness_id)
	assert_lt(tiredness, 100.0, "Pawn should have reached and used the bed (tiredness < 100)")


# 3x3 world, wall at (1,1), pawn should never move onto wall tile.
func test_pawn_never_on_wall() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(2, 2)
	var tiredness_id := builder.define_need("Tiredness", 0.5, 15.0, 35.0, 0.0, -5.0)
	var bed_id := builder.define_building("Home", tiredness_id, 50.0)
	builder.define_terrain("Floor", true)
	var wall_id := builder.define_terrain("Wall", false)
	builder.add_pawn("Carol", 0, 1, {tiredness_id: 100.0})
	builder.add_building(bed_id, 2, 1)
	var sim = builder.build()

	sim.paint_terrain(Vector2i(1, 1), wall_id)

	var pawn_id := _get_pawn_by_name(sim, "Carol")
	assert_not_null(pawn_id)
	assert_not_eq(pawn_id, -1, "Pawn Carol should exist")

	_run_ticks(sim, 100)

	var pos_comp = sim.entities.positions.get(pawn_id)
	if pos_comp != null:
		assert_not_eq(pos_comp.coord, Vector2i(1, 1), "Pawn should never be on wall tile")


# 3x3 world, no walls — pawn can reach building diagonally.
func test_pawn_reach_diagonal() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(2, 2)
	var tiredness_id := builder.define_need("Tiredness", 0.5, 15.0, 35.0, 0.0, -5.0)
	var bed_id := builder.define_building("Home", tiredness_id, 50.0)
	builder.define_terrain("Floor", true)
	builder.add_pawn("Dave", 0, 0, {tiredness_id: 100.0})
	builder.add_building(bed_id, 2, 2)
	var sim = builder.build()

	var pawn_id := _get_pawn_by_name(sim, "Dave")
	assert_not_null(pawn_id)
	assert_not_eq(pawn_id, -1, "Pawn Dave should exist")

	_run_ticks(sim, 100)

	var tiredness := _get_need_value(sim, pawn_id, tiredness_id)
	assert_lt(tiredness, 100.0, "Pawn should have reached and used the bed")


# Diagonal wall blocks pawn from reaching building.
func test_pawn_blocked_diagonal() -> void:
	var builder := _Builder.new()
	var tiredness_id := builder.define_need("Tiredness", 1.0, 15.0, 35.0, 0.0, -5.0)
	var bed_id := builder.define_building("Home", tiredness_id, 50.0)
	builder.define_terrain("Floor", true)
	var wall_id := builder.define_terrain("Wall", false)
	builder.add_pawn("Eve", 0, 0, {tiredness_id: 0.0})
	builder.add_building(bed_id, 4, 4)
	var sim = builder.build()

	# Diagonal wall: paint (4,0),(3,1),(2,2),(1,3),(0,4) — anti-diagonal
	for x in 5:
		sim.paint_terrain(Vector2i(4 - x, x), wall_id)

	var pawn_id := _get_pawn_by_name(sim, "Eve")
	assert_not_null(pawn_id)
	assert_not_eq(pawn_id, -1, "Pawn Eve should exist")

	_run_ticks(sim, 100)

	# Need started at 0 and decays — should remain at 0 (cannot increase without building use)
	var tiredness := _get_need_value(sim, pawn_id, tiredness_id)
	assert_approx(tiredness, 0.0, 5.0, "Pawn should not have reached building through diagonal wall")


# -------------------------------------------------------------------------
# Helpers
# -------------------------------------------------------------------------

func _get_pawn_by_name(sim, name: String) -> int:
	for pawn_id in sim.entities.all_pawns():
		var pc = sim.entities.pawns.get(pawn_id)
		if pc != null and pc.name == name:
			return pawn_id
	return -1


func _get_need_value(sim, pawn_id: int, need_id: int) -> float:
	var need_comp = sim.entities.needs.get(pawn_id)
	if need_comp == null:
		return 0.0
	return need_comp.needs.get(need_id, 0.0)


func _run_ticks(sim, count: int) -> void:
	for _i in count:
		sim.tick()
