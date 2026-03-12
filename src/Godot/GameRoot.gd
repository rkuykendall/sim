class_name GameRoot
extends Node2D

# ---------------------------------------------------------------------------
# Exports
# ---------------------------------------------------------------------------
@export var pawn_scene: PackedScene
@export var building_scene: PackedScene
@export var pawns_root_path: NodePath = "."
@export var buildings_root_path: NodePath = "."
@export var tiles_root_path: NodePath = "."
@export var shadow_rect_path: NodePath = ""
@export var crt_shader_layer_path: NodePath = ""
@export var camera_path: NodePath = ""
@export var ui_layer_path: NodePath = ""
@export var toolbar_path: NodePath = ""
@export var music_manager_path: NodePath = ""
@export var sound_manager_path: NodePath = ""
@export var home_screen_path: NodePath = ""

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------
const MAX_TICKS_PER_FRAME: int = 100
const AUTOSAVE_INTERVAL: float = 60.0
const PAWN_HITBOX_SIZE: float = 24.0
const BUILDING_HITBOX_SIZE: float = 28.0

enum _SimSpeed { PAUSED = 0, NORMAL = 1, FAST_4X = 4, FAST_16X = 16, FAST_64X = 64 }
enum _AppScreen { HOME, GAME }

# ---------------------------------------------------------------------------
# State
# ---------------------------------------------------------------------------
var _sim: Simulation = null
var _content: ContentRegistry = null
var _user_settings: UserSettings = null
var _current_screen: _AppScreen = _AppScreen.HOME
var _current_save_slot: String = ""
var _sim_speed: _SimSpeed = _SimSpeed.NORMAL
var _accumulator: float = 0.0
var _tick_delta: float = 0.0
var _time_since_autosave: float = 0.0
var _debug_mode: bool = false

# Brush drag for fill/outline tools
var _brush_drag_start: Vector2i = Vector2i(-1, -1)
var _brush_drag_current: Vector2i = Vector2i(-1, -1)
var _is_painting_terrain: bool = false
var _last_painted_tile: Vector2i = Vector2i(-1, -1)

# Selection
var _selected_pawn_id: int = -1
var _selected_building_id: int = -1
var _hovered_tile: Vector2i = Vector2i(-1, -1)
var _last_snapshot: Dictionary = {}

# Palette cache
var _current_palette: Array = []
var _current_palette_id: int = -1

# Node references
var _pawns_root: Node2D
var _buildings_root: Node2D
var _tiles_root: Node2D
var _shadow_rect: ColorRect = null
var _shadow_shader_mat: ShaderMaterial = null
var _crt_shader_controller: CRTShaderController = null
var _camera: CameraController = null
var _ui_layer: CanvasLayer = null
var _toolbar: BuildToolbar = null
var _music_manager: MusicManager = null
var _sound_manager: SoundManager = null
var _home_screen: HomeScreen = null
var _debug_panel: DebugPanel = null

# Entity nodes
var _pawn_nodes: Dictionary = {}      # int -> Node2D
var _building_nodes: Dictionary = {}  # int -> Node2D

# Autotile layers
var _auto_tile_layers: Dictionary = {}     # int (terrain_id) -> ModulatableTileMapLayer
var _tile_sprites: Dictionary = {}         # Vector2i -> [Sprite2D base, Sprite2D overlay]
var _autotile_updates: Dictionary = {}     # int -> Array of [Vector2i, Color]
var _autotile_clear_cells: Dictionary = {} # int -> Array of Vector2i

# Reusable collections
var _active_ids: Dictionary = {}
var _ids_to_remove: Array = []


func _ready() -> void:
	z_index = ZIndexConstants.UI_OVERLAY

	_user_settings = UserSettings.load()
	_apply_fullscreen()

	var content_path: String = _get_content_path()
	print("[GameRoot] Content path: %s" % content_path)
	_content = ContentLoader.load_all(content_path)
	_tick_delta = 1.0 / Simulation.TICK_RATE

	_pawns_root    = get_node(pawns_root_path)
	_buildings_root = get_node(buildings_root_path)
	_tiles_root    = get_node(tiles_root_path)

	if not shadow_rect_path.is_empty():
		_shadow_rect = get_node_or_null(shadow_rect_path)
		if _shadow_rect != null:
			var shader: Shader = load("res://shaders/sdf_shadows.gdshader") if ResourceLoader.exists("res://shaders/sdf_shadows.gdshader") else null
			if shader != null:
				_shadow_shader_mat = ShaderMaterial.new()
				_shadow_shader_mat.shader = shader
				_shadow_rect.material = _shadow_shader_mat

				var gradient := Gradient.new()
				gradient.add_point(0.0, Color.WHITE)
				gradient.add_point(0.9, Color.WHITE)
				gradient.add_point(1.0, Color.BLACK)
				var grad_tex := GradientTexture2D.new()
				grad_tex.gradient = gradient
				grad_tex.width = 256
				grad_tex.height = 1
				_shadow_shader_mat.set_shader_parameter("shadow_gradient", grad_tex)

	if not crt_shader_layer_path.is_empty():
		_crt_shader_controller = get_node_or_null(crt_shader_layer_path)
	if not camera_path.is_empty():
		_camera = get_node_or_null(camera_path)
	if not ui_layer_path.is_empty():
		_ui_layer = get_node_or_null(ui_layer_path)

	# Debug panel
	_debug_panel = DebugPanel.new()
	_debug_panel.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	_debug_panel.position = Vector2(-260, 10)
	if _ui_layer != null:
		_ui_layer.add_child(_debug_panel)

	if not toolbar_path.is_empty():
		_toolbar = get_node_or_null(toolbar_path)
		if _toolbar != null:
			_toolbar.home_button_pressed.connect(_on_home_button_pressed)

	if not music_manager_path.is_empty():
		_music_manager = get_node_or_null(music_manager_path)
		if _music_manager != null:
			_music_manager.music_finished.connect(_on_music_finished)

	if not sound_manager_path.is_empty():
		_sound_manager = get_node_or_null(sound_manager_path)

	if not home_screen_path.is_empty():
		_home_screen = get_node_or_null(home_screen_path)
		if _home_screen != null:
			_home_screen.new_game_requested.connect(_on_new_game_requested)
			_home_screen.load_game_requested.connect(_on_load_game_requested)
			_home_screen.quit_requested.connect(_on_quit_requested)
			_home_screen.initialize(_content, _sound_manager)

	_show_home_screen()


