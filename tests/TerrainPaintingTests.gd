extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")


func run() -> void:
	print("  [TerrainPaintingTests]")
	run_test("PaintRectangle_PaintsAllTilesInclusive", test_paint_rectangle_inclusive)
	run_test("PaintRectangle_OrderOfCornersDoesNotMatter", test_paint_rectangle_corner_order)
	run_test("PaintRectangleOutline_OnlyPaintsBorderTiles", test_paint_rectangle_outline)
	run_test("FloodFill_StopsAtDifferentTerrain", test_flood_fill_bounded)


func test_paint_rectangle_inclusive() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 9)
	var path_id := builder.define_terrain("Path", true)
	var sim = builder.build()

	sim.paint_rectangle(Vector2i(1, 1), Vector2i(3, 3), path_id)

	for x in range(1, 4):
		for y in range(1, 4):
			assert_eq(
				sim.world.get_tile(Vector2i(x, y)).base_terrain_type_id,
				path_id,
				"Tile (%d,%d) should be painted" % [x, y]
			)

	# One step outside the rectangle on each side should be untouched.
	assert_not_eq(
		sim.world.get_tile(Vector2i(0, 1)).base_terrain_type_id,
		path_id,
		"Tile outside rectangle should not be painted"
	)
	assert_not_eq(
		sim.world.get_tile(Vector2i(4, 3)).base_terrain_type_id,
		path_id,
		"Tile outside rectangle should not be painted"
	)


func test_paint_rectangle_corner_order() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 9)
	var path_id := builder.define_terrain("Path", true)
	var sim = builder.build()

	# Bottom-right to top-left corner order should paint the same region as top-left to bottom-right.
	sim.paint_rectangle(Vector2i(3, 3), Vector2i(1, 1), path_id)

	for x in range(1, 4):
		for y in range(1, 4):
			assert_eq(
				sim.world.get_tile(Vector2i(x, y)).base_terrain_type_id,
				path_id,
				"Tile (%d,%d) should be painted regardless of corner order" % [x, y]
			)


func test_paint_rectangle_outline() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 9)
	var wall_id := builder.define_terrain("Wall", false)
	var sim = builder.build()

	sim.paint_rectangle_outline(Vector2i(1, 1), Vector2i(5, 5), wall_id)

	# Border tiles painted.
	for x in range(1, 6):
		assert_eq(
			sim.world.get_tile(Vector2i(x, 1)).base_terrain_type_id,
			wall_id,
			"Top border should be painted"
		)
		assert_eq(
			sim.world.get_tile(Vector2i(x, 5)).base_terrain_type_id,
			wall_id,
			"Bottom border should be painted"
		)
	for y in range(1, 6):
		assert_eq(
			sim.world.get_tile(Vector2i(1, y)).base_terrain_type_id,
			wall_id,
			"Left border should be painted"
		)
		assert_eq(
			sim.world.get_tile(Vector2i(5, y)).base_terrain_type_id,
			wall_id,
			"Right border should be painted"
		)

	# Interior tiles must remain untouched.
	for x in range(2, 5):
		for y in range(2, 5):
			assert_not_eq(
				sim.world.get_tile(Vector2i(x, y)).base_terrain_type_id,
				wall_id,
				"Interior tile (%d,%d) should not be painted by an outline" % [x, y]
			)


func test_flood_fill_bounded() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 9)
	builder.define_terrain("Floor", true, "flat")  # seeds the whole world as one connected region
	var wall_id := builder.define_terrain("Wall", false)
	var path_id := builder.define_terrain("Path", true)
	var sim = builder.build()

	# Enclose a 3x3 interior (2,2)-(4,4) with a wall ring at x/y = 1 and 5.
	sim.paint_rectangle_outline(Vector2i(1, 1), Vector2i(5, 5), wall_id)

	sim.flood_fill(Vector2i(3, 3), path_id, 0)

	for x in range(2, 5):
		for y in range(2, 5):
			assert_eq(
				sim.world.get_tile(Vector2i(x, y)).base_terrain_type_id,
				path_id,
				"Interior tile (%d,%d) should be flood-filled" % [x, y]
			)

	# Outside the wall ring should be untouched by the flood fill.
	assert_not_eq(
		sim.world.get_tile(Vector2i(0, 0)).base_terrain_type_id,
		path_id,
		"Flood fill should not cross the wall boundary"
	)
	assert_not_eq(
		sim.world.get_tile(Vector2i(7, 7)).base_terrain_type_id,
		path_id,
		"Flood fill should not cross the wall boundary"
	)
