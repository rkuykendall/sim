class_name BuildToolbar
extends HBoxContainer

signal home_button_pressed

@export var left_panel_path: NodePath = ""
@export var options_container_path: NodePath = ""

var _tools_grid: GridContainer = null
var _options_container: FlowContainer = null
var _content: ContentRegistry = null
var _sound_manager: SoundManager = null
var _current_palette: Array = []
var _debug_mode: bool = false

var _color_buttons: Array = []
var _tool_buttons: Array = []
var _tool_button_modes: Array = []   # Array of int (BuildToolMode.Mode) or -1 for home
var _option_buttons: Array[SpriteIconButton] = []


func _ready() -> void:
	if not left_panel_path.is_empty():
		_tools_grid = get_node_or_null(left_panel_path)
	if not options_container_path.is_empty():
		_options_container = get_node_or_null(options_container_path)


func initialize(content: ContentRegistry, sound_manager: SoundManager, debug_mode: bool = false) -> void:
	_content = content
	_sound_manager = sound_manager
	_debug_mode = debug_mode

	if _tools_grid == null and not left_panel_path.is_empty():
		_tools_grid = get_node_or_null(left_panel_path)
	if _options_container == null and not options_container_path.is_empty():
		_options_container = get_node_or_null(options_container_path)

	if _tools_grid == null or _options_container == null:
		push_error("BuildToolbar: required containers not found")
		return

	# Select first terrain/building by default
	var terrain_keys: Array = content.terrains.keys()
	terrain_keys.sort()
	if not terrain_keys.is_empty():
		BuildToolMode.selected_terrain_def_id = terrain_keys[0]

	var building_keys: Array = content.buildings.keys()
	building_keys.sort()
	if not building_keys.is_empty():
		BuildToolMode.selected_building_def_id = building_keys[0]

	_create_color_and_tool_buttons()
	_rebuild_options()
	_update_all_buttons()


func update_palette(palette: Array) -> void:
	_current_palette = palette
	BuildToolMode.selected_color_index = 3 if palette.size() > 3 else 0
	_create_color_and_tool_buttons()
	_update_all_buttons()


func set_debug_mode(debug_mode: bool) -> void:
	if _debug_mode != debug_mode:
		_debug_mode = debug_mode
		_create_color_and_tool_buttons()
		_rebuild_options()
		_update_all_buttons()


func _create_color_and_tool_buttons() -> void:
	_color_buttons.clear()
	_tool_buttons.clear()
	_tool_button_modes.clear()
	if _tools_grid != null:
		for child in _tools_grid.get_children():
			child.queue_free()

	# Tool definitions: [create_callable, mode_or_-1]
	var tool_defs: Array = [
		[func() -> Button: return _create_home_button(), -1],
		[func() -> Button: return _create_paint_tool_button(), BuildToolMode.Mode.PLACE_TERRAIN],
		[func() -> Button: return _create_icon_tool_button("res://sprites/tools/box.png", BuildToolMode.Mode.FILL_SQUARE), BuildToolMode.Mode.FILL_SQUARE],
		[func() -> Button: return _create_icon_tool_button("res://sprites/tools/square.png", BuildToolMode.Mode.OUTLINE_SQUARE), BuildToolMode.Mode.OUTLINE_SQUARE],
		[func() -> Button: return _create_icon_tool_button("res://sprites/tools/fill.png", BuildToolMode.Mode.FLOOD_FILL), BuildToolMode.Mode.FLOOD_FILL],
		[func() -> Button: return _create_icon_tool_button("res://sprites/menu/build.png", BuildToolMode.Mode.PLACE_BUILDING), BuildToolMode.Mode.PLACE_BUILDING],
	]
	if _debug_mode:
		tool_defs.append([func() -> Button: return _create_icon_tool_button("res://sprites/tools/select.png", BuildToolMode.Mode.SELECT), BuildToolMode.Mode.SELECT])

	var color_rows: int = _current_palette.size()
	var tool_rows: int = tool_defs.size()
	var total_rows: int = maxi(color_rows, tool_rows)

	for row in total_rows:
		# Color column
		if row < color_rows:
			var color_index: int = row
			var color_btn := PreviewSquare.new()
			color_btn.custom_minimum_size = Vector2(96, 96)
			color_btn.pressed.connect(func() -> void: _on_color_selected(color_index))
			if _tools_grid != null: _tools_grid.add_child(color_btn)
			_color_buttons.append(color_btn)
		else:
			var spacer := Control.new()
			spacer.custom_minimum_size = Vector2(96, 96)
			if _tools_grid != null: _tools_grid.add_child(spacer)

		# Tool column
		if row < tool_rows:
			var btn: Button = tool_defs[row][0].call()
			if _tools_grid != null: _tools_grid.add_child(btn)
			_tool_buttons.append(btn)
			_tool_button_modes.append(tool_defs[row][1])
		else:
			var spacer := Control.new()
			spacer.custom_minimum_size = Vector2(96, 96)
			if _tools_grid != null: _tools_grid.add_child(spacer)

	for i in _color_buttons.size():
		_update_color_button(i)


