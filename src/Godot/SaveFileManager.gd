class_name SaveFileManager

const SAVE_DIRECTORY: String = "user://saves/"

# Godot's user:// data dir is keyed by project name (see project.godot config/name), so renaming
# the project orphans any saves made under the old name in a sibling folder Godot no longer looks
# in. Update this if the project is ever renamed again.
const LEGACY_PROJECT_NAME: String = "SimGame"


static func ensure_save_directory() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(SAVE_DIRECTORY))


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
	saves.sort_custom(
		func(a: Dictionary, b: Dictionary) -> bool:
			return a.get("saved_at", "") > b.get("saved_at", "")
	)
	return saves


static func _load_metadata_for_slot(slot_name: String) -> Dictionary:
	var path: String = SAVE_DIRECTORY + slot_name + ".json"
	var meta: Dictionary = SaveService.read_metadata(path)
	if not meta.is_empty():
		meta["slot_name"] = slot_name
	return meta


## One-time migration for players upgrading from the old "SimGame" project name — copies any
## saves found in the old sibling app_userdata folder into the current save directory. Only runs
## when the current save directory has no .json files at all yet — deliberately not gated on
## get_all_saves() (which filters by SAVE_VERSION), since a migrated save from an older, now-
## incompatible version wouldn't count as "existing" there and this would re-copy on every boot.
static func migrate_legacy_saves() -> void:
	ensure_save_directory()
	if _has_any_save_file(SAVE_DIRECTORY):
		return

	var legacy_dir_path: String = (
		OS.get_user_data_dir().get_base_dir().path_join(LEGACY_PROJECT_NAME).path_join("saves")
	)
	var legacy_dir := DirAccess.open(legacy_dir_path)
	if legacy_dir == null:
		return

	var migrated: int = 0
	legacy_dir.list_dir_begin()
	var file_name: String = legacy_dir.get_next()
	while not file_name.is_empty():
		if not legacy_dir.current_is_dir() and file_name.ends_with(".json"):
			var from_path: String = legacy_dir_path.path_join(file_name)
			var to_path: String = ProjectSettings.globalize_path(SAVE_DIRECTORY + file_name)
			if DirAccess.copy_absolute(from_path, to_path) == OK:
				migrated += 1
			else:
				push_error("SaveFileManager: failed to migrate legacy save '%s'" % file_name)
		file_name = legacy_dir.get_next()
	legacy_dir.list_dir_end()

	if migrated > 0:
		print(
			(
				"SaveFileManager: migrated %d legacy save(s) from '%s'"
				% [migrated, LEGACY_PROJECT_NAME]
			)
		)


static func _has_any_save_file(directory: String) -> bool:
	var dir := DirAccess.open(directory)
	if dir == null:
		return false
	dir.list_dir_begin()
	var file_name: String = dir.get_next()
	while not file_name.is_empty():
		if not dir.current_is_dir() and file_name.ends_with(".json"):
			dir.list_dir_end()
			return true
		file_name = dir.get_next()
	dir.list_dir_end()
	return false


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
