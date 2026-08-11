class_name AISystem

const NEED_THRESHOLD: float = 90.0

# How long a pawn with no valid action (no reachable building, no walkable wander candidate)
# waits before retrying — without this, AISystem retries the full decision (building search +
# wander candidate search) on that pawn every single tick, forever, since an empty queue never
# trips the "already has something to do" skip.
const STUCK_RETRY_DELAY_TICKS: int = 30

# Economy balance — tuned so hauling can roughly keep pace with consumption (see
# ActionSystem.CONSUMER_DEPLETION_AMOUNT for the demand side of this).
const WORK_DURATION_TICKS: int = 2000
const HAUL_AMOUNT: float = 80.0
const HARVEST_DURATION_TICKS: int = 750

# Chance, per wander decision, that an idle colonist swings by home for a brief, purely
# cosmetic visit instead of aimless wandering — see _wander_randomly. This is what actually
# gets a house's skin_override (see Simulation.set_building_skin_override) onto colonists
# during normal play, since real Energy-driven home visits only happen at night.
const WANDER_HOME_VISIT_CHANCE: float = 0.06
const WANDER_HOME_VISIT_DURATION_TICKS: int = 40

# Chance, per wander decision, that the pawn sits down for a long, deliberate observe/emote
# moment about their best/worst need (see _decide_need_expression) instead of a brief plain
# idle — roughly every few wander cycles on average, not every single one.
const SIT_AND_EMOTE_CHANCE: float = 0.3

# Cached per-tile diversity scores (see _compute_diversity_map). Recomputing this over the
# full world grid is expensive, so it's cached here and only rebuilt when the world's terrain
# actually changes — callers must invoke mark_world_dirty() after painting/clearing tiles.
var _diversity_map: Array = []
var _diversity_map_dirty: bool = true


## Call after any terrain mutation (painting, deleting) so the cached diversity map rebuilds
## on next use instead of serving stale scores.
func mark_world_dirty() -> void:
	_diversity_map_dirty = true


func tick(sim: Simulation) -> void:
	for pawn_id in sim.entities.pawns:
		var action_comp: Components.ActionComponent = sim.entities.actions.get(pawn_id)
		if action_comp == null:
			continue
		var need_comp: Components.NeedsComponent = sim.entities.needs.get(pawn_id)
		if need_comp == null:
			continue

		if action_comp.current_action != null or action_comp.action_queue.size() > 0:
			continue

		_decide_next_action(sim, pawn_id, action_comp, need_comp)


func _decide_next_action(
	sim: Simulation,
	pawn_id: int,
	action_comp: Components.ActionComponent,
	need_comp: Components.NeedsComponent
) -> void:
	var urgent_needs := _calculate_urgent_needs(need_comp)
	var purpose_need_id: int = sim.content.get_need_id("Purpose")

	for pair: Array in urgent_needs:
		var need_id: int = pair[0]
		if need_id == purpose_need_id:
			if _try_queue_work(sim, pawn_id, action_comp, purpose_need_id):
				return
		else:
			var target_building: int = _find_building_for_need(sim, pawn_id, need_id)
			if target_building != -1:
				_queue_use_building(sim, action_comp, target_building)
				return

	_wander_randomly(sim, pawn_id, action_comp)


# Tries each reachable work candidate, highest-scoring first, until one actually yields
# queueable work — a candidate can pass the urgency/attachment scoring and still fail to
# provide work (e.g. a haul job with no source currently in stock).
func _try_queue_work(
	sim: Simulation, pawn_id: int, action_comp: Components.ActionComponent, purpose_need_id: int
) -> bool:
	for candidate_id in _find_work_candidates(sim, pawn_id, purpose_need_id):
		if _queue_work_at_building(sim, pawn_id, action_comp, candidate_id, purpose_need_id):
			return true
	return false


# --- Need urgency ----------------------------------------------------------


func _calculate_urgent_needs(need_comp: Components.NeedsComponent) -> Array:
	var urgent: Array = []
	for need_id: int in need_comp.needs.keys():
		var value: float = need_comp.needs[need_id]
		if value < NEED_THRESHOLD:
			urgent.append([need_id, value])
	# Sort ascending by value (lowest = most urgent first)
	urgent.sort_custom(func(a: Array, b: Array) -> bool: return a[1] < b[1])
	return urgent