func _create_home_button() -> Button:
	var btn := SpriteIconButton.new()
	btn.custom_minimum_size = Vector2(96, 96)
	var tex: Texture2D = null
	if ResourceLoader.exists("res://sprites/menu/home.png"):
		tex = load("res://sprites/menu/home.png")
	if tex != null:
		btn.set_sprite(tex, Color.WHITE)
	else:
		btn.text = "Home"
	btn.pressed.connect(_on_home_button_pressed)
	return btn


func _create_paint_tool_button() -> Button:
	var btn := PreviewSquare.new()
	btn.custom_minimum_size = Vector2(96, 96)
	btn.pressed.connect(func() -> void: _on_tool_selected(BuildToolMode.Mode.PLACE_TERRAIN))
	return btn


func _create_icon_tool_button(sprite_path: String, mode: BuildToolMode.Mode) -> Button:
	var btn := SpriteIconButton.new()
	btn.custom_minimum_size = Vector2(96, 96)
	if ResourceLoader.exists(sprite_path):
		var tex: Texture2D = load(sprite_path)
		var color: Color = _current_palette[BuildToolMode.selected_color_index] if not _current_palette.is_empty() else Color.WHITE
		btn.set_sprite(tex, color)
	btn.pressed.connect(func() -> void: _on_tool_selected(mode))
	return btn


func _rebuild_options() -> void:
	for btn in _option_buttons:
		btn.queue_free()
	_option_buttons.clear()
	if _options_container != null:
		for child in _options_container.get_children():
			child.queue_free()
	if _content == null:
		return

	if BuildToolMode.current_mode == BuildToolMode.Mode.PLACE_BUILDING:
		var building_keys: Array = _content.buildings.keys()
		building_keys.sort()
		for id in building_keys:
			var btn := _create_option_button(id, _content.buildings[id].get("spriteKey", ""), true, false)
			_option_buttons.append(btn)
			if _options_container != null: _options_container.add_child(btn)
	elif BuildToolMode.current_mode in [
		BuildToolMode.Mode.PLACE_TERRAIN,
		BuildToolMode.Mode.FILL_SQUARE,
		BuildToolMode.Mode.OUTLINE_SQUARE,
		BuildToolMode.Mode.FLOOD_FILL,
	]:
		# Delete option first
		var del_btn := _create_option_button(-1, "res://sprites/tools/delete.png", false, true)
		_option_buttons.append(del_btn)
		if _options_container != null: _options_container.add_child(del_btn)

		var terrain_keys: Array = _content.terrains.keys()
		terrain_keys.sort()
		for id in terrain_keys:
			var btn := _create_option_button(id, _content.terrains[id].get("spriteKey", ""), false, false)
			_option_buttons.append(btn)
			if _options_container != null: _options_container.add_child(btn)


func _create_option_button(id: int, sprite_key: String, is_building: bool, is_delete: bool) -> SpriteIconButton:
	var btn := SpriteIconButton.new()
	btn.custom_minimum_size = Vector2(96, 96)

	var texture: Texture2D = null
	if is_delete:
		if ResourceLoader.exists(sprite_key):
			texture = load(sprite_key)
	elif is_building:
		var ts: int = int(_content.buildings[id].get("tileSize", 1))
		var px: int = ts * RenderingConstants.SOURCE_TILE_SIZE
		texture = SpriteResourceManager.get_icon_texture(sprite_key, Rect2(0, 0, px, px))
	else:
		var tile_size: int = RenderingConstants.SOURCE_TILE_SIZE
		var is_auto: bool = _content.terrains[id].get("isAutotiling", false)
		# Autotile row 3 col 0 = isolated tile (bitmask 0x00) — best representative icon
		var icon_y: int = 3 * tile_size if is_auto else 0
		texture = SpriteResourceManager.get_icon_texture(sprite_key, Rect2(0, icon_y, tile_size, tile_size))

	if texture != null:
		var color: Color = _current_palette[BuildToolMode.selected_color_index] if not _current_palette.is_empty() else Color.WHITE
		btn.set_sprite(texture, color)

	if is_building:
		btn.pressed.connect(func() -> void: _on_building_option_selected(id))
	elif is_delete:
		btn.pressed.connect(_on_delete_option_selected)
	else:
		btn.pressed.connect(func() -> void: _on_terrain_option_selected(id))

	return btn


