class_name GameRoot
extends Node2D

enum _SimSpeed { PAUSED = 0, NORMAL = 1, FAST_4X = 4, FAST_16X = 16, FAST_64X = 64 }
enum _AppScreen { MAIN_MENU, GALLERY, CREDITS, GAME }

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------
const MAX_TICKS_PER_FRAME: int = 100
const AUTOSAVE_INTERVAL: float = 60.0
const PAWN_HITBOX_SIZE: float = 24.0
const BUILDING_HITBOX_SIZE: float = 28.0
# Fraction of a shadow theme's own playback spent glowing warm at each end (its "dawn"/"dusk").
const THEME_SUNRISE_LEN: float = 0.15
# Fraction of a no-shadow theme's (e.g. Gymnopédie) playback spent easing in/out of full dim,
# rather than snapping abruptly when it starts/ends.
const THEME_NIGHT_FADE: float = 0.05
const MENU_MUSIC_PATH: String = "res://music/tracks/wanderers_tale.ogg"
const CREDITS_MUSIC_PATH: String = "res://music/classics/gymnopedie_no_1.ogg"

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
@export var weather_layer_path: NodePath = ""
@export var camera_path: NodePath = ""
@export var ui_layer_path: NodePath = ""
@export var toolbar_path: NodePath = ""
@export var music_manager_path: NodePath = ""
@export var sound_manager_path: NodePath = ""
@export var main_menu_path: NodePath = ""
@export var gallery_path: NodePath = ""
@export var credits_path: NodePath = ""
@export var loading_screen_path: NodePath = ""

# ---------------------------------------------------------------------------
# State
# ---------------------------------------------------------------------------
var _sim: Simulation = null
var _content: ContentRegistry = null
var _user_settings: UserSettings = null
var _current_screen: _AppScreen = _AppScreen.MAIN_MENU
var _current_save_slot: String = ""
# True once the player has actually changed something (painted/deleted terrain, placed a
# building) since the current game started or was last saved — an untouched game shouldn't
# create/overwrite a save file just because an autosave interval or exit-to-menu happened.
var _world_dirty: bool = false
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
var _has_occluders: bool = false
var _crt_shader_controller: CRTShaderController = null
var _weather_controller: WeatherEffectController = null
var _camera: CameraController = null
var _ui_layer: CanvasLayer = null
var _toolbar: BuildToolbar = null
var _music_manager: MusicManager = null
var _sound_manager: SoundManager = null
var _main_menu: MainMenu = null
var _gallery: HomeScreen = null
var _credits: CreditsScreen = null
var _loading_screen: Control = null
var _debug_panel: DebugPanel = null

# Entity nodes
var _pawn_nodes: Dictionary[int, Node2D] = {}
var _building_nodes: Dictionary[int, Node2D] = {}

# Autotile layers
var _auto_tile_layers: Dictionary[int, ModulatableTileMapLayer] = {}
var _tile_sprites: Dictionary[Vector2i, Array] = {}  # value: [Sprite2D base, Sprite2D overlay]
var _autotile_updates: Dictionary[int, Array] = {}  # value: Array of [Vector2i, Color]
var _autotile_clear_cells: Dictionary[int, Array] = {}  # value: Array of Vector2i

# Reusable collections
var _active_ids: Dictionary = {}
var _ids_to_remove: Array = []


func _ready() -> void:
	z_index = ZIndexConstants.UI_OVERLAY

	_user_settings = UserSettings.load()
	_apply_fullscreen()

	var mod_path: String = _get_mod_content_path()
	print("[GameRoot] Mod content path: %s" % (mod_path if not mod_path.is_empty() else "(none)"))
	_content = ContentLoader.load_all(mod_path)
	_tick_delta = 1.0 / Simulation.TICK_RATE

	_pawns_root = get_node(pawns_root_path)
	_buildings_root = get_node(buildings_root_path)
	_tiles_root = get_node(tiles_root_path)

	if not shadow_rect_path.is_empty():
		_shadow_rect = get_node_or_null(shadow_rect_path)
		if _shadow_rect != null:
			var shader: Shader = (
				load("res://shaders/sdf_shadows.gdshader")
				if ResourceLoader.exists("res://shaders/sdf_shadows.gdshader")
				else null
			)
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
	if not weather_layer_path.is_empty():
		_weather_controller = get_node_or_null(weather_layer_path)
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

	SaveFileManager.migrate_legacy_saves()

	if not main_menu_path.is_empty():
		_main_menu = get_node_or_null(main_menu_path)
		if _main_menu != null:
			_main_menu.resume_requested.connect(_on_load_game_requested)
			_main_menu.new_game_requested.connect(_on_new_game_requested)
			_main_menu.gallery_requested.connect(_show_gallery)
			_main_menu.credits_requested.connect(_show_credits)
			_main_menu.quit_requested.connect(_on_quit_requested)
			_main_menu.initialize(_content, _sound_manager)

	if not gallery_path.is_empty():
		_gallery = get_node_or_null(gallery_path)
		if _gallery != null:
			_gallery.load_game_requested.connect(_on_load_game_requested)
			_gallery.back_requested.connect(_show_main_menu)
			_gallery.initialize(_content, _sound_manager)

	if not credits_path.is_empty():
		_credits = get_node_or_null(credits_path)
		if _credits != null:
			_credits.back_requested.connect(_show_main_menu)

	if not loading_screen_path.is_empty():
		_loading_screen = get_node_or_null(loading_screen_path)

	# Deferred: GameRoot is declared before MusicManager under Main, so MusicManager's own
	# _ready() (which sets up its AudioStreamPlayer) hasn't run yet at this point — a direct
	# call here would silently no-op and boot with no menu music (see MusicManager.play_track).
	_show_main_menu.call_deferred()


func _apply_fullscreen() -> void:
	if _user_settings.fullscreen:
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_EXCLUSIVE_FULLSCREEN)
	else:
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)