func _apply_fullscreen() -> void:
	if _user_settings.fullscreen:
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_EXCLUSIVE_FULLSCREEN)
	else:
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)


func _show_home_screen() -> void:
	_current_screen = _AppScreen.HOME
	if _toolbar != null: _toolbar.hide()
	if _debug_panel != null: _debug_panel.set_debug_mode(false)
	if _pawns_root != null: _pawns_root.hide()
	if _buildings_root != null: _buildings_root.hide()
	if _tiles_root != null: _tiles_root.hide()
	if _home_screen != null:
		_home_screen.show()
		_home_screen.refresh_saves_list()


func _show_game() -> void:
	_current_screen = _AppScreen.GAME
	if _home_screen != null: _home_screen.hide()
	if _toolbar != null: _toolbar.show()
	if _pawns_root != null: _pawns_root.show()
	if _buildings_root != null: _buildings_root.show()
	if _tiles_root != null: _tiles_root.show()
	_update_speed_display()


func _on_new_game_requested() -> void:
	_current_save_slot = SaveFileManager.generate_save_name()
	_sim = Simulation.new(_content)
	_initialize_game_world()
	_show_game()
	print("[GameRoot] Started new game: %s" % _current_save_slot)


func _on_load_game_requested(slot_name: String) -> void:
	var loaded_sim: Simulation = SaveFileManager.load_save(slot_name, _content)
	if loaded_sim == null:
		push_error("[GameRoot] Failed to load save: %s" % slot_name)
		return
	_current_save_slot = slot_name
	_sim = loaded_sim
	_initialize_game_world()
	_show_game()
	print("[GameRoot] Loaded game: %s" % slot_name)


func _on_quit_requested() -> void:
	get_tree().quit()


func _on_home_button_pressed() -> void:
	_return_to_home()


func _on_music_finished() -> void:
	if _sim != null and _sim.theme_system != null:
		_sim.theme_system.on_music_finished(_sim)


func _return_to_home() -> void:
	if _sim != null and not _current_save_slot.is_empty():
		SaveFileManager.write_save(_current_save_slot, _sim, _current_save_slot)
	_show_home_screen()


func _initialize_game_world() -> void:
	_clear_all_nodes()
	_initialize_auto_tile_layers()

	var initial_snapshot: Dictionary = _sim.create_render_snapshot()
	_current_palette = initial_snapshot.get("palette", [])
	_current_palette_id = -1

	_initialize_tile_nodes()

	var all_tiles: Array[Vector2i] = []
	for x in _sim.world.width:
		for y in _sim.world.height:
			all_tiles.append(Vector2i(x, y))
	_sync_tiles(all_tiles)

	if _toolbar != null:
		_toolbar.initialize(_sim.content, _sound_manager, _debug_mode)
	if _debug_panel != null:
		_debug_panel.initialize(_sim.content)
		_debug_panel.set_simulation(_sim)
		_debug_panel.set_debug_mode(_debug_mode)

	if _camera != null:
		var world_w: int = _sim.world.width * RenderingConstants.RENDERED_TILE_SIZE
		var world_h: int = _sim.world.height * RenderingConstants.RENDERED_TILE_SIZE
		_camera.position = Vector2(world_w * 0.5, world_h * 0.5)
		_camera.set_world_bounds(world_w, world_h)

	_selected_pawn_id = -1
	_selected_building_id = -1
	_accumulator = 0.0
	_time_since_autosave = 0.0


func _clear_all_nodes() -> void:
	for node in _pawn_nodes.values():
		node.queue_free()
	_pawn_nodes.clear()
	for node in _building_nodes.values():
		node.queue_free()
	_building_nodes.clear()
	for sprites in _tile_sprites.values():
		sprites[0].queue_free()
		sprites[1].queue_free()
	_tile_sprites.clear()
	for layer in _auto_tile_layers.values():
		layer.queue_free()
	_auto_tile_layers.clear()


# ---------------------------------------------------------------------------
# Main loop
# ---------------------------------------------------------------------------

func _process(delta: float) -> void:
	if _current_screen != _AppScreen.GAME or _sim == null:
		return

	var effective_delta: float = 0.0 if _sim_speed == _SimSpeed.PAUSED else delta * int(_sim_speed)
	_accumulator += effective_delta

	var ticks_processed: int = 0
	while _accumulator >= _tick_delta and ticks_processed < MAX_TICKS_PER_FRAME:
		_sim.tick()
		_accumulator -= _tick_delta
		ticks_processed += 1
		if _sim.time.tick % TimeService.TICKS_PER_DAY == 0 and _sim.time.tick > 0:
			print("[Day %d] Total wealth: %dg, Tax pool: %dg, Pawns: %d" % [
				_sim.time.day, _sim.get_total_wealth(), _sim.tax_pool, _sim.entities.all_pawns().size()])

	if ticks_processed >= MAX_TICKS_PER_FRAME and _accumulator >= _tick_delta:
		_accumulator = 0.0
		push_warning("GameRoot: tick cap reached, resetting accumulator")

	var snapshot: Dictionary = _sim.create_render_snapshot()
	_last_snapshot = snapshot

	# Palette change
	if _sim.selected_palette_id != _current_palette_id:
		_current_palette = snapshot.get("palette", [])
		_current_palette_id = _sim.selected_palette_id
		if _toolbar != null: _toolbar.update_palette(_current_palette)
		var all_tiles: Array[Vector2i] = []
		for x in _sim.world.width:
			for y in _sim.world.height:
				all_tiles.append(Vector2i(x, y))
		_sync_tiles(all_tiles)

	# Music
	if _music_manager != null:
		_music_manager.update_music_state(snapshot.get("theme", {}))

	# Autosave
	_time_since_autosave += delta
	if _time_since_autosave >= AUTOSAVE_INTERVAL:
		_perform_autosave()

	_sync_pawns(snapshot)
	_sync_buildings(snapshot)
	_update_info_panel(snapshot)
	_update_building_info_panel(snapshot)
	_update_time_display(snapshot)
	_update_night_overlay(snapshot)

	var mouse_pos: Vector2 = get_local_mouse_position()
	_hovered_tile = _screen_to_tile(mouse_pos)

	if _debug_mode or BuildToolMode.current_mode != BuildToolMode.Mode.SELECT:
		queue_redraw()


