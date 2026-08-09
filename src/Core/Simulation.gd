class_name Simulation

const TICK_RATE: int = 20  # Ticks per real second

# How often to try spawning a new pawn (every 1/48th of an in-game day)
const PAWN_SPAWN_INTERVAL: int = TimeService.TICKS_PER_DAY / 48

# Attachment decay (ties fade over time unless kept up by repeat visits)
const ATTACHMENT_DECAY_INTERVAL: int = TimeService.TICKS_PER_DAY

# How much a single point of town attachment offsets mood when picking who emigrates —
# tuned so a handful of strong ties (e.g. 10 attachment at one building) can outweigh a
# moderately bad mood, but not a severely bad one.
const EMIGRATION_ATTACHMENT_WEIGHT: float = 3.0

var world: World
var entities: EntityManager
var content: ContentRegistry
var time: TimeService
var theme_system: ThemeSystem
var ai_system: AISystem

var sim_seed: int

var selected_palette_id: int = -1
var palette: Array[Color] = []

var _systems: SystemManager
var _pawns_pending_removal: Array[int] = []
var _emigrating_pawn_id: int = -1  # -1 = nobody currently mid-emigration


func _init(
	p_content: ContentRegistry,
	p_seed: int = -1,
	start_hour: int = TimeService.DEFAULT_START_HOUR,
	world_width: int = World.DEFAULT_WIDTH,
	world_height: int = World.DEFAULT_HEIGHT,
	disable_themes: bool = false
) -> void:
	content = p_content

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
	ai_system = AISystem.new()
	_systems.add(ai_system)


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

	# Pawns ActionSystem finished walking off the map edge this tick — destroyed here rather
	# than from inside ActionSystem's own "for pawn_id in entities.pawns" loop, since erasing
	# from that dictionary mid-iteration is unsafe.
	for pawn_id in _pawns_pending_removal:
		destroy_entity(pawn_id)
	_pawns_pending_removal.clear()

	# Pawn population — grows toward capacity, or sheds one pawn if over it (e.g. after a
	# home was downgraded/destroyed below the current population). Emigration only applies
	# once at least one home exists — a town with no homes yet (e.g. right at game start)
	# shouldn't have pawns evicted just for lacking housing that was never built.
	if time.tick % PAWN_SPAWN_INTERVAL == 0:
		# The walk to the map edge can easily take longer than this interval, so re-validate
		# rather than blindly re-checking — otherwise a still-walking emigrant looks like a
		# fresh "over capacity" case every interval and gets retargeted to a new edge before
		# ever arriving at the last one.
		if not _is_pawn_emigrating(_emigrating_pawn_id):
			_emigrating_pawn_id = -1

		var pawn_count: int = _colonist_count()
		var home_capacity: int = _total_home_capacity()
		if pawn_count < maxi(1, home_capacity):
			create_pawn()
		elif home_capacity > 0 and pawn_count > home_capacity and _emigrating_pawn_id == -1:
			_start_emigration()

	# Attachment decay
	if time.tick % ATTACHMENT_DECAY_INTERVAL == 0 and time.tick > 0:
		_perform_attachment_decay()


## Called by ActionSystem once a LEAVE_TOWN action reaches the map edge. Actual removal is
## deferred to the start of the next tick() — see there for why.
func mark_pawn_for_removal(pawn_id: int) -> void:
	_pawns_pending_removal.append(pawn_id)


# --- Emigration --------------------------------------------------------------

## Picks the pawn with the lowest combined mood + town attachment and sends them walking to
## the nearest map edge; ActionSystem removes them once they arrive (see mark_pawn_for_removal).
func _start_emigration() -> void:
	var pawn_id: int = _find_least_interesting_pawn()
	if pawn_id == -1:
		return
	if send_pawn_away(pawn_id):
		_emigrating_pawn_id = pawn_id


