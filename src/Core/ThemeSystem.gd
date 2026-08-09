class_name ThemeSystem

# One theme per available music track (music/tracks/ + music/classics/), picked randomly.
# {display name, music file} — has_shadows defaults to true (see SimTheme); the one
# exception (Gymnopédie No. 1) is its own SimTheme.GymnopedieTheme subclass, appended below.
const ROSTER_DATA: Array = [
	["Cuddle Clouds", "res://music/tracks/cuddle_clouds.ogg"],
	["Drifting Memories", "res://music/tracks/drifting_memories.ogg"],
	["Evening Harmony", "res://music/tracks/evening_harmony.ogg"],
	["Floating Dream", "res://music/tracks/floating_dream.ogg"],
	["Forgotten Biomes", "res://music/tracks/forgotten_biomes.ogg"],
	["Gentle Breeze", "res://music/tracks/gentle_breeze.ogg"],
	["Golden Gleam", "res://music/tracks/golden_gleam.ogg"],
	["Polar Lights", "res://music/tracks/polar_lights.ogg"],
	["Strange Worlds", "res://music/tracks/strange_worlds.ogg"],
	["Sunlight Through Leaves", "res://music/tracks/sunlight_through_leaves.ogg"],
	["Wanderer's Tale", "res://music/tracks/wanderers_tale.ogg"],
	["Whispering Woods", "res://music/tracks/whispering_woods.ogg"],
	["Minuet", "res://music/classics/minuet.ogg"],
	["Minuet (Slower)", "res://music/classics/minuet_slower.ogg"],
	["Tales From The Vienna Woods", "res://music/classics/tales_from_the_vienna_woods.ogg"],
]

var current_theme = null
var queued_theme = null  # not set internally anymore; external callers may still force a pick

var _current_theme_start_tick: int = 0
var _available_themes: Array = []
var _disabled: bool = false


func _init(disabled: bool = false) -> void:
	_disabled = disabled
	_available_themes = []
	for entry in ROSTER_DATA:
		_available_themes.append(SimTheme.SimpleTheme.new(entry[0], entry[1]))
	_available_themes.append(SimTheme.GymnopedieTheme.new())


func tick(sim: Simulation) -> void:
	if _disabled:
		return

	if current_theme == null:
		_start_theme(sim, _pick_random_theme())

	# No-music themes (none exist today, but future non-musical themes might) can't wait
	# for a "finished" signal that will never come — move on immediately instead of stalling.
	if current_theme.get_music_file().is_empty():
		_transition_to_next(sim)
		return

	current_theme.on_tick(sim)

	if current_theme.is_complete(sim, _current_theme_start_tick):
		_transition_to_next(sim)


# Called by Godot MusicManager when a music file finishes playing.
func on_music_finished(sim: Simulation) -> void:
	_transition_to_next(sim)


# --- Private ---------------------------------------------------------------

## Uniform-random pick, excluding whichever theme just ended so the same song can't
## immediately repeat back-to-back.
func _pick_random_theme():
	var candidates: Array = _available_themes
	if current_theme != null:
		var current_name: String = current_theme.get_name()
		candidates = _available_themes.filter(func(t): return t.get_name() != current_name)
	if candidates.is_empty():
		candidates = _available_themes
	return candidates[randi() % candidates.size()]


func _start_theme(sim: Simulation, theme) -> void:
	if current_theme != null:
		current_theme.on_end(sim)

	current_theme = theme
	_current_theme_start_tick = sim.time.tick
	theme.on_start(sim)


func _transition_to_next(sim: Simulation) -> void:
	if queued_theme != null:
		_start_theme(sim, queued_theme)
		queued_theme = null
	else:
		_start_theme(sim, _pick_random_theme())
