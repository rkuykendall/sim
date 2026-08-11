class_name SaveThumbnailGenerator


## Generate a thumbnail ImageTexture from a save file path.
## Returns null on failure.
static func generate_from_path(path: String, content: ContentRegistry) -> ImageTexture:
	var meta: Dictionary = SaveService.read_metadata(path)
	if meta.is_empty():
		return null

	# We need to read full save data for tile colors
	var text: String = FileAccess.get_file_as_string(path)
	var parsed: Variant = JSON.parse_string(text)
	if not parsed is Dictionary:
		return null
	var data: Dictionary = parsed

	return _generate_from_data(data, content)


static func _generate_from_data(data: Dictionary, content: ContentRegistry) -> ImageTexture:
	var world_data: Dictionary = DefUtils.get_dict(data, "world", {})
	var world_width: int = DefUtils.get_int(world_data, "width", World.DEFAULT_WIDTH)
	var world_height: int = DefUtils.get_int(world_data, "height", World.DEFAULT_HEIGHT)

	var image := Image.create_empty(world_width, world_height, false, Image.FORMAT_RGB8)

	# Build palette from saved hex strings or fall back to content palette
	var palette: Array[Color] = []
	var palette_hexes: Array = DefUtils.get_array(data, "palette", [])
	if not palette_hexes.is_empty():
		for hex: String in palette_hexes:
			palette.append(Color(hex))
	else:
		var palette_id: int = DefUtils.get_int(data, "selected_palette_id", -1)
		if content.palettes.has(palette_id):
			palette.assign(DefUtils.get_array(content.palettes[palette_id], "colors", []))

	if palette.is_empty():
		image.fill(Color(0.3, 0.3, 0.3))
		return ImageTexture.create_from_image(image)

	# Render 1 pixel per tile (tiles stored column-major: (0,0), (0,1)...)
	var tiles: Array = DefUtils.get_array(world_data, "tiles", [])
	for tile: Variant in tiles:
		if not tile is Dictionary:
			continue
		var td: Dictionary = tile
		var x: int = DefUtils.get_int(td, "x", 0)
		var y: int = DefUtils.get_int(td, "y", 0)
		if x < 0 or x >= world_width or y < 0 or y >= world_height:
			continue

		var color_index: int
		if td.has("overlay_terrain_type_id"):
			color_index = DefUtils.get_int(td, "overlay_color_index", 0)
		else:
			color_index = DefUtils.get_int(td, "color_index", 0)

		if color_index >= 0 and color_index < palette.size():
			image.set_pixel(x, y, palette[color_index])
		else:
			image.set_pixel(x, y, Color(0.2, 0.2, 0.2))

	return ImageTexture.create_from_image(image)


static func generate_new_game_placeholder() -> ImageTexture:
	var width: int = World.DEFAULT_WIDTH
	var height: int = World.DEFAULT_HEIGHT
	var image := Image.create_empty(width, height, false, Image.FORMAT_RGB8)
	var base_color := Color(0.15, 0.4, 0.15)

	for y in height:
		for x in width:
			var pattern: bool = (x + y) % 8 < 4
			var color: Color = base_color.lightened(0.1) if not pattern else base_color
			image.set_pixel(x, y, color)

	return ImageTexture.create_from_image(image)
