class_name SimTheme

# Base class for simulation themes. Subclass and override whatever's relevant.
# Themes govern music and can modify simulation state on start/tick/end.
#
# Every nested class below `extends SimTheme`, so any method a subclass doesn't override
# falls back to the base's default here — no need to touch every subclass just to add one new
# base method.


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


## Called once when the theme ends, before ThemeSystem clears every home's skin_override and
## removes every visitor automatically (see Simulation.clear_all_home_skin_overrides/
## remove_all_visitors) — only needed for something beyond that.
func on_end(_sim: Simulation) -> void:
	pass


# ---------------------------------------------------------------------------
# SimpleTheme — a plain theme that just plays one track with no other effects.
# Covers most of the roster; see ThemeSystem for the full theme list.
# ---------------------------------------------------------------------------

class SimpleTheme extends SimTheme:
	# Rare stray-critter wanderers, shared across every plain track. STRAY_TRIGGER_CHANCE gates
	# whether anything happens at all; STRAY_INCLUDE_CHANCE then independently decides each
	# sheet, so any combination can show up. Forced to include at least one sheet if every
	# independent roll misses, so a successful trigger always has something to show for it.
	const STRAY_TRIGGER_CHANCE: float = 0.2
	const STRAY_INCLUDE_CHANCE: float = 0.5
	const STRAY_POOL: Array[String] = [
		"gato_v2", "gato_2_v2", "gato_3_v2", "gato_4_v2", "gato_5_v2", "special_5_v2",
	]

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

	func on_start(sim: Simulation) -> void:
		if randf() >= STRAY_TRIGGER_CHANCE:
			return

		var chosen: Array[String] = []
		for sheet in STRAY_POOL:
			if randf() < STRAY_INCLUDE_CHANCE:
				chosen.append(sheet)
		if chosen.is_empty():
			chosen.append(STRAY_POOL[randi() % STRAY_POOL.size()])

		for i in chosen.size():
			sim.spawn_visitor_pawn(chosen[i], {}, "Visitor %d" % (i + 1))


# ---------------------------------------------------------------------------
# GymnopedieTheme — this is what "night" means now, rather than an hour-of-day check.
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
# StrangeWorldsTheme — the skin override isn't instant: a colonist only picks it up next time
# they're resolved as being home (see PawnView.sync_house_sheet).
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


# ---------------------------------------------------------------------------
# ViennaWoodsTheme
# ---------------------------------------------------------------------------

class ViennaWoodsTheme extends SimTheme:
	const SKIN_OVERRIDE: String = "spring_1_v2"
	const VISITOR_SHEET_A: String = "spring_2_v2"
	const VISITOR_SHEET_B: String = "spring_3_v2"

	func get_name() -> String:
		return "Tales From The Vienna Woods"

	func get_music_file() -> String:
		return "res://music/classics/tales_from_the_vienna_woods.ogg"

	func on_start(sim: Simulation) -> void:
		for building_id in sim.get_home_building_ids():
			sim.set_building_skin_override(building_id, SKIN_OVERRIDE)

		var group_count: int = sim.get_max_pawns() / 2
		for i in group_count:
			sim.spawn_visitor_pawn(VISITOR_SHEET_A, {}, "Visitor %d" % (i + 1))
		for i in group_count:
			sim.spawn_visitor_pawn(VISITOR_SHEET_B, {}, "Visitor %d" % (group_count + i + 1))


# ---------------------------------------------------------------------------
# PolarLightsTheme — visitors are a random per-visitor mix, unlike Vienna Woods' even split
# into two fixed groups.
# ---------------------------------------------------------------------------

class PolarLightsTheme extends SimTheme:
	const HOME_SKIN_A: String = "winter_1_v2"
	const HOME_SKIN_B: String = "winter_2_v2"
	const VISITOR_SHEET_A: String = "winter_3_v2"
	const VISITOR_SHEET_B: String = "winter_4_v2"

	func get_name() -> String:
		return "Polar Lights"

	func get_music_file() -> String:
		return "res://music/tracks/polar_lights.ogg"

	func on_start(sim: Simulation) -> void:
		var home_ids: Array[int] = sim.get_home_building_ids()
		for i in home_ids.size():
			var skin: String = HOME_SKIN_A if i % 2 == 0 else HOME_SKIN_B
			sim.set_building_skin_override(home_ids[i], skin)

		var visitor_count: int = sim.get_max_pawns()
		for i in visitor_count:
			var sheet: String = VISITOR_SHEET_A if randf() < 0.5 else VISITOR_SHEET_B
			sim.spawn_visitor_pawn(sheet, {}, "Visitor %d" % (i + 1))


# ---------------------------------------------------------------------------
# GoldenGleamTheme
# ---------------------------------------------------------------------------

class GoldenGleamTheme extends SimTheme:
	const HOME_SKINS: Array[String] = ["dg_knight_1_v2", "dg_knight_2_v2", "dg_knight_3_v2", "dg_knight_4_v2"]
	const VISITOR_SHEET: String = "character_15_v2"

	func get_name() -> String:
		return "Golden Gleam"

	func get_music_file() -> String:
		return "res://music/tracks/golden_gleam.ogg"

	func on_start(sim: Simulation) -> void:
		var home_ids: Array[int] = sim.get_home_building_ids()
		for i in home_ids.size():
			sim.set_building_skin_override(home_ids[i], HOME_SKINS[i % HOME_SKINS.size()])

		sim.spawn_visitor_pawn(VISITOR_SHEET, {}, "Visitor")


# ---------------------------------------------------------------------------
# DriftingMemoriesTheme
# ---------------------------------------------------------------------------

class DriftingMemoriesTheme extends SimTheme:
	const SKIN_OVERRIDE: String = "character_17_v2"
	const VISITOR_SHEET: String = "character_18_v2"

	func get_name() -> String:
		return "Drifting Memories"

	func get_music_file() -> String:
		return "res://music/tracks/drifting_memories.ogg"

	func on_start(sim: Simulation) -> void:
		for building_id in sim.get_home_building_ids():
			sim.set_building_skin_override(building_id, SKIN_OVERRIDE)

		var visitor_count: int = randi_range(1, sim.get_max_pawns() * 2)
		for i in visitor_count:
			sim.spawn_visitor_pawn(VISITOR_SHEET, {}, "Visitor %d" % (i + 1))


# ---------------------------------------------------------------------------
# GentleBreezeTheme — no skin override, unlike the rest of the roster's special themes.
# ---------------------------------------------------------------------------

class GentleBreezeTheme extends SimTheme:
	const VISITOR_SHEET: String = "autumn_1_v2"

	func get_name() -> String:
		return "Gentle Breeze"

	func get_music_file() -> String:
		return "res://music/tracks/gentle_breeze.ogg"

	func on_start(sim: Simulation) -> void:
		var visitor_count: int = sim.get_max_pawns() * 2
		for i in visitor_count:
			sim.spawn_visitor_pawn(VISITOR_SHEET, {}, "Visitor %d" % (i + 1))


# ---------------------------------------------------------------------------
# ForgottenBiomesTheme — also no skin override.
# ---------------------------------------------------------------------------

class ForgottenBiomesTheme extends SimTheme:
	const VISITOR_SHEET: String = "special_3_v2"

	func get_name() -> String:
		return "Forgotten Biomes"

	func get_music_file() -> String:
		return "res://music/tracks/forgotten_biomes.ogg"

	func on_start(sim: Simulation) -> void:
		var visitor_count: int = sim.get_max_pawns()
		for i in visitor_count:
			sim.spawn_visitor_pawn(VISITOR_SHEET, {}, "Visitor %d" % (i + 1))
