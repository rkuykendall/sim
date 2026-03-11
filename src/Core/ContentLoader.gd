class_name ContentLoader

# Impassable sentinel — Lua used math.huge; JSON uses 1e9.
const IMPASSABLE: float = 1e9

# Default values matching C# BuildingDef constants.
const DEFAULT_BASE_COST: int = 10
const DEFAULT_BASE_PRODUCTION: float = 2.0


static func load_all(content_path: String) -> ContentRegistry:
	var registry := ContentRegistry.new()

	_load_file(registry, content_path.path_join("core/palettes.json"), _parse_palette)
	_load_file(registry, content_path.path_join("core/needs.json"), _parse_need)
	_load_file(registry, content_path.path_join("core/terrains.json"), _parse_terrain)
	_load_file(registry, content_path.path_join("core/buildings.json"), _parse_building)

	_load_mods(registry)

	return registry


# --- Mod loading -----------------------------------------------------------

static func _load_mods(registry: ContentRegistry) -> void:
	var mod_dir := DirAccess.open("user://mods")
	if mod_dir == null:
		return

	for mod_name in mod_dir.get_directories():
		var base := "user://mods/".path_join(mod_name).path_join("/")
		var parsers := {
			"palettes.json": _parse_palette,
			"needs.json": _parse_need,
			"terrains.json": _parse_terrain,
			"buildings.json": _parse_building,
		}
		for file_name in parsers:
			var path := base.path_join(file_name)
			if FileAccess.file_exists(path):
				_load_file(registry, path, parsers[file_name])


# --- File loading ----------------------------------------------------------

static func _load_file(registry: ContentRegistry, path: String, parser: Callable) -> void:
	if not FileAccess.file_exists(path):
		push_warning("ContentLoader: file not found: %s" % path)
		return

	var text := FileAccess.get_file_as_string(path)
	var data = JSON.parse_string(text)

	if data == null or not data is Dictionary:
		push_error("ContentLoader: failed to parse JSON: %s" % path)
		return

	for key in data:
		parser.call(registry, key, data[key])


# --- Parsers ---------------------------------------------------------------

static func _parse_palette(registry: ContentRegistry, key: String, value) -> void:
	if not value is Array:
		push_error("ContentLoader: palette '%s' must be an array of hex strings" % key)
		return

	var colors: Array[Color] = []
	for hex in value:
		colors.append(Color(hex))

	registry.register_palette(key, { "colors": colors })


static func _parse_need(registry: ContentRegistry, key: String, value: Dictionary) -> void:
	registry.register_need(key, {
		"name":               value.get("name", key),
		"decayPerTick":       float(value.get("decayPerTick", 0.05)),
		"criticalThreshold":  float(value.get("criticalThreshold", 20.0)),
		"lowThreshold":       float(value.get("lowThreshold", 40.0)),
		"criticalDebuff":     float(value.get("criticalDebuff", 0.0)),
		"lowDebuff":          float(value.get("lowDebuff", 0.0)),
		"spriteKey":          value.get("spriteKey", ""),
	})


static func _parse_terrain(registry: ContentRegistry, key: String, value: Dictionary) -> void:
	registry.register_terrain(key, {
		"walkabilityCost": float(value.get("walkabilityCost", 1.0)),
		"blocksLight":     bool(value.get("blocksLight", false)),
		"spriteKey":       value.get("spriteKey", ""),
		"isAutotiling":    bool(value.get("isAutotiling", false)),
		"paintsToBase":    bool(value.get("paintsToBase", false)),
		"variantCount":    int(value.get("variantCount", 1)),
	})


static func _parse_building(registry: ContentRegistry, key: String, value: Dictionary) -> void:
	# Resolve satisfiesNeed string -> need id
	var satisfies_need_id: int = -1
	if value.has("satisfiesNeed"):
		satisfies_need_id = registry.get_need_id(value["satisfiesNeed"])
		if satisfies_need_id == -1:
			push_error("ContentLoader: building '%s' references unknown need '%s'" % [key, value["satisfiesNeed"]])

	# Resolve haulSourceTerrainKey string -> terrain id
	var haul_terrain_id: int = -1
	if value.has("haulSourceTerrainKey"):
		haul_terrain_id = registry.get_terrain_id(value["haulSourceTerrainKey"])

	registry.register_building(key, {
		"satisfiesNeedId":        satisfies_need_id,
		"grantsBuff":             float(value.get("grantsBuff", 0.0)),
		"buffDuration":           int(value.get("buffDuration", 0)),
		"spriteKey":              value.get("spriteKey", ""),
		"tileSize":               int(value.get("tileSize", 1)),
		"baseCost":               int(value.get("baseCost", DEFAULT_BASE_COST)),
		"baseProduction":         float(value.get("baseProduction", DEFAULT_BASE_PRODUCTION)),
		"spriteVariants":         int(value.get("spriteVariants", 1)),
		"spritePhases":           int(value.get("spritePhases", 1)),
		"capacityPerPhase":       value.get("capacityPerPhase", []),
		"capacity":               int(value.get("capacity", 1)),
		"resourceType":           value.get("resourceType", ""),
		"maxResourceAmount":      float(value.get("maxResourceAmount", 100.0)),
		"depletionMult":          float(value.get("depletionMult", 1.0)),
		"canBeWorkedAt":          bool(value.get("canBeWorkedAt", false)),
		"workType":               value.get("workType", "direct"),
		"haulSourceResourceType": value.get("haulSourceResourceType", ""),
		"haulSourceTerrainKey":   value.get("haulSourceTerrainKey", ""),
		"haulSourceTerrainId":    haul_terrain_id,
		"canSellToConsumers":     bool(value.get("canSellToConsumers", true)),
		"satisfactionAmount":     float(value.get("satisfactionAmount", 100.0)),
		"interactionDuration":    int(value.get("interactionDuration", 100)),
	})
