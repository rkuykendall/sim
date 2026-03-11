class_name MusicManager
extends Node

signal music_finished

@export var audio_player_path: NodePath = ""

var _audio_player: AudioStreamPlayer = null
var _current_music_file: String = ""
var _current_theme_name: String = ""


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
	_audio_player.play()
