class_name ThemeSystem

# One theme per available music track (music/tracks/ + music/classics/), picked randomly.
# has_shadows defaults to true (see SimTheme) unless overridden here. Themes needing their own
# simulation-layer behavior (Gymnopédie No. 1's night/energy-drain, Strange Worlds' skin
# override + visitors) are dedicated SimTheme.* subclasses instead of plain data entries here —
# see Theme.gd — and get appended to the roster below.
const ROSTER_DATA: Array = [
	{"name": "Cuddle Clouds", "file": "res://music/tracks/cuddle_clouds.ogg"},
	# {"name": "Drifting Memories", "file": "res://music/tracks/drifting_memories.ogg"},
	# {"name": "Evening Harmony", "file": "res://music/tracks/evening_harmony.ogg"},
	# {"name": "Floating Dream", "file": "res://music/tracks/floating_dream.ogg"},
	# {"name": "Forgotten Biomes", "file": "res://music/tracks/forgotten_biomes.ogg"},
	# {"name": "Gentle Breeze", "file": "res://music/tracks/gentle_breeze.ogg"},
	# {"name": "Golden Gleam", "file": "res://music/tracks/golden_gleam.ogg"},
	# {"name": "Polar Lights", "file": "res://music/tracks/polar_lights.ogg"},
	# {"name": "Sunlight Through Leaves", "file": "res://music/tracks/sunlight_through_leaves.ogg"},
	# {"name": "Wanderer's Tale", "file": "res://music/tracks/wanderers_tale.ogg"},
	# {"name": "Whispering Woods", "file": "res://music/tracks/whispering_woods.ogg"},
	# {"name": "Minuet", "file": "res://music/classics/minuet.ogg"},
	# {"name": "Minuet (Slower)", "file": "res://music/classics/minuet_slower.ogg"},
	# {"name": "Tales From The Vienna Woods", "file": "res://music/classics/tales_from_the_vienna_woods.ogg"},
]

var current_theme: SimTheme = null
var queued_theme: SimTheme = null  # not set internally anymore; external callers may still force a pick

var _current_theme_start_tick: int = 0
var _available_themes: Array[SimTheme] = []
var _disabled: bool = false


func _init(disabled: bool = false) -> void:
	_disabled = disabled
	_available_themes = []
	for entry in ROSTER_DATA:
		_available_themes.append(SimTheme.SimpleTheme.new(
			entry.get("name", ""),
			entry.get("file", ""),
			entry.get("shadows", true)
		))
	_available_themes.append(SimTheme.GymnopedieTheme.new())
	_available_themes.append(SimTheme.StrangeWorldsTheme.new())


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
func _pick_random_theme() -> SimTheme:
	var candidates: Array[SimTheme] = _available_themes
	if current_theme != null:
		var current_name: String = current_theme.get_name()
		candidates = _available_themes.filter(func(t): return t.get_name() != current_name)
	if candidates.is_empty():
		candidates = _available_themes
	return candidates[randi() % candidates.size()]


func _start_theme(sim: Simulation, theme: SimTheme) -> void:
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
