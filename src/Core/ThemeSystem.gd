class_name ThemeSystem


var current_theme = null
var queued_theme = null

var _current_theme_start_tick: int = 0
var _available_themes: Array = []
var _disabled: bool = false


func _init(disabled: bool = false) -> void:
	_disabled = disabled
	_available_themes = [SimTheme.DayTheme.new(), SimTheme.NightTheme.new()]


func tick(sim: Simulation) -> void:
	if _disabled:
		return

	# Initialize with highest-priority theme on first tick
	if current_theme == null:
		_start_theme(sim, _select_by_priority(sim))

	# Queue next theme if none pending and priority changed
	if queued_theme == null:
		var next = _select_by_priority(sim)
		if next != null and next.get_name() != current_theme.get_name():
			queued_theme = next
			# Transition immediately if current theme has no music
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

func _select_by_priority(sim: Simulation):
	var max_priority: int = 0
	var top: Array = []

	for theme in _available_themes:
		var p: int = theme.get_priority(sim)
		if p <= 0:
			continue
		if p > max_priority:
			max_priority = p
			top.clear()
			top.append(theme)
		elif p == max_priority:
			top.append(theme)

	if top.is_empty():
		return SimTheme.DayTheme.new()  # Fallback

	return top[randi() % top.size()]


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
		_start_theme(sim, _select_by_priority(sim))