## Hides every screen/game layer except whatever the caller shows next — the shared teardown
## behind all of _show_main_menu/_show_gallery/_show_credits/_show_game.
func _hide_all_screens() -> void:
	if _toolbar != null:
		_toolbar.hide()
	if _debug_panel != null:
		_debug_panel.set_debug_mode(false)
	if _pawns_root != null:
		_pawns_root.hide()
	if _buildings_root != null:
		_buildings_root.hide()
	if _tiles_root != null:
		_tiles_root.hide()
	if _main_menu != null:
		_main_menu.hide()
	if _gallery != null:
		_gallery.hide()
	if _credits != null:
		_credits.hide()


func _show_main_menu() -> void:
	_current_screen = _AppScreen.MAIN_MENU
	_hide_all_screens()
	if _music_manager != null:
		_music_manager.play_track(MENU_MUSIC_PATH)
	if _main_menu != null:
		_main_menu.on_shown()


func _show_gallery() -> void:
	_current_screen = _AppScreen.GALLERY
	_hide_all_screens()
	if _music_manager != null:
		_music_manager.play_track(MENU_MUSIC_PATH)
	if _gallery != null:
		if _main_menu != null:
			_gallery.set_background_color(_main_menu.get_background_color())
		_gallery.show()
		_gallery.refresh_saves_list()


func _show_credits() -> void:
	_current_screen = _AppScreen.CREDITS
	_hide_all_screens()
	if _music_manager != null:
		_music_manager.play_track(CREDITS_MUSIC_PATH)
	if _credits != null:
		_credits.show()
		_credits.play_scroll()


func _show_game() -> void:
	_current_screen = _AppScreen.GAME
	_hide_all_screens()
	if _toolbar != null:
		_toolbar.show()
	if _pawns_root != null:
		_pawns_root.show()
	if _buildings_root != null:
		_buildings_root.show()
	if _tiles_root != null:
		_tiles_root.show()
	_update_speed_display()


func _on_new_game_requested() -> void:
	_current_save_slot = SaveFileManager.generate_save_name()
	_sim = Simulation.new(_content)
	_initialize_game_world()
	_show_game()
	print("[GameRoot] Started new game: %s" % _current_save_slot)


func _on_load_game_requested(slot_name: String) -> void:
	# Loading (JSON parse of a multi-MB save + rebuilding every tile/pawn/building node) is
	# synchronous and can take a few seconds — not worth the complexity of threading it, but
	# without yielding a couple of frames first, the loading screen would never actually get
	# drawn before the freeze starts, making the whole app look hung instead of "loading."
	if _loading_screen != null:
		_loading_screen.show()
	await get_tree().process_frame
	await get_tree().process_frame

	var loaded_sim: Simulation = SaveFileManager.load_save(slot_name, _content)
	if loaded_sim == null:
		push_error("[GameRoot] Failed to load save: %s" % slot_name)
		if _loading_screen != null:
			_loading_screen.hide()
		return
	_current_save_slot = slot_name
	_sim = loaded_sim
	_initialize_game_world()
	_show_game()
	if _loading_screen != null:
		_loading_screen.hide()
	print("[GameRoot] Loaded game: %s" % slot_name)


func _on_quit_requested() -> void:
	get_tree().quit()


func _on_home_button_pressed() -> void:
	_return_to_home()


func _on_music_finished() -> void:
	if _sim != null and _sim.theme_system != null:
		_sim.theme_system.on_music_finished(_sim)


func _return_to_home() -> void:
	if _sim != null and not _current_save_slot.is_empty() and _world_dirty:
		SaveFileManager.write_save(_current_save_slot, _sim, _current_save_slot)
	_show_main_menu()


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
	_world_dirty = false


func _clear_all_nodes() -> void:
	for node: Node2D in _pawn_nodes.values():
		node.queue_free()
	_pawn_nodes.clear()
	for node: Node2D in _building_nodes.values():
		node.queue_free()
	_building_nodes.clear()
	for sprites: Array in _tile_sprites.values():
		var base_sprite: Sprite2D = sprites[0]
		var overlay_sprite: Sprite2D = sprites[1]
		base_sprite.queue_free()
		overlay_sprite.queue_free()
	_tile_sprites.clear()
	for layer: ModulatableTileMapLayer in _auto_tile_layers.values():
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
			print("[Day %d] Pawns: %d" % [_sim.time.day, _sim.entities.all_pawns().size()])

	if ticks_processed >= MAX_TICKS_PER_FRAME and _accumulator >= _tick_delta:
		_accumulator = 0.0
		push_warning("GameRoot: tick cap reached, resetting accumulator")

	var snapshot: Dictionary = _sim.create_render_snapshot()
	_last_snapshot = snapshot

	# Palette change
	if _sim.selected_palette_id != _current_palette_id:
		_current_palette = snapshot.get("palette", [])
		_current_palette_id = _sim.selected_palette_id
		if _toolbar != null:
			_toolbar.update_palette(_current_palette)
		var all_tiles: Array[Vector2i] = []
		for x in _sim.world.width:
			for y in _sim.world.height:
				all_tiles.append(Vector2i(x, y))
		_sync_tiles(all_tiles)

	# Music
	if _music_manager != null:
		_music_manager.update_music_state(DefUtils.get_dict(snapshot, "theme", {}))

	# Autosave
	_time_since_autosave += delta
	if _time_since_autosave >= AUTOSAVE_INTERVAL:
		_perform_autosave()

	_sync_pawns(snapshot)
	_sync_buildings(snapshot)
	_update_info_panel(snapshot)
	_update_building_info_panel(snapshot)
	_update_time_display(snapshot)
	_update_theme_visuals(snapshot)
	_update_shadow_rect_bounds()

	var mouse_pos: Vector2 = get_local_mouse_position()
	_hovered_tile = _screen_to_tile(mouse_pos)

	if _debug_mode or BuildToolMode.current_mode != BuildToolMode.Mode.SELECT:
		queue_redraw()


func _perform_autosave() -> void:
	if _sim == null or _current_save_slot.is_empty():
		return
	# Always reset the timer, even when skipping the write — otherwise an untouched game would
	# re-check (and re-skip) every single frame once past the interval instead of waiting another
	# full interval.
	_time_since_autosave = 0.0
	if not _world_dirty:
		return
	SaveFileManager.write_save(_current_save_slot, _sim, _current_save_slot)
	_world_dirty = false
	print("[GameRoot] Autosaved: %s" % _current_save_slot)