# --- Action queuing --------------------------------------------------------


func _queue_use_building(
	sim: Simulation, action_comp: Components.ActionComponent, target_id: int
) -> void:
	var building_comp: Components.BuildingComponent = sim.entities.buildings[target_id]
	var building_def: Dictionary = sim.content.buildings[building_comp.building_def_id]

	var action := Definitions.ActionDef.new()
	action.type = Definitions.ActionType.USE_BUILDING
	action.animation = Definitions.AnimationType.IDLE
	action.target_entity = target_id
	action.duration_ticks = DefUtils.get_int(building_def, "interactionDuration", 100)
	action.satisfies_need_id = DefUtils.get_int(building_def, "satisfiesNeedId", -1)
	action.need_satisfaction_amount = DefUtils.get_float(building_def, "satisfactionAmount", 100.0)
	action.display_name = "Going to %s" % building_def["name"]
	action_comp.action_queue.push_back(action)


func _queue_work_at_building(
	sim: Simulation,
	pawn_id: int,
	action_comp: Components.ActionComponent,
	target_id: int,
	purpose_need_id: int
) -> bool:
	var building_comp: Components.BuildingComponent = sim.entities.buildings[target_id]
	var building_def: Dictionary = sim.content.buildings[building_comp.building_def_id]
	var work_type: String = building_def.get("workType", "direct")

	match work_type:
		"direct":
			_queue_direct_work(sim, action_comp, target_id, building_def, purpose_need_id)
			return true
		"haulFromBuilding":
			return _queue_haul_from_building(
				sim, pawn_id, action_comp, target_id, building_def, purpose_need_id
			)
		"haulFromTerrain":
			return _queue_haul_from_terrain(
				sim, action_comp, target_id, building_def, purpose_need_id
			)
	return false


func _queue_direct_work(
	_sim: Simulation,
	action_comp: Components.ActionComponent,
	target_id: int,
	building_def: Dictionary,
	purpose_need_id: int
) -> void:
	var action := Definitions.ActionDef.new()
	action.type = Definitions.ActionType.WORK
	action.animation = Definitions.AnimationType.PICKAXE
	action.target_entity = target_id
	action.duration_ticks = WORK_DURATION_TICKS
	action.satisfies_need_id = purpose_need_id
	action.need_satisfaction_amount = 40.0
	action.display_name = "Going to work at %s" % building_def["name"]
	action_comp.action_queue.push_back(action)


func _queue_haul_from_building(
	sim: Simulation,
	pawn_id: int,
	action_comp: Components.ActionComponent,
	dest_id: int,
	dest_def: Dictionary,
	purpose_need_id: int
) -> bool:
	var resource_type: String = dest_def.get("haulSourceResourceType", "")
	var source_id: int = _find_source_building(sim, pawn_id, resource_type, dest_id)
	if source_id == -1:
		return false

	var source_comp: Components.BuildingComponent = sim.entities.buildings[source_id]
	var source_def: Dictionary = sim.content.buildings[source_comp.building_def_id]

	var pick_up := Definitions.ActionDef.new()
	pick_up.type = Definitions.ActionType.PICK_UP
	pick_up.animation = Definitions.AnimationType.IDLE
	pick_up.target_entity = source_id
	pick_up.duration_ticks = 100
	pick_up.resource_type = resource_type
	pick_up.resource_amount = HAUL_AMOUNT
	pick_up.display_name = "Loading %s from %s" % [resource_type, source_def["name"]]
	action_comp.action_queue.push_back(pick_up)

	var drop_off := Definitions.ActionDef.new()
	drop_off.type = Definitions.ActionType.DROP_OFF
	drop_off.animation = Definitions.AnimationType.IDLE
	drop_off.target_entity = dest_id
	drop_off.source_entity = source_id
	drop_off.duration_ticks = 100
	drop_off.resource_type = resource_type
	drop_off.resource_amount = HAUL_AMOUNT
	drop_off.satisfies_need_id = purpose_need_id
	drop_off.need_satisfaction_amount = 40.0
	drop_off.display_name = "Delivering to %s" % dest_def["name"]
	action_comp.action_queue.push_back(drop_off)
	return true