func _update_color_button(color_index: int) -> void:
	if color_index >= _color_buttons.size() or color_index >= _current_palette.size():
		return
	var btn = _color_buttons[color_index]
	if btn is PreviewSquare:
		btn.update_preview(color_index, -1, -1, _content, _current_palette)
		btn.set_selected(color_index == BuildToolMode.selected_color_index)


func _on_color_selected(color_index: int) -> void:
	if _sound_manager != null: _sound_manager.play_click()
	BuildToolMode.selected_color_index = color_index
	_update_all_buttons()


func _on_tool_selected(mode: BuildToolMode.Mode) -> void:
	if _sound_manager != null: _sound_manager.play_click()
	BuildToolMode.current_mode = mode
	_rebuild_options()
	_update_all_buttons()


func _on_building_option_selected(building_def_id: int) -> void:
	if _sound_manager != null: _sound_manager.play_select()
	BuildToolMode.current_mode = BuildToolMode.Mode.PLACE_BUILDING
	BuildToolMode.selected_building_def_id = building_def_id
	_update_all_buttons()


func _on_terrain_option_selected(terrain_def_id: int) -> void:
	if _sound_manager != null: _sound_manager.play_select()
	BuildToolMode.selected_terrain_def_id = terrain_def_id
	_update_all_buttons()


func _on_delete_option_selected() -> void:
	if _sound_manager != null: _sound_manager.play_select()
	BuildToolMode.selected_terrain_def_id = -1
	_update_all_buttons()


func _on_home_button_pressed() -> void:
	if _sound_manager != null: _sound_manager.play_click()
	home_button_pressed.emit()


func _update_all_buttons() -> void:
	for i in _color_buttons.size():
		_update_color_button(i)

	for i in _tool_buttons.size():
		var btn = _tool_buttons[i]
		var mode: int = _tool_button_modes[i]
		var is_active: bool = mode != -1 and mode == BuildToolMode.current_mode

		if btn is PreviewSquare:
			btn.update_preview(BuildToolMode.selected_color_index, -1, -1, _content, _current_palette,
				false, true, false, false)
			btn.set_selected(is_active)
		elif btn is SpriteIconButton:
			var tex_rect: TextureRect = btn.get_node_or_null("TextureRect")
			if tex_rect != null and tex_rect.texture != null:
				var color: Color = _current_palette[BuildToolMode.selected_color_index] if not _current_palette.is_empty() else Color.WHITE
				btn.set_sprite(tex_rect.texture, color)
			btn.set_selected(is_active)

	var building_keys_sorted: Array = []
	var terrain_keys_sorted: Array = []
	if _content != null:
		building_keys_sorted = _content.buildings.keys()
		building_keys_sorted.sort()
		terrain_keys_sorted = _content.terrains.keys()
		terrain_keys_sorted.sort()

	for i in _option_buttons.size():
		var btn: SpriteIconButton = _option_buttons[i]
		var is_selected: bool = false

		if BuildToolMode.current_mode == BuildToolMode.Mode.PLACE_BUILDING:
			if i < building_keys_sorted.size():
				is_selected = building_keys_sorted[i] == BuildToolMode.selected_building_def_id
		elif BuildToolMode.current_mode in [
			BuildToolMode.Mode.PLACE_TERRAIN,
			BuildToolMode.Mode.FILL_SQUARE,
			BuildToolMode.Mode.OUTLINE_SQUARE,
			BuildToolMode.Mode.FLOOD_FILL,
		]:
			if i == 0:
				is_selected = BuildToolMode.selected_terrain_def_id == -1
			else:
				var terrain_index: int = i - 1
				if terrain_index < terrain_keys_sorted.size():
					is_selected = terrain_keys_sorted[terrain_index] == BuildToolMode.selected_terrain_def_id

		btn.set_selected(is_selected)
		var tex_rect: TextureRect = btn.get_node_or_null("TextureRect")
		if tex_rect != null and tex_rect.texture != null:
			var color: Color = _current_palette[BuildToolMode.selected_color_index] if not _current_palette.is_empty() else Color.WHITE
			btn.set_sprite(tex_rect.texture, color)
