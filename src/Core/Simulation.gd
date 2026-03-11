class_name Simulation

const TICK_RATE: int = 20  # Ticks per real second

# How often to try spawning a new pawn (every 1/48th of an in-game day)
const PAWN_SPAWN_INTERVAL: int = TimeService.TICKS_PER_DAY / 48

# Economy
const TAX_INTERVAL: int = TimeService.TICKS_PER_DAY
const TAX_RATE: float = 15.0            # Percent collected each interval
const DEFAULT_TAX_MULTIPLIER: float = 1.1  # Idle-game growth multiplier

# Attachment decay (weak ties fade over time)
const ATTACHMENT_DECAY_INTERVAL: int = TimeService.TICKS_PER_DAY
const ATTACHMENT_DECAY_THRESHOLD: int = 5  # Only decay attachments at or below this

var world: World
var entities: EntityManager
var content: ContentRegistry
var time: TimeService
var theme_system: ThemeSystem

var sim_seed: int
var tax_pool: int = 0

var selected_palette_id: int = -1
var palette: Array[Color] = []

var _systems: SystemManager
var _tax_multiplier: float = DEFAULT_TAX_MULTIPLIER


func _init(
	p_content: ContentRegistry,
	p_seed: int = -1,
	start_hour: int = TimeService.DEFAULT_START_HOUR,
	world_width: int = World.DEFAULT_WIDTH,
	world_height: int = World.DEFAULT_HEIGHT,
	disable_themes: bool = false,
	tax_multiplier: float = DEFAULT_TAX_MULTIPLIER
) -> void:
	content = p_content
	_tax_multiplier = tax_multiplier

	sim_seed = p_seed if p_seed >= 0 else randi()
	seed(sim_seed)

	time = TimeService.new(start_hour)
	world = World.new(world_width, world_height)
	entities = EntityManager.new()

	_initialize_world_terrain()

	# Palette selection — deterministic from seed
	var palette_keys: Array = content.palettes.keys()
	if not palette_keys.is_empty():
		selected_palette_id = palette_keys[sim_seed % palette_keys.size()]
		var palette_def: Dictionary = content.palettes[selected_palette_id]
		palette.assign(palette_def.get("colors", []))

	theme_system = ThemeSystem.new(disable_themes)

	_systems = SystemManager.new()
	_systems.add(NeedsSystem.new())
	_systems.add(BuffSystem.new())
	_systems.add(MoodSystem.new())
	if not disable_themes:
		_systems.add(theme_system)
	_systems.add(ActionSystem.new())
	_systems.add(AISystem.new())


# Initialize all world tiles with Flat terrain.
func _initialize_world_terrain() -> void:
	var flat_id: int = -1
	for id in content.terrains:
		var tdef: Dictionary = content.terrains[id]
		if tdef.get("spriteKey", "") == "flat":
			flat_id = id
			break

	if flat_id == -1:
		return

	var flat_def: Dictionary = content.terrains[flat_id]
	for x in world.width:
		for y in world.height:
			var tile: World.Tile = world.get_tile_xy(x, y)
			tile.base_terrain_type_id = flat_id
			tile.color_index = 2  # Default color #3
			tile.walkability_cost = float(flat_def.get("walkabilityCost", 1.0))
			tile.blocks_light = bool(flat_def.get("blocksLight", false))


# --- Tick ------------------------------------------------------------------

func tick() -> void:
	_systems.tick_all(self)
	time.advance_tick()

	# Pawn spawning
	if time.tick % PAWN_SPAWN_INTERVAL == 0:
		var pawn_count: int = entities.all_pawns().size()
		if pawn_count < get_max_pawns():
			create_pawn()

	# Tax redistribution
	if time.tick % TAX_INTERVAL == 0 and time.tick > 0:
		_perform_tax_redistribution()

	# Attachment decay
	if time.tick % ATTACHMENT_DECAY_INTERVAL == 0 and time.tick > 0:
		_perform_attachment_decay()


# --- Pawn creation ---------------------------------------------------------

func create_pawn(name: String = "Pawn") -> int:
	var position: Vector2i = _get_random_walkable_edge_tile()
	if position == Vector2i(-1, -1):
		return -1
	return entities.create_pawn(position, name, _full_needs())