func _queue_haul_from_terrain(
	sim: Simulation,
	action_comp: Components.ActionComponent,
	dest_id: int,
	dest_def: Dictionary,
	purpose_need_id: int
) -> bool:
	var terrain_def_id: int = DefUtils.get_int(dest_def, "haulSourceTerrainId", -1)
	var terrain_coord: Vector2i = _find_nearest_terrain(sim, dest_id, terrain_def_id)
	if terrain_coord == Vector2i(-1, -1):
		return false

	# Distinct from the destination's own storage "resourceType" (which LumberMill no longer
	# has — see buildings.json) — this only labels what the pawn is carrying, independent of
	# whether the destination tracks stock of it at all.
	var resource_type: String = dest_def.get("haulSourceResourceType", "")

	var pick_up := Definitions.ActionDef.new()
	pick_up.type = Definitions.ActionType.PICK_UP
	pick_up.animation = Definitions.AnimationType.AXE
	pick_up.terrain_target_coord = terrain_coord
	pick_up.duration_ticks = HARVEST_DURATION_TICKS
	pick_up.resource_type = resource_type
	pick_up.resource_amount = HAUL_AMOUNT
	pick_up.display_name = "Harvesting %s" % dest_def.get("haulSourceTerrainKey", "")
	action_comp.action_queue.push_back(pick_up)

	var drop_off := Definitions.ActionDef.new()
	drop_off.type = Definitions.ActionType.DROP_OFF
	drop_off.animation = Definitions.AnimationType.IDLE
	drop_off.target_entity = dest_id
	drop_off.duration_ticks = 100
	drop_off.resource_type = resource_type
	drop_off.resource_amount = HAUL_AMOUNT
	drop_off.satisfies_need_id = purpose_need_id
	drop_off.need_satisfaction_amount = 40.0
	drop_off.display_name = "Delivering to %s" % dest_def["name"]
	action_comp.action_queue.push_back(drop_off)
	return true


# --- Wandering -------------------------------------------------------------


func _wander_randomly(
	sim: Simulation, pawn_id: int, action_comp: Components.ActionComponent
) -> void:
	if _try_queue_home_visit(sim, pawn_id, action_comp):
		return

	var pos: Components.PositionComponent = sim.entities.positions.get(pawn_id)
	if pos == null:
		return

	# Flat diversity map: int value per tile, indexed x + y * width
	var diversity_map: Array = _get_diversity_map(sim.world)

	# Gather candidate destinations: 10 random tiles + nearby 8-dir tiles at dist 1-3
	var potential: Array[Vector2i] = []
	for _i in 10:
		potential.append(Vector2i(randi() % sim.world.width, randi() % sim.world.height))

	var dirs: Array[Vector2i] = [
		Vector2i(0, 1),
		Vector2i(0, -1),
		Vector2i(1, 0),
		Vector2i(-1, 0),
		Vector2i(1, 1),
		Vector2i(1, -1),
		Vector2i(-1, 1),
		Vector2i(-1, -1),
	]
	# Cap diversity for nearby tiles to avoid pawns getting stuck near low-variety areas.
	# Tracked separately rather than written into diversity_map, since that's a shared cache
	# reused across every pawn's decision — mutating it here would corrupt it for everyone else.
	var capped_nearby: Dictionary = {}
	for d in dirs:
		for dist in range(1, 4):
			var nearby := Vector2i(pos.coord.x + d.x * dist, pos.coord.y + d.y * dist)
			potential.append(nearby)
			if sim.world.is_in_bounds(nearby):
				capped_nearby[nearby.x + nearby.y * sim.world.width] = true

	var candidates: Array[Dictionary] = []
	for target in potential:
		if target == pos.coord:
			continue
		if not sim.world.is_walkable(target):
			continue
		var idx: int = target.x + target.y * sim.world.width
		var diversity: int = diversity_map[idx]
		if capped_nearby.has(idx):
			diversity = mini(1, diversity)
		candidates.append({"coord": target, "diversity": diversity})

	if candidates.is_empty():
		_queue_stuck_idle(action_comp)
		return

	# Sort by diversity descending (higher diversity = more interesting area)
	candidates.sort_custom(
		func(a: Dictionary, b: Dictionary) -> bool: return a["diversity"] > b["diversity"]
	)

	var selected: Dictionary
	var all_zero: bool = candidates.all(func(c: Dictionary) -> bool: return c["diversity"] == 0)
	if all_zero:
		# Prefer closer tiles when everything looks the same
		candidates.sort_custom(
			func(a: Dictionary, b: Dictionary) -> bool:
				var da: int = abs(a["coord"].x - pos.coord.x) + abs(a["coord"].y - pos.coord.y)
				var db: int = abs(b["coord"].x - pos.coord.x) + abs(b["coord"].y - pos.coord.y)
				return da < db
		)
	selected = candidates[0]

	var walk := Definitions.ActionDef.new()
	walk.type = Definitions.ActionType.MOVE_TO
	walk.animation = Definitions.AnimationType.WALK
	walk.target_coord = selected["coord"]
	walk.display_name = "Wandering"
	action_comp.action_queue.push_back(walk)

	var idle := Definitions.ActionDef.new()
	idle.type = Definitions.ActionType.IDLE

	if randf() < SIT_AND_EMOTE_CHANCE:
		# Long, deliberate sit-and-observe with an expression bubble about the pawn's best/worst
		# need — this is the one moment a need gets communicated regardless of whether anything
		# can currently be done about it (see _decide_need_expression).
		var base_sit: int = randi_range(1000, 2000)
		var diversity_bonus: int = selected["diversity"] * 150
		idle.animation = Definitions.AnimationType.SIT
		idle.duration_ticks = mini(2500, base_sit + diversity_bonus)
		idle.display_name = "Sitting"
		var expr_result: Array = _decide_need_expression(sim, pawn_id)
		if expr_result[0] != -1:
			idle.has_expression = true
			@warning_ignore("unsafe_cast")
			idle.expression = expr_result[0] as Definitions.ExpressionType
			idle.expression_icon_def_id = expr_result[1]
	else:
		# Most wander cycles are just a brief pause — no sit, no expression.
		var base_idle: int = randi_range(20, 40)
		var diversity_bonus: int = selected["diversity"] * 3
		idle.duration_ticks = mini(50, base_idle + diversity_bonus)
		idle.display_name = "Idle"

	action_comp.action_queue.push_back(idle)


