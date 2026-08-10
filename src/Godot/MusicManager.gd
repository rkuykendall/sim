class_name MusicManager
extends Node

signal music_finished

@export var audio_player_path: NodePath = ""

var _audio_player: AudioStreamPlayer = null
var _current_music_file: String = ""
var _current_theme_name: String = ""
var _current_stream_length: float = 0.0


func _ready() -> void:
	if not audio_player_path.is_empty():
		_audio_player = get_node_or_null(audio_player_path)

	if _audio_player == null:
		_audio_player = AudioStreamPlayer.new()
		add_child(_audio_player)

	_audio_player.finished.connect(_on_audio_finished)


func _on_audio_finished() -> void:
	music_finished.emit()


## Called every frame with current snapshot theme data.
## snapshot_theme is a Dictionary: { current_theme_name, current_music_file, queued_theme_name }
func update_music_state(snapshot_theme: Dictionary) -> void:
	var theme_name: String = snapshot_theme.get("current_theme_name", "")
	var music_file: String = snapshot_theme.get("current_music_file", "")

	# No change — do nothing
	if theme_name == _current_theme_name and music_file == _current_music_file:
		return

	_current_theme_name = theme_name
	_current_music_file = music_file

	if music_file.is_empty():
		_audio_player.stop()
		return

	if not ResourceLoader.exists(music_file):
		push_warning("MusicManager: music file not found: %s" % music_file)
		_audio_player.stop()
		return

	var stream: AudioStream = load(music_file)
	if stream == null:
		_audio_player.stop()
		return

	_audio_player.stream = stream
	_current_stream_length = stream.get_length()
	_audio_player.play()


## Stops playback and clears tracked theme/file state — without the reset, resuming a save whose
## restored theme happens to match what was already loaded (see ThemeSystem.restore_current_theme)
## would look like "no change" to update_music_state and silently never call play() again.
func stop() -> void:
	# GameRoot and MusicManager are siblings under Main, and GameRoot is declared earlier in the
	# scene tree, so GameRoot._ready() (which calls this at boot, before any game has started) can
	# run before this node's own _ready() has set up _audio_player.
	if _audio_player == null:
		return
	_audio_player.stop()
	_current_music_file = ""
	_current_theme_name = ""


## 0-1 fraction of the way through the current track (0 if nothing is playing, or the
## stream has no known length). Drives theme-progress-based visuals (shadows, screen tint)
## in GameRoot instead of a real-world clock, so they always align with the current song.
func get_playback_progress() -> float:
	if _audio_player == null or not _audio_player.playing or _current_stream_length <= 0.0:
		return 0.0
	return clampf(_audio_player.get_playback_position() / _current_stream_length, 0.0, 1.0)