func _perform_autosave() -> void:
	if _sim == null or _current_save_slot.is_empty():
		return
	SaveFileManager.write_save(_current_save_slot, _sim, _current_save_slot)
	_time_since_autosave = 0.0
	print("[GameRoot] Autosaved: %s" % _current_save_slot)


# ---------------------------------------------------------------------------
# Input
# ---------------------------------------------------------------------------

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed:
		# F11: Toggle fullscreen (any screen)
		if event.keycode == KEY_F11:
			_user_settings.fullscreen = not _user_settings.fullscreen
			_user_settings.save()
			_apply_fullscreen()
			return

		# Escape: Return to home (game only)
		if event.keycode == KEY_ESCAPE and _current_screen == _AppScreen.GAME:
			_return_to_home()
			return

		if _current_screen != _AppScreen.GAME:
			return

		match event.keycode:
			KEY_F3:
				_debug_mode = not _debug_mode
				if _toolbar != null: _toolbar.set_debug_mode(_debug_mode)
				if _debug_panel != null: _debug_panel.set_debug_mode(_debug_mode)
				queue_redraw()
			KEY_0:
				_sim_speed = _SimSpeed.PAUSED
				_update_speed_display()
			KEY_1:
				_sim_speed = _SimSpeed.NORMAL
				_update_speed_display()
			KEY_2:
				_sim_speed = _SimSpeed.FAST_4X
				_update_speed_display()
			KEY_3:
				_sim_speed = _SimSpeed.FAST_16X
				_update_speed_display()
			KEY_4:
				_sim_speed = _SimSpeed.FAST_64X
				_update_speed_display()
		return

	if _current_screen != _AppScreen.GAME:
		return

	if event is InputEventMouseButton:
		var local_pos: Vector2 = get_local_mouse_position()
		var tile_coord: Vector2i = _screen_to_tile(local_pos)

		if event.button_index == MOUSE_BUTTON_LEFT:
			if event.pressed:
				_handle_left_press(tile_coord)
			else:
				_handle_left_release(tile_coord)
			return

	if event is InputEventMouseMotion:
		_handle_mouse_motion()


func _handle_left_press(tile_coord: Vector2i) -> void:
	var mode: BuildToolMode.Mode = BuildToolMode.current_mode

	if mode == BuildToolMode.Mode.PLACE_TERRAIN:
		_is_painting_terrain = true
		_last_painted_tile = tile_coord
		var tiles_to_update: Array[Vector2i]
		if BuildToolMode.selected_terrain_def_id != -1:
			tiles_to_update = _sim.paint_terrain(tile_coord, BuildToolMode.selected_terrain_def_id, BuildToolMode.selected_color_index)
			if _sound_manager != null: _sound_manager.play_paint()
		else:
			tiles_to_update = _sim.delete_at_tile(tile_coord)
			if _sound_manager != null: _sound_manager.play_delete()
		_sync_tiles(tiles_to_update)
		return

	if mode == BuildToolMode.Mode.FILL_SQUARE or mode == BuildToolMode.Mode.OUTLINE_SQUARE:
		_brush_drag_start = tile_coord
		_brush_drag_current = tile_coord
		queue_redraw()
		return

	if mode == BuildToolMode.Mode.FLOOD_FILL:
		var tiles_to_update: Array[Vector2i]
		if BuildToolMode.selected_terrain_def_id != -1:
			tiles_to_update = _sim.flood_fill(tile_coord, BuildToolMode.selected_terrain_def_id, BuildToolMode.selected_color_index)
			if _sound_manager != null: _sound_manager.play_paint()
		else:
			tiles_to_update = _sim.flood_delete(tile_coord)
			if _sound_manager != null: _sound_manager.play_delete()
		_sync_tiles(tiles_to_update)
		return

	if mode == BuildToolMode.Mode.PLACE_BUILDING and BuildToolMode.selected_building_def_id != -1:
		var result: int = _sim.create_building(BuildToolMode.selected_building_def_id, tile_coord, BuildToolMode.selected_color_index)
		if result != -1:
			if _sound_manager != null: _sound_manager.play_build()
		return

	# Selection / click — check pawn or building
	var pawn_id: int = _find_pawn_at(get_local_mouse_position())
	if pawn_id != -1:
		_deselect_pawn()
		_selected_pawn_id = pawn_id
		_selected_building_id = -1
		var pawn_node = _pawn_nodes.get(pawn_id)
		if pawn_node is PawnView:
			pawn_node.set_selected(true)
		return

	var building_id: int = _find_building_at(get_local_mouse_position())
	if building_id != -1:
		_deselect_pawn()
		_selected_pawn_id = -1
		_selected_building_id = building_id
		return

	_deselect_pawn()
	_selected_pawn_id = -1
	_selected_building_id = -1
	if _debug_panel != null: _debug_panel.clear_selection()