# ---------------------------------------------------------------------------
# Input
# ---------------------------------------------------------------------------


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey:
		var key_event: InputEventKey = event
		if not key_event.pressed:
			return
		# F11: Toggle fullscreen (any screen)
		if key_event.keycode == KEY_F11:
			_user_settings.fullscreen = not _user_settings.fullscreen
			_user_settings.save()
			_apply_fullscreen()
			return

		# Escape: back out one level (game -> main menu; gallery/credits -> main menu)
		if key_event.keycode == KEY_ESCAPE:
			if _current_screen == _AppScreen.GAME:
				_return_to_home()
				return
			if _current_screen == _AppScreen.GALLERY or _current_screen == _AppScreen.CREDITS:
				_show_main_menu()
				return

		if _current_screen != _AppScreen.GAME:
			return

		match key_event.keycode:
			KEY_F3:
				_debug_mode = not _debug_mode
				if _toolbar != null:
					_toolbar.set_debug_mode(_debug_mode)
				if _debug_panel != null:
					_debug_panel.set_debug_mode(_debug_mode)
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
		var mb_event: InputEventMouseButton = event
		var local_pos: Vector2 = get_local_mouse_position()
		var tile_coord: Vector2i = _screen_to_tile(local_pos)

		if mb_event.button_index == MOUSE_BUTTON_LEFT:
			if mb_event.pressed:
				_handle_left_press(tile_coord)
			else:
				_handle_left_release()
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
			tiles_to_update = _sim.paint_terrain(
				tile_coord,
				BuildToolMode.selected_terrain_def_id,
				BuildToolMode.selected_color_index
			)
			if _sound_manager != null:
				_sound_manager.play_paint()
		else:
			tiles_to_update = _sim.delete_at_tile(tile_coord)
			if _sound_manager != null:
				_sound_manager.play_delete()
		_sync_edited_tiles(tiles_to_update)
		return

	if mode == BuildToolMode.Mode.FILL_SQUARE or mode == BuildToolMode.Mode.OUTLINE_SQUARE:
		_brush_drag_start = tile_coord
		_brush_drag_current = tile_coord
		queue_redraw()
		return

	if mode == BuildToolMode.Mode.FLOOD_FILL:
		var tiles_to_update: Array[Vector2i]
		if BuildToolMode.selected_terrain_def_id != -1:
			tiles_to_update = _sim.flood_fill(
				tile_coord,
				BuildToolMode.selected_terrain_def_id,
				BuildToolMode.selected_color_index
			)
			if _sound_manager != null:
				_sound_manager.play_paint()
		else:
			tiles_to_update = _sim.flood_delete(tile_coord)
			if _sound_manager != null:
				_sound_manager.play_delete()
		_sync_edited_tiles(tiles_to_update)
		return

	if mode == BuildToolMode.Mode.PLACE_BUILDING and BuildToolMode.selected_building_def_id != -1:
		var result: int = _sim.create_building(
			BuildToolMode.selected_building_def_id, tile_coord, BuildToolMode.selected_color_index
		)
		if result != -1:
			_world_dirty = true
			if _sound_manager != null:
				_sound_manager.play_build()
		return

	# Selection / click — check pawn or building
	var pawn_id: int = _find_pawn_at(get_local_mouse_position())
	if pawn_id != -1:
		_deselect_pawn()
		_selected_pawn_id = pawn_id
		_selected_building_id = -1
		var pawn_node: Node2D = _pawn_nodes.get(pawn_id)
		if pawn_node is PawnView:
			var view: PawnView = pawn_node
			view.set_selected(true)
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
	if _debug_panel != null:
		_debug_panel.clear_selection()


func _handle_left_release() -> void:
	_is_painting_terrain = false
	_last_painted_tile = Vector2i(-1, -1)

	var mode: BuildToolMode.Mode = BuildToolMode.current_mode
	if (
		(mode == BuildToolMode.Mode.FILL_SQUARE or mode == BuildToolMode.Mode.OUTLINE_SQUARE)
		and _brush_drag_start != Vector2i(-1, -1)
	):
		if mode == BuildToolMode.Mode.FILL_SQUARE:
			if BuildToolMode.selected_terrain_def_id != -1:
				var painted: Array[Vector2i] = _sim.paint_rectangle(
					_brush_drag_start,
					_brush_drag_current,
					BuildToolMode.selected_terrain_def_id,
					BuildToolMode.selected_color_index
				)
				_sync_edited_tiles(_sim.get_tiles_with_neighbors(painted))
				if _sound_manager != null:
					_sound_manager.play_paint()
			else:
				_sync_edited_tiles(_sim.delete_rectangle(_brush_drag_start, _brush_drag_current))
				if _sound_manager != null:
					_sound_manager.play_delete()
		else:
			if BuildToolMode.selected_terrain_def_id != -1:
				var painted: Array[Vector2i] = _sim.paint_rectangle_outline(
					_brush_drag_start,
					_brush_drag_current,
					BuildToolMode.selected_terrain_def_id,
					BuildToolMode.selected_color_index
				)
				_sync_edited_tiles(_sim.get_tiles_with_neighbors(painted))
				if _sound_manager != null:
					_sound_manager.play_paint()
			else:
				_sync_edited_tiles(
					_sim.delete_rectangle_outline(_brush_drag_start, _brush_drag_current)
				)
				if _sound_manager != null:
					_sound_manager.play_delete()

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
			tiles_to_update = _sim.paint_terrain(
				tile_coord,
				BuildToolMode.selected_terrain_def_id,
				BuildToolMode.selected_color_index
			)
			if _sound_manager != null:
				_sound_manager.play_paint_tick()
		else:
			tiles_to_update = _sim.delete_at_tile(tile_coord)
			if _sound_manager != null:
				_sound_manager.play_paint_tick()
		_sync_edited_tiles(tiles_to_update)
		return

	var mode: BuildToolMode.Mode = BuildToolMode.current_mode
	if (
		(mode == BuildToolMode.Mode.FILL_SQUARE or mode == BuildToolMode.Mode.OUTLINE_SQUARE)
		and _brush_drag_start != Vector2i(-1, -1)
	):
		_brush_drag_current = _screen_to_tile(get_local_mouse_position())
		queue_redraw()


# ---------------------------------------------------------------------------
# Drawing
# ---------------------------------------------------------------------------


