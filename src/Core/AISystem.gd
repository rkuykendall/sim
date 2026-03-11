class_name AISystem

const NEED_THRESHOLD: float = 90.0


func tick(sim: Simulation) -> void:
	for pawn_id in sim.entities.all_pawns():
		var action_comp: Components.ActionComponent = sim.entities.actions.get(pawn_id)
		if action_comp == null:
			continue
		var need_comp: Components.NeedsComponent = sim.entities.needs.get(pawn_id)
		if need_comp == null:
			continue

		# Skip pawns that already have something to do
		if action_comp.current_action != null or action_comp.action_queue.size() > 0:
			continue

		_decide_next_action(sim, pawn_id, action_comp, need_comp)


func _decide_next_action(
	sim: Simulation,
	pawn_id: int,
	action_comp: Components.ActionComponent,
	need_comp: Components.NeedsComponent
) -> void:
	var urgent_needs := _calculate_urgent_needs(sim, need_comp)
	var purpose_need_id: int = sim.content.get_need_id("Purpose")

	var target_building: int = -1
	var is_work_action: bool = false

	for pair in urgent_needs:
		var need_id: int = pair[0]
		if need_id == purpose_need_id:
			target_building = _find_building_to_work_at(sim, pawn_id)
			if target_building != -1:
				is_work_action = true
				break
		else:
			target_building = _find_building_for_need(sim, pawn_id, need_id)
			if target_building != -1:
				break

	if target_building != -1:
		if is_work_action:
			_queue_work_at_building(sim, pawn_id, action_comp, target_building, purpose_need_id)
		else:
			_queue_use_building(sim, action_comp, target_building)
	else:
		_wander_randomly(sim, pawn_id, action_comp)


# --- Need urgency ----------------------------------------------------------

func _calculate_urgent_needs(sim: Simulation, need_comp: Components.NeedsComponent) -> Array:
	var urgent: Array = []
	for need_id in need_comp.needs.keys():
		var value: float = need_comp.needs[need_id]
		if value < NEED_THRESHOLD:
			urgent.append([need_id, value])
	# Sort ascending by value (lowest = most urgent first)
	urgent.sort_custom(func(a, b): return a[1] < b[1])
	return urgent


# --- Action queuing --------------------------------------------------------

func _queue_use_building(
	sim: Simulation,
	action_comp: Components.ActionComponent,
	target_id: int
) -> void:
	var building_comp: Components.BuildingComponent = sim.entities.buildings[target_id]
	var building_def: Dictionary = sim.content.buildings[building_comp.building_def_id]

	var action := Definitions.ActionDef.new()
	action.type = Definitions.ActionType.USE_BUILDING
	action.animation = Definitions.AnimationType.IDLE
	action.target_entity = target_id
	action.duration_ticks = int(building_def.get("interactionDuration", 100))
	action.satisfies_need_id = int(building_def.get("satisfiesNeedId", -1))
	action.need_satisfaction_amount = float(building_def.get("satisfactionAmount", 100.0))
	action.display_name = "Going to %s" % building_def["name"]
	action_comp.action_queue.push_back(action)


