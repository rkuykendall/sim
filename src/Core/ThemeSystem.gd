class_name ThemeSystem

# One theme per available music track (music/tracks/ + music/classics/), picked randomly.
# has_shadows defaults to true (see SimTheme) unless overridden here. Themes needing their own
# simulation-layer behavior are dedicated SimTheme.* subclasses instead of plain data entries
# here — see Theme.gd — and get appended to the roster below.
const ROSTER_DATA: Array[Dictionary] = [
	{"name": "Cuddle Clouds", "file": "res://music/tracks/cuddle_clouds.ogg"},
	{"name": "Evening Harmony", "file": "res://music/tracks/evening_harmony.ogg"},
	{"name": "Sunlight Through Leaves", "file": "res://music/tracks/sunlight_through_leaves.ogg"},
	{"name": "Wanderer's Tale", "file": "res://music/tracks/wanderers_tale.ogg"},
	{"name": "Whispering Woods", "file": "res://music/tracks/whispering_woods.ogg"},
	{"name": "Minuet", "file": "res://music/classics/minuet.ogg"},
	{"name": "Minuet (Slower)", "file": "res://music/classics/minuet_slower.ogg"},
]

var current_theme: SimTheme = null
# not set internally anymore; external callers may still force a pick
var queued_theme: SimTheme = null

var _current_theme_start_tick: int = 0
var _available_themes: Array[SimTheme] = []
var _disabled: bool = false


func _init(disabled: bool = false) -> void:
	_disabled = disabled
	_available_themes = []
	for entry: Dictionary in ROSTER_DATA:
		_available_themes.append(
			SimTheme.SimpleTheme.new(
				DefUtils.get_string(entry, "name", ""),
				DefUtils.get_string(entry, "file", ""),
				DefUtils.get_bool(entry, "shadows", true)
			)
		)
	_available_themes.append(SimTheme.GymnopedieTheme.new())
	_available_themes.append(SimTheme.StrangeWorldsTheme.new())
	_available_themes.append(SimTheme.ViennaWoodsTheme.new())
	_available_themes.append(SimTheme.PolarLightsTheme.new())
	_available_themes.append(SimTheme.GoldenGleamTheme.new())
	_available_themes.append(SimTheme.DriftingMemoriesTheme.new())
	_available_themes.append(SimTheme.GentleBreezeTheme.new())
	_available_themes.append(SimTheme.ForgottenBiomesTheme.new())
	_available_themes.append(SimTheme.FloatingDreamTheme.new())

	# Seed every theme's priority to its own gain rather than leaving it at the SimTheme default
	# of 0 — otherwise every theme, rare or common, ties for lowest on the very first pick (both
	# on a new game and after every load, since priorities aren't persisted), making rare themes
	# just as likely to open a game as common ones instead of needing to "earn" their rarity over
	# time like they do for every pick after the first.
	for t in _available_themes:
		t.priority = t.get_priority_gain()


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


## Restores current_theme by name (see SaveService) — deliberately bypasses on_start/on_end:
## a save already correctly persists everything a theme's on_start would otherwise (re-)create
## (visitor pawns, home skin_override), so re-running it would double-spawn/re-touch state that's
## already right. No-op (current_theme stays null, a fresh theme gets picked on the next tick,
## same as any other new game) if theme_name doesn't match anything in the current roster — e.g.
## an older save from before a theme was added/renamed.
func restore_current_theme(theme_name: String, start_tick: int) -> void:
	if theme_name.is_empty():
		return
	for t in _available_themes:
		if t.get_name() == theme_name:
			current_theme = t
			_current_theme_start_tick = start_tick
			return


## See restore_current_theme — the read-side counterpart, for SaveService.
func get_current_theme_start_tick() -> int:
	return _current_theme_start_tick


# --- Private ---------------------------------------------------------------


## Weighted rotation via a "cooldown" priority (see SimTheme.priority/get_priority_gain):
## every pick first decays every theme's priority by 1 (floored at 0), then picks uniformly
## at random from whichever theme(s) currently have the lowest priority — the most "overdue."
## The picked theme's priority then jumps up by its own get_priority_gain(), taking it out of
## contention until enough later picks have decayed it back down — rarer themes (higher gain)
## come up less often. This also naturally prevents immediate repeats without a separate rule:
## a just-picked theme's priority is always above 0 afterward, and as long as there are more
## themes than the largest gain in play, some other theme is always sitting at 0.
func _pick_random_theme() -> SimTheme:
	for t in _available_themes:
		t.priority = maxi(0, t.priority - 1)

	var min_priority: int = _available_themes[0].priority
	for t in _available_themes:
		min_priority = mini(min_priority, t.priority)

	var lowest: Array[SimTheme] = _available_themes.filter(
		func(t: SimTheme) -> bool: return t.priority == min_priority
	)
	var picked: SimTheme = lowest[randi() % lowest.size()]
	picked.priority += picked.get_priority_gain()
	return picked


func _start_theme(sim: Simulation, theme: SimTheme) -> void:
	if current_theme != null:
		current_theme.on_end(sim)
		# Automatic, universal cleanup — themes don't need their own on_end just to revert a
		# skin override or send their visitors away (see Simulation.clear_all_home_skin_overrides
		# / remove_all_visitors). on_end above still exists for anything more theme-specific.
		sim.clear_all_home_skin_overrides()
		sim.remove_all_visitors()

	current_theme = theme
	_current_theme_start_tick = sim.time.tick
	theme.on_start(sim)


func _transition_to_next(sim: Simulation) -> void:
	if queued_theme != null:
		_start_theme(sim, queued_theme)
		queued_theme = null
	else:
		_start_theme(sim, _pick_random_theme())
