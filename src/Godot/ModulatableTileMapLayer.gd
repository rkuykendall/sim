class_name ModulatableTileMapLayer
extends TileMapLayer

# Per-tile color overrides: Vector2i -> Color
var _tile_colors: Dictionary = {}


func set_tile_color(coords: Vector2i, color: Color) -> void:
	_tile_colors[coords] = color
	notify_runtime_tile_data_update()


func clear_tile_color(coords: Vector2i) -> void:
	if _tile_colors.erase(coords):
		notify_runtime_tile_data_update()


func _use_tile_data_runtime_update(coords: Vector2i) -> bool:
	return _tile_colors.has(coords)


func _tile_data_runtime_update(coords: Vector2i, tile_data: TileData) -> void:
	if _tile_colors.has(coords):
		tile_data.modulate = _tile_colors[coords]