func _draw() -> void:
	if (
		_hovered_tile != Vector2i(-1, -1)
		and BuildToolMode.current_mode != BuildToolMode.Mode.SELECT
	):
		_draw_hover_preview(_hovered_tile)

	if not _debug_mode:
		return

	var half: float = PAWN_HITBOX_SIZE * 0.5
	for node: Node2D in _pawn_nodes.values():
		draw_rect(
			Rect2(
				node.position.x - half, node.position.y - half, PAWN_HITBOX_SIZE, PAWN_HITBOX_SIZE
			),
			Color.MAGENTA,
			false,
			2.0
		)

	var buildings_snap: Array = DefUtils.get_array(_last_snapshot, "buildings", [])
	for building_id: int in _building_nodes.keys():
		var b_snap: Dictionary = {}
		for b: Dictionary in buildings_snap:
			if DefUtils.get_int(b, "id", -1) == building_id:
				b_snap = b
				break
		if not b_snap.is_empty() and _sim != null:
			var bdef: Dictionary = _sim.content.buildings.get(
				DefUtils.get_int(b_snap, "building_def_id", -1), {}
			)
			if not bdef.is_empty():
				var ts: int = DefUtils.get_int(bdef, "tileSize", 1)
				var occupied: Array[Vector2i] = BuildingUtilities.get_occupied_tiles(
					Vector2i(DefUtils.get_int(b_snap, "x", 0), DefUtils.get_int(b_snap, "y", 0)), ts
				)
				for tile: Vector2i in occupied:
					draw_rect(
						Rect2(
							tile.x * RenderingConstants.RENDERED_TILE_SIZE,
							tile.y * RenderingConstants.RENDERED_TILE_SIZE,
							RenderingConstants.RENDERED_TILE_SIZE,
							RenderingConstants.RENDERED_TILE_SIZE
						),
						Color.CYAN,
						false,
						2.0
					)

	var pawns_snap: Array = DefUtils.get_array(_last_snapshot, "pawns", [])
	for pawn_snap: Dictionary in pawns_snap:
		var path: Array[Dictionary] = []
		path.assign(DefUtils.get_array(pawn_snap, "current_path", []))
		var path_idx: int = DefUtils.get_int(pawn_snap, "path_index", 0)
		for i in range(path_idx, path.size() - 1):
			var from_p := Vector2(
				(
					DefUtils.get_int(path[i], "x", 0) * RenderingConstants.RENDERED_TILE_SIZE
					+ RenderingConstants.RENDERED_TILE_SIZE * 0.5
				),
				(
					DefUtils.get_int(path[i], "y", 0) * RenderingConstants.RENDERED_TILE_SIZE
					+ RenderingConstants.RENDERED_TILE_SIZE * 0.5
				)
			)
			var to_p := Vector2(
				(
					DefUtils.get_int(path[i + 1], "x", 0) * RenderingConstants.RENDERED_TILE_SIZE
					+ RenderingConstants.RENDERED_TILE_SIZE * 0.5
				),
				(
					DefUtils.get_int(path[i + 1], "y", 0) * RenderingConstants.RENDERED_TILE_SIZE
					+ RenderingConstants.RENDERED_TILE_SIZE * 0.5
				)
			)
			draw_line(from_p, to_p, Color.ORANGE, 2.0)

		var target: Vector2i = pawn_snap.get("target_tile", Vector2i(-1, -1))
		if target != Vector2i(-1, -1):
			draw_rect(
				Rect2(
					target.x * RenderingConstants.RENDERED_TILE_SIZE + 4,
					target.y * RenderingConstants.RENDERED_TILE_SIZE + 4,
					RenderingConstants.RENDERED_TILE_SIZE - 8,
					RenderingConstants.RENDERED_TILE_SIZE - 8
				),
				Color(1, 0.5, 0, 0.3),
				true
			)
			draw_rect(
				Rect2(
					target.x * RenderingConstants.RENDERED_TILE_SIZE + 4,
					target.y * RenderingConstants.RENDERED_TILE_SIZE + 4,
					RenderingConstants.RENDERED_TILE_SIZE - 8,
					RenderingConstants.RENDERED_TILE_SIZE - 8
				),
				Color.ORANGE,
				false,
				2.0
			)

	draw_circle(get_local_mouse_position(), 5.0, Color.YELLOW)


func _draw_hover_preview(coord: Vector2i) -> void:
	var ts: int = RenderingConstants.RENDERED_TILE_SIZE
	var rect := Rect2(coord.x * ts, coord.y * ts, ts, ts)
	var mode: BuildToolMode.Mode = BuildToolMode.current_mode

	if mode == BuildToolMode.Mode.PLACE_TERRAIN or mode == BuildToolMode.Mode.FLOOD_FILL:
		if BuildToolMode.selected_terrain_def_id != -1:
			var color: Color = (
				_current_palette[BuildToolMode.selected_color_index]
				if not _current_palette.is_empty()
				else Color.WHITE
			)
			color.a = 0.5
			draw_rect(rect, color, true)
		else:
			draw_rect(rect, Color(1, 0, 0, 0.3), true)
		draw_rect(rect, Color.WHITE, false, 2.0)

	elif (
		(mode == BuildToolMode.Mode.FILL_SQUARE or mode == BuildToolMode.Mode.OUTLINE_SQUARE)
		and _brush_drag_start != Vector2i(-1, -1)
	):
		var x0: int = mini(_brush_drag_start.x, _brush_drag_current.x)
		var x1: int = maxi(_brush_drag_start.x, _brush_drag_current.x)
		var y0: int = mini(_brush_drag_start.y, _brush_drag_current.y)
		var y1: int = maxi(_brush_drag_start.y, _brush_drag_current.y)
		var color: Color
		if BuildToolMode.selected_terrain_def_id != -1:
			color = (
				_current_palette[BuildToolMode.selected_color_index]
				if not _current_palette.is_empty()
				else Color.WHITE
			)
			color.a = 0.3
		else:
			color = Color(1, 0, 0, 0.3)
		var preview_rect := Rect2(x0 * ts, y0 * ts, (x1 - x0 + 1) * ts, (y1 - y0 + 1) * ts)
		if mode == BuildToolMode.Mode.FILL_SQUARE:
			draw_rect(preview_rect, color, true)
		draw_rect(preview_rect, Color.WHITE, false, 2.0)

	elif (
		mode == BuildToolMode.Mode.PLACE_BUILDING
		and BuildToolMode.selected_building_def_id != -1
		and _sim != null
	):
		var bdef: Dictionary = _sim.content.buildings.get(
			BuildToolMode.selected_building_def_id, {}
		)
		if not bdef.is_empty():
			var building_tile_size: int = DefUtils.get_int(bdef, "tileSize", 1)
			var occupied: Array[Vector2i] = BuildingUtilities.get_occupied_tiles(
				coord, building_tile_size
			)
			var color: Color = (
				_current_palette[BuildToolMode.selected_color_index]
				if not _current_palette.is_empty()
				else Color.WHITE
			)
			color.a = 0.5
			for tile: Vector2i in occupied:
				var tile_rect := Rect2(tile.x * ts, tile.y * ts, ts, ts)
				draw_rect(tile_rect, color, true)
				draw_rect(tile_rect, Color.WHITE, false, 2.0)


