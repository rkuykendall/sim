class_name World

const DEFAULT_WIDTH: int = 150
const DEFAULT_HEIGHT: int = 100

# Matches the sentinel used in terrains.json for impassable terrain.
const IMPASSABLE: float = 1e9


class Tile:
	var color_index: int = 0
	var base_terrain_type_id: int = -1
	var base_variant_index: int = 0
	var overlay_terrain_type_id: int = -1  # -1 = no overlay
	var overlay_color_index: int = 0
	var overlay_variant_index: int = 0
	var walkability_cost: float = 1.0
	var blocks_light: bool = false
	var building_blocks_movement: bool = false

	var walkable: bool:
		get: return walkability_cost < IMPASSABLE and not building_blocks_movement

	var tile_hash: int:
		get:
			return hash(
				str(base_terrain_type_id)
				+ str(color_index)
				+ str(overlay_terrain_type_id)
				+ str(overlay_color_index)
			)


var width: int
var height: int

# Flat array indexed as x + y * width
var _tiles: Array[Tile]


func _init(w: int = DEFAULT_WIDTH, h: int = DEFAULT_HEIGHT) -> void:
	width = w
	height = h
	_tiles.resize(w * h)
	for i in w * h:
		_tiles[i] = Tile.new()


func is_in_bounds(coord: Vector2i) -> bool:
	return coord.x >= 0 and coord.x < width and coord.y >= 0 and coord.y < height


func is_walkable(coord: Vector2i) -> bool:
	return is_in_bounds(coord) and _tiles[coord.x + coord.y * width].walkable


func get_tile(coord: Vector2i) -> Tile:
	return _tiles[coord.x + coord.y * width]


func get_tile_xy(x: int, y: int) -> Tile:
	return _tiles[x + y * width]