func create_pawn_at(coord: Vector2i, name: String = "Pawn", needs: Dictionary = {}) -> int:
	var use_needs: Dictionary = needs if not needs.is_empty() else _full_needs()
	return entities.create_pawn(coord, name, use_needs)


func _full_needs() -> Dictionary:
	var needs: Dictionary = {}
	for need_id in content.needs.keys():
		needs[need_id] = 100.0
	return needs


func _get_random_walkable_edge_tile() -> Vector2i:
	var candidates: Array[Vector2i] = []
	for x in world.width:
		for y in world.height:
			var is_edge: bool = (x == 0 or x == world.width - 1 or y == 0 or y == world.height - 1)
			if is_edge and world.is_walkable(Vector2i(x, y)):
				candidates.append(Vector2i(x, y))
	if candidates.is_empty():
		return Vector2i(-1, -1)
	return candidates[randi() % candidates.size()]


# --- Building creation / destruction ---------------------------------------

# Returns the new entity ID, or -1 on failure.
func create_building(building_def_id: int, coord: Vector2i, color_index: int = 0) -> int:
	var building_def: Dictionary = content.buildings.get(building_def_id, {})
	if building_def.is_empty():
		push_error("Simulation.create_building: unknown building_def_id %d" % building_def_id)
		return -1

	var tile_size: int = int(building_def.get("tileSize", 1))
	var occupied: Array[Vector2i] = BuildingUtilities.get_occupied_tiles(coord, tile_size)

	for tile_coord in occupied:
		if not world.is_in_bounds(tile_coord):
			push_error("Simulation.create_building: tile %s out of bounds" % tile_coord)
			return -1

	for tile_coord in occupied:
		if not world.get_tile(tile_coord).walkable:
			push_error("Simulation.create_building: tile %s already occupied" % tile_coord)
			return -1

	var palette_size: int = palette.size() if not palette.is_empty() else 1
	var safe_color: int = color_index % palette_size

	var entity_id: int = entities.create_building(coord, building_def_id, safe_color)

	# Resource component
	var resource_type: String = building_def.get("resourceType", "")
	if not resource_type.is_empty():
		var max_amount: float = float(building_def.get("maxResourceAmount", 100.0))
		var rc := Components.ResourceComponent.new()
		rc.resource_type = resource_type
		rc.current_amount = max_amount
		rc.max_amount = max_amount
		rc.depletion_mult = float(building_def.get("depletionMult", 1.0))
		entities.resources[entity_id] = rc

	# Attachment component (all buildings track which pawns use them)
	entities.attachments[entity_id] = Components.AttachmentComponent.new()

	# Block movement on all occupied tiles
	for tile_coord in occupied:
		world.get_tile(tile_coord).building_blocks_movement = true

	return entity_id


func destroy_entity(entity_id: int) -> void:
	var building_comp: Components.BuildingComponent = entities.buildings.get(entity_id)
	if building_comp != null:
		var pos: Components.PositionComponent = entities.positions.get(entity_id)
		if pos != null:
			var building_def: Dictionary = content.buildings[building_comp.building_def_id]
			var tile_size: int = int(building_def.get("tileSize", 1))
			var occupied: Array[Vector2i] = BuildingUtilities.get_occupied_tiles(pos.coord, tile_size)
			for tile_coord in occupied:
				if world.is_in_bounds(tile_coord):
					world.get_tile(tile_coord).building_blocks_movement = false

		# Capture building's gold into tax pool before destruction
		var gold: Components.GoldComponent = entities.gold.get(entity_id)
		if gold != null and gold.amount > 0:
			tax_pool += gold.amount

	entities.destroy(entity_id)


# --- Terrain painting ------------------------------------------------------

