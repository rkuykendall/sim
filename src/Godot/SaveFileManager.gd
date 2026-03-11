class_name SaveFileManager

const SAVE_DIRECTORY: String = "user://saves/"


static func ensure_save_directory() -> void:
	DirAccess.make_dir_recursive_absolute(
		ProjectSettings.globalize_path(SAVE_DIRECTORY)
	)


## Returns an Array of metadata Dictionaries sorted newest first.
## Each dict: { slot_name, display_name, day, pawn_count, saved_at }
static func get_all_saves() -> Array:
	ensure_save_directory()
	var saves: Array = []

	var dir := DirAccess.open(SAVE_DIRECTORY)
	if dir == null:
		return saves

	dir.list_dir_begin()
	var file_name: String = dir.get_next()
	while not file_name.is_empty():
		if not dir.current_is_dir() and file_name.ends_with(".json"):
			var slot_name: String = file_name.trim_suffix(".json")
			var meta: Dictionary = _load_metadata_for_slot(slot_name)
			if not meta.is_empty() and meta.get("version", 0) == SaveService.SAVE_VERSION:
				saves.append(meta)
		file_name = dir.get_next()
	dir.list_dir_end()

	# Sort newest first by saved_at string (ISO format sorts lexicographically)
	saves.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		return a.get("saved_at", "") > b.get("saved_at", "")
	)
	return saves


static func _load_metadata_for_slot(slot_name: String) -> Dictionary:
	var path: String = SAVE_DIRECTORY + slot_name + ".json"
	var meta: Dictionary = SaveService.read_metadata(path)
	if not meta.is_empty():
		meta["slot_name"] = slot_name
	return meta


static func write_save(slot_name: String, sim: Simulation, display_name: String = "") -> void:
	ensure_save_directory()
	var path: String = SAVE_DIRECTORY + slot_name + ".json"
	SaveService.save(sim, path, display_name if not display_name.is_empty() else slot_name)
	print("SaveFileManager: saved to %s" % path)


static func load_save(slot_name: String, content: ContentRegistry) -> Simulation:
	var path: String = SAVE_DIRECTORY + slot_name + ".json"
	return SaveService.load_file(path, content)


static func delete_save(slot_name: String) -> void:
	var path: String = SAVE_DIRECTORY + slot_name + ".json"
	var global_path: String = ProjectSettings.globalize_path(path)
	if FileAccess.file_exists(path):
		var err: Error = DirAccess.remove_absolute(global_path)
		if err != OK:
			push_error("SaveFileManager: failed to delete '%s'" % path)
		else:
			print("SaveFileManager: deleted save '%s'" % slot_name)


## Returns "Save N" where N is the smallest unused number.
static func generate_save_name() -> String:
	var existing := get_all_saves()
	var used_numbers: Dictionary = {}
	for save in existing:
		var name: String = save.get("display_name", "")
		if name.begins_with("Save "):
			var num_str: String = name.substr(5)
			if num_str.is_valid_int():
				used_numbers[int(num_str)] = true

	var next: int = 1
	while used_numbers.has(next):
		next += 1
	return "Save %d" % next