func _handle_left_release(tile_coord: Vector2i) -> void:
	_is_painting_terrain = false
	_last_painted_tile = Vector2i(-1, -1)

	var mode: BuildToolMode.Mode = BuildToolMode.current_mode
	if (mode == BuildToolMode.Mode.FILL_SQUARE or mode == BuildToolMode.Mode.OUTLINE_SQUARE) \
			and _brush_drag_start != Vector2i(-1, -1):
		if mode == BuildToolMode.Mode.FILL_SQUARE:
			if BuildToolMode.selected_terrain_def_id != -1:
				var painted: Array[Vector2i] = _sim.paint_rectangle(_brush_drag_start, _brush_drag_current, BuildToolMode.selected_terrain_def_id, BuildToolMode.selected_color_index)
				_sync_tiles(_sim.get_tiles_with_neighbors(painted))
				if _sound_manager != null: _sound_manager.play_paint()
			else:
				_sync_tiles(_sim.delete_rectangle(_brush_drag_start, _brush_drag_current))
				if _sound_manager != null: _sound_manager.play_delete()
		else:
			if BuildToolMode.selected_terrain_def_id != -1:
				var painted: Array[Vector2i] = _sim.paint_rectangle_outline(_brush_drag_start, _brush_drag_current, BuildToolMode.selected_terrain_def_id, BuildToolMode.selected_color_index)
				_sync_tiles(_sim.get_tiles_with_neighbors(painted))
				if _sound_manager != null: _sound_manager.play_paint()
			else:
				_sync_tiles(_sim.delete_rectangle_outline(_brush_drag_start, _brush_drag_current))
				if _sound_manager != null: _sound_manager.play_delete()

		_brush_drag_start = Vector2i(-1, -1)
		_brush_drag_current = Vector2i(-1, -1)
		queue_redraw()


func _handle_mouse_motion() -> void:
	if _is_painting_terrain and BuildToolMode.current_mode == BuildToolMode.Mode.PLACE_TERRAIN:
		var tile_coord: Vector2i = _screen_to_tile(get_local_mouse_position())
		if tile_coord == _last_painted_tile:
			return
		_last_painted_tile = tile_coord
		var tiles_to_update: Array[Vector2i]
		if BuildToolMode.selected_terrain_def_id != -1:
			tiles_to_update = _sim.paint_terrain(tile_coord, BuildToolMode.selected_terrain_def_id, BuildToolMode.selected_color_index)
			if _sound_manager != null: _sound_manager.play_paint_tick()
		else:
			tiles_to_update = _sim.delete_at_tile(tile_coord)
			if _sound_manager != null: _sound_manager.play_paint_tick()
		_sync_tiles(tiles_to_update)
		return

	var mode: BuildToolMode.Mode = BuildToolMode.current_mode
	if (mode == BuildToolMode.Mode.FILL_SQUARE or mode == BuildToolMode.Mode.OUTLINE_SQUARE) \
			and _brush_drag_start != Vector2i(-1, -1):
		_brush_drag_current = _screen_to_tile(get_local_mouse_position())
		queue_redraw()


# ---------------------------------------------------------------------------
# Drawing
# ---------------------------------------------------------------------------

func _draw() -> void:
	if _hovered_tile != Vector2i(-1, -1) and BuildToolMode.current_mode != BuildToolMode.Mode.SELECT:
		_draw_hover_preview(_hovered_tile)

	if not _debug_mode:
		return

	var half: float = PAWN_HITBOX_SIZE * 0.5
	for node in _pawn_nodes.values():
		draw_rect(Rect2(node.position.x - half, node.position.y - half, PAWN_HITBOX_SIZE, PAWN_HITBOX_SIZE), Color.MAGENTA, false, 2.0)

	for building_id in _building_nodes.keys():
		var b_snap: Dictionary = {}
		for b in _last_snapshot.get("buildings", []):
			if b.get("id", -1) == building_id:
				b_snap = b
				break
		if not b_snap.is_empty() and _sim != null:
			var bdef: Dictionary = _sim.content.buildings.get(b_snap.get("building_def_id", -1), {})
			if not bdef.is_empty():
				var ts: int = int(bdef.get("tileSize", 1))
				var occupied: Array[Vector2i] = BuildingUtilities.get_occupied_tiles(Vector2i(b_snap.get("x", 0), b_snap.get("y", 0)), ts)
				for tile in occupied:
					draw_rect(Rect2(tile.x * RenderingConstants.RENDERED_TILE_SIZE, tile.y * RenderingConstants.RENDERED_TILE_SIZE, RenderingConstants.RENDERED_TILE_SIZE, RenderingConstants.RENDERED_TILE_SIZE), Color.CYAN, false, 2.0)

	for pawn_snap in _last_snapshot.get("pawns", []):
		var center := Vector2(
			pawn_snap.get("x", 0) * RenderingConstants.RENDERED_TILE_SIZE + RenderingConstants.RENDERED_TILE_SIZE * 0.5,
			pawn_snap.get("y", 0) * RenderingConstants.RENDERED_TILE_SIZE + RenderingConstants.RENDERED_TILE_SIZE * 0.5
		)
		var path: Array = pawn_snap.get("current_path", [])
		var path_idx: int = pawn_snap.get("path_index", 0)
		for i in range(path_idx, path.size() - 1):
			var from_p := Vector2(path[i].get("x", 0) * RenderingConstants.RENDERED_TILE_SIZE + RenderingConstants.RENDERED_TILE_SIZE * 0.5,
				path[i].get("y", 0) * RenderingConstants.RENDERED_TILE_SIZE + RenderingConstants.RENDERED_TILE_SIZE * 0.5)
			var to_p := Vector2(path[i+1].get("x", 0) * RenderingConstants.RENDERED_TILE_SIZE + RenderingConstants.RENDERED_TILE_SIZE * 0.5,
				path[i+1].get("y", 0) * RenderingConstants.RENDERED_TILE_SIZE + RenderingConstants.RENDERED_TILE_SIZE * 0.5)
			draw_line(from_p, to_p, Color.ORANGE, 2.0)

		var target: Vector2i = pawn_snap.get("target_tile", Vector2i(-1, -1))
		if target != Vector2i(-1, -1):
			draw_rect(Rect2(target.x * RenderingConstants.RENDERED_TILE_SIZE + 4, target.y * RenderingConstants.RENDERED_TILE_SIZE + 4,
				RenderingConstants.RENDERED_TILE_SIZE - 8, RenderingConstants.RENDERED_TILE_SIZE - 8), Color(1, 0.5, 0, 0.3), true)
			draw_rect(Rect2(target.x * RenderingConstants.RENDERED_TILE_SIZE + 4, target.y * RenderingConstants.RENDERED_TILE_SIZE + 4,
				RenderingConstants.RENDERED_TILE_SIZE - 8, RenderingConstants.RENDERED_TILE_SIZE - 8), Color.ORANGE, false, 2.0)

	draw_circle(get_local_mouse_position(), 5.0, Color.YELLOW)