# ---------------------------------------------------------------------------
# Tile rendering
# ---------------------------------------------------------------------------


func _initialize_auto_tile_layers() -> void:
	if _sim == null:
		return
	for terrain_id: int in _sim.content.terrains.keys():
		var tdef: Dictionary = _sim.content.terrains[terrain_id]
		if not DefUtils.get_bool(tdef, "isAutotiling", false):
			continue
		var sprite_key: String = DefUtils.get_string(tdef, "spriteKey", "")
		var texture: Texture2D = SpriteResourceManager.get_texture(sprite_key)
		if texture == null:
			push_error("GameRoot: no texture for autotile terrain %d" % terrain_id)
			continue
		var blocks_light: bool = DefUtils.get_bool(tdef, "blocksLight", false)
		var layer := ModulatableTileMapLayer.new()
		layer.name = "%sTileMapLayer" % sprite_key
		layer.tile_set = AutoTileSetBuilder.create_auto_tile_set(texture, sprite_key, blocks_light)
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
			tile_node.position = Vector2(
				x * RenderingConstants.RENDERED_TILE_SIZE, y * RenderingConstants.RENDERED_TILE_SIZE
			)
			tile_node.name = "Tile_%d_%d" % [x, y]
			tile_node.z_index = ZIndexConstants.TILE_NODES
			_tiles_root.add_child(tile_node)

			var base_sprite := Sprite2D.new()
			base_sprite.name = "BaseTileSprite"
			base_sprite.position = Vector2(
				RenderingConstants.RENDERED_TILE_SIZE * 0.5,
				RenderingConstants.RENDERED_TILE_SIZE * 0.5
			)
			base_sprite.centered = true
			base_sprite.visible = false
			base_sprite.scale = Vector2(
				RenderingConstants.SPRITE_SCALE, RenderingConstants.SPRITE_SCALE
			)
			base_sprite.z_index = -1
			tile_node.add_child(base_sprite)

			var overlay_sprite := Sprite2D.new()
			overlay_sprite.name = "OverlayTileSprite"
			overlay_sprite.position = Vector2(
				RenderingConstants.RENDERED_TILE_SIZE * 0.5,
				RenderingConstants.RENDERED_TILE_SIZE * 0.5
			)
			overlay_sprite.centered = true
			overlay_sprite.visible = false
			overlay_sprite.scale = Vector2(
				RenderingConstants.SPRITE_SCALE, RenderingConstants.SPRITE_SCALE
			)
			overlay_sprite.z_index = 0
			tile_node.add_child(overlay_sprite)

			_tile_sprites[coord] = [base_sprite, overlay_sprite]


## Wraps _sync_tiles for the handful of call sites that represent an actual player edit (paint,
## delete, fill, flood) — as opposed to _sync_tiles' other callers (initial world load, palette
## re-render), which redraw tiles without the world having actually changed.
func _sync_edited_tiles(coords: Array[Vector2i]) -> void:
	if not coords.is_empty():
		_world_dirty = true
	_sync_tiles(coords)


func _sync_tiles(coords: Array) -> void:
	_prepare_autotile_batches()
	for coord: Vector2i in coords:
		_sync_single_tile(coord)
	_apply_autotile_batches()
	_recompute_has_occluders()


func _recompute_has_occluders() -> void:
	var found := false
	for x in _sim.world.width:
		for y in _sim.world.height:
			if _sim.world.get_tile_xy(x, y).blocks_light:
				found = true
				break
		if found:
			break

	_has_occluders = found
	# Actual shadow_rect.visible assignment happens every frame in _update_theme_visuals,
	# which also factors in has_shadows — no need to set it here too.


func _prepare_autotile_batches() -> void:
	for terrain_id: int in _auto_tile_layers.keys():
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
	_update_terrain_sprite(
		overlay_sprite, overlay_tdef, tile.overlay_color_index, tile.overlay_variant_index
	)


func _process_autotile_layers(
	tile: World.Tile, base_tdef: Dictionary, overlay_tdef: Dictionary, map_coord: Vector2i
) -> void:
	if not base_tdef.is_empty() and DefUtils.get_bool(base_tdef, "isAutotiling", false):
		var color: Color = (
			_current_palette[tile.color_index]
			if tile.color_index < _current_palette.size()
			else Color.WHITE
		)
		_autotile_updates[tile.base_terrain_type_id].append([map_coord, color])

	if (
		not overlay_tdef.is_empty()
		and DefUtils.get_bool(overlay_tdef, "isAutotiling", false)
		and tile.overlay_terrain_type_id != -1
	):
		var color: Color = (
			_current_palette[tile.overlay_color_index]
			if tile.overlay_color_index < _current_palette.size()
			else Color.WHITE
		)
		_autotile_updates[tile.overlay_terrain_type_id].append([map_coord, color])

	for terrain_id: int in _auto_tile_layers.keys():
		_autotile_clear_cells[terrain_id].append(map_coord)