func _queue_work_at_building(
	sim: Simulation,
	pawn_id: int,
	action_comp: Components.ActionComponent,
	target_id: int,
	purpose_need_id: int
) -> void:
	var building_comp: Components.BuildingComponent = sim.entities.buildings[target_id]
	var building_def: Dictionary = sim.content.buildings[building_comp.building_def_id]
	var work_type: String = building_def.get("workType", "direct")

	match work_type:
		"direct":
			_queue_direct_work(sim, action_comp, target_id, building_def, purpose_need_id)
		"haulFromBuilding":
			_queue_haul_from_building(sim, pawn_id, action_comp, target_id, building_def, purpose_need_id)
		"haulFromTerrain":
			_queue_haul_from_terrain(sim, action_comp, target_id, building_def, purpose_need_id)


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
	action.duration_ticks = 2500
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
) -> void:
	var resource_type: String = dest_def.get("haulSourceResourceType", "")
	var source_id: int = _find_source_building(sim, pawn_id, resource_type, dest_id)
	if source_id == -1:
		return  # No source — fall back to wandering

	var source_comp: Components.BuildingComponent = sim.entities.buildings[source_id]
	var source_def: Dictionary = sim.content.buildings[source_comp.building_def_id]

	var pick_up := Definitions.ActionDef.new()
	pick_up.type = Definitions.ActionType.PICK_UP
	pick_up.animation = Definitions.AnimationType.IDLE
	pick_up.target_entity = source_id
	pick_up.duration_ticks = 100
	pick_up.resource_type = resource_type
	pick_up.resource_amount = 30.0
	pick_up.display_name = "Loading %s from %s" % [resource_type, source_def["name"]]
	action_comp.action_queue.push_back(pick_up)

	var drop_off := Definitions.ActionDef.new()
	drop_off.type = Definitions.ActionType.DROP_OFF
	drop_off.animation = Definitions.AnimationType.IDLE
	drop_off.target_entity = dest_id
	drop_off.source_entity = source_id
	drop_off.duration_ticks = 100
	drop_off.resource_type = resource_type
	drop_off.resource_amount = 30.0
	drop_off.satisfies_need_id = purpose_need_id
	drop_off.need_satisfaction_amount = 40.0
	drop_off.display_name = "Delivering to %s" % dest_def["name"]
	action_comp.action_queue.push_back(drop_off)


func _queue_haul_from_terrain(
	sim: Simulation,
	action_comp: Components.ActionComponent,
	dest_id: int,
	dest_def: Dictionary,
	purpose_need_id: int
) -> void:
	var terrain_def_id: int = int(dest_def.get("haulSourceTerrainId", -1))
	var terrain_coord: Vector2i = _find_nearest_terrain(sim, dest_id, terrain_def_id)
	if terrain_coord == Vector2i(-1, -1):
		return  # No terrain — fall back to wandering

	var resource_type: String = dest_def.get("resourceType", "")

	var pick_up := Definitions.ActionDef.new()
	pick_up.type = Definitions.ActionType.PICK_UP
	pick_up.animation = Definitions.AnimationType.AXE
	pick_up.terrain_target_coord = terrain_coord
	pick_up.duration_ticks = 1500
	pick_up.resource_type = resource_type
	pick_up.resource_amount = 30.0
	pick_up.display_name = "Harvesting %s" % dest_def.get("haulSourceTerrainKey", "")
	action_comp.action_queue.push_back(pick_up)

	var drop_off := Definitions.ActionDef.new()
	drop_off.type = Definitions.ActionType.DROP_OFF
	drop_off.animation = Definitions.AnimationType.IDLE
	drop_off.target_entity = dest_id
	drop_off.duration_ticks = 100
	drop_off.resource_type = resource_type
	drop_off.resource_amount = 30.0
	drop_off.satisfies_need_id = purpose_need_id
	drop_off.need_satisfaction_amount = 40.0
	drop_off.display_name = "Delivering to %s" % dest_def["name"]
	action_comp.action_queue.push_back(drop_off)


# --- Wandering -------------------------------------------------------------