func _draw_hover_preview(coord: Vector2i) -> void:
	var ts: int = RenderingConstants.RENDERED_TILE_SIZE
	var rect := Rect2(coord.x * ts, coord.y * ts, ts, ts)
	var mode: BuildToolMode.Mode = BuildToolMode.current_mode

	if mode == BuildToolMode.Mode.PLACE_TERRAIN or mode == BuildToolMode.Mode.FLOOD_FILL:
		if BuildToolMode.selected_terrain_def_id != -1:
			var color: Color = _current_palette[BuildToolMode.selected_color_index] if not _current_palette.is_empty() else Color.WHITE
			color.a = 0.5
			draw_rect(rect, color, true)
		else:
			draw_rect(rect, Color(1, 0, 0, 0.3), true)
		draw_rect(rect, Color.WHITE, false, 2.0)

	elif (mode == BuildToolMode.Mode.FILL_SQUARE or mode == BuildToolMode.Mode.OUTLINE_SQUARE) \
			and _brush_drag_start != Vector2i(-1, -1):
		var x0: int = mini(_brush_drag_start.x, _brush_drag_current.x)
		var x1: int = maxi(_brush_drag_start.x, _brush_drag_current.x)
		var y0: int = mini(_brush_drag_start.y, _brush_drag_current.y)
		var y1: int = maxi(_brush_drag_start.y, _brush_drag_current.y)
		var color: Color
		if BuildToolMode.selected_terrain_def_id != -1:
			color = _current_palette[BuildToolMode.selected_color_index] if not _current_palette.is_empty() else Color.WHITE
			color.a = 0.3
		else:
			color = Color(1, 0, 0, 0.3)
		var preview_rect := Rect2(x0 * ts, y0 * ts, (x1 - x0 + 1) * ts, (y1 - y0 + 1) * ts)
		if mode == BuildToolMode.Mode.FILL_SQUARE:
			draw_rect(preview_rect, color, true)
		draw_rect(preview_rect, Color.WHITE, false, 2.0)

	elif mode == BuildToolMode.Mode.PLACE_BUILDING and BuildToolMode.selected_building_def_id != -1 and _sim != null:
		var bdef: Dictionary = _sim.content.buildings.get(BuildToolMode.selected_building_def_id, {})
		if not bdef.is_empty():
			var building_tile_size: int = int(bdef.get("tileSize", 1))
			var occupied: Array[Vector2i] = BuildingUtilities.get_occupied_tiles(coord, building_tile_size)
			var color: Color = _current_palette[BuildToolMode.selected_color_index] if not _current_palette.is_empty() else Color.WHITE
			color.a = 0.5
			for tile in occupied:
				var tile_rect := Rect2(tile.x * ts, tile.y * ts, ts, ts)
				draw_rect(tile_rect, color, true)
				draw_rect(tile_rect, Color.WHITE, false, 2.0)


# ---------------------------------------------------------------------------
# Tile rendering
# ---------------------------------------------------------------------------

func _initialize_auto_tile_layers() -> void:
	if _sim == null:
		return
	for terrain_id in _sim.content.terrains.keys():
		var tdef: Dictionary = _sim.content.terrains[terrain_id]
		if not bool(tdef.get("isAutotiling", false)):
			continue
		var texture: Texture2D = SpriteResourceManager.get_texture(tdef.get("spriteKey", ""))
		if texture == null:
			push_error("GameRoot: no texture for autotile terrain %d" % terrain_id)
			continue
		var blocks_light: bool = bool(tdef.get("blocksLight", false))
		var layer := ModulatableTileMapLayer.new()
		layer.name = "%sTileMapLayer" % tdef.get("spriteKey", str(terrain_id))
		layer.tile_set = AutoTileSetBuilder.create_auto_tile_set(texture, tdef.get("spriteKey", ""), blocks_light)
		layer.scale = Vector2(RenderingConstants.SPRITE_SCALE, RenderingConstants.SPRITE_SCALE)
		if blocks_light:
			layer.z_index = ZIndexConstants.TERRAIN_BLOCKING_AND_PAWNS
			layer.y_sort_enabled = true
			layer.y_sort_origin = 4
			_pawns_root.get_parent().call_deferred("add_child", layer)
		else:
			layer.z_index = ZIndexConstants.TERRAIN_NON_BLOCKING
			_tiles_root.add_child(layer)
		_auto_tile_layers[terrain_id] = layer


