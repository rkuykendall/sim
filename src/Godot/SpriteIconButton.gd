class_name SpriteIconButton
extends Button

var _texture_rect: TextureRect = null
var _selected: bool = false


func _ready() -> void:
	_ensure_texture_rect()
	_init_border()


func _ensure_texture_rect() -> void:
	if _texture_rect != null:
		return
	_texture_rect = TextureRect.new()
	_texture_rect.name = "TextureRect"
	_texture_rect.expand_mode = TextureRect.EXPAND_FIT_WIDTH
	_texture_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_texture_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_texture_rect.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_texture_rect)
	move_child(_texture_rect, 0)


func _init_border() -> void:
	var style := _make_style(false)
	add_theme_stylebox_override("normal",  style)
	add_theme_stylebox_override("hover",   style)
	add_theme_stylebox_override("pressed", style)
	add_theme_stylebox_override("focus",   style)

	if _texture_rect != null:
		_texture_rect.offset_left   = 8
		_texture_rect.offset_top    = 8
		_texture_rect.offset_right  = -8
		_texture_rect.offset_bottom = -8


func set_sprite(texture: Texture2D, modulation: Color = Color.WHITE) -> void:
	_ensure_texture_rect()
	if _texture_rect == null:
		return
	_texture_rect.texture = texture
	_texture_rect.modulate = modulation


func update_color(modulation: Color) -> void:
	_ensure_texture_rect()
	if _texture_rect != null:
		_texture_rect.modulate = modulation


func set_selected(selected: bool) -> void:
	_selected = selected
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
