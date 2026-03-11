class_name AutoTileSetBuilder


static func create_auto_tile_set(
	texture: Texture2D,
	terrain_name: String,
	blocks_light: bool = false
) -> TileSet:
	var tile_set := TileSet.new()
	var atlas_source := TileSetAtlasSource.new()
	atlas_source.texture = texture
	atlas_source.texture_region_size = Vector2i(16, 16)

	# Add occlusion layer before creating tiles (required for SDF shadows)
	if blocks_light:
		tile_set.add_occlusion_layer()
		tile_set.set_occlusion_layer_sdf_collision(0, true)

	# Create terrain set and terrain
	tile_set.add_terrain_set()
	tile_set.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
	tile_set.add_terrain(0)
	tile_set.set_terrain_name(0, 0, terrain_name)
	tile_set.set_terrain_color(0, 0, Color.WHITE)

	# Add source BEFORE configuring tiles
	tile_set.add_source(atlas_source, 0)

	# Configure all 47 tile patterns
	for pattern in AutoTileConfig.STANDARD_47_TILE_PATTERNS:
		_configure_tile(tile_set, atlas_source, pattern[0], pattern[1], 0, 0, pattern[2], blocks_light)

	return tile_set


static func _configure_tile(
	tile_set: TileSet,
	atlas_source: TileSetAtlasSource,
	atlas_x: int,
	atlas_y: int,
	terrain_set: int,
	terrain: int,
	peering_bits: int,
	blocks_light: bool
) -> void:
	var atlas_coords := Vector2i(atlas_x, atlas_y)
	atlas_source.create_tile(atlas_coords)

	var tile_data: TileData = atlas_source.get_tile_data(atlas_coords, 0)
	tile_data.terrain_set = terrain_set
	tile_data.terrain = terrain

	# Peering bits: bit0=TL, bit1=T, bit2=TR, bit3=L, bit4=R, bit5=BL, bit6=B, bit7=BR
	if peering_bits & 0b00000001:
		tile_data.set_terrain_peering_bit(TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, terrain)
	if peering_bits & 0b00000010:
		tile_data.set_terrain_peering_bit(TileSet.CELL_NEIGHBOR_TOP_SIDE, terrain)
	if peering_bits & 0b00000100:
		tile_data.set_terrain_peering_bit(TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER, terrain)
	if peering_bits & 0b00001000:
		tile_data.set_terrain_peering_bit(TileSet.CELL_NEIGHBOR_LEFT_SIDE, terrain)
	if peering_bits & 0b00010000:
		tile_data.set_terrain_peering_bit(TileSet.CELL_NEIGHBOR_RIGHT_SIDE, terrain)
	if peering_bits & 0b00100000:
		tile_data.set_terrain_peering_bit(TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, terrain)
	if peering_bits & 0b01000000:
		tile_data.set_terrain_peering_bit(TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, terrain)
	if peering_bits & 0b10000000:
		tile_data.set_terrain_peering_bit(TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, terrain)

	if blocks_light:
		var occluder := OccluderPolygon2D.new()
		occluder.polygon = PackedVector2Array([
			Vector2(-8, -8),
			Vector2(8, -8),
			Vector2(8, 8),
			Vector2(-8, 8),
		])
		tile_data.set_occluder_polygons_count(0, 1)
		tile_data.set_occluder_polygon(0, 0, occluder)
