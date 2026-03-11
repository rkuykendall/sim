class_name PreviewSquare
extends Button

var _color_rect: ColorRect = null
var _texture_rect: TextureRect = null


func _ready() -> void:
	custom_minimum_size = Vector2(96, 96)

	_color_rect = ColorRect.new()
	_color_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_color_rect.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_color_rect)
	move_child(_color_rect, 0)

	_texture_rect = TextureRect.new()
	_texture_rect.expand_mode = TextureRect.EXPAND_FIT_WIDTH
	_texture_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_texture_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_texture_rect.visible = false
	_texture_rect.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_texture_rect)
	move_child(_texture_rect, 1)

	_init_border()


func _init_border() -> void:
	var style := _make_style(false)
	add_theme_stylebox_override("normal",  style)
	add_theme_stylebox_override("hover",   style)
	add_theme_stylebox_override("pressed", style)
	add_theme_stylebox_override("focus",   style)

	for node in [_color_rect, _texture_rect]:
		if node != null:
			node.offset_left   = 8
			node.offset_top    = 8
			node.offset_right  = -8
			node.offset_bottom = -8


## Update the preview display.
## Pass building_def_id / terrain_def_id = -1 when not applicable.
func update_preview(
	color_index: int,
	building_def_id: int,
	terrain_def_id: int,
	content: ContentRegistry,
	palette: Array,
	is_building_preview: bool = false,
	is_terrain_preview: bool = false,
	is_delete_preview: bool = false,
	is_select_preview: bool = false
) -> void:
	if _color_rect == null or _texture_rect == null:
		return

	var base_color: Color = palette[color_index] if color_index >= 0 and color_index < palette.size() else Color.WHITE
	var texture: Texture2D = null

	if content != null:
		if building_def_id != -1:
			var bdef: Dictionary = content.buildings.get(building_def_id, {})
			if not bdef.is_empty():
				texture = SpriteResourceManager.get_texture(bdef.get("spriteKey", ""))
		elif terrain_def_id != -1:
			var tdef: Dictionary = content.terrains.get(terrain_def_id, {})
			if not tdef.is_empty():
				texture = SpriteResourceManager.get_texture(tdef.get("spriteKey", ""))

	# Fallback to generic icons
	if texture == null and (building_def_id != -1 or terrain_def_id != -1):
		texture = load("res://sprites/placeholders/unknown.png") if ResourceLoader.exists("res://sprites/placeholders/unknown.png") else null

	if texture == null:
		if is_select_preview:
			if ResourceLoader.exists("res://sprites/tools/select.png"):
				texture = load("res://sprites/tools/select.png")
		elif is_building_preview:
			if ResourceLoader.exists("res://sprites/menu/build.png"):
				texture = load("res://sprites/menu/build.png")
		elif is_terrain_preview:
			if ResourceLoader.exists("res://sprites/menu/paint.png"):
				texture = load("res://sprites/menu/paint.png")
		elif is_delete_preview:
			if ResourceLoader.exists("res://sprites/tools/delete.png"):
				texture = load("res://sprites/tools/delete.png")

	if texture != null:
		_texture_rect.texture = texture
		_texture_rect.modulate = base_color
		_texture_rect.visible = true
		_color_rect.visible = false
	else:
		_color_rect.color = base_color
		_color_rect.visible = true
		_texture_rect.visible = false


func set_selected(selected: bool) -> void:
	var style := _make_style(selected)
	add_theme_stylebox_override("normal",  style)
	add_theme_stylebox_override("hover",   style)
	add_theme_stylebox_override("pressed", style)
	add_theme_stylebox_override("focus",   style)


func _make_style(selected: bool) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color        = Color.WHITE if selected else Color(0, 0, 0, 0)
	s.border_color    = Color(1, 1, 1, 0)
	s.border_width_left   = 4
	s.border_width_right  = 4
	s.border_width_top    = 4
	s.border_width_bottom = 4
	s.content_margin_left   = 4
	s.content_margin_right  = 4
	s.content_margin_top    = 4
	s.content_margin_bottom = 4
	s.draw_center = true
	return s