func _update_terrain_sprite(
	sprite: Sprite2D, tdef: Dictionary, color_index: int, variant_index: int
) -> void:
	if tdef.is_empty() or DefUtils.get_bool(tdef, "isAutotiling", false):
		sprite.visible = false
		return
	var texture: Texture2D = SpriteResourceManager.get_texture(
		DefUtils.get_string(tdef, "spriteKey", "")
	)
	if texture == null:
		sprite.visible = false
		return
	sprite.texture = texture
	sprite.modulate = (
		_current_palette[color_index] if color_index < _current_palette.size() else Color.WHITE
	)
	var variant_count: int = DefUtils.get_int(tdef, "variantCount", 1)
	if variant_count > 1:
		var atlas_x: int = (
			(variant_index % RenderingConstants.VARIANTS_PER_ROW)
			* RenderingConstants.SOURCE_TILE_SIZE
		)
		var atlas_y: int = (
			(variant_index / RenderingConstants.VARIANTS_PER_ROW)
			* RenderingConstants.SOURCE_TILE_SIZE
		)
		sprite.region_enabled = true
		sprite.region_rect = Rect2(
			atlas_x,
			atlas_y,
			RenderingConstants.SOURCE_TILE_SIZE,
			RenderingConstants.SOURCE_TILE_SIZE
		)
	else:
		sprite.region_enabled = false
	sprite.visible = true


func _apply_autotile_batches() -> void:
	for terrain_id: int in _auto_tile_layers.keys():
		var layer: ModulatableTileMapLayer = _auto_tile_layers[terrain_id]
		_clear_inactive_cells(layer, terrain_id)
		_apply_autotile_updates(layer, terrain_id)


func _clear_inactive_cells(layer: ModulatableTileMapLayer, terrain_id: int) -> void:
	for cell: Vector2i in _autotile_clear_cells[terrain_id]:
		layer.erase_cell(cell)
		layer.clear_tile_color(cell)


func _apply_autotile_updates(layer: ModulatableTileMapLayer, terrain_id: int) -> void:
	var updates: Array = _autotile_updates[terrain_id]
	if updates.is_empty():
		return
	var cells := Array([], TYPE_VECTOR2I, "", null)
	for entry: Array in updates:
		cells.append(entry[0])
	layer.set_cells_terrain_connect(cells, 0, 0, false)
	for entry: Array in updates:
		var map_coord: Vector2i = entry[0]
		var color: Color = entry[1]
		layer.set_tile_color(map_coord, color)


# ---------------------------------------------------------------------------
# Entity sync
# ---------------------------------------------------------------------------


func _sync_pawns(snapshot: Dictionary) -> void:
	_active_ids.clear()

	# One tile-step's real-world duration: sim-tick time, scaled down by the current
	# fast-forward multiplier so pawns visually keep pace with tick processing.
	var move_ticks_per_tile: int = snapshot.get("move_ticks_per_tile", 10)
	var tick_rate: int = snapshot.get("tick_rate", 20)
	var speed_multiplier: int = maxi(int(_sim_speed), 1)
	var move_duration: float = (
		(float(move_ticks_per_tile) / float(tick_rate)) / float(speed_multiplier)
	)

	for pawn: Dictionary in DefUtils.get_array(snapshot, "pawns", []):
		var pawn_id: int = DefUtils.get_int(pawn, "id", -1)
		if pawn_id == -1:
			continue
		_active_ids[pawn_id] = true

		var is_new: bool = not _pawn_nodes.has(pawn_id)
		if is_new:
			var new_node: Node2D
			if pawn_scene != null:
				new_node = pawn_scene.instantiate()
			else:
				new_node = PawnView.new()
			_pawns_root.get_parent().add_child(new_node)
			_pawn_nodes[pawn_id] = new_node
			if new_node is PawnView:
				var new_view: PawnView = new_node
				var forced_sheet_key: String = DefUtils.get_string(pawn, "forced_sheet_key", "")
				if not forced_sheet_key.is_empty():
					new_view.assign_forced_sheet(forced_sheet_key)
				else:
					new_view.initialize_with_sprite(
						SpriteResourceManager.get_texture("character_sheet")
					)

		var pawn_x: int = DefUtils.get_int(pawn, "x", 0)
		var pawn_y: int = DefUtils.get_int(pawn, "y", 0)
		var target_pos := Vector2(
			(
				pawn_x * RenderingConstants.RENDERED_TILE_SIZE
				+ RenderingConstants.RENDERED_TILE_SIZE * 0.5
			),
			(
				pawn_y * RenderingConstants.RENDERED_TILE_SIZE
				+ RenderingConstants.RENDERED_TILE_SIZE * 0.5
			)
		)
		var node: Node2D = _pawn_nodes[pawn_id]
		if node is PawnView:
			var view: PawnView = node
			if is_new:
				var entry_pos: Vector2 = _calculate_entry_position(pawn_x, pawn_y)
				view.set_initial_position(entry_pos)
			view.set_move_duration(move_duration)
			view.set_target_position(target_pos)
			var animation: int = DefUtils.get_int(pawn, "animation", Definitions.AnimationType.IDLE)
			view.set_current_animation(animation)
			view.set_carrying(DefUtils.get_string(pawn, "carrying_resource_type", ""))
			var home_visit_building_id: int = DefUtils.get_int(pawn, "home_visit_building_id", -1)
			if home_visit_building_id != -1:
				var home_visit_skin_override: String = DefUtils.get_string(
					pawn, "home_visit_skin_override", ""
				)
				var house_tex: Texture2D
				if not home_visit_skin_override.is_empty():
					house_tex = CharacterSheetPool.get_sheet_by_name(home_visit_skin_override)
				else:
					house_tex = CharacterSheetPool.get_sheet_for_house(home_visit_building_id)
				view.sync_house_sheet(house_tex)
			view.set_mood(DefUtils.get_float(pawn, "mood", 0.0))
			view.set_selected(pawn_id == _selected_pawn_id)
			view.set_expression(
				DefUtils.get_bool(pawn, "has_expression", false),
				DefUtils.get_int(pawn, "expression", Definitions.ExpressionType.THOUGHT),
				DefUtils.get_int(pawn, "expression_icon_def_id", -1),
				_sim.content
			)
			var action_type: int = DefUtils.get_int(
				pawn, "current_action_type", Definitions.ActionType.IDLE
			)
			# PICK_UP covers both hauling from a building (pawn is inside it) and harvesting
			# from open terrain (e.g. chopping trees) — only the former should hide the pawn.
			var at_building: bool = (
				(
					action_type
					in [
						Definitions.ActionType.USE_BUILDING,
						Definitions.ActionType.WORK,
						Definitions.ActionType.DROP_OFF,
					]
				)
				or (
					action_type == Definitions.ActionType.PICK_UP
					and DefUtils.get_bool(pawn, "has_building_target", false)
				)
			)
			view.visible = not at_building

	_ids_to_remove.clear()
	for id: int in _pawn_nodes.keys():
		if not _active_ids.has(id):
			_ids_to_remove.append(id)
	for id: int in _ids_to_remove:
		_pawn_nodes[id].queue_free()
		_pawn_nodes.erase(id)
		if _selected_pawn_id == id:
			_selected_pawn_id = -1
			if _debug_panel != null:
				_debug_panel.clear_selection()