func _initialize_tile_nodes() -> void:
	for x in _sim.world.width:
		for y in _sim.world.height:
			var coord := Vector2i(x, y)
			var tile_node := Node2D.new()
			tile_node.position = Vector2(x * RenderingConstants.RENDERED_TILE_SIZE, y * RenderingConstants.RENDERED_TILE_SIZE)
			tile_node.name = "Tile_%d_%d" % [x, y]
			tile_node.z_index = ZIndexConstants.TILE_NODES
			_tiles_root.add_child(tile_node)

			var base_sprite := Sprite2D.new()
			base_sprite.name = "BaseTileSprite"
			base_sprite.position = Vector2(RenderingConstants.RENDERED_TILE_SIZE * 0.5, RenderingConstants.RENDERED_TILE_SIZE * 0.5)
			base_sprite.centered = true
			base_sprite.visible = false
			base_sprite.scale = Vector2(RenderingConstants.SPRITE_SCALE, RenderingConstants.SPRITE_SCALE)
			base_sprite.z_index = -1
			tile_node.add_child(base_sprite)

			var overlay_sprite := Sprite2D.new()
			overlay_sprite.name = "OverlayTileSprite"
			overlay_sprite.position = Vector2(RenderingConstants.RENDERED_TILE_SIZE * 0.5, RenderingConstants.RENDERED_TILE_SIZE * 0.5)
			overlay_sprite.centered = true
			overlay_sprite.visible = false
			overlay_sprite.scale = Vector2(RenderingConstants.SPRITE_SCALE, RenderingConstants.SPRITE_SCALE)
			overlay_sprite.z_index = 0
			tile_node.add_child(overlay_sprite)

			_tile_sprites[coord] = [base_sprite, overlay_sprite]


func _sync_tiles(coords: Array) -> void:
	_prepare_autotile_batches()
	for coord in coords:
		_sync_single_tile(coord)
	_apply_autotile_batches()


func _prepare_autotile_batches() -> void:
	for terrain_id in _auto_tile_layers.keys():
		if not _autotile_updates.has(terrain_id):
			_autotile_updates[terrain_id] = []
			_autotile_clear_cells[terrain_id] = []
		else:
			_autotile_updates[terrain_id].clear()
			_autotile_clear_cells[terrain_id].clear()


func _sync_single_tile(coord: Vector2i) -> void:
	if not _tile_sprites.has(coord):
		return
	var sprites: Array = _tile_sprites[coord]
	var base_sprite: Sprite2D = sprites[0]
	var overlay_sprite: Sprite2D = sprites[1]
	var tile: World.Tile = _sim.world.get_tile(coord)
	var map_coord := coord

	var base_tdef: Dictionary = _sim.content.terrains.get(tile.base_terrain_type_id, {})
	var overlay_tdef: Dictionary = {}
	if tile.overlay_terrain_type_id != -1:
		overlay_tdef = _sim.content.terrains.get(tile.overlay_terrain_type_id, {})

	_process_autotile_layers(tile, base_tdef, overlay_tdef, map_coord)
	_update_terrain_sprite(base_sprite, base_tdef, tile.color_index, tile.base_variant_index)
	_update_terrain_sprite(overlay_sprite, overlay_tdef, tile.overlay_color_index, tile.overlay_variant_index)


func _process_autotile_layers(tile: World.Tile, base_tdef: Dictionary, overlay_tdef: Dictionary, map_coord: Vector2i) -> void:
	if not base_tdef.is_empty() and bool(base_tdef.get("isAutotiling", false)):
		var color: Color = _current_palette[tile.color_index] if tile.color_index < _current_palette.size() else Color.WHITE
		_autotile_updates[tile.base_terrain_type_id].append([map_coord, color])

	if not overlay_tdef.is_empty() and bool(overlay_tdef.get("isAutotiling", false)) and tile.overlay_terrain_type_id != -1:
		var color: Color = _current_palette[tile.overlay_color_index] if tile.overlay_color_index < _current_palette.size() else Color.WHITE
		_autotile_updates[tile.overlay_terrain_type_id].append([map_coord, color])

	for terrain_id in _auto_tile_layers.keys():
		_autotile_clear_cells[terrain_id].append(map_coord)


func _update_terrain_sprite(sprite: Sprite2D, tdef: Dictionary, color_index: int, variant_index: int) -> void:
	if tdef.is_empty() or bool(tdef.get("isAutotiling", false)):
		sprite.visible = false
		return
	var texture: Texture2D = SpriteResourceManager.get_texture(tdef.get("spriteKey", ""))
	if texture == null:
		sprite.visible = false
		return
	sprite.texture = texture
	sprite.modulate = _current_palette[color_index] if color_index < _current_palette.size() else Color.WHITE
	var variant_count: int = int(tdef.get("variantCount", 1))
	if variant_count > 1:
		var atlas_x: int = (variant_index % RenderingConstants.VARIANTS_PER_ROW) * RenderingConstants.SOURCE_TILE_SIZE
		var atlas_y: int = (variant_index / RenderingConstants.VARIANTS_PER_ROW) * RenderingConstants.SOURCE_TILE_SIZE
		sprite.region_enabled = true
		sprite.region_rect = Rect2(atlas_x, atlas_y, RenderingConstants.SOURCE_TILE_SIZE, RenderingConstants.SOURCE_TILE_SIZE)
	else:
		sprite.region_enabled = false
	sprite.visible = true


func _apply_autotile_batches() -> void:
	for terrain_id in _auto_tile_layers.keys():
		var layer: ModulatableTileMapLayer = _auto_tile_layers[terrain_id]
		_clear_inactive_cells(layer, terrain_id)
		_apply_autotile_updates(layer, terrain_id)


func _clear_inactive_cells(layer: ModulatableTileMapLayer, terrain_id: int) -> void:
	for cell in _autotile_clear_cells[terrain_id]:
		layer.erase_cell(cell)
		layer.clear_tile_color(cell)


func _apply_autotile_updates(layer: ModulatableTileMapLayer, terrain_id: int) -> void:
	var updates: Array = _autotile_updates[terrain_id]
	if updates.is_empty():
		return
	var cells := Array([], TYPE_VECTOR2I, "", null)
	for entry in updates:
		cells.append(entry[0])
	layer.set_cells_terrain_connect(cells, 0, 0, false)
	for entry in updates:
		layer.set_tile_color(entry[0], entry[1])


