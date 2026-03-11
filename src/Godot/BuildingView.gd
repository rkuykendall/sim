class_name BuildingView
extends Node2D

const SOURCE_SIZE: int = RenderingConstants.SOURCE_TILE_SIZE

var _sprite: Sprite2D
var _sprite_variants: int = 1
var _sprite_phases: int = 1
var _tile_size: int = 1
var _entity_id: int = 0
var _current_phase: int = -1


func _ready() -> void:
	_sprite = Sprite2D.new()
	_sprite.name = "Sprite"
	_sprite.centered = false
	add_child(_sprite)


func initialize_with_sprite(
	texture: Texture2D,
	tile_size: int,
	sprite_variants: int,
	sprite_phases: int,
	entity_id: int
) -> void:
	_tile_size = tile_size
	_sprite_variants = sprite_variants
	_sprite_phases = sprite_phases
	_entity_id = entity_id
	_sprite.texture = texture
	_sprite.centered = true
	_sprite.scale = Vector2(RenderingConstants.SPRITE_SCALE, RenderingConstants.SPRITE_SCALE)

	# Match C# BuildingView: center horizontally over footprint, align bottom with footprint bottom.
	# Node position is the anchor tile center. With centered=true and scale=2, the rendered
	# half-height equals frame_height (source pixels), so bottom = position.y + frame_height.
	var frame_height: int = texture.get_height() / sprite_variants
	var footprint_center_x: float = (tile_size - 1) * RenderingConstants.RENDERED_TILE_SIZE / 2.0
	var footprint_bottom: float = (tile_size - 0.5) * RenderingConstants.RENDERED_TILE_SIZE
	_sprite.position = Vector2(footprint_center_x, footprint_bottom - frame_height)

	# Pick variant row (stable per entity)
	var variant_row: int = entity_id % _sprite_variants if _sprite_variants > 1 else 0
	_set_atlas_region(variant_row, 0)


func set_building_info(name: String, in_use: bool, color_index: int, palette: Array) -> void:
	if color_index >= 0 and color_index < palette.size():
		_sprite.modulate = palette[color_index]
	else:
		_sprite.modulate = Color.WHITE


func update_sprite_phase(max_pawn_wealth: int) -> void:
	if _sprite_phases <= 1:
		return

	var phase: int = max_pawn_wealth / 100
	phase = clampi(phase, 0, _sprite_phases - 1)
	if phase == _current_phase:
		return

	_current_phase = phase
	var variant_row: int = _entity_id % _sprite_variants if _sprite_variants > 1 else 0
	_set_atlas_region(variant_row, phase)


func _set_atlas_region(variant_row: int, phase_col: int) -> void:
	var px: int = _tile_size * SOURCE_SIZE
	_sprite.region_enabled = true
	_sprite.region_rect = Rect2(
		phase_col * px,
		variant_row * px,
		px,
		px
	)