# Returns the painted tile coord + all 8 neighbors (for rendering updates).
func paint_terrain(coord: Vector2i, terrain_def_id: int, color_index: int = 0) -> Array[Vector2i]:
	if not world.is_in_bounds(coord):
		return []

	var terrain_def: Dictionary = content.terrains.get(terrain_def_id, {})
	if terrain_def.is_empty():
		push_error("Simulation.paint_terrain: unknown terrain_def_id %d" % terrain_def_id)
		return []

	var tile: World.Tile = world.get_tile(coord)
	var palette_size: int = palette.size() if not palette.is_empty() else 1
	var safe_color: int = color_index % palette_size
	var variant_count: int = int(terrain_def.get("variantCount", 1))

	if bool(terrain_def.get("paintsToBase", false)):
		tile.base_terrain_type_id = terrain_def_id
		tile.color_index = safe_color
		tile.base_variant_index = randi() % variant_count if variant_count > 1 else 0
	else:
		tile.overlay_terrain_type_id = terrain_def_id
		tile.overlay_color_index = safe_color
		tile.overlay_variant_index = randi() % variant_count if variant_count > 1 else 0

	tile.walkability_cost = float(terrain_def.get("walkabilityCost", 1.0))
	tile.blocks_light = bool(terrain_def.get("blocksLight", false))

	return get_tiles_with_neighbors([coord])


func flood_fill(start: Vector2i, new_terrain_id: int, new_color_index: int) -> Array[Vector2i]:
	var tiles_to_paint: Array[Vector2i] = _get_flood_tiles(start)
	if tiles_to_paint.is_empty():
		return []

	var first_tile: World.Tile = world.get_tile(tiles_to_paint[0])
	var new_terrain_def: Dictionary = content.terrains.get(new_terrain_id, {})
	if new_terrain_def.is_empty():
		return []

	# Skip if already this terrain + color
	if first_tile.base_terrain_type_id == new_terrain_id and first_tile.color_index == new_color_index:
		return []

	var affected: Dictionary = {}  # Using dict as set for deduplication
	for coord in tiles_to_paint:
		for affected_coord in paint_terrain(coord, new_terrain_id, new_color_index):
			affected[affected_coord] = true

	var result: Array[Vector2i] = []
	for coord in affected.keys():
		result.append(coord)
	return result


func paint_rectangle(start: Vector2i, end: Vector2i, terrain_def_id: int, color_index: int = 0) -> Array[Vector2i]:
	var affected: Dictionary = {}
	for coord in _get_rectangle_tiles(start, end):
		for affected_coord in paint_terrain(coord, terrain_def_id, color_index):
			affected[affected_coord] = true
	var result: Array[Vector2i] = []
	for coord in affected.keys():
		result.append(coord)
	return result


func paint_rectangle_outline(start: Vector2i, end: Vector2i, terrain_def_id: int, color_index: int = 0) -> Array[Vector2i]:
	var affected: Dictionary = {}
	for coord in _get_rectangle_outline_tiles(start, end):
		for affected_coord in paint_terrain(coord, terrain_def_id, color_index):
			affected[affected_coord] = true
	var result: Array[Vector2i] = []
	for coord in affected.keys():
		result.append(coord)
	return result


# --- Deletion tools --------------------------------------------------------

# Smart delete: building > overlay > reset base to flat.
func delete_at_tile(coord: Vector2i) -> Array[Vector2i]:
	if not world.is_in_bounds(coord):
		return []

	if try_delete_building(coord):
		return get_tiles_with_neighbors([coord])

	var tile: World.Tile = world.get_tile(coord)

	# Clear overlay if present
	if tile.overlay_terrain_type_id != -1:
		tile.overlay_terrain_type_id = -1
		# Restore walkability from base terrain
		var base_def: Dictionary = content.terrains.get(tile.base_terrain_type_id, {})
		if not base_def.is_empty():
			tile.walkability_cost = float(base_def.get("walkabilityCost", 1.0))
			tile.blocks_light = bool(base_def.get("blocksLight", false))
		return get_tiles_with_neighbors([coord])

	# Reset base to flat
	var flat_id: int = _get_flat_terrain_id()
	if flat_id != -1:
		var flat_def: Dictionary = content.terrains[flat_id]
		tile.base_terrain_type_id = flat_id
		tile.walkability_cost = float(flat_def.get("walkabilityCost", 1.0))
		tile.blocks_light = bool(flat_def.get("blocksLight", false))
		tile.color_index = 2

	return get_tiles_with_neighbors([coord])