## Sends any pawn walking to a random map edge, then despawning once it arrives (see
## mark_pawn_for_removal / ActionSystem's LEAVE_TOWN handling). Reusable by any caller —
## capacity-driven colonist emigration and theme-driven visitor departure (remove_all_visitors)
## are just two different reasons to invoke the same underlying mechanism.
func send_pawn_away(pawn_id: int) -> bool:
	var edge: Vector2i = _get_random_walkable_edge_tile()
	if edge == Vector2i(-1, -1):
		return false
	var action_comp: Components.ActionComponent = entities.actions.get(pawn_id)
	if action_comp == null:
		return false

	var move := Definitions.ActionDef.new()
	move.type = Definitions.ActionType.MOVE_TO
	move.animation = Definitions.AnimationType.WALK
	move.target_coord = edge
	move.display_name = "Leaving town"

	var leave := Definitions.ActionDef.new()
	leave.type = Definitions.ActionType.LEAVE_TOWN
	leave.display_name = "Leaving town"

	action_comp.current_action = move
	action_comp.action_start_tick = time.tick
	action_comp.current_path.clear()
	action_comp.path_index = 0
	action_comp.action_queue.clear()
	action_comp.action_queue.push_back(leave)
	return true


## True if this pawn still has an active leave-town sequence (walking to the edge, or the
## final LEAVE_TOWN step). False once they've arrived and been removed, or if the sequence
## was abandoned (e.g. the edge tile turned out unreachable) — either way, safe to start a
## new emigration afterward.
func _is_pawn_emigrating(pawn_id: int) -> bool:
	if pawn_id == -1:
		return false
	var action_comp: Components.ActionComponent = entities.actions.get(pawn_id)
	if action_comp == null:
		return false
	if action_comp.current_action != null and action_comp.current_action.type == Definitions.ActionType.LEAVE_TOWN:
		return true
	if action_comp.current_action != null and action_comp.current_action.type == Definitions.ActionType.MOVE_TO:
		for queued in action_comp.action_queue:
			if queued.type == Definitions.ActionType.LEAVE_TOWN:
				return true
	return false


func _find_least_interesting_pawn() -> int:
	var worst_id: int = -1
	var worst_score: float = INF
	for pawn_id in entities.pawns:
		# Visitors always score exactly 0.0 (no needs -> no mood debuffs -> no attachment),
		# which can look "safer" than a real colonist with positive mood — capacity-driven
		# emigration must only ever pick real colonists. Visitors leave via remove_all_visitors.
		if entities.pawns[pawn_id].membership != Definitions.PawnMembership.COLONIST:
			continue
		var mood_comp: Components.MoodComponent = entities.moods.get(pawn_id)
		var mood: float = mood_comp.mood if mood_comp != null else 0.0
		var score: float = mood + _total_attachment(pawn_id) * EMIGRATION_ATTACHMENT_WEIGHT
		if score < worst_score:
			worst_score = score
			worst_id = pawn_id
	return worst_id


## Sum of a pawn's attachment strength across every building and need — how "tied down" they
## are to the town overall, regardless of which specific need or building it came from.
func _total_attachment(pawn_id: int) -> int:
	var total: int = 0
	for building_id in entities.buildings:
		var ac: Components.AttachmentComponent = entities.attachments.get(building_id)
		if ac == null:
			continue
		for need_id in ac.need_attachments:
			total += ac.need_attachments[need_id].get(pawn_id, 0)
	return total


# --- Pawn creation ---------------------------------------------------------

func create_pawn(name: String = "Pawn") -> int:
	var position: Vector2i = _get_random_walkable_edge_tile()
	if position == Vector2i(-1, -1):
		return -1
	return entities.create_pawn(position, name, _full_needs())


func create_pawn_at(coord: Vector2i, name: String = "Pawn", needs: Dictionary = {}) -> int:
	var use_needs: Dictionary = needs if not needs.is_empty() else _full_needs()
	return entities.create_pawn(coord, name, use_needs)