# Rolls a chance for an idle colonist to detour home for a brief, purely cosmetic visit —
# real Energy-driven home visits only happen once Energy is actually low (normally just at
# night), which left most themes' skin overrides (see StrangeWorldsTheme) with no reliable way
# to ever reach a colonist during normal daytime play. Reuses the same need-based building
# search as a real Energy visit (so it respects capacity/crowding and prefers a pawn's own
# established home the same way), but the queued visit itself satisfies no need, grants no
# buff, and builds no attachment (see _queue_quick_home_visit) — it's just a walk there, a
# short pause (long enough for PawnView.sync_house_sheet to catch the current look), and back
# to normal life. Visitors (see Definitions.PawnMembership) are excluded — they only ever
# wander, never seek out buildings of any kind.
func _try_queue_home_visit(
	sim: Simulation, pawn_id: int, action_comp: Components.ActionComponent
) -> bool:
	if sim.entities.pawns[pawn_id].membership != Definitions.PawnMembership.COLONIST:
		return false
	if randf() >= WANDER_HOME_VISIT_CHANCE:
		return false

	var energy_need_id: int = sim.content.get_need_id("Energy")
	if energy_need_id == -1:
		return false

	var home_id: int = _find_building_for_need(sim, pawn_id, energy_need_id)
	if home_id == -1:
		return false

	_queue_quick_home_visit(sim, action_comp, home_id)
	return true


## A brief USE_BUILDING visit with no need/buff/attachment side effects — see
## _try_queue_home_visit. ActionSystem still handles the walk there and the capacity/use-area
## checks exactly like a real visit; only the interaction itself is a no-op beyond its duration.
func _queue_quick_home_visit(
	sim: Simulation, action_comp: Components.ActionComponent, target_id: int
) -> void:
	var building_comp: Components.BuildingComponent = sim.entities.buildings[target_id]
	var building_def: Dictionary = sim.content.buildings[building_comp.building_def_id]

	var action := Definitions.ActionDef.new()
	action.type = Definitions.ActionType.USE_BUILDING
	action.animation = Definitions.AnimationType.IDLE
	action.target_entity = target_id
	action.duration_ticks = WANDER_HOME_VISIT_DURATION_TICKS
	action.display_name = "Stopping by %s" % building_def.get("name", "home")
	action_comp.action_queue.push_back(action)


