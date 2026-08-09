class_name SimTheme

# Base class for simulation themes. Subclass and override all methods.
# Themes govern music and can modify simulation state on start/tick/end.
#
# Extension pattern for future themes:
# - Simulation-layer effects (touching pawn/entity state, like GymnopedieTheme's Energy
#   drain below): add a new SimTheme subclass and override on_start/on_tick/on_end.
# - Godot-rendering-layer effects (particles, sprite swaps, extra shaders): GameRoot can
#   branch on snapshot.theme.current_theme_name (a plain string match), the same way it
#   already branches on has_shadows. Don't build a generic effect-request system until
#   there are 3+ special-cased themes that would actually need one.


## Display name for debugging and UI.
func get_name() -> String:
	return "Theme"


## Path to the music file to play (empty string = no music).
func get_music_file() -> String:
	return ""


## Whether this theme runs the shadow shader for its duration, progressing across the
## theme's own playback (see GameRoot). Most themes do; a theme meant to represent "night"
## (currently just GymnopedieTheme) should not.
func has_shadows() -> bool:
	return true


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
# SimpleTheme — a plain theme that just plays one track with no other effects.
# Covers most of the roster; see ThemeSystem for the full theme list.
# ---------------------------------------------------------------------------

class SimpleTheme:
	var _name: String
	var _music_file: String
	var _has_shadows: bool

	func _init(theme_name: String, music_file: String, shadows: bool = true) -> void:
		_name = theme_name
		_music_file = music_file
		_has_shadows = shadows

	func get_name() -> String:
		return _name

	func get_music_file() -> String:
		return _music_file

	func has_shadows() -> bool:
		return _has_shadows

	func on_start(_sim: Simulation) -> void:
		pass

	func on_tick(_sim: Simulation) -> void:
		pass

	func on_end(_sim: Simulation) -> void:
		pass

	func is_complete(_sim: Simulation, _theme_start_tick: int) -> bool:
		return false


# ---------------------------------------------------------------------------
# GymnopedieTheme — calming Gymnopédie No. 1. No shadows, dims the screen (see GameRoot),
# and drains everyone's Energy the moment it starts — this is what "night" means now,
# rather than an hour-of-day check.
# ---------------------------------------------------------------------------

class GymnopedieTheme:
	func get_name() -> String:
		return "Gymnopédie No. 1"

	func get_music_file() -> String:
		return "res://music/classics/gymnopedie_no_1.ogg"

	func has_shadows() -> bool:
		return false

	func on_start(sim: Simulation) -> void:
		var energy_id: int = sim.content.get_need_id("Energy")
		if energy_id == -1:
			return
		for pawn_id in sim.entities.pawns:
			var need_comp: Components.NeedsComponent = sim.entities.needs.get(pawn_id)
			if need_comp != null and need_comp.needs.has(energy_id):
				need_comp.needs[energy_id] = 0.0

	func on_tick(_sim: Simulation) -> void:
		pass

	func on_end(_sim: Simulation) -> void:
		pass

	func is_complete(_sim: Simulation, _theme_start_tick: int) -> bool:
		return false
