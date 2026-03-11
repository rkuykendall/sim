class_name UserSettings

const SETTINGS_PATH: String = "user://settings.json"

var fullscreen: bool = true


static func load() -> UserSettings:
	var settings := UserSettings.new()
	if not FileAccess.file_exists(SETTINGS_PATH):
		return settings

	var text: String = FileAccess.get_file_as_string(SETTINGS_PATH)
	var data = JSON.parse_string(text)
	if data is Dictionary:
		settings.fullscreen = bool(data.get("fullscreen", true))

	return settings


func save() -> void:
	var data: Dictionary = { "fullscreen": fullscreen }
	var json: String = JSON.stringify(data)
	var file := FileAccess.open(SETTINGS_PATH, FileAccess.WRITE)
	if file == null:
		push_error("UserSettings.save: cannot open settings file for writing")
		return
	file.store_string(json)