# ---------------------------------------------------------------------------
# Entity sync
# ---------------------------------------------------------------------------

func _sync_pawns(snapshot: Dictionary) -> void:
	_active_ids.clear()
	for pawn in snapshot.get("pawns", []):
		var pawn_id: int = pawn.get("id", -1)
		if pawn_id == -1:
			continue
		_active_ids[pawn_id] = true

		var is_new: bool = not _pawn_nodes.has(pawn_id)
		if is_new:
			var node: Node2D
			if pawn_scene != null:
				node = pawn_scene.instantiate()
			else:
				node = PawnView.new()
			_pawns_root.get_parent().add_child(node)
			_pawn_nodes[pawn_id] = node
			if node is PawnView:
				node.initialize_with_sprite(
					SpriteResourceManager.get_texture("character_walk"),
					SpriteResourceManager.get_texture("character_idle"),
					SpriteResourceManager.get_texture("character_axe"),
					SpriteResourceManager.get_texture("character_pickaxe"),
					SpriteResourceManager.get_texture("character_look_down"),
					SpriteResourceManager.get_texture("character_look_up")
				)

		var target_pos := Vector2(
			pawn.get("x", 0) * RenderingConstants.RENDERED_TILE_SIZE + RenderingConstants.RENDERED_TILE_SIZE * 0.5,
			pawn.get("y", 0) * RenderingConstants.RENDERED_TILE_SIZE + RenderingConstants.RENDERED_TILE_SIZE * 0.5
		)
		var node = _pawn_nodes[pawn_id]
		if node is PawnView:
			if is_new:
				var entry_pos: Vector2 = _calculate_entry_position(pawn.get("x", 0), pawn.get("y", 0))
				node.set_initial_position(entry_pos)
			node.set_target_position(target_pos)
			node.set_current_animation(pawn.get("animation", Definitions.AnimationType.IDLE))
			node.set_mood(pawn.get("mood", 0.0))
			node.set_selected(pawn_id == _selected_pawn_id)
			node.set_expression(
				pawn.get("has_expression", false),
				pawn.get("expression", Definitions.ExpressionType.THOUGHT),
				pawn.get("expression_icon_def_id", -1),
				_sim.content
			)
			var action_type: int = pawn.get("current_action_type", Definitions.ActionType.IDLE)
			var inside: bool = action_type in [
				Definitions.ActionType.USE_BUILDING,
				Definitions.ActionType.WORK,
				Definitions.ActionType.PICK_UP,
				Definitions.ActionType.DROP_OFF,
			]
			node.visible = not inside

	_ids_to_remove.clear()
	for id in _pawn_nodes.keys():
		if not _active_ids.has(id):
			_ids_to_remove.append(id)
	for id in _ids_to_remove:
		_pawn_nodes[id].queue_free()
		_pawn_nodes.erase(id)
		if _selected_pawn_id == id:
			_selected_pawn_id = -1
			if _debug_panel != null: _debug_panel.clear_selection()


func _sync_buildings(snapshot: Dictionary) -> void:
	_active_ids.clear()
	for obj in snapshot.get("buildings", []):
		var obj_id: int = obj.get("id", -1)
		if obj_id == -1:
			continue
		_active_ids[obj_id] = true

		if not _building_nodes.has(obj_id):
			var node: Node2D
			if building_scene != null:
				node = building_scene.instantiate()
			else:
				node = BuildingView.new()
			node.z_index = ZIndexConstants.BUILDINGS
			_buildings_root.get_parent().add_child(node)
			_building_nodes[obj_id] = node

			if node is BuildingView:
				var bdef: Dictionary = _sim.content.buildings.get(obj.get("building_def_id", -1), {})
				if not bdef.is_empty():
					var texture: Texture2D = SpriteResourceManager.get_texture(bdef.get("spriteKey", ""))
					if texture != null:
						node.initialize_with_sprite(texture, int(bdef.get("tileSize", 1)),
							int(bdef.get("spriteVariants", 1)), int(bdef.get("spritePhases", 1)), obj_id)

		var node = _building_nodes[obj_id]
		node.position = Vector2(
			obj.get("x", 0) * RenderingConstants.RENDERED_TILE_SIZE + RenderingConstants.RENDERED_TILE_SIZE * 0.5,
			obj.get("y", 0) * RenderingConstants.RENDERED_TILE_SIZE + RenderingConstants.RENDERED_TILE_SIZE * 0.5
		)
		if node is BuildingView:
			node.set_building_info(obj.get("name", ""), obj.get("in_use", false), obj.get("color_index", 0), _current_palette)
			node.update_sprite_phase(obj.get("max_pawn_wealth", 0))

	_ids_to_remove.clear()
	for id in _building_nodes.keys():
		if not _active_ids.has(id):
			_ids_to_remove.append(id)
	for id in _ids_to_remove:
		_building_nodes[id].queue_free()
		_building_nodes.erase(id)
		if _selected_building_id == id:
			_selected_building_id = -1
			if _debug_panel != null: _debug_panel.clear_selection()


# ---------------------------------------------------------------------------
# Info panels
# ---------------------------------------------------------------------------

func _update_info_panel(snapshot: Dictionary) -> void:
	if _debug_panel == null or _selected_pawn_id == -1:
		return
	var pawn_snap: Dictionary = {}
	for p in snapshot.get("pawns", []):
		if p.get("id", -1) == _selected_pawn_id:
			pawn_snap = p
			break
	if pawn_snap.is_empty():
		_selected_pawn_id = -1
		_debug_panel.clear_selection()
		return
	_debug_panel.show_pawn(pawn_snap, _sim)