func _wander_randomly(sim: Simulation, pawn_id: int, action_comp: Components.ActionComponent) -> void:
	var pos: Components.PositionComponent = sim.entities.positions.get(pawn_id)
	if pos == null:
		return

	# Flat diversity map: int value per tile, indexed x + y * width
	var diversity_map: Array = _compute_diversity_map(sim.world)

	# Gather candidate destinations: 10 random tiles + nearby 8-dir tiles at dist 1-3
	var potential: Array[Vector2i] = []
	for _i in 10:
		potential.append(Vector2i(randi() % sim.world.width, randi() % sim.world.height))

	var dirs := [
		Vector2i(0, 1), Vector2i(0, -1), Vector2i(1, 0), Vector2i(-1, 0),
		Vector2i(1, 1), Vector2i(1, -1), Vector2i(-1, 1), Vector2i(-1, -1),
	]
	for d in dirs:
		for dist in range(1, 4):
			var nearby := Vector2i(pos.coord.x + d.x * dist, pos.coord.y + d.y * dist)
			potential.append(nearby)
			# Cap diversity for nearby tiles to avoid pawns getting stuck near low-variety areas
			if sim.world.is_in_bounds(nearby):
				var idx: int = nearby.x + nearby.y * sim.world.width
				diversity_map[idx] = mini(1, diversity_map[idx])

	# Filter to valid walkable candidates (not current position)
	var candidates: Array = []
	for target in potential:
		if target == pos.coord:
			continue
		if not sim.world.is_walkable(target):
			continue
		var idx: int = target.x + target.y * sim.world.width
		candidates.append({ "coord": target, "diversity": diversity_map[idx] })

	if candidates.is_empty():
		return

	# Sort by diversity descending (higher diversity = more interesting area)
	candidates.sort_custom(func(a, b): return a["diversity"] > b["diversity"])

	var selected: Dictionary
	var all_zero: bool = candidates.all(func(c): return c["diversity"] == 0)
	if all_zero:
		# Prefer closer tiles when everything looks the same
		candidates.sort_custom(func(a, b):
			var da: int = abs(a["coord"].x - pos.coord.x) + abs(a["coord"].y - pos.coord.y)
			var db: int = abs(b["coord"].x - pos.coord.x) + abs(b["coord"].y - pos.coord.y)
			return da < db
		)
	selected = candidates[0]

	# Queue walk action
	var walk := Definitions.ActionDef.new()
	walk.type = Definitions.ActionType.MOVE_TO
	walk.animation = Definitions.AnimationType.WALK
	walk.target_coord = selected["coord"]
	walk.display_name = "Wandering"
	action_comp.action_queue.push_back(walk)

	# Queue idle with optional expression bubble
	var base_idle: int = randi_range(20, 40)
	var diversity_bonus: int = selected["diversity"] * 3
	var idle_duration: int = mini(50, base_idle + diversity_bonus)

	var expr_result: Array = _decide_expression(sim, pawn_id)

	var idle := Definitions.ActionDef.new()
	idle.type = Definitions.ActionType.IDLE
	idle.animation = Definitions.AnimationType.IDLE
	idle.duration_ticks = idle_duration
	idle.display_name = "Idle"
	if expr_result[0] != -1:
		idle.has_expression = true
		idle.expression = expr_result[0] as Definitions.ExpressionType
		idle.expression_icon_def_id = expr_result[1]
	action_comp.action_queue.push_back(idle)


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

func _decide_expression(sim: Simulation, pawn_id: int) -> Array:
	# Returns [ExpressionType, icon_def_id] or [-1, -1] if no expression.
	var buff_comp: Components.BuffComponent = sim.entities.buffs.get(pawn_id)
	if buff_comp != null:
		# Strongest positive buff
		var best_pos: Definitions.BuffInstance = null
		for b in buff_comp.active_buffs:
			if b.mood_offset > 0 and (best_pos == null or b.mood_offset > best_pos.mood_offset):
				best_pos = b
		if best_pos != null:
			var icon_id: int = _get_buff_icon_id(sim, best_pos)
			if icon_id != -1:
				return [Definitions.ExpressionType.HAPPY, icon_id]

		# Strongest negative buff
		var best_neg: Definitions.BuffInstance = null
		for b in buff_comp.active_buffs:
			if b.mood_offset < 0 and (best_neg == null or b.mood_offset < best_neg.mood_offset):
				best_neg = b
		if best_neg != null:
			var icon_id: int = _get_buff_icon_id(sim, best_neg)
			if icon_id != -1:
				return [Definitions.ExpressionType.COMPLAINT, icon_id]

	# Lowest need below low threshold
	var need_comp: Components.NeedsComponent = sim.entities.needs.get(pawn_id)
	if need_comp != null:
		var lowest_need_id: int = -1
		var lowest_value: float = 100.0
		for need_id in need_comp.needs.keys():
			var value: float = need_comp.needs[need_id]
			var need_def: Dictionary = sim.content.needs.get(need_id, {})
			if need_def.is_empty():
				continue
			if value < float(need_def["lowThreshold"]) and value < lowest_value:
				lowest_value = value
				lowest_need_id = need_id
		if lowest_need_id != -1:
			return [Definitions.ExpressionType.THOUGHT, lowest_need_id]

	return [-1, -1]


