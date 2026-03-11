class_name TestSimulationBuilder
extends RefCounted

# Fixed seed ensures deterministic palette selection and world generation.
const DEFAULT_SEED: int = 12345
const DEFAULT_WORLD_SIZE: int = 5  # 5x5 world (0..4, 0..4)

var _seed: int = DEFAULT_SEED
var _world_width: int = DEFAULT_WORLD_SIZE
var _world_height: int = DEFAULT_WORLD_SIZE
var _disable_themes: bool = true
var _tax_multiplier: float = 1.0
var _start_hour: int = TimeService.DEFAULT_START_HOUR

var _content: ContentRegistry
var _pending_pawns: Array = []    # [{name, x, y, needs}]
var _pending_buildings: Array = [] # [{building_def_id, x, y}]


func _init() -> void:
	_content = ContentRegistry.new()
	# Register a single test palette so palette selection is deterministic.
	_content.register_palette("test", {
		"colors": [
			Color(0.2, 0.6, 0.2),  # Green
			Color(0.5, 0.3, 0.1),  # Brown
			Color(0.7, 0.7, 0.7),  # Light Gray
			Color(0.8, 0.6, 0.3),  # Tan
		]
	})


# -------------------------------------------------------------------------
# Configuration
# -------------------------------------------------------------------------

## Set world size. C# equivalent: WithWorldBounds(maxX, maxY) where size = maxX+1.
func with_world_size(width: int, height: int) -> TestSimulationBuilder:
	_world_width = width
	_world_height = height
	return self


## Helper matching C# API: WithWorldBounds(maxX, maxY) → width = maxX+1, height = maxY+1.
func with_world_bounds(max_x: int, max_y: int) -> TestSimulationBuilder:
	_world_width = max_x + 1
	_world_height = max_y + 1
	return self


func with_themes_enabled() -> TestSimulationBuilder:
	_disable_themes = false
	return self


func with_start_hour(hour: int) -> TestSimulationBuilder:
	_start_hour = hour
	return self


func with_tax_multiplier(mult: float) -> TestSimulationBuilder:
	_tax_multiplier = mult
	return self


# -------------------------------------------------------------------------
# Content registration
# -------------------------------------------------------------------------

func define_need(
	key: String = "",
	decay_per_tick: float = 0.02,
	critical_threshold: float = 15.0,
	low_threshold: float = 35.0,
	critical_debuff: float = 0.0,
	low_debuff: float = 0.0,
	sprite_key: String = "question"
) -> int:
	return _content.register_need(key, {
		"decayPerTick":       decay_per_tick,
		"criticalThreshold":  critical_threshold,
		"lowThreshold":       low_threshold,
		"criticalDebuff":     critical_debuff,
		"lowDebuff":          low_debuff,
		"spriteKey":          sprite_key,
	})


func define_building(
	key: String = "",
	satisfies_need_id: int = -1,
	satisfaction_amount: float = 50.0,
	interaction_duration: int = 20,
	grants_buff: float = 0.0,
	buff_duration: int = 0,
	use_areas: Array = [],          # Array of Vector2i offsets; [] = use default adjacency
	tile_size: int = 1,
	base_cost: int = 10,
	base_production: float = 1.0,
	can_be_worked_at: bool = false,
	resource_type: String = "",
	max_resource_amount: float = 100.0,
	work_type: String = "direct",
	haul_source_resource_type: String = "",
	haul_source_terrain_key: String = "",
	can_sell_to_consumers: bool = true
) -> int:
	var haul_terrain_id: int = -1
	if not haul_source_terrain_key.is_empty():
		haul_terrain_id = _content.get_terrain_id(haul_source_terrain_key)

	var def: Dictionary = {
		"satisfiesNeedId":        satisfies_need_id,
		"satisfactionAmount":     satisfaction_amount,
		"interactionDuration":    interaction_duration,
		"grantsBuff":             grants_buff,
		"buffDuration":           buff_duration,
		"tileSize":               tile_size,
		"baseCost":               base_cost,
		"baseProduction":         base_production,
		"canBeWorkedAt":          can_be_worked_at,
		"resourceType":           resource_type,
		"maxResourceAmount":      max_resource_amount,
		"depletionMult":          1.0,
		"workType":               work_type,
		"haulSourceResourceType": haul_source_resource_type,
		"haulSourceTerrainKey":   haul_source_terrain_key,
		"haulSourceTerrainId":    haul_terrain_id,
		"canSellToConsumers":     can_sell_to_consumers,
		"capacity":               1,
		"capacityPerPhase":       [],
		"spriteKey":              "",
		"spriteVariants":         1,
		"spritePhases":           1,
	}
	# Explicit use areas override default adjacency logic in ActionSystem
	if not use_areas.is_empty():
		def["useAreas"] = use_areas
	return _content.register_building(key, def)


func define_terrain(
	key: String = "",
	walkable: bool = true,
	sprite_key: String = "",
	is_autotiling: bool = false,
	paints_to_base: bool = true
) -> int:
	# Use World.IMPASSABLE (1e9) instead of INF so JSON serialization works.
	var walk_cost: float = 1.0 if walkable else World.IMPASSABLE
	return _content.register_terrain(key, {
		"walkabilityCost": walk_cost,
		"blocksLight":     not walkable,
		"spriteKey":       sprite_key,
		"isAutotiling":    is_autotiling,
		"paintsToBase":    paints_to_base,
		"variantCount":    1,
	})


# -------------------------------------------------------------------------
# World entity setup (deferred until build())
# -------------------------------------------------------------------------

func add_pawn(name: String = "Pawn", x: int = 0, y: int = 0, needs: Dictionary = {}) -> void:
	_pending_pawns.append({"name": name, "x": x, "y": y, "needs": needs})


func add_building(building_def_id: int, x: int = 0, y: int = 0) -> void:
	_pending_buildings.append({"building_def_id": building_def_id, "x": x, "y": y})


# -------------------------------------------------------------------------
# Build
# -------------------------------------------------------------------------

func build():
	var sim := Simulation.new(
		_content,
		_seed,
		_start_hour,
		_world_width,
		_world_height,
		_disable_themes,
		_tax_multiplier
	)

	for b in _pending_buildings:
		sim.create_building(b["building_def_id"], Vector2i(b["x"], b["y"]))

	for p in _pending_pawns:
		var use_needs: Dictionary = p["needs"]
		if use_needs.is_empty():
			# Full needs for all registered needs
			for need_id in _content.needs.keys():
				use_needs[need_id] = 100.0
		sim.create_pawn_at(Vector2i(p["x"], p["y"]), p["name"], use_needs)

	return sim