func _update_building_info_panel(snapshot: Dictionary) -> void:
	if _debug_panel == null or _selected_building_id == -1:
		return
	var b_snap: Dictionary = {}
	for b in snapshot.get("buildings", []):
		if b.get("id", -1) == _selected_building_id:
			b_snap = b
			break
	if b_snap.is_empty():
		_selected_building_id = -1
		_debug_panel.clear_selection()
		return
	_debug_panel.show_building(b_snap, _sim)


func _update_time_display(snapshot: Dictionary) -> void:
	if _debug_panel == null:
		return
	_debug_panel.update_time(snapshot.get("time", {}))
	if _selected_pawn_id == -1 and _selected_building_id == -1:
		_debug_panel.clear_selection()


func _update_speed_display() -> void:
	if _debug_panel == null:
		return
	var speed_text: String
	match _sim_speed:
		_SimSpeed.PAUSED:   speed_text = "PAUSED"
		_SimSpeed.NORMAL:   speed_text = "1x"
		_SimSpeed.FAST_4X:  speed_text = "4x"
		_SimSpeed.FAST_16X: speed_text = "16x"
		_SimSpeed.FAST_64X: speed_text = "64x"
		_:                  speed_text = "1x"
	_debug_panel.update_speed(speed_text)


func _update_night_overlay(snapshot: Dictionary) -> void:
	var time: Dictionary = snapshot.get("time", {})
	var day_fraction: float = time.get("day_fraction", 0.0)

	if _crt_shader_controller != null:
		_crt_shader_controller.set_time_of_day(day_fraction)

	if _shadow_shader_mat != null:
		var sun_angle: float = (day_fraction - 0.5) * 180.0
		_shadow_shader_mat.set_shader_parameter("sun_angle", sun_angle + 90.0)
		var sun_elevation: float = maxf(0.0, cos(sun_angle * PI / 180.0))
		var shadow_distance: float = 16.0 * (1.0 + (1.0 - sun_elevation) * 4.0)
		_shadow_shader_mat.set_shader_parameter("max_shadow_distance", shadow_distance)
		var shadow_alpha: float = 0.3 * sun_elevation * sun_elevation
		_shadow_shader_mat.set_shader_parameter("shadow_color", Color(0, 0, 0, shadow_alpha))


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

func _screen_to_tile(screen_pos: Vector2) -> Vector2i:
	return Vector2i(
		int(screen_pos.x / RenderingConstants.RENDERED_TILE_SIZE),
		int(screen_pos.y / RenderingConstants.RENDERED_TILE_SIZE)
	)


func _find_pawn_at(pos: Vector2) -> int:
	var half: float = PAWN_HITBOX_SIZE * 0.5
	for id in _pawn_nodes.keys():
		var p: Vector2 = _pawn_nodes[id].position
		if pos.x >= p.x - half and pos.x <= p.x + half and pos.y >= p.y - half and pos.y <= p.y + half:
			return id
	return -1


func _find_building_at(pos: Vector2) -> int:
	var half: float = BUILDING_HITBOX_SIZE * 0.5
	for id in _building_nodes.keys():
		var node = _building_nodes[id]
		var b_snap: Dictionary = {}
		for b in _last_snapshot.get("buildings", []):
			if b.get("id", -1) == id:
				b_snap = b
				break
		if b_snap.is_empty():
			continue
		var bdef: Dictionary = _sim.content.buildings.get(b_snap.get("building_def_id", -1), {})
		var ts: int = int(bdef.get("tileSize", 1)) if not bdef.is_empty() else 1
		var p: Vector2 = node.position
		var right_expand: float = (ts - 1) * RenderingConstants.RENDERED_TILE_SIZE + half
		var down_expand: float = (ts - 1) * RenderingConstants.RENDERED_TILE_SIZE + half
		if pos.x >= p.x - half and pos.x <= p.x + right_expand and pos.y >= p.y - half and pos.y <= p.y + down_expand:
			return id
	return -1


func _deselect_pawn() -> void:
	if _selected_pawn_id != -1 and _pawn_nodes.has(_selected_pawn_id):
		var node = _pawn_nodes[_selected_pawn_id]
		if node is PawnView:
			node.set_selected(false)


func _calculate_entry_position(tile_x: int, tile_y: int) -> Vector2:
	var ts: int = RenderingConstants.RENDERED_TILE_SIZE
	var cx: float = tile_x * ts + ts * 0.5
	var cy: float = tile_y * ts + ts * 0.5
	var on_left: bool = tile_x == 0
	var on_right: bool = tile_x == _sim.world.width - 1
	var on_top: bool = tile_y == 0
	var on_bottom: bool = tile_y == _sim.world.height - 1
	if on_left and on_top: return Vector2(cx - ts, cy - ts)
	if on_right and on_top: return Vector2(cx + ts, cy - ts)
	if on_left and on_bottom: return Vector2(cx - ts, cy + ts)
	if on_right and on_bottom: return Vector2(cx + ts, cy + ts)
	if on_left: return Vector2(cx - ts, cy)
	if on_right: return Vector2(cx + ts, cy)
	if on_top: return Vector2(cx, cy - ts)
	if on_bottom: return Vector2(cx, cy + ts)
	return Vector2(cx, cy)


static func _get_content_path() -> String:
	# Web: always read from packed resources
	if OS.has_feature("web"):
		return "res://content"
	# Desktop release: try content folder next to exe first (allows modding),
	# fall back to packed res:// if not present
	if not OS.has_feature("editor"):
		var exe_dir: String = OS.get_executable_path().get_base_dir()
		var override_path: String
		if OS.has_feature("macos"):
			override_path = exe_dir.path_join("..").path_join("Resources").path_join("content")
		else:
			override_path = exe_dir.path_join("content")
		if FileAccess.file_exists(override_path.path_join("core/buildings.json")):
			return override_path
	# Editor or no override found: use packed resources
	return "res://content"