# Queued when a pawn has no walkable wander candidate anywhere (fully boxed in) — see
# STUCK_RETRY_DELAY_TICKS.
func _queue_stuck_idle(action_comp: Components.ActionComponent) -> void:
	var idle := Definitions.ActionDef.new()
	idle.type = Definitions.ActionType.IDLE
	idle.animation = Definitions.AnimationType.IDLE
	idle.duration_ticks = STUCK_RETRY_DELAY_TICKS
	idle.display_name = "Stuck"
	action_comp.action_queue.push_back(idle)


func _get_diversity_map(world: World) -> Array:
	if _diversity_map_dirty or _diversity_map.size() != world.width * world.height:
		_diversity_map = _compute_diversity_map(world)
		_diversity_map_dirty = false
	return _diversity_map


# Compute per-tile diversity scores based on adjacent tile differences.
# Each cell's score propagates from the left and above, incrementing on change
# and decrementing on repetition. Clamped to 0–9.
func _compute_diversity_map(world: World) -> Array:
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
				var x_tile: World.Tile = world.get_tile_xy(x - 1, y)
				if tile_hash != x_tile.tile_hash:
					x_score += 1
				elif x_score > 0:
					x_score -= 1

			if y > 0:
				y_score = scores[x + (y - 1) * world.width]
				var y_tile: World.Tile = world.get_tile_xy(x, y - 1)
				if tile_hash != y_tile.tile_hash:
					y_score += 1
				elif y_score > 0:
					y_score -= 1

			scores[x + y * world.width] = mini(9, (x_score + y_score) / 2)

	return scores


# --- Expression decisions --------------------------------------------------


## Picks a sit-and-observe bubble from the pawn's actual best/worst need value — deliberately
## not filtered by whether anything can currently be done about it (contrast the old buff-driven
## version this replaced, which only ever surfaced needs a pawn was actively acting on). A need
## with no building anywhere to satisfy it — e.g. Hygiene with no Well built — previously never
## showed up at all, since _decide_next_action always finds something else actionable to do
## first. Returns [ExpressionType, need_id] or [-1, -1] if the pawn has no needs at all.
func _decide_need_expression(sim: Simulation, pawn_id: int) -> Array:
	var need_comp: Components.NeedsComponent = sim.entities.needs.get(pawn_id)
	if need_comp == null or need_comp.needs.is_empty():
		return [-1, -1]

	var worst_id: int = -1
	var worst_value: float = INF
	var best_id: int = -1
	var best_value: float = -INF
	for need_id in need_comp.needs:
		var value: float = need_comp.needs[need_id]
		if value < worst_value:
			worst_value = value
			worst_id = need_id
		if value > best_value:
			best_value = value
			best_id = need_id

	if sim.get_mood(pawn_id) >= 0.0:
		return [Definitions.ExpressionType.HAPPY, best_id]
	return [Definitions.ExpressionType.COMPLAINT, worst_id]


# --- Building search -------------------------------------------------------


func _find_building_for_need(sim: Simulation, pawn_id: int, need_id: int) -> int:
	var captured_need_id: int = need_id

	return _find_best_reachable_building(
		sim,
		pawn_id,
		func(ctx: Dictionary) -> bool:
			var bdef: Dictionary = ctx["obj_def"]
			if DefUtils.get_int(bdef, "satisfiesNeedId", -1) != captured_need_id:
				return false
			if not DefUtils.get_bool(bdef, "canSellToConsumers", true):
				return false
			var res: Components.ResourceComponent = ctx["resource_comp"]
			if res != null and res.current_amount <= 0:
				return false
			return true,
		func(ctx: Dictionary, pid: int) -> float:
			var ac: Components.AttachmentComponent = ctx["attachment_comp"]
			return _get_attachment_score(ac, pid, captured_need_id, 20.0, 15.0)
	)