func try_delete_building(coord: Vector2i) -> bool:
	for obj_id in entities.all_buildings():
		var pos: Components.PositionComponent = entities.positions.get(obj_id)
		var building_comp: Components.BuildingComponent = entities.buildings.get(obj_id)
		if pos == null or building_comp == null:
			continue
		var building_def: Dictionary = content.buildings[building_comp.building_def_id]
		var tile_size: int = int(building_def.get("tileSize", 1))
		var occupied: Array[Vector2i] = BuildingUtilities.get_occupied_tiles(pos.coord, tile_size)
		if occupied.has(coord):
			destroy_entity(obj_id)
			return true
	return false


func flood_delete(start: Vector2i) -> Array[Vector2i]:
	var affected: Dictionary = {}
	for coord in _get_flood_tiles(start):
		for affected_coord in delete_at_tile(coord):
			affected[affected_coord] = true
	var result: Array[Vector2i] = []
	for coord in affected.keys():
		result.append(coord)
	return result


func delete_rectangle(start: Vector2i, end: Vector2i) -> Array[Vector2i]:
	var affected: Dictionary = {}
	for coord in _get_rectangle_tiles(start, end):
		for affected_coord in delete_at_tile(coord):
			affected[affected_coord] = true
	var result: Array[Vector2i] = []
	for coord in affected.keys():
		result.append(coord)
	return result


func delete_rectangle_outline(start: Vector2i, end: Vector2i) -> Array[Vector2i]:
	var affected: Dictionary = {}
	for coord in _get_rectangle_outline_tiles(start, end):
		for affected_coord in delete_at_tile(coord):
			affected[affected_coord] = true
	var result: Array[Vector2i] = []
	for coord in affected.keys():
		result.append(coord)
	return result


# --- Palette ---------------------------------------------------------------

func cycle_palette() -> void:
	var palette_ids: Array = content.palettes.keys()
	if palette_ids.is_empty():
		return
	palette_ids.sort()
	var current_index: int = palette_ids.find(selected_palette_id)
	var next_index: int = (current_index + 1) % palette_ids.size()
	selected_palette_id = palette_ids[next_index]
	var palette_def: Dictionary = content.palettes[selected_palette_id]
	palette = palette_def.get("colors", [])


# --- Capacity / pawns ------------------------------------------------------

func get_max_pawns() -> int:
	var home_id: int = content.get_building_id("Home")
	if home_id == -1:
		return 1

	var home_def: Dictionary = content.buildings[home_id]
	var total: int = 0

	for obj_id in entities.all_buildings():
		var bc: Components.BuildingComponent = entities.buildings.get(obj_id)
		if bc == null or bc.building_def_id != home_id:
			continue
		var phase: int = _get_building_phase(obj_id)
		total += _get_capacity(home_def, phase)

	return maxi(1, total)


# --- Map analysis ----------------------------------------------------------

func score_map_diversity() -> int:
	var diversity_map: Array = _compute_diversity_map()
	var score: int = 0
	for value in diversity_map:
		score += value
	score += entities.all_buildings().size()
	return score


func _compute_diversity_map() -> Array:
	var scores: Array = []
	scores.resize(world.width * world.height)
	for i in scores.size():
		scores[i] = 0

	for x in world.width:
		for y in world.height:
			var x_score: int = 0
			var y_score: int = 0
			var tile: World.Tile = world.get_tile_xy(x, y)
			var tile_hash: int = tile.tile_hash

			if x > 0:
				x_score = scores[(x - 1) + y * world.width]
				if tile_hash != world.get_tile_xy(x - 1, y).tile_hash:
					x_score += 1
				elif x_score > 0:
					x_score -= 1

			if y > 0:
				y_score = scores[x + (y - 1) * world.width]
				if tile_hash != world.get_tile_xy(x, y - 1).tile_hash:
					y_score += 1
				elif y_score > 0:
					y_score -= 1

			scores[x + y * world.width] = mini(9, (x_score + y_score) / 2)

	return scores


# --- Economy ---------------------------------------------------------------

func get_total_wealth() -> int:
	var total: int = tax_pool
	for gold in entities.gold.values():
		total += gold.amount
	return total