## Spawns a temporary theme-driven pawn (see SimTheme.on_start) at a random map edge, excluded
## from population/home-capacity accounting (see _colonist_count) and from capacity-driven
## emigration (see _find_least_interesting_pawn). needs defaults empty — a needs-less pawn
## just wanders forever via AISystem's normal "nothing urgent -> wander" fallback, no new AI
## logic required — but any needs dict can be passed for richer, still fully needs-driven
## behavior (e.g. a visitor that seeks food like any colonist would). forced_sheet_key always
## wins over any theme skin override or house-assigned sheet (see PawnView).
func spawn_visitor_pawn(forced_sheet_key: String, needs: Dictionary = {}, pawn_name: String = "Visitor") -> int:
	var position: Vector2i = _get_random_walkable_edge_tile()
	if position == Vector2i(-1, -1):
		return -1
	var pawn_id: int = entities.create_pawn(position, pawn_name, needs)
	var pawn_comp: Components.PawnComponent = entities.pawns[pawn_id]
	pawn_comp.membership = Definitions.PawnMembership.VISITOR
	pawn_comp.forced_sheet_key = forced_sheet_key
	return pawn_id


## Sends every current visitor pawn walking off the map (see spawn_visitor_pawn). Used by
## SimTheme.on_end when a theme's visitor event is over. A clean no-op if none exist.
func remove_all_visitors() -> void:
	for pawn_id in entities.pawns:
		if entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			send_pawn_away(pawn_id)


## Real colonist headcount for population/home-capacity purposes — excludes visitors, who were
## never part of the town's population to begin with (get_max_pawns/_total_home_capacity are
## building-driven and unaffected either way).
func _colonist_count() -> int:
	var count: int = 0
	for pawn_id in entities.pawns:
		if entities.pawns[pawn_id].membership == Definitions.PawnMembership.COLONIST:
			count += 1
	return count


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

	# A pawn's attachment strength lives on the buildings it visited, not on the pawn itself —
	# entities.destroy() only erases the destroyed entity's own component entries, so without
	# this an emigrated/removed pawn's entries would linger in every building it ever visited.
	for building_id in entities.buildings:
		var ac: Components.AttachmentComponent = entities.attachments.get(building_id)
		if ac == null:
			continue
		for need_id in ac.need_attachments:
			ac.need_attachments[need_id].erase(entity_id)

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

	ai_system.mark_world_dirty()
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
	ai_system.mark_world_dirty()

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
	return maxi(1, _total_home_capacity())


## Raw sum of home building capacity, with no floor — 0 means no homes have been built at
## all, which is treated differently from "over capacity" (see tick()'s emigration check).
func _total_home_capacity() -> int:
	var total: int = 0
	for obj_id in entities.buildings:
		var bc: Components.BuildingComponent = entities.buildings[obj_id]
		var bdef: Dictionary = content.buildings.get(bc.building_def_id, {})
		if bool(bdef.get("isHome", false)):
			total += int(bdef.get("capacity", 1))
	return total


# --- Homes -------------------------------------------------------------------

## All building ids whose def is marked isHome — the generic hook for any theme (or future
## system) that wants to affect "every house" without hardcoding names. See
## set_building_skin_override.
func get_home_building_ids() -> Array[int]:
	var result: Array[int] = []
	for building_id in entities.buildings:
		var bc: Components.BuildingComponent = entities.buildings[building_id]
		var bdef: Dictionary = content.buildings.get(bc.building_def_id, {})
		if bool(bdef.get("isHome", false)):
			result.append(building_id)
	return result


## Sets (or clears, with "") the sheet key a home hands out to whichever colonist is actively
## using it (see _build_pawn_snapshots' home_visit_skin_override) — in place of the
## deterministic per-house default. Not instant for every colonist who's ever lived there: it
## only reaches a pawn's rendered sheet on their NEXT visit (see PawnView.sync_house_sheet), so
## a change naturally rolls out house-by-house, visit-by-visit, rather than town-wide. See
## StrangeWorldsTheme for an example driving this across every home; a future theme could just
## as easily target one.
func set_building_skin_override(building_id: int, sheet_key: String) -> void:
	var bc: Components.BuildingComponent = entities.buildings.get(building_id)
	if bc != null:
		bc.skin_override = sheet_key


