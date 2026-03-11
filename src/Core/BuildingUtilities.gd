class_name BuildingUtilities


# All tiles occupied by a building given its anchor position and tile size.
# A 2x2 building at (5,5) returns [(5,5),(6,5),(5,6),(6,6)].
static func get_occupied_tiles(anchor: Vector2i, tile_size: int) -> Array[Vector2i]:
	var tiles: Array[Vector2i] = []
	for dx in range(tile_size):
		for dy in range(tile_size):
			tiles.append(anchor + Vector2i(dx, dy))
	return tiles


# All tiles adjacent to the building's footprint (the "use areas").
# Returns offsets (dx, dy) relative to the anchor.
static func generate_use_areas_for_size(tile_size: int) -> Array[Vector2i]:
	var use_areas: Array[Vector2i] = []

	# Top edge (y = -1)
	for x in range(-1, tile_size + 1):
		use_areas.append(Vector2i(x, -1))

	# Bottom edge (y = tile_size)
	for x in range(-1, tile_size + 1):
		use_areas.append(Vector2i(x, tile_size))

	# Left edge (x = -1, y from 0 to tile_size-1)
	for y in range(tile_size):
		use_areas.append(Vector2i(-1, y))

	# Right edge (x = tile_size, y from 0 to tile_size-1)
	for y in range(tile_size):
		use_areas.append(Vector2i(tile_size, y))

	return use_areas
