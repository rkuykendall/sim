class_name SimTheme

# Base class for simulation themes. Subclass and override whatever's relevant.
# Themes govern music and can modify simulation state on start/tick/end.
#
# Extension pattern for future themes:
# - Simulation-layer effects (touching pawn/entity state): add a new SimTheme subclass and
#   override on_start/on_tick/on_end — see GymnopedieTheme's Energy drain and
#   StrangeWorldsTheme's per-house skin override + visitor spawn/despawn below for two
#   different shapes of this. Simulation.spawn_visitor_pawn/remove_all_visitors are the
#   reusable primitives a visitor-spawning theme's on_start/on_end should call — no new AI or
#   population code needed. Simulation.set_building_skin_override/get_home_building_ids are the
#   reusable primitives for a theme that wants to change what colonists look like — scoped to
#   individual homes rather than instantly affecting every colonist (see PawnView.sync_house_
#   sheet): a colonist's rendered sheet only picks up a house's override once that pawn is next
#   resolved as being home there. This is what makes "make one house the king's, another random
#   knights" or "override just one house" naturally expressible without new plumbing.
# - Godot-rendering-layer effects (particles, sprite swaps, extra shaders): GameRoot can
#   branch on snapshot.theme.current_theme_name (a plain string match), the same way it
#   already branches on has_shadows. Don't build a generic effect-request system until
#   there are 3+ special-cased themes that would actually need one.
#
# SimpleTheme, GymnopedieTheme, and StrangeWorldsTheme below all `extends SimTheme`, so any
# method they don't override falls back to the base's default here — no need to touch every
# subclass just to add one new base method.


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

class SimpleTheme extends SimTheme:
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


# ---------------------------------------------------------------------------
# GymnopedieTheme — calming Gymnopédie No. 1. No shadows, dims the screen (see GameRoot),
# and drains everyone's Energy the moment it starts — this is what "night" means now,
# rather than an hour-of-day check.
# ---------------------------------------------------------------------------

class GymnopedieTheme extends SimTheme:
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


# ---------------------------------------------------------------------------
# StrangeWorldsTheme — sets every home's skin override to character_7_v2 (colonists pick it up
# as they're next resolved as being home there — see PawnView.sync_house_sheet) and brings in
# a wandering special_4_v2 visitor for every pawn the colony could ever hold (get_max_pawns())
# — the town briefly looks twice as full. Both revert when the theme ends.
# ---------------------------------------------------------------------------

class StrangeWorldsTheme extends SimTheme:
	const SKIN_OVERRIDE: String = "character_7_v2"
	const VISITOR_SHEET: String = "special_4_v2"

	func get_name() -> String:
		return "Strange Worlds"

	func get_music_file() -> String:
		return "res://music/tracks/strange_worlds.ogg"

	func on_start(sim: Simulation) -> void:
		for building_id in sim.get_home_building_ids():
			sim.set_building_skin_override(building_id, SKIN_OVERRIDE)

		var visitor_count: int = sim.get_max_pawns()
		for i in visitor_count:
			sim.spawn_visitor_pawn(VISITOR_SHEET, {}, "Visitor %d" % (i + 1))

	func on_end(sim: Simulation) -> void:
		for building_id in sim.get_home_building_ids():
			sim.set_building_skin_override(building_id, "")
		sim.remove_all_visitors()