# --- Queries -----------------------------------------------------------------

## Finds a pawn entity id by its display name, or -1 if none match.
func find_pawn_by_name(name: String) -> int:
	for pawn_id in entities.pawns:
		if entities.pawns[pawn_id].name == name:
			return pawn_id
	return -1


## Returns a pawn's current value for the given need, or 0.0 if the pawn or need is unknown.
func get_need_value(pawn_id: int, need_id: int) -> float:
	var need_comp: Components.NeedsComponent = entities.needs.get(pawn_id)
	if need_comp == null:
		return 0.0
	return need_comp.needs.get(need_id, 0.0)


## Returns a pawn's current mood, or 0.0 if the pawn is unknown.
func get_mood(pawn_id: int) -> float:
	var mood_comp: Components.MoodComponent = entities.moods.get(pawn_id)
	return mood_comp.mood if mood_comp != null else 0.0


## Advances the simulation by the given number of ticks.
func run_ticks(count: int) -> void:
	for _i in count:
		tick()


# --- Map analysis ----------------------------------------------------------

# Reuses AISystem's cached diversity map (see AISystem._get_diversity_map) rather than
# recomputing the same per-tile scan independently.
func score_map_diversity() -> int:
	var diversity_map: Array = ai_system._get_diversity_map(world)
	var score: int = 0
	for value in diversity_map:
		score += value
	score += entities.buildings.size()
	return score


func _perform_attachment_decay() -> void:
	for building_id in entities.buildings:
		var ac: Components.AttachmentComponent = entities.attachments.get(building_id)
		if ac == null:
			continue

		for need_id in ac.need_attachments:
			var per_pawn: Dictionary = ac.need_attachments[need_id]
			var to_remove: Array[int] = []
			for pawn_id in per_pawn.keys():
				var new_strength: int = per_pawn[pawn_id] - 1
				if new_strength <= 0:
					to_remove.append(pawn_id)
				else:
					per_pawn[pawn_id] = new_strength

			for pawn_id in to_remove:
				per_pawn.erase(pawn_id)


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
		snap_theme["has_shadows"] = ct.has_shadows() if ct != null else true

	return {
		"pawns": _build_pawn_snapshots(),
		"buildings": _build_building_snapshots(),
		"time": snap_time,
		"theme": snap_theme,
		"palette": palette,
		"move_ticks_per_tile": ActionSystem.MOVE_TICKS_PER_TILE,
		"tick_rate": TICK_RATE,
	}