func _perform_tax_redistribution() -> void:
	# Collect from all pawns
	for pawn_id in entities.all_pawns():
		var gold: Components.GoldComponent = entities.gold.get(pawn_id)
		if gold != null and gold.amount > 0:
			var tax: int = int(gold.amount * TAX_RATE / 100.0)
			gold.amount -= tax
			tax_pool += tax

	# Collect from all buildings
	for building_id in entities.all_buildings():
		var gold: Components.GoldComponent = entities.gold.get(building_id)
		if gold != null and gold.amount > 0:
			var tax: int = int(gold.amount * TAX_RATE / 100.0)
			gold.amount -= tax
			tax_pool += tax

	# Apply idle-game multiplier
	tax_pool = int(tax_pool * _tax_multiplier)

	if tax_pool == 0:
		return

	# Distribute to all pawns + workable buildings
	var recipients: Array[int] = []
	for pawn_id in entities.all_pawns():
		recipients.append(pawn_id)
	for building_id in entities.all_buildings():
		var bc: Components.BuildingComponent = entities.buildings.get(building_id)
		if bc != null:
			var bdef: Dictionary = content.buildings.get(bc.building_def_id, {})
			if bool(bdef.get("canBeWorkedAt", false)):
				recipients.append(building_id)

	if recipients.is_empty():
		return

	var per_recipient: int = tax_pool / recipients.size()
	var remainder: int = tax_pool % recipients.size()

	for recipient_id in recipients:
		var gold: Components.GoldComponent = entities.gold.get(recipient_id)
		if gold != null:
			gold.amount += per_recipient

	tax_pool = remainder  # Carry over remainder


func _perform_attachment_decay() -> void:
	for building_id in entities.all_buildings():
		var ac: Components.AttachmentComponent = entities.attachments.get(building_id)
		if ac == null:
			continue

		var to_remove: Array[int] = []
		for pawn_id in ac.user_attachments.keys():
			var strength: int = ac.user_attachments[pawn_id]
			if strength <= ATTACHMENT_DECAY_THRESHOLD:
				var new_strength: int = strength - 1
				if new_strength <= 0:
					to_remove.append(pawn_id)
				else:
					ac.user_attachments[pawn_id] = new_strength

		for pawn_id in to_remove:
			ac.user_attachments.erase(pawn_id)


# --- Tile geometry helpers -------------------------------------------------

func get_tiles_with_neighbors(coords: Array) -> Array[Vector2i]:
	var result: Dictionary = {}
	var offsets := [
		Vector2i(-1, -1), Vector2i(0, -1), Vector2i(1, -1),
		Vector2i(-1, 0),  Vector2i(0, 0),  Vector2i(1, 0),
		Vector2i(-1, 1),  Vector2i(0, 1),  Vector2i(1, 1),
	]
	for coord in coords:
		for offset in offsets:
			var neighbor: Vector2i = coord + offset
			if world.is_in_bounds(neighbor):
				result[neighbor] = true
	var out: Array[Vector2i] = []
	for coord in result.keys():
		out.append(coord)
	return out


func _get_flood_tiles(start: Vector2i) -> Array[Vector2i]:
	if not world.is_in_bounds(start):
		return []

	var target_hash: int = world.get_tile(start).tile_hash
	var visited: Dictionary = {}
	var queue: Array[Vector2i] = [start]
	visited[start] = true

	while not queue.is_empty():
		var coord: Vector2i = queue.pop_front()
		for d in [Vector2i(0, -1), Vector2i(1, 0), Vector2i(0, 1), Vector2i(-1, 0)]:
			var neighbor: Vector2i = coord + d
			if world.is_in_bounds(neighbor) and not visited.has(neighbor):
				if world.get_tile(neighbor).tile_hash == target_hash:
					visited[neighbor] = true
					queue.push_back(neighbor)

	var result: Array[Vector2i] = []
	for coord in visited.keys():
		result.append(coord)
	return result


func _get_rectangle_tiles(start: Vector2i, end: Vector2i) -> Array[Vector2i]:
	var min_x: int = mini(start.x, end.x)
	var max_x: int = maxi(start.x, end.x)
	var min_y: int = mini(start.y, end.y)
	var max_y: int = maxi(start.y, end.y)
	var result: Array[Vector2i] = []
	for x in range(min_x, max_x + 1):
		for y in range(min_y, max_y + 1):
			result.append(Vector2i(x, y))
	return result