## All reachable, work-eligible buildings, best-scoring first (see _try_queue_work — callers
## try these in order since the top pick can still fail to yield actual work).
func _find_work_candidates(sim: Simulation, pawn_id: int, purpose_need_id: int) -> Array[int]:
	return _gather_reachable_candidates(
		sim,
		pawn_id,
		func(ctx: Dictionary) -> bool:
			var bdef: Dictionary = ctx["obj_def"]
			if not DefUtils.get_bool(bdef, "canBeWorkedAt", false):
				return false
			# haulFromTerrain sources (e.g. Trees) never run dry, so there's no real
			# "already fully stocked, back off" signal the way there is for a Farm or
			# Market whose stock reflects genuine scarcity — and unlike those, a
			# haulFromTerrain destination doesn't need to track its own stock at all,
			# so it's also fine for it to have no ResourceComponent whatsoever.
			if bdef.get("workType", "direct") == "haulFromTerrain":
				return true
			var res: Components.ResourceComponent = ctx["resource_comp"]
			if res == null:
				return false
			return (res.current_amount / res.max_amount) < 0.8,
		func(ctx: Dictionary, pid: int) -> float:
			var ac: Components.AttachmentComponent = ctx["attachment_comp"]
			return _get_attachment_score(ac, pid, purpose_need_id, 10.0, 5.0)
	)


func _find_source_building(
	sim: Simulation, pawn_id: int, resource_type: String, exclude_id: int
) -> int:
	if resource_type.is_empty():
		return -1

	var captured_type: String = resource_type
	var captured_exclude: int = exclude_id

	return _find_best_reachable_building(
		sim,
		pawn_id,
		func(ctx: Dictionary) -> bool:
			if ctx["obj_id"] == captured_exclude:
				return false
			var res: Components.ResourceComponent = ctx["resource_comp"]
			return res != null and res.resource_type == captured_type and res.current_amount >= 10.0,
		func(ctx: Dictionary, _pid: int) -> float: return ctx["resource_comp"].current_amount
	)


func _find_nearest_terrain(sim: Simulation, near_building_id: int, terrain_def_id: int) -> Vector2i:
	if terrain_def_id == -1:
		return Vector2i(-1, -1)

	var building_pos: Components.PositionComponent = sim.entities.positions.get(near_building_id)
	if building_pos == null:
		return Vector2i(-1, -1)

	var center: Vector2i = building_pos.coord
	var best := Vector2i(-1, -1)
	var best_dist: int = 2147483647

	for dx in range(-20, 21):
		for dy in range(-20, 21):
			var coord := Vector2i(center.x + dx, center.y + dy)
			if not sim.world.is_in_bounds(coord):
				continue
			var tile: World.Tile = sim.world.get_tile(coord)
			if (
				tile.base_terrain_type_id != terrain_def_id
				and tile.overlay_terrain_type_id != terrain_def_id
			):
				continue
			if not _has_adjacent_walkable(sim.world, coord):
				continue
			var dist: int = abs(dx) + abs(dy)
			if dist < best_dist:
				best_dist = dist
				best = coord

	return best


func _has_adjacent_walkable(world: World, coord: Vector2i) -> bool:
	for d: Vector2i in [Vector2i(0, 1), Vector2i(0, -1), Vector2i(1, 0), Vector2i(-1, 0)]:
		if world.is_in_bounds(coord + d) and world.is_walkable(coord + d):
			return true
	return false


# Generic building search with filter and scorer callables — returns every matching, reachable
# building, best-scoring first.
# ctx dict keys: obj_id, obj_comp, obj_def, obj_pos (Vector2i), distance,
#                resource_comp, attachment_comp, other_pawns_targeting
func _gather_reachable_candidates(
	sim: Simulation, pawn_id: int, filter: Callable, scorer: Callable
) -> Array[int]:
	var pawn_pos: Components.PositionComponent = sim.entities.positions.get(pawn_id)
	if pawn_pos == null:
		return []

	var candidates: Array[Dictionary] = []

	for obj_id: int in sim.entities.buildings:
		var obj_comp: Components.BuildingComponent = sim.entities.buildings[obj_id]
		var obj_def: Dictionary = sim.content.buildings.get(obj_comp.building_def_id, {})
		if obj_def.is_empty():
			continue
		var obj_pos_comp: Components.PositionComponent = sim.entities.positions.get(obj_id)
		if obj_pos_comp == null:
			continue

		var dist: int = (
			abs(pawn_pos.coord.x - obj_pos_comp.coord.x)
			+ abs(pawn_pos.coord.y - obj_pos_comp.coord.y)
		)
		var resource_comp: Components.ResourceComponent = sim.entities.resources.get(obj_id)
		var attachment_comp: Components.AttachmentComponent = sim.entities.attachments.get(obj_id)
		var other_targeting: int = _count_pawns_targeting(sim, obj_id, pawn_id)

		var capacity: int = _get_capacity(obj_def)
		if other_targeting + 1 > capacity:
			continue

		var ctx: Dictionary = {
			"obj_id": obj_id,
			"obj_comp": obj_comp,
			"obj_def": obj_def,
			"obj_pos": obj_pos_comp.coord,
			"distance": dist,
			"resource_comp": resource_comp,
			"attachment_comp": attachment_comp,
			"other_pawns_targeting": other_targeting,
		}

		if not filter.call(ctx):
			continue

		var base_score: float = -(dist * 0.5) - (other_targeting * 10.0)
		@warning_ignore("unsafe_call_argument")  # Callable.call() erases the callable's own return type
		var custom_score: float = scorer.call(ctx, pawn_id)
		candidates.append({"id": obj_id, "score": base_score + custom_score})

	candidates.sort_custom(
		func(a: Dictionary, b: Dictionary) -> bool: return a["score"] > b["score"]
	)

	var reachable: Array[int] = []
	for candidate: Dictionary in candidates:
		var candidate_id: int = candidate["id"]
		if _is_building_reachable(sim, pawn_id, candidate_id):
			reachable.append(candidate_id)
	return reachable