func _sync_buildings(snapshot: Dictionary) -> void:
	_active_ids.clear()
	for obj: Dictionary in DefUtils.get_array(snapshot, "buildings", []):
		var obj_id: int = DefUtils.get_int(obj, "id", -1)
		if obj_id == -1:
			continue
		_active_ids[obj_id] = true

		if not _building_nodes.has(obj_id):
			var new_node: Node2D
			if building_scene != null:
				new_node = building_scene.instantiate()
			else:
				new_node = BuildingView.new()
			new_node.z_index = ZIndexConstants.BUILDINGS
			_buildings_root.get_parent().add_child(new_node)
			_building_nodes[obj_id] = new_node

			if new_node is BuildingView:
				var new_view: BuildingView = new_node
				var bdef: Dictionary = _sim.content.buildings.get(
					DefUtils.get_int(obj, "building_def_id", -1), {}
				)
				if not bdef.is_empty():
					var texture: Texture2D = SpriteResourceManager.get_texture(
						DefUtils.get_string(bdef, "spriteKey", "")
					)
					if texture != null:
						new_view.initialize_with_sprite(
							texture,
							DefUtils.get_int(bdef, "tileSize", 1),
							DefUtils.get_int(bdef, "spriteVariants", 1),
							DefUtils.get_int(bdef, "spriteColumn", 0),
							obj_id
						)

		var node: Node2D = _building_nodes[obj_id]
		var obj_x: int = DefUtils.get_int(obj, "x", 0)
		var obj_y: int = DefUtils.get_int(obj, "y", 0)
		node.position = Vector2(
			(
				obj_x * RenderingConstants.RENDERED_TILE_SIZE
				+ RenderingConstants.RENDERED_TILE_SIZE * 0.5
			),
			(
				obj_y * RenderingConstants.RENDERED_TILE_SIZE
				+ RenderingConstants.RENDERED_TILE_SIZE * 0.5
			)
		)
		if node is BuildingView:
			var view: BuildingView = node
			view.set_building_info(DefUtils.get_int(obj, "color_index", 0), _current_palette)

	_ids_to_remove.clear()
	for id: int in _building_nodes.keys():
		if not _active_ids.has(id):
			_ids_to_remove.append(id)
	for id: int in _ids_to_remove:
		_building_nodes[id].queue_free()
		_building_nodes.erase(id)
		if _selected_building_id == id:
			_selected_building_id = -1
			if _debug_panel != null:
				_debug_panel.clear_selection()


# ---------------------------------------------------------------------------
# Info panels
# ---------------------------------------------------------------------------


func _update_info_panel(snapshot: Dictionary) -> void:
	if _debug_panel == null or _selected_pawn_id == -1:
		return
	var pawn_snap: Dictionary = {}
	for p: Dictionary in DefUtils.get_array(snapshot, "pawns", []):
		if DefUtils.get_int(p, "id", -1) == _selected_pawn_id:
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
	for b: Dictionary in DefUtils.get_array(snapshot, "buildings", []):
		if DefUtils.get_int(b, "id", -1) == _selected_building_id:
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
	_debug_panel.update_time(DefUtils.get_dict(snapshot, "time", {}))
	if _selected_pawn_id == -1 and _selected_building_id == -1:
		_debug_panel.clear_selection()


func _update_speed_display() -> void:
	if _debug_panel == null:
		return
	var speed_text: String
	match _sim_speed:
		_SimSpeed.PAUSED:
			speed_text = "PAUSED"
		_SimSpeed.NORMAL:
			speed_text = "1x"
		_SimSpeed.FAST_4X:
			speed_text = "4x"
		_SimSpeed.FAST_16X:
			speed_text = "16x"
		_SimSpeed.FAST_64X:
			speed_text = "64x"
		_:
			speed_text = "1x"
	_debug_panel.update_speed(speed_text)


## Shadows and screen tint are driven by the CURRENT THEME's own playback progress (0 = just
## started, 1 = about to end) instead of a real-world clock, so every theme's "day" (if it has
## shadows) or "night" (if it doesn't) always aligns with its own song.
func _update_theme_visuals(snapshot: Dictionary) -> void:
	var theme: Dictionary = DefUtils.get_dict(snapshot, "theme", {})
	var has_shadows: bool = DefUtils.get_bool(theme, "has_shadows", true)
	var weather_tint: float = DefUtils.get_float(theme, "weather_tint", 0.0)
	var progress: float = _music_manager.get_playback_progress() if _music_manager != null else 0.0

	var visuals: Dictionary = _compute_theme_visuals(progress, has_shadows, weather_tint)
	if _crt_shader_controller != null:
		_crt_shader_controller.set_theme_visuals(
			DefUtils.get_float(visuals, "warm", 0.0),
			DefUtils.get_float(visuals, "night", 0.0),
			DefUtils.get_float(visuals, "weather_tint", 0.0)
		)

	if _weather_controller != null:
		_weather_controller.set_active_weather(DefUtils.get_string(theme, "weather_effect_key", ""))

	if _shadow_shader_mat != null:
		if has_shadows:
			# Compressed dawn (progress 0) -> noon (0.5) -> dusk (1) arc across this song.
			var sun_angle: float = (progress - 0.5) * 180.0
			_shadow_shader_mat.set_shader_parameter("sun_angle", sun_angle + 90.0)
			var sun_elevation: float = maxf(0.0, cos(sun_angle * PI / 180.0))
			var shadow_distance: float = 16.0 * (1.0 + (1.0 - sun_elevation) * 4.0)
			_shadow_shader_mat.set_shader_parameter("max_shadow_distance", shadow_distance)
			var shadow_alpha: float = 0.3 * sun_elevation * sun_elevation
			_shadow_shader_mat.set_shader_parameter("shadow_color", Color(0, 0, 0, shadow_alpha))
		else:
			_shadow_shader_mat.set_shader_parameter("shadow_color", Color(0, 0, 0, 0.0))

	if _shadow_rect != null:
		_shadow_rect.visible = _has_occluders and has_shadows


