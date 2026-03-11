class_name SaveThumbnailGenerator


## Generate a thumbnail ImageTexture from a save file path.
## Returns null on failure.
static func generate_from_path(path: String, content: ContentRegistry) -> ImageTexture:
	var meta: Dictionary = SaveService.read_metadata(path)
	if meta.is_empty():
		return null

	# We need to read full save data for tile colors
	var text: String = FileAccess.get_file_as_string(path)
	var data = JSON.parse_string(text)
	if not data is Dictionary:
		return null

	return _generate_from_data(data, content)


static func _generate_from_data(data: Dictionary, content: ContentRegistry) -> ImageTexture:
	var world_data: Dictionary = data.get("world", {})
	var world_width: int = int(world_data.get("width", World.DEFAULT_WIDTH))
	var world_height: int = int(world_data.get("height", World.DEFAULT_HEIGHT))

	var image := Image.create_empty(world_width, world_height, false, Image.FORMAT_RGB8)

	# Build palette from saved hex strings or fall back to content palette
	var palette: Array[Color] = []
	var palette_hexes: Array = data.get("palette", [])
	if not palette_hexes.is_empty():
		for hex in palette_hexes:
			palette.append(Color(hex))
	else:
		var palette_id: int = int(data.get("selected_palette_id", -1))
		if content.palettes.has(palette_id):
			palette = content.palettes[palette_id].get("colors", [])

	if palette.is_empty():
		image.fill(Color(0.3, 0.3, 0.3))
		return ImageTexture.create_from_image(image)

	# Render 1 pixel per tile (tiles stored column-major: (0,0), (0,1)...)
	var tiles: Array = world_data.get("tiles", [])
	for tile in tiles:
		if not tile is Dictionary:
			continue
		var x: int = int(tile.get("x", 0))
		var y: int = int(tile.get("y", 0))
		if x < 0 or x >= world_width or y < 0 or y >= world_height:
			continue

		var color_index: int
		if tile.has("overlay_terrain_type_id"):
			color_index = int(tile.get("overlay_color_index", 0))
		else:
			color_index = int(tile.get("color_index", 0))

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
			var pattern: bool = ((x + y) % 8 < 4)
			var color: Color = base_color.lightened(0.1) if not pattern else base_color
			image.set_pixel(x, y, color)

	return ImageTexture.create_from_image(image)
