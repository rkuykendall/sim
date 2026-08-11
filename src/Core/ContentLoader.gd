class_name ContentLoader

# Impassable sentinel — Lua used math.huge; JSON uses 1e9.
const IMPASSABLE: float = 1e9


static func load_all(mod_path: String = "") -> ContentRegistry:
	var registry := ContentRegistry.new()

	# Always load base content from packed resources (works on all platforms)
	_load_file(registry, "res://content/core/palettes.json", _parse_palette)
	_load_file(registry, "res://content/core/needs.json", _parse_need)
	_load_file(registry, "res://content/core/terrains.json", _parse_terrain)
	_load_file(registry, "res://content/core/buildings.json", _parse_building)

	# Additively load from content/ folder next to exe on desktop (for modding)
	if not mod_path.is_empty():
		_load_file(registry, mod_path.path_join("core/palettes.json"), _parse_palette, true)
		_load_file(registry, mod_path.path_join("core/needs.json"), _parse_need, true)
		_load_file(registry, mod_path.path_join("core/terrains.json"), _parse_terrain, true)
		_load_file(registry, mod_path.path_join("core/buildings.json"), _parse_building, true)

	_load_mods(registry)

	return registry


# --- Mod loading -----------------------------------------------------------


static func _load_mods(registry: ContentRegistry) -> void:
	var mod_dir := DirAccess.open("user://mods")
	if mod_dir == null:
		return

	for mod_name in mod_dir.get_directories():
		var base := "user://mods/".path_join(mod_name).path_join("/")
		var parsers: Dictionary[String, Callable] = {
			"palettes.json": _parse_palette,
			"needs.json": _parse_need,
			"terrains.json": _parse_terrain,
			"buildings.json": _parse_building,
		}
		for file_name: String in parsers:
			var path := base.path_join(file_name)
			if FileAccess.file_exists(path):
				_load_file(registry, path, parsers[file_name])


# --- File loading ----------------------------------------------------------


static func _load_file(
	registry: ContentRegistry, path: String, parser: Callable, optional: bool = false
) -> void:
	if not FileAccess.file_exists(path):
		if not optional:
			push_warning("ContentLoader: file not found: %s" % path)
		return

	var text := FileAccess.get_file_as_string(path)
	var data: Variant = JSON.parse_string(text)

	if data == null or not data is Dictionary:
		push_error("ContentLoader: failed to parse JSON: %s" % path)
		return

	var dict: Dictionary = data
	for key: String in dict:
		parser.call(registry, key, dict[key])


# --- Parsers ---------------------------------------------------------------


static func _parse_palette(registry: ContentRegistry, key: String, value: Variant) -> void:
	if not value is Array:
		push_error("ContentLoader: palette '%s' must be an array of hex strings" % key)
		return

	var hex_values: Array = value
	var colors: Array[Color] = []
	for hex: String in hex_values:
		colors.append(Color(hex))

	registry.register_palette(key, {"colors": colors})


static func _parse_need(registry: ContentRegistry, key: String, value: Dictionary) -> void:
	(
		registry
		. register_need(
			key,
			{
				"name": DefUtils.get_string(value, "name", key),
				"decayPerTick": DefUtils.get_float(value, "decayPerTick", 0.05),
				"criticalThreshold": DefUtils.get_float(value, "criticalThreshold", 20.0),
				"lowThreshold": DefUtils.get_float(value, "lowThreshold", 40.0),
				"spriteKey": DefUtils.get_string(value, "spriteKey", ""),
			}
		)
	)


static func _parse_terrain(registry: ContentRegistry, key: String, value: Dictionary) -> void:
	(
		registry
		. register_terrain(
			key,
			{
				"walkabilityCost": DefUtils.get_float(value, "walkabilityCost", 1.0),
				"blocksLight": DefUtils.get_bool(value, "blocksLight", false),
				"spriteKey": DefUtils.get_string(value, "spriteKey", ""),
				"isAutotiling": DefUtils.get_bool(value, "isAutotiling", false),
				"paintsToBase": DefUtils.get_bool(value, "paintsToBase", false),
				"variantCount": DefUtils.get_int(value, "variantCount", 1),
			}
		)
	)


static func _parse_building(registry: ContentRegistry, key: String, value: Dictionary) -> void:
	# Resolve satisfiesNeed string -> need id
	var satisfies_need_id: int = -1
	if value.has("satisfiesNeed"):
		var satisfies_need_key: String = DefUtils.get_string(value, "satisfiesNeed", "")
		satisfies_need_id = registry.get_need_id(satisfies_need_key)
		if satisfies_need_id == -1:
			push_error(
				(
					"ContentLoader: building '%s' references unknown need '%s'"
					% [key, satisfies_need_key]
				)
			)

	# Resolve haulSourceTerrainKey string -> terrain id
	var haul_terrain_id: int = -1
	if value.has("haulSourceTerrainKey"):
		haul_terrain_id = registry.get_terrain_id(
			DefUtils.get_string(value, "haulSourceTerrainKey", "")
		)

	(
		registry
		. register_building(
			key,
			{
				"satisfiesNeedId": satisfies_need_id,
				"spriteKey": DefUtils.get_string(value, "spriteKey", ""),
				"tileSize": DefUtils.get_int(value, "tileSize", 1),
				"spriteVariants": DefUtils.get_int(value, "spriteVariants", 1),
				"spriteColumn": DefUtils.get_int(value, "spriteColumn", 0),
				"isHome": DefUtils.get_bool(value, "isHome", false),
				"capacity": DefUtils.get_int(value, "capacity", 1),
				"resourceType": DefUtils.get_string(value, "resourceType", ""),
				"maxResourceAmount": DefUtils.get_float(value, "maxResourceAmount", 100.0),
				"depletionMult": DefUtils.get_float(value, "depletionMult", 1.0),
				"canBeWorkedAt": DefUtils.get_bool(value, "canBeWorkedAt", false),
				"workType": DefUtils.get_string(value, "workType", "direct"),
				"haulSourceResourceType": DefUtils.get_string(value, "haulSourceResourceType", ""),
				"haulSourceTerrainKey": DefUtils.get_string(value, "haulSourceTerrainKey", ""),
				"haulSourceTerrainId": haul_terrain_id,
				"canSellToConsumers": DefUtils.get_bool(value, "canSellToConsumers", true),
				"satisfactionAmount": DefUtils.get_float(value, "satisfactionAmount", 100.0),
				"interactionDuration": DefUtils.get_int(value, "interactionDuration", 100),
			}
		)
	)
