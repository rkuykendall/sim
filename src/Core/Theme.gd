class_name SimTheme

# Base class for simulation themes. Subclass and override all methods.
# Themes govern music and can modify simulation state on start/tick/end.


## Display name for debugging.
func get_name() -> String:
	return "Theme"


## Path to the music file to play (empty string = no music).
func get_music_file() -> String:
	return ""


## Priority for this theme at the current simulation state.
## 0 = should not run; higher = more preferred.
## ThemeSystem selects the highest-priority theme.
func get_priority(_sim: Simulation) -> int:
	return 0


## Called once when the theme becomes active.
func on_start(_sim: Simulation) -> void:
	pass


## Called every tick while this theme is active.
func on_tick(_sim: Simulation) -> void:
	pass


## Return true when the theme should end.
## Music-driven themes end via ThemeSystem.on_music_finished() rather than here.
func is_complete(_sim: Simulation, _theme_start_tick: int) -> bool:
	return false


## Called once when the theme ends.
func on_end(_sim: Simulation) -> void:
	pass


# ---------------------------------------------------------------------------
# DayTheme — relaxing ambient music during daytime
# ---------------------------------------------------------------------------

class DayTheme:
	const DAY_TRACKS: Array[String] = [
		"res://music/tracks/cuddle_clouds.ogg",
		"res://music/tracks/drifting_memories.ogg",
		"res://music/tracks/evening_harmony.ogg",
		"res://music/tracks/floating_dream.ogg",
		"res://music/tracks/forgotten_biomes.ogg",
		"res://music/tracks/gentle_breeze.ogg",
		"res://music/tracks/golden_gleam.ogg",
		"res://music/tracks/polar_lights.ogg",
		"res://music/tracks/strange_worlds.ogg",
		"res://music/tracks/sunlight_through_leaves.ogg",
		"res://music/tracks/wanderers_tale.ogg",
		"res://music/tracks/whispering_woods.ogg",
	]

	var _selected_music_file: String = ""

	func get_name() -> String:
		return "Day"

	func get_music_file() -> String:
		return _selected_music_file

	# Priority 5 during day, 0 at night
	func get_priority(sim: Simulation) -> int:
		return 0 if sim.time.is_night else 5

	func on_start(_sim: Simulation) -> void:
		_selected_music_file = DAY_TRACKS[randi() % DAY_TRACKS.size()]

	func on_tick(_sim: Simulation) -> void:
		pass

	func on_end(_sim: Simulation) -> void:
		pass

	# Day theme runs until music finishes (never self-completes)
	func is_complete(_sim: Simulation, _theme_start_tick: int) -> bool:
		return false


# ---------------------------------------------------------------------------
# NightTheme — calming Gymnopédie; drains pawn Energy on nightfall
# ---------------------------------------------------------------------------

class NightTheme:
	var _last_day_ran: int = -1

	func get_name() -> String:
		return "Night"

	func get_music_file() -> String:
		return "res://music/classics/gymnopedie_no_1.ogg"

	# Priority 10 at night, 0 during day
	func get_priority(sim: Simulation) -> int:
		return 10 if sim.time.is_night else 0

	func on_start(sim: Simulation) -> void:
		# Drain all pawn Energy to 0 once per day when night begins
		if _last_day_ran == sim.time.day:
			return
		_last_day_ran = sim.time.day

		var energy_id: int = sim.content.get_need_id("Energy")
		if energy_id == -1:
			return

		for pawn_id in sim.entities.all_pawns():
			var need_comp: Components.NeedsComponent = sim.entities.needs.get(pawn_id)
			if need_comp != null and need_comp.needs.has(energy_id):
				need_comp.needs[energy_id] = 0.0

	func on_tick(_sim: Simulation) -> void:
		pass

	func on_end(_sim: Simulation) -> void:
		pass

	# Night theme runs until music finishes (never self-completes)
	func is_complete(_sim: Simulation, _theme_start_tick: int) -> bool:
		return false
