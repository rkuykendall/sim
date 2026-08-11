class_name MainMenu
extends Control

signal resume_requested(slot_name: String)
signal new_game_requested
signal gallery_requested
signal credits_requested
signal quit_requested

const TITLE_TEXT: String = "Paint Town"

@export var background_path: NodePath = ""
@export var title_label_path: NodePath = ""
@export var button_container_path: NodePath = ""

var _background: ColorRect = null
var _title_label: Label = null
var _button_container: VBoxContainer = null
var _content: ContentRegistry = null
var _sound_manager: SoundManager = null
var _buttons: Array[Button] = []
var _current_background_color: Color = Color(0.9, 0.9, 0.85)


func _ready() -> void:
	if not background_path.is_empty():
		_background = get_node_or_null(background_path)
	if not title_label_path.is_empty():
		_title_label = get_node_or_null(title_label_path)
	if not button_container_path.is_empty():
		_button_container = get_node_or_null(button_container_path)
	if _title_label != null:
		_title_label.text = TITLE_TEXT
	_build_buttons()


func initialize(content: ContentRegistry, sound_manager: SoundManager) -> void:
	_content = content
	_sound_manager = sound_manager


## Called by GameRoot instead of a raw show() — rebuilds the button list (so Resume's presence
## reflects whatever's on disk right now) and re-rolls the palette every time the menu becomes
## the active screen, same "pick a random palette" feel as the in-game palette cycler.
func on_shown() -> void:
	show()
	_build_buttons()
	apply_random_palette()


## Gallery mirrors this screen's current background so the two read as one continuous menu —
## see HomeScreen.set_background_color.
func get_background_color() -> Color:
	return _current_background_color


func _build_buttons() -> void:
	if _button_container == null:
		return
	for child in _button_container.get_children():
		child.queue_free()
	_buttons.clear()

	var most_recent_slot: String = _get_most_recent_save_slot()
	if not most_recent_slot.is_empty():
		var resume_btn := _create_button(
			"RESUME", 64, func() -> void: _on_resume_pressed(most_recent_slot)
		)
		_button_container.add_child(resume_btn)
		_buttons.append(resume_btn)

	var new_town_btn := _create_button(
		"NEW TOWN", 64, func() -> void: _on_action(new_game_requested)
	)
	_button_container.add_child(new_town_btn)
	_buttons.append(new_town_btn)

	var gallery_btn := _create_button("GALLERY", 44, func() -> void: _on_action(gallery_requested))
	_button_container.add_child(gallery_btn)
	_buttons.append(gallery_btn)

	var credits_btn := _create_button("CREDITS", 44, func() -> void: _on_action(credits_requested))
	_button_container.add_child(credits_btn)
	_buttons.append(credits_btn)

	if OS.has_feature("web"):
		var fullscreen_btn := _create_button("FULLSCREEN", 44, _on_fullscreen_pressed)
		_button_container.add_child(fullscreen_btn)
		_buttons.append(fullscreen_btn)
	else:
		var quit_btn := _create_button("QUIT", 44, func() -> void: _on_action(quit_requested))
		_button_container.add_child(quit_btn)
		_buttons.append(quit_btn)


func _create_button(text: String, font_size: int, on_pressed: Callable) -> Button:
	var btn := Button.new()
	btn.text = text
	btn.custom_minimum_size = Vector2(640, 48 + font_size * 2)
	btn.add_theme_font_size_override("font_size", font_size)
	btn.pressed.connect(on_pressed)
	return btn


func _on_action(requested_signal: Signal) -> void:
	if _sound_manager != null:
		_sound_manager.play_select()
	requested_signal.emit()


func _on_resume_pressed(slot_name: String) -> void:
	if _sound_manager != null:
		_sound_manager.play_select()
	resume_requested.emit(slot_name)


## Empty string if there's no save to resume — SaveFileManager already returns saves sorted
## newest first.
func _get_most_recent_save_slot() -> String:
	var saves: Array = SaveFileManager.get_all_saves()
	if saves.is_empty():
		return ""
	var most_recent: Dictionary = saves[0]
	return DefUtils.get_string(most_recent, "slot_name", "")


func _on_fullscreen_pressed() -> void:
	if _sound_manager != null:
		_sound_manager.play_click()
	var is_fullscreen := DisplayServer.window_get_mode() == DisplayServer.WINDOW_MODE_FULLSCREEN
	if is_fullscreen:
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
	else:
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)


## Picks a random content palette and washes the menu in it: a heavily lightened background
## tint, each button filled with a different palette color (cycling if there are more buttons
## than colors), and a darkened title color for contrast against the light background.
func apply_random_palette() -> void:
	if _content == null or _content.palettes.is_empty():
		return

	var palette_ids: Array = _content.palettes.keys()
	var chosen_id: int = palette_ids[randi() % palette_ids.size()]
	var palette_def: Dictionary = _content.palettes[chosen_id]
	var colors: Array[Color] = []
	colors.assign(DefUtils.get_array(palette_def, "colors", []))
	if colors.is_empty():
		return

	_current_background_color = colors[0].lightened(0.75)
	if _background != null:
		_background.color = _current_background_color

	if _title_label != null:
		_title_label.add_theme_color_override("font_color", colors[0].darkened(0.4))

	# Reversed so the "go" actions (Resume/New Town) land on the palette's cooler/greener end
	# and "stop" (Quit) lands on the warmer/redder end, instead of the other way around.
	for i in _buttons.size():
		var color: Color = colors[(_buttons.size() - 1 - i) % colors.size()]
		_style_button(_buttons[i], color)


func _style_button(btn: Button, color: Color) -> void:
	var font_color: Color = Color.BLACK if _perceived_lightness(color) > 0.6 else Color.WHITE

	var normal := StyleBoxFlat.new()
	normal.bg_color = color
	normal.set_corner_radius_all(16)
	normal.set_content_margin_all(16)

	var hover := StyleBoxFlat.new()
	hover.bg_color = color.lightened(0.2)
	hover.set_corner_radius_all(16)
	hover.set_content_margin_all(16)

	var pressed := StyleBoxFlat.new()
	pressed.bg_color = color.darkened(0.15)
	pressed.set_corner_radius_all(16)
	pressed.set_content_margin_all(16)

	btn.add_theme_stylebox_override("normal", normal)
	btn.add_theme_stylebox_override("hover", hover)
	btn.add_theme_stylebox_override("pressed", pressed)
	btn.add_theme_stylebox_override("focus", normal)
	btn.add_theme_color_override("font_color", font_color)
	btn.add_theme_color_override("font_hover_color", font_color)
	btn.add_theme_color_override("font_pressed_color", font_color)


func _perceived_lightness(color: Color) -> float:
	return 0.299 * color.r + 0.587 * color.g + 0.114 * color.b