## A theme with no shadows AND no weather tint of its own (currently just Gymnopédie) means
## actual night: dim the whole screen and cool it, easing in/out across its own playback. A
## theme that supplies its own weather_tint (e.g. rain) skips night entirely — it tints
## without darkening, easing in/out the same way, independent of has_shadows.
func _compute_theme_visuals(progress: float, has_shadows: bool, weather_tint: float) -> Dictionary:
	if not has_shadows and weather_tint <= 0.0:
		var ease_in: float = smoothstep(0.0, THEME_NIGHT_FADE, progress)
		var ease_out: float = 1.0 - smoothstep(1.0 - THEME_NIGHT_FADE, 1.0, progress)
		return {"warm": 0.0, "night": minf(ease_in, ease_out), "weather_tint": 0.0}

	var warm: float = 0.0
	if has_shadows:
		warm = clampf(
			(
				_band_ease(progress, 0.0, THEME_SUNRISE_LEN)
				+ _band_ease(progress, 1.0 - THEME_SUNRISE_LEN, 1.0)
			),
			0.0,
			1.0
		)

	var tint_ease: float = minf(
		smoothstep(0.0, THEME_NIGHT_FADE, progress),
		1.0 - smoothstep(1.0 - THEME_NIGHT_FADE, 1.0, progress)
	)
	return {"warm": warm, "night": 0.0, "weather_tint": weather_tint * tint_ease}


## Ported from the old shaders/screen_effects.gdshader band_ease() — eases a value up then
## back down across [start, end], now driving warm glow bands from theme progress instead of
## a 24-hour clock.
func _band_ease(t: float, start: float, end: float) -> float:
	var band: float = end - start
	var span: float = band * 0.45
	var ease_in: float = smoothstep(start, start + span, t)
	var ease_out: float = smoothstep(end - span, end, t)
	return clampf(ease_in - ease_out, 0.0, 1.0)


func _update_shadow_rect_bounds() -> void:
	if _shadow_rect == null or _camera == null:
		return

	var view_size: Vector2 = get_viewport_rect().size / _camera.zoom
	_shadow_rect.position = _camera.position - view_size * 0.5
	_shadow_rect.size = view_size


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
	for id: int in _pawn_nodes.keys():
		var p: Vector2 = _pawn_nodes[id].position
		if (
			pos.x >= p.x - half
			and pos.x <= p.x + half
			and pos.y >= p.y - half
			and pos.y <= p.y + half
		):
			return id
	return -1


func _find_building_at(pos: Vector2) -> int:
	var half: float = BUILDING_HITBOX_SIZE * 0.5
	var buildings_snap: Array = DefUtils.get_array(_last_snapshot, "buildings", [])
	for id: int in _building_nodes.keys():
		var node: Node2D = _building_nodes[id]
		var b_snap: Dictionary = {}
		for b: Dictionary in buildings_snap:
			if DefUtils.get_int(b, "id", -1) == id:
				b_snap = b
				break
		if b_snap.is_empty():
			continue
		var bdef: Dictionary = _sim.content.buildings.get(
			DefUtils.get_int(b_snap, "building_def_id", -1), {}
		)
		var ts: int = DefUtils.get_int(bdef, "tileSize", 1) if not bdef.is_empty() else 1
		var p: Vector2 = node.position
		var right_expand: float = (ts - 1) * RenderingConstants.RENDERED_TILE_SIZE + half
		var down_expand: float = (ts - 1) * RenderingConstants.RENDERED_TILE_SIZE + half
		if (
			pos.x >= p.x - half
			and pos.x <= p.x + right_expand
			and pos.y >= p.y - half
			and pos.y <= p.y + down_expand
		):
			return id
	return -1


func _deselect_pawn() -> void:
	if _selected_pawn_id != -1 and _pawn_nodes.has(_selected_pawn_id):
		var node: Node2D = _pawn_nodes[_selected_pawn_id]
		if node is PawnView:
			var view: PawnView = node
			view.set_selected(false)


func _calculate_entry_position(tile_x: int, tile_y: int) -> Vector2:
	var ts: int = RenderingConstants.RENDERED_TILE_SIZE
	var cx: float = tile_x * ts + ts * 0.5
	var cy: float = tile_y * ts + ts * 0.5
	var on_left: bool = tile_x == 0
	var on_right: bool = tile_x == _sim.world.width - 1
	var on_top: bool = tile_y == 0
	var on_bottom: bool = tile_y == _sim.world.height - 1
	if on_left and on_top:
		return Vector2(cx - ts, cy - ts)
	if on_right and on_top:
		return Vector2(cx + ts, cy - ts)
	if on_left and on_bottom:
		return Vector2(cx - ts, cy + ts)
	if on_right and on_bottom:
		return Vector2(cx + ts, cy + ts)
	if on_left:
		return Vector2(cx - ts, cy)
	if on_right:
		return Vector2(cx + ts, cy)
	if on_top:
		return Vector2(cx, cy - ts)
	if on_bottom:
		return Vector2(cx, cy + ts)
	return Vector2(cx, cy)


static func _get_mod_content_path() -> String:
	# Web and editor: no filesystem mod path; base content loaded from res://
	if OS.has_feature("web") or OS.has_feature("editor"):
		return ""
	# Desktop release: return content/ folder next to exe for additive mod loading
	var exe_dir: String = OS.get_executable_path().get_base_dir()
	if OS.has_feature("macos"):
		return exe_dir.path_join("..").path_join("Resources").path_join("content")
	return exe_dir.path_join("content")