func _get_buff_icon_id(sim: Simulation, buff: Definitions.BuffInstance) -> int:
	match buff.source:
		Definitions.BuffSource.BUILDING, Definitions.BuffSource.WORK:
			var building_def: Dictionary = sim.content.buildings.get(buff.source_id, {})
			if not building_def.is_empty():
				return int(building_def.get("satisfiesNeedId", -1))
			return -1
		Definitions.BuffSource.NEED_CRITICAL, Definitions.BuffSource.NEED_LOW:
			return buff.source_id
		_:
			return -1


# --- Building search -------------------------------------------------------

func _find_building_for_need(sim: Simulation, pawn_id: int, need_id: int) -> int:
	var pawn_gold: int = 0
	var gold_comp: Components.GoldComponent = sim.entities.gold.get(pawn_id)
	if gold_comp != null:
		pawn_gold = gold_comp.amount

	# Capture loop variables for use in filter lambda
	var captured_need_id: int = need_id
	var captured_gold: int = pawn_gold

	return _find_best_reachable_building(
		sim,
		pawn_id,
		func(ctx: Dictionary) -> bool:
			var bdef: Dictionary = ctx["obj_def"]
			if int(bdef.get("satisfiesNeedId", -1)) != captured_need_id:
				return false
			if not bool(bdef.get("canSellToConsumers", true)):
				return false
			var cost: int = int(int(bdef.get("baseCost", 10)) * pow(1.15, 0))
			if captured_gold < cost:
				return false
			var res: Components.ResourceComponent = ctx["resource_comp"]
			if res != null and res.current_amount <= 0:
				return false
			return true,
		func(ctx: Dictionary, pid: int) -> float:
			return _get_attachment_score(ctx["attachment_comp"], pid, 20.0, 15.0)
	)


func _find_building_to_work_at(sim: Simulation, pawn_id: int) -> int:
	var pawn_gold: int = 0
	var gold_comp: Components.GoldComponent = sim.entities.gold.get(pawn_id)
	if gold_comp != null:
		pawn_gold = gold_comp.amount

	var captured_gold: int = pawn_gold

	return _find_best_reachable_building(
		sim,
		pawn_id,
		func(ctx: Dictionary) -> bool:
			var bdef: Dictionary = ctx["obj_def"]
			if not bool(bdef.get("canBeWorkedAt", false)):
				return false
			var payout: int = int(int(bdef.get("baseCost", 10)) * float(bdef.get("baseProduction", 2.0)))
			var buy_in: int = 0 if payout <= 10 else payout / 2
			if captured_gold < buy_in:
				return false
			var res: Components.ResourceComponent = ctx["resource_comp"]
			if res == null:
				return false
			return (res.current_amount / res.max_amount) < 0.8,
		func(ctx: Dictionary, pid: int) -> float:
			var res: Components.ResourceComponent = ctx["resource_comp"]
			var urgency: float = 100.0 - (res.current_amount / res.max_amount) * 100.0
			return urgency + _get_attachment_score(ctx["attachment_comp"], pid, 10.0, 5.0)
	)