func _get_rectangle_outline_tiles(start: Vector2i, end: Vector2i) -> Array[Vector2i]:
	var min_x: int = mini(start.x, end.x)
	var max_x: int = maxi(start.x, end.x)
	var min_y: int = mini(start.y, end.y)
	var max_y: int = maxi(start.y, end.y)
	var result: Dictionary = {}
	for x in range(min_x, max_x + 1):
		result[Vector2i(x, min_y)] = true
		result[Vector2i(x, max_y)] = true
	for y in range(min_y + 1, max_y):
		result[Vector2i(min_x, y)] = true
		result[Vector2i(max_x, y)] = true
	var out: Array[Vector2i] = []
	for coord in result.keys():
		out.append(coord)
	return out


# --- Private helpers -------------------------------------------------------

func _get_flat_terrain_id() -> int:
	for id in content.terrains:
		if content.terrains[id].get("spriteKey", "") == "flat":
			return id
	return -1


func _get_building_phase(building_id: int) -> int:
	var ac: Components.AttachmentComponent = entities.attachments.get(building_id)
	if ac == null:
		return 0
	var max_wealth: int = 0
	for pawn_id in ac.user_attachments.keys():
		var gold: Components.GoldComponent = entities.gold.get(pawn_id)
		if gold != null:
			max_wealth = maxi(max_wealth, gold.amount)
	return max_wealth / 100


func _get_capacity(building_def: Dictionary, phase: int) -> int:
	var per_phase: Array = building_def.get("capacityPerPhase", [])
	if not per_phase.is_empty():
		return int(per_phase[clampi(phase, 0, per_phase.size() - 1)])
	return int(building_def.get("capacity", 1))


# --- Debug / display -------------------------------------------------------

func format_entity_id(entity_id: int) -> String:
	if entities.pawns.has(entity_id):
		return "Pawn #%d" % entity_id
	var bc: Components.BuildingComponent = entities.buildings.get(entity_id)
	if bc != null:
		var bdef: Dictionary = content.buildings.get(bc.building_def_id, {})
		return "%s #%d" % [bdef.get("name", "Building"), entity_id]
	return "Entity #%d" % entity_id


# --- Rendering snapshot ----------------------------------------------------

func create_render_snapshot() -> Dictionary:
	var snap_time: Dictionary = {
		"hour": time.hour,
		"minute": time.minute,
		"day": time.day,
		"is_night": time.is_night,
		"time_string": time.time_string,
		"day_fraction": float(time.tick % TimeService.TICKS_PER_DAY) / float(TimeService.TICKS_PER_DAY),
	}

	var snap_theme: Dictionary = {}
	if theme_system != null:
		var ct = theme_system.current_theme
		var qt = theme_system.queued_theme
		snap_theme["current_theme_name"] = ct.get_name() if ct != null else ""
		snap_theme["current_music_file"] = ct.get_music_file() if ct != null else ""
		snap_theme["queued_theme_name"] = qt.get_name() if qt != null else ""

	return {
		"pawns": _build_pawn_snapshots(),
		"buildings": _build_building_snapshots(),
		"time": snap_time,
		"theme": snap_theme,
		"palette": palette,
	}


func _build_pawn_snapshots() -> Array:
	# Pre-build reverse attachment map: pawn_id -> {building_id: strength}
	var pawn_attachments: Dictionary = {}
	for building_id in entities.all_buildings():
		var ac: Components.AttachmentComponent = entities.attachments.get(building_id)
		if ac == null:
			continue
		for attached_pawn_id in ac.user_attachments.keys():
			if not pawn_attachments.has(attached_pawn_id):
				pawn_attachments[attached_pawn_id] = {}
			pawn_attachments[attached_pawn_id][building_id] = ac.user_attachments[attached_pawn_id]

	var result: Array = []
	for pawn_id in entities.all_pawns():
		var pos: Components.PositionComponent = entities.positions.get(pawn_id)
		if pos == null:
			continue

		var mood_comp: Components.MoodComponent = entities.moods.get(pawn_id)
		var gold_comp: Components.GoldComponent = entities.gold.get(pawn_id)

		var animation: int = Definitions.AnimationType.IDLE
		var current_action_name: String = "Idle"
		var current_action_type: int = Definitions.ActionType.IDLE
		var has_expression: bool = false
		var expression: int = Definitions.ExpressionType.THOUGHT
		var expression_icon_def_id: int = -1
		var path_coords: Array = []
		var path_index: int = 0
		var target_tile: Vector2i = Vector2i(-1, -1)

		var action_comp: Components.ActionComponent = entities.actions.get(pawn_id)
		if action_comp != null and action_comp.current_action != null:
			var act: Definitions.ActionDef = action_comp.current_action
			animation = act.animation
			current_action_name = act.display_name if not act.display_name.is_empty() else "Idle"
			current_action_type = act.type
			has_expression = act.has_expression
			expression = act.expression
			expression_icon_def_id = act.expression_icon_def_id
			target_tile = act.target_coord
			path_index = action_comp.path_index
			for coord in action_comp.current_path:
				path_coords.append({"x": coord.x, "y": coord.y})

		result.append({
			"id": pawn_id,
			"x": pos.coord.x,
			"y": pos.coord.y,
			"mood": mood_comp.mood if mood_comp != null else 0.0,
			"gold": gold_comp.amount if gold_comp != null else 0,
			"animation": animation,
			"current_action": current_action_name,
			"current_action_type": current_action_type,
			"has_expression": has_expression,
			"expression": expression,
			"expression_icon_def_id": expression_icon_def_id,
			"current_path": path_coords,
			"path_index": path_index,
			"target_tile": target_tile,
			"attachments": pawn_attachments.get(pawn_id, {}),
		})
	return result


