class_name HomeScreen
extends Control

signal new_game_requested
signal load_game_requested(slot_name: String)
signal quit_requested

@export var grid_container_path: NodePath = ""

const THUMBNAIL_DISPLAY_WIDTH: int = 450
const THUMBNAIL_DISPLAY_HEIGHT: int = 300

var _grid_container: HFlowContainer = null
var _content: ContentRegistry = null
var _sound_manager: SoundManager = null


func _ready() -> void:
	if not grid_container_path.is_empty():
		_grid_container = get_node_or_null(grid_container_path)
	if _content != null:
		refresh_saves_list()


func initialize(content: ContentRegistry, sound_manager: SoundManager) -> void:
	_content = content
	_sound_manager = sound_manager
	if _grid_container != null:
		refresh_saves_list()


func refresh_saves_list() -> void:
	if _grid_container == null:
		return

	for child in _grid_container.get_children():
		child.queue_free()

	_grid_container.add_child(_create_new_game_item())

	var saves: Array = SaveFileManager.get_all_saves()
	for save_meta in saves:
		_grid_container.add_child(_create_save_item(save_meta))

	_grid_container.add_child(_create_quit_item())


func _create_new_game_item() -> Control:
	var container := PanelContainer.new()
	container.custom_minimum_size = Vector2(THUMBNAIL_DISPLAY_WIDTH, THUMBNAIL_DISPLAY_HEIGHT)
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.15, 0.35, 0.15)
	style.set_corner_radius_all(8)
	container.add_theme_stylebox_override("panel", style)

	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var label := Label.new()
	label.text = "+"
	label.add_theme_font_size_override("font_size", 64)
	label.add_theme_color_override("font_color", Color(0.4, 0.7, 0.4))
	center.add_child(label)
	container.add_child(center)

	container.gui_input.connect(func(event: InputEvent) -> void:
		if event is InputEventMouseButton:
			if event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
				if _sound_manager != null: _sound_manager.play_select()
				new_game_requested.emit()
	)
	container.mouse_entered.connect(func() -> void: style.bg_color = Color(0.2, 0.45, 0.2))
	container.mouse_exited.connect(func() -> void:  style.bg_color = Color(0.15, 0.35, 0.15))

	return container


func _create_save_item(save_meta: Dictionary) -> Control:
	var container := PanelContainer.new()
	container.custom_minimum_size = Vector2(THUMBNAIL_DISPLAY_WIDTH, THUMBNAIL_DISPLAY_HEIGHT)
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.1, 0.1, 0.1)
	style.set_corner_radius_all(8)
	container.add_theme_stylebox_override("panel", style)

	var slot_name: String = save_meta.get("slot_name", "")
	var save_path: String = SaveFileManager.SAVE_DIRECTORY + slot_name + ".json"
	var thumb: ImageTexture = SaveThumbnailGenerator.generate_from_path(save_path, _content)

	if thumb != null:
		var tex_rect := TextureRect.new()
		tex_rect.texture = thumb
		tex_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		tex_rect.stretch_mode = TextureRect.STRETCH_SCALE
		tex_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		container.add_child(tex_rect)
	else:
		var center := CenterContainer.new()
		center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		var label := Label.new()
		label.text = save_meta.get("display_name", slot_name)
		label.add_theme_font_size_override("font_size", 16)
		center.add_child(label)
		container.add_child(center)

	container.gui_input.connect(func(event: InputEvent) -> void:
		if event is InputEventMouseButton:
			if event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
				if _sound_manager != null: _sound_manager.play_select()
				load_game_requested.emit(slot_name)
	)
	container.mouse_entered.connect(func() -> void: style.bg_color = Color(0.2, 0.2, 0.2))
	container.mouse_exited.connect(func() -> void:  style.bg_color = Color(0.1, 0.1, 0.1))

	return container


func _create_quit_item() -> Control:
	var container := PanelContainer.new()
	container.custom_minimum_size = Vector2(THUMBNAIL_DISPLAY_WIDTH, THUMBNAIL_DISPLAY_HEIGHT)
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.35, 0.15, 0.15)
	style.set_corner_radius_all(8)
	container.add_theme_stylebox_override("panel", style)

	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var label := Label.new()
	label.text = "✕"
	label.add_theme_font_size_override("font_size", 64)
	label.add_theme_color_override("font_color", Color(0.7, 0.4, 0.4))
	center.add_child(label)
	container.add_child(center)

	container.gui_input.connect(func(event: InputEvent) -> void:
		if event is InputEventMouseButton:
			if event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
				if _sound_manager != null: _sound_manager.play_select()
				quit_requested.emit()
	)
	container.mouse_entered.connect(func() -> void: style.bg_color = Color(0.45, 0.2, 0.2))
	container.mouse_exited.connect(func() -> void:  style.bg_color = Color(0.35, 0.15, 0.15))

	return container