func _find_source_building(
	sim: Simulation,
	pawn_id: int,
	resource_type: String,
	exclude_id: int
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
		func(ctx: Dictionary, _pid: int) -> float:
			return ctx["resource_comp"].current_amount
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
			if tile.base_terrain_type_id != terrain_def_id and tile.overlay_terrain_type_id != terrain_def_id:
				continue
			if not _has_adjacent_walkable(sim.world, coord):
				continue
			var dist: int = abs(dx) + abs(dy)
			if dist < best_dist:
				best_dist = dist
				best = coord

	return best


func _has_adjacent_walkable(world: World, coord: Vector2i) -> bool:
	for d in [Vector2i(0, 1), Vector2i(0, -1), Vector2i(1, 0), Vector2i(-1, 0)]:
		if world.is_in_bounds(coord + d) and world.is_walkable(coord + d):
			return true
	return false


# Generic building search with filter and scorer callables.
# ctx dict keys: obj_id, obj_comp, obj_def, obj_pos (Vector2i), distance,
#                resource_comp, attachment_comp, other_pawns_targeting
func _find_best_reachable_building(
	sim: Simulation,
	pawn_id: int,
	filter: Callable,
	scorer: Callable
) -> int:
	var pawn_pos: Components.PositionComponent = sim.entities.positions.get(pawn_id)
	if pawn_pos == null:
		return -1

	var candidates: Array = []

	for obj_id in sim.entities.all_buildings():
		var obj_comp: Components.BuildingComponent = sim.entities.buildings.get(obj_id)
		if obj_comp == null:
			continue
		var obj_def: Dictionary = sim.content.buildings.get(obj_comp.building_def_id, {})
		if obj_def.is_empty():
			continue
		var obj_pos_comp: Components.PositionComponent = sim.entities.positions.get(obj_id)
		if obj_pos_comp == null:
			continue

		var dist: int = abs(pawn_pos.coord.x - obj_pos_comp.coord.x) + abs(pawn_pos.coord.y - obj_pos_comp.coord.y)
		var resource_comp: Components.ResourceComponent = sim.entities.resources.get(obj_id)
		var attachment_comp: Components.AttachmentComponent = sim.entities.attachments.get(obj_id)
		var other_targeting: int = _count_pawns_targeting(sim, obj_id, pawn_id)

		# Phase-based capacity check
		var phase: int = _get_building_phase(sim, obj_id)
		var capacity: int = _get_capacity(obj_def, phase)
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
		var custom_score: float = scorer.call(ctx, pawn_id)
		candidates.append({ "id": obj_id, "score": base_score + custom_score })

	candidates.sort_custom(func(a, b): return a["score"] > b["score"])

	for candidate in candidates:
		if _is_building_reachable(sim, pawn_id, candidate["id"]):
			return candidate["id"]

	return -1


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
	var use_areas: Array = obj_def.get("useAreas", [])
	if use_areas.is_empty():
		use_areas = [Vector2i(0, 1), Vector2i(0, -1), Vector2i(1, 0), Vector2i(-1, 0)]

	for offset in use_areas:
		var target: Vector2i = obj_pos.coord + offset
		if not sim.world.is_walkable(target):
			continue
		var path := Pathfinder.find_path(sim.world, pawn_pos.coord, target)
		if not path.is_empty():
			return true

	return false


# --- Helpers ---------------------------------------------------------------

func _get_capacity(building_def: Dictionary, phase: int) -> int:
	var per_phase: Array = building_def.get("capacityPerPhase", [])
	if not per_phase.is_empty():
		return int(per_phase[clampi(phase, 0, per_phase.size() - 1)])
	return int(building_def.get("capacity", 1))


func _get_building_phase(sim: Simulation, building_id: int) -> int:
	var attachment_comp: Components.AttachmentComponent = sim.entities.attachments.get(building_id)
	if attachment_comp == null:
		return 0
	var max_wealth: int = 0
	for p_id in attachment_comp.user_attachments.keys():
		var gold: Components.GoldComponent = sim.entities.gold.get(p_id)
		if gold != null:
			max_wealth = maxi(max_wealth, gold.amount)
	return max_wealth / 100


func _count_pawns_targeting(sim: Simulation, building_id: int, exclude_pawn: int) -> int:
	var count: int = 0
	for other_id in sim.entities.all_pawns():
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
	my_weight: float,
	other_weight: float
) -> float:
	if attachment_comp == null:
		return 0.0
	var my_attachment: int = attachment_comp.user_attachments.get(pawn_id, 0)
	var score: float = my_attachment * my_weight
	var highest_other: int = 0
	for other_id in attachment_comp.user_attachments.keys():
		if other_id != pawn_id:
			var att: int = attachment_comp.user_attachments[other_id]
			if att > highest_other:
				highest_other = att
	score -= highest_other * other_weight
	return score