# Convenience wrapper for callers that only want the single best match.
func _find_best_reachable_building(
	sim: Simulation, pawn_id: int, filter: Callable, scorer: Callable
) -> int:
	var candidates := _gather_reachable_candidates(sim, pawn_id, filter, scorer)
	return candidates[0] if not candidates.is_empty() else -1


func _is_building_reachable(sim: Simulation, pawn_id: int, obj_id: int) -> bool:
	var pawn_pos: Components.PositionComponent = sim.entities.positions.get(pawn_id)
	if pawn_pos == null:
		return false
	var obj_pos: Components.PositionComponent = sim.entities.positions.get(obj_id)
	if obj_pos == null:
		return false
	var obj_comp: Components.BuildingComponent = sim.entities.buildings.get(obj_id)
	if obj_comp == null:
		return false

	var obj_def: Dictionary = sim.content.buildings[obj_comp.building_def_id]
	var use_areas: Array[Vector2i] = []
	use_areas.assign(DefUtils.get_array(obj_def, "useAreas", []))
	if use_areas.is_empty():
		use_areas = [Vector2i(0, 1), Vector2i(0, -1), Vector2i(1, 0), Vector2i(-1, 0)]

	for offset: Vector2i in use_areas:
		var target: Vector2i = obj_pos.coord + offset
		if not sim.world.is_walkable(target):
			continue
		var path := Pathfinder.find_path(sim.world, pawn_pos.coord, target)
		if not path.is_empty():
			return true

	return false


# --- Helpers ---------------------------------------------------------------


func _get_capacity(building_def: Dictionary) -> int:
	return DefUtils.get_int(building_def, "capacity", 1)


func _count_pawns_targeting(sim: Simulation, building_id: int, exclude_pawn: int) -> int:
	var count: int = 0
	for other_id in sim.entities.pawns:
		if other_id == exclude_pawn:
			continue
		var ac: Components.ActionComponent = sim.entities.actions.get(other_id)
		if ac == null:
			continue
		if ac.current_action != null and ac.current_action.target_entity == building_id:
			count += 1
			continue
		for qa in ac.action_queue:
			if qa.target_entity == building_id:
				count += 1
				break
	return count


func _get_attachment_score(
	attachment_comp: Components.AttachmentComponent,
	pawn_id: int,
	need_id: int,
	my_weight: float,
	other_weight: float
) -> float:
	if attachment_comp == null:
		return 0.0
	var per_pawn: Dictionary = attachment_comp.need_attachments.get(need_id, {})
	# per_pawn's value type isn't statically known past Dictionary[int, Dictionary]
	@warning_ignore("unsafe_call_argument")
	var my_attachment: int = int(per_pawn.get(pawn_id, 0))
	var score: float = my_attachment * my_weight
	# Sum (not max) of everyone else's attachment — a building three others are already
	# attached to should look less inviting than one only a single pawn has claimed, so
	# demand naturally spreads across capacity instead of piling onto whichever building
	# happened to recruit first.
	var others_total: int = 0
	for other_id: int in per_pawn.keys():
		if other_id != pawn_id:
			others_total += per_pawn[other_id]
	score -= others_total * other_weight
	return score