func _build_pawn_snapshots() -> Array:
	# Pre-build reverse attachment map: pawn_id -> {building_id: {need_id: strength}}
	var pawn_attachments: Dictionary = {}
	for building_id in entities.buildings:
		var ac: Components.AttachmentComponent = entities.attachments.get(building_id)
		if ac == null:
			continue
		var breakdown: Dictionary = ac.per_pawn_breakdown()
		for attached_pawn_id in breakdown:
			if not pawn_attachments.has(attached_pawn_id):
				pawn_attachments[attached_pawn_id] = {}
			pawn_attachments[attached_pawn_id][building_id] = breakdown[attached_pawn_id]

	var result: Array = []
	for pawn_id in entities.pawns:
		var pos: Components.PositionComponent = entities.positions.get(pawn_id)
		if pos == null:
			continue

		var mood_comp: Components.MoodComponent = entities.moods.get(pawn_id)
		var inv_comp: Components.InventoryComponent = entities.inventory.get(pawn_id)
		var pawn_comp: Components.PawnComponent = entities.pawns.get(pawn_id)

		var animation: int = Definitions.AnimationType.IDLE
		var current_action_name: String = "Idle"
		var current_action_type: int = Definitions.ActionType.IDLE
		var has_expression: bool = false
		var expression: int = Definitions.ExpressionType.THOUGHT
		var expression_icon_def_id: int = -1
		var path_coords: Array = []
		var path_index: int = 0
		var target_tile: Vector2i = Vector2i(-1, -1)
		var has_building_target: bool = false

		var action_comp: Components.ActionComponent = entities.actions.get(pawn_id)
		# "Going home" in the literal sense — actively using an isHome building right now (i.e.
		# sleeping there), not a stored assignment or an attachment-strength computation. This
		# is what gates when a pawn's rendered sheet re-syncs to that house's current look (see
		# PawnView.sync_house_sheet): a house's skin_override change only reaches a pawn on
		# their NEXT visit, not instantly for everyone who's ever slept there.
		var home_visit_building_id: int = -1
		var home_visit_skin_override: String = ""
		if action_comp != null and action_comp.current_action != null:
			var act: Definitions.ActionDef = action_comp.current_action
			animation = act.animation
			current_action_name = act.display_name if not act.display_name.is_empty() else "Idle"
			current_action_type = act.type
			has_expression = act.has_expression
			expression = act.expression
			expression_icon_def_id = act.expression_icon_def_id
			target_tile = act.target_coord
			has_building_target = act.target_entity != -1
			path_index = action_comp.path_index
			for coord in action_comp.current_path:
				path_coords.append({"x": coord.x, "y": coord.y})

			if act.type == Definitions.ActionType.USE_BUILDING and act.target_entity != -1:
				var target_bc: Components.BuildingComponent = entities.buildings.get(act.target_entity)
				if target_bc != null:
					var target_bdef: Dictionary = content.buildings.get(target_bc.building_def_id, {})
					if bool(target_bdef.get("isHome", false)):
						home_visit_building_id = act.target_entity
						home_visit_skin_override = target_bc.skin_override

		result.append({
			"id": pawn_id,
			"x": pos.coord.x,
			"y": pos.coord.y,
			"mood": mood_comp.mood if mood_comp != null else 0.0,
			"carrying_resource_type": inv_comp.resource_type if inv_comp != null else "",
			"forced_sheet_key": pawn_comp.forced_sheet_key if pawn_comp != null else "",
			"home_visit_building_id": home_visit_building_id,
			"home_visit_skin_override": home_visit_skin_override,
			"animation": animation,
			"current_action": current_action_name,
			"current_action_type": current_action_type,
			"has_building_target": has_building_target,
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
	for building_id in entities.buildings:
		var pos: Components.PositionComponent = entities.positions.get(building_id)
		var bc: Components.BuildingComponent = entities.buildings.get(building_id)
		if pos == null or bc == null:
			continue

		var bdef: Dictionary = content.buildings.get(bc.building_def_id, {})

		# Count active users and find first user for display
		var current_users: int = 0
		var in_use: bool = false
		var used_by_pawn_id: int = -1
		for pawn_id in entities.pawns:
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

		var rc: Components.ResourceComponent = entities.resources.get(building_id)
		var ac: Components.AttachmentComponent = entities.attachments.get(building_id)

		var attachments: Dictionary = ac.per_pawn_breakdown() if ac != null else {}

		result.append({
			"id": building_id,
			"x": pos.coord.x,
			"y": pos.coord.y,
			"building_def_id": bc.building_def_id,
			"name": bdef.get("name", "Building"),
			"in_use": in_use,
			"used_by_name": used_by_name,
			"color_index": bc.color_index,
			"capacity": int(bdef.get("capacity", 1)),
			"current_users": current_users,
			"resource_type": rc.resource_type if rc != null else "",
			"current_resource": rc.current_amount if rc != null else -1.0,
			"max_resource": rc.max_amount if rc != null else -1.0,
			"can_be_worked_at": bool(bdef.get("canBeWorkedAt", false)),
			"attachments": attachments,
		})
	return result