func _build_building_snapshots() -> Array:
	var result: Array = []
	for building_id in entities.all_buildings():
		var pos: Components.PositionComponent = entities.positions.get(building_id)
		var bc: Components.BuildingComponent = entities.buildings.get(building_id)
		if pos == null or bc == null:
			continue

		var bdef: Dictionary = content.buildings.get(bc.building_def_id, {})
		var phase: int = _get_building_phase(building_id)

		# Count active users and find first user for display
		var current_users: int = 0
		var in_use: bool = false
		var used_by_pawn_id: int = -1
		for pawn_id in entities.all_pawns():
			var action_comp: Components.ActionComponent = entities.actions.get(pawn_id)
			if action_comp == null:
				continue
			if action_comp.current_action != null and action_comp.current_action.target_entity == building_id:
				current_users += 1
				in_use = true
				if used_by_pawn_id == -1:
					used_by_pawn_id = pawn_id
			else:
				for queued in action_comp.action_queue:
					if queued.target_entity == building_id:
						in_use = true
						if used_by_pawn_id == -1:
							used_by_pawn_id = pawn_id
						break

		var used_by_name: String = ""
		if used_by_pawn_id != -1:
			var pawn_comp: Components.PawnComponent = entities.pawns.get(used_by_pawn_id)
			if pawn_comp != null:
				used_by_name = pawn_comp.name

		var gold_comp: Components.GoldComponent = entities.gold.get(building_id)
		var rc: Components.ResourceComponent = entities.resources.get(building_id)
		var ac: Components.AttachmentComponent = entities.attachments.get(building_id)

		var attachments: Dictionary = {}
		var max_pawn_wealth: int = 0
		if ac != null:
			attachments = ac.user_attachments.duplicate()
			for attached_pawn_id in ac.user_attachments.keys():
				var pg: Components.GoldComponent = entities.gold.get(attached_pawn_id)
				if pg != null:
					max_pawn_wealth = maxi(max_pawn_wealth, pg.amount)

		result.append({
			"id": building_id,
			"x": pos.coord.x,
			"y": pos.coord.y,
			"building_def_id": bc.building_def_id,
			"name": bdef.get("name", "Building"),
			"in_use": in_use,
			"used_by_name": used_by_name,
			"color_index": bc.color_index,
			"max_pawn_wealth": max_pawn_wealth,
			"gold": gold_comp.amount if gold_comp != null else 0,
			"capacity": _get_capacity(bdef, phase),
			"current_users": current_users,
			"phase": phase,
			"cost": int(bdef.get("useCost", 0)),
			"resource_type": rc.resource_type if rc != null else "",
			"current_resource": rc.current_amount if rc != null else -1.0,
			"max_resource": rc.max_amount if rc != null else -1.0,
			"can_be_worked_at": bool(bdef.get("canBeWorkedAt", false)),
			"work_buy_in": int(bdef.get("workBuyIn", 0)),
			"payout": int(bdef.get("payout", 0)),
			"attachments": attachments,
		})
	return result
