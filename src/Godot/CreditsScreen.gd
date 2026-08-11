class_name CreditsScreen
extends Control

signal back_requested

## Mirrors README.md's "Credits" section — kept in sync by hand, since the README isn't a
## packaged resource in exported builds. Order matches the README (contribution order, per the
## project owner). Every entry is a plain label/text pair, no special-casing — the closing joke
## is just this credit's "label", the same way "Characters" is Pixel Plains's.
const CREDITS: Array[Dictionary] = [
	{"label": "Characters", "text": "Pixel Plains by SnowHex"},
	{"label": "Music", "text": "Cozy Tunes by Pizza Doggy"},
	{"label": "Sound effects", "text": "Universal UI Soundpack by Nathan Gibson"},
	{"label": "Inspiration", "text": "KidPix 1.0 by Craig Hickman"},
	{"label": "Pixels", "text": "Tileset Templates by Patrik"},
	{"label": "Pixels", "text": "Puny World by Shade"},
	{"label": "Pixels", "text": "Tinyworld by Fliegevogel"},
	{"label": "Thanks to", "text": "Webtyler by Alexander Nadeau"},
	{
		"label": "Characters (early version)",
		"text": "Pixel Simple Human Character 16x16 px by Brysia"
	},
	{
		"label":
		(
			"Building a bad Rimworld clone for kids\n"
			+ "then removing the mechanics one-by-one\n"
			+ "until you get KidPix with Sims"
		),
		"text": "Robert Kuykendall"
	},
]

const LABEL_FONT_SIZE: int = 32
const BODY_FONT_SIZE: int = 52
const ENTRY_SPACING: int = 96
const SCROLL_SPEED_PX_PER_SEC: float = 40.0

@export var scroll_track_path: NodePath = ""
@export var back_button_path: NodePath = ""

var _scroll_track: VBoxContainer = null
var _back_button: Button = null
var _tween: Tween = null


func _ready() -> void:
	if not scroll_track_path.is_empty():
		_scroll_track = get_node_or_null(scroll_track_path)
	if not back_button_path.is_empty():
		_back_button = get_node_or_null(back_button_path)
		if _back_button != null:
			_back_button.pressed.connect(func() -> void: back_requested.emit())
	_build_entries()


func _build_entries() -> void:
	if _scroll_track == null:
		return
	for child in _scroll_track.get_children():
		child.queue_free()

	for entry: Dictionary in CREDITS:
		var label_text: String = entry.get("label", "")
		var body_text: String = entry.get("text", "")

		if not label_text.is_empty():
			var label_lbl := Label.new()
			label_lbl.text = label_text.to_upper()
			label_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			label_lbl.add_theme_font_size_override("font_size", LABEL_FONT_SIZE)
			label_lbl.add_theme_color_override("font_color", Color(0.6, 0.6, 0.65))
			_scroll_track.add_child(label_lbl)

		var body_lbl := Label.new()
		body_lbl.text = body_text
		body_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		body_lbl.add_theme_font_size_override("font_size", BODY_FONT_SIZE)
		body_lbl.add_theme_color_override("font_color", Color(0.9, 0.9, 0.92))
		_scroll_track.add_child(body_lbl)

		var spacer := Control.new()
		spacer.custom_minimum_size = Vector2(0, ENTRY_SPACING)
		_scroll_track.add_child(spacer)


## Restarts the scroll from the bottom every time this screen becomes active. Scrolls once
## bottom-to-top and stops — no loop.
func play_scroll() -> void:
	if _scroll_track == null:
		return
	if _tween != null and _tween.is_valid():
		_tween.kill()

	var viewport_height: float = get_viewport_rect().size.y
	_scroll_track.position.y = viewport_height
	var target_y: float = -_scroll_track.size.y
	var distance: float = viewport_height - target_y
	var duration: float = distance / SCROLL_SPEED_PX_PER_SEC

	_tween = create_tween()
	_tween.tween_property(_scroll_track, "position:y", target_y, duration)
