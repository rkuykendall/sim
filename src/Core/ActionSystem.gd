class_name ActionSystem

const MOVE_TICKS_PER_TILE: int = 10


func tick(sim: Simulation) -> void:
	for pawn_id in sim.entities.all_pawns():
		var action_comp: Components.ActionComponent = sim.entities.actions.get(pawn_id)
		if action_comp == null:
			continue

		if action_comp.current_action == null:
			if action_comp.action_queue.size() > 0:
				action_comp.current_action = action_comp.action_queue.pop_front()
				action_comp.action_start_tick = sim.time.tick
				action_comp.current_path.clear()
				action_comp.path_index = 0
			else:
				continue

		var action: Definitions.ActionDef = action_comp.current_action

		match action.type:
			Definitions.ActionType.MOVE_TO:
				_execute_move_to(sim, pawn_id, action_comp)
			Definitions.ActionType.USE_BUILDING:
				_execute_use_building(sim, pawn_id, action_comp)
			Definitions.ActionType.WORK:
				_execute_work(sim, pawn_id, action_comp)
			Definitions.ActionType.PICK_UP:
				_execute_pick_up(sim, pawn_id, action_comp)
			Definitions.ActionType.DROP_OFF:
				_execute_drop_off(sim, pawn_id, action_comp)
			Definitions.ActionType.IDLE:
				if sim.time.tick - action_comp.action_start_tick >= action.duration_ticks:
					action_comp.current_action = null


func _execute_move_to(sim: Simulation, pawn_id: int, action_comp: Components.ActionComponent) -> void:
	var action := action_comp.current_action
	if action.target_coord == Vector2i(-1, -1):
		action_comp.current_action = null
		return

	var pos: Components.PositionComponent = sim.entities.positions[pawn_id]
	var target: Vector2i = action.target_coord

	if action_comp.current_path.is_empty():
		action_comp.current_path = Pathfinder.find_path(sim.world, pos.coord, target)
		action_comp.path_index = 0

		if action_comp.current_path.is_empty():
			action_comp.current_action = null
			action_comp.action_queue.clear()
			return

	var ticks_in_action: int = sim.time.tick - action_comp.action_start_tick
	var expected_index: int = mini(
		ticks_in_action / MOVE_TICKS_PER_TILE,
		action_comp.current_path.size() - 1
	)

	if expected_index > action_comp.path_index:
		action_comp.path_index = expected_index
		pos.coord = action_comp.current_path[action_comp.path_index]

	if pos.coord == target:
		action_comp.current_action = null
		action_comp.current_path.clear()


func _execute_use_building(sim: Simulation, pawn_id: int, action_comp: Components.ActionComponent) -> void:
	var action := action_comp.current_action
	if action.target_entity == -1:
		action_comp.current_action = null
		return

	var target_id: int = action.target_entity
	var pawn_pos: Components.PositionComponent = sim.entities.positions.get(pawn_id)
	if pawn_pos == null:
		return
	var obj_pos: Components.PositionComponent = sim.entities.positions.get(target_id)
	if obj_pos == null:
		return
	var obj_comp: Components.BuildingComponent = sim.entities.buildings.get(target_id)
	if obj_comp == null:
		return

	var obj_def: Dictionary = sim.content.buildings[obj_comp.building_def_id]

	if not _is_in_use_area(pawn_pos.coord, obj_pos.coord, obj_def):
		var use_target := _find_valid_use_area(sim, obj_pos.coord, pawn_pos.coord, obj_def, pawn_id)
		if use_target == Vector2i(-1, -1):
			action_comp.current_action = null
			action_comp.action_queue.clear()
			return
		action_comp.action_queue.push_front(action)
		var move_action := Definitions.ActionDef.new()
		move_action.type = Definitions.ActionType.MOVE_TO
		move_action.animation = Definitions.AnimationType.WALK
		move_action.target_coord = use_target
		move_action.display_name = "Going to %s" % obj_def["name"]
		action_comp.current_action = move_action
		action_comp.action_start_tick = sim.time.tick
		return

	# Update display name to "Using X" once we arrive
	var building_def: Dictionary = sim.content.buildings[obj_comp.building_def_id]
	if action.display_name != "Using %s" % building_def["name"]:
		var updated := Definitions.ActionDef.new()
		updated.type = action.type
		updated.animation = action.animation
		updated.target_coord = action.target_coord
		updated.target_entity = action.target_entity
		updated.duration_ticks = action.duration_ticks
		updated.satisfies_need_id = action.satisfies_need_id
		updated.need_satisfaction_amount = action.need_satisfaction_amount
		updated.display_name = "Using %s" % building_def["name"]
		action_comp.current_action = updated
		action = action_comp.current_action

	var elapsed: int = sim.time.tick - action_comp.action_start_tick
	if elapsed < action.duration_ticks:
		return

	# Check resources
	var has_resources: bool = true
	var resource_comp: Components.ResourceComponent = sim.entities.resources.get(target_id)
	if resource_comp != null:
		if resource_comp.current_amount > 0:
			resource_comp.current_amount = maxf(
				0.0, resource_comp.current_amount - 20.0 * resource_comp.depletion_mult
			)
		else:
			has_resources = false

	# Economic transaction
	if has_resources:
		var cost: int = _get_building_cost(building_def)
		var pawn_gold: Components.GoldComponent = sim.entities.gold.get(pawn_id)
		if pawn_gold != null:
			if pawn_gold.amount < cost:
				has_resources = false
			else:
				pawn_gold.amount -= cost
				var building_gold: Components.GoldComponent = sim.entities.gold.get(target_id)
				if building_gold != null:
					building_gold.amount += cost

	# Satisfy need
	if has_resources and action.satisfies_need_id != -1:
		var need_comp: Components.NeedsComponent = sim.entities.needs.get(pawn_id)
		if need_comp != null and need_comp.needs.has(action.satisfies_need_id):
			need_comp.needs[action.satisfies_need_id] = clampf(
				need_comp.needs[action.satisfies_need_id] + action.need_satisfaction_amount,
				0.0, 100.0
			)

	# Grant buff
	if has_resources and float(building_def.get("grantsBuff", 0.0)) != 0.0:
		var buff_comp: Components.BuffComponent = sim.entities.buffs.get(pawn_id)
		if buff_comp != null:
			buff_comp.active_buffs = buff_comp.active_buffs.filter(func(b: Definitions.BuffInstance) -> bool:
				return not (b.source == Definitions.BuffSource.BUILDING and b.source_id == building_def["id"])
			)
			var b := Definitions.BuffInstance.new()
			b.source = Definitions.BuffSource.BUILDING
			b.source_id = building_def["id"]
			b.mood_offset = float(building_def["grantsBuff"])
			b.start_tick = sim.time.tick
			b.end_tick = sim.time.tick + int(building_def["buffDuration"])
			buff_comp.active_buffs.append(b)

	# Increment attachment
	if has_resources:
		var attachment_comp: Components.AttachmentComponent = sim.entities.attachments.get(target_id)
		if attachment_comp != null:
			attachment_comp.user_attachments[pawn_id] = mini(
				10, attachment_comp.user_attachments.get(pawn_id, 0) + 1
			)

	# Brief idle reaction
	if action.satisfies_need_id != -1:
		var idle := Definitions.ActionDef.new()
		idle.type = Definitions.ActionType.IDLE
		idle.duration_ticks = 10
		idle.display_name = "Satisfied" if has_resources else "Out of Resources"
		idle.has_expression = true
		idle.expression = Definitions.ExpressionType.HAPPY if has_resources else Definitions.ExpressionType.COMPLAINT
		idle.expression_icon_def_id = action.satisfies_need_id
		action_comp.action_queue.push_front(idle)

	action_comp.current_action = null


func _execute_work(sim: Simulation, pawn_id: int, action_comp: Components.ActionComponent) -> void:
	var action := action_comp.current_action
	if action.target_entity == -1:
		action_comp.current_action = null
		return

	var target_id: int = action.target_entity
	var pawn_pos: Components.PositionComponent = sim.entities.positions.get(pawn_id)
	if pawn_pos == null:
		return
	var obj_pos: Components.PositionComponent = sim.entities.positions.get(target_id)
	if obj_pos == null:
		return
	var obj_comp: Components.BuildingComponent = sim.entities.buildings.get(target_id)
	if obj_comp == null:
		return

	var obj_def: Dictionary = sim.content.buildings[obj_comp.building_def_id]

	if not _is_in_use_area(pawn_pos.coord, obj_pos.coord, obj_def):
		var use_target := _find_valid_use_area(sim, obj_pos.coord, pawn_pos.coord, obj_def, pawn_id)
		if use_target == Vector2i(-1, -1):
			action_comp.current_action = null
			action_comp.action_queue.clear()
			return
		action_comp.action_queue.push_front(action)
		var move_action := Definitions.ActionDef.new()
		move_action.type = Definitions.ActionType.MOVE_TO
		move_action.animation = Definitions.AnimationType.WALK
		move_action.target_coord = use_target
		move_action.display_name = "Going to work at %s" % obj_def["name"]
		action_comp.current_action = move_action
		action_comp.action_start_tick = sim.time.tick
		return

	if action.display_name != "Working at %s" % obj_def["name"]:
		var updated := Definitions.ActionDef.new()
		updated.type = action.type
		updated.animation = Definitions.AnimationType.PICKAXE
		updated.target_coord = action.target_coord
		updated.target_entity = action.target_entity
		updated.duration_ticks = action.duration_ticks
		updated.satisfies_need_id = action.satisfies_need_id
		updated.need_satisfaction_amount = action.need_satisfaction_amount
		updated.display_name = "Working at %s" % obj_def["name"]
		action_comp.current_action = updated
		action = action_comp.current_action

	var elapsed: int = sim.time.tick - action_comp.action_start_tick
	if elapsed < action.duration_ticks:
		return

	# Replenish building resources
	var resource_comp: Components.ResourceComponent = sim.entities.resources.get(target_id)
	if resource_comp != null:
		resource_comp.current_amount = minf(resource_comp.max_amount, resource_comp.current_amount + 30.0)

	# Economic transaction
	var buy_in: int = _get_work_buy_in(obj_def)
	var payout: int = _get_payout(obj_def)
	var pawn_gold: Components.GoldComponent = sim.entities.gold.get(pawn_id)
	if pawn_gold != null:
		pawn_gold.amount -= buy_in
		var building_gold: Components.GoldComponent = sim.entities.gold.get(target_id)
		if building_gold != null:
			building_gold.amount += buy_in

		if bool(obj_def.get("isGoldSource", false)) or int(obj_def.get("baseCost", 10)) == 0:
			pawn_gold.amount += payout
		elif building_gold != null:
			var actual_payout: int = mini(payout, building_gold.amount)
			building_gold.amount -= actual_payout
			pawn_gold.amount += actual_payout

	# Satisfy Purpose need
	if action.satisfies_need_id != -1:
		var need_comp: Components.NeedsComponent = sim.entities.needs.get(pawn_id)
		if need_comp != null and need_comp.needs.has(action.satisfies_need_id):
			need_comp.needs[action.satisfies_need_id] = clampf(
				need_comp.needs[action.satisfies_need_id] + action.need_satisfaction_amount,
				0.0, 100.0
			)

	# Grant work buff
	var buff_comp: Components.BuffComponent = sim.entities.buffs.get(pawn_id)
	if buff_comp != null:
		buff_comp.active_buffs = buff_comp.active_buffs.filter(func(b: Definitions.BuffInstance) -> bool:
			return not (b.source == Definitions.BuffSource.WORK and b.source_id == obj_def["id"])
		)
		var b := Definitions.BuffInstance.new()
		b.source = Definitions.BuffSource.WORK
		b.source_id = obj_def["id"]
		b.mood_offset = 15.0
		b.start_tick = sim.time.tick
		b.end_tick = sim.time.tick + 1200
		buff_comp.active_buffs.append(b)

	# Increment attachment
	var attachment_comp: Components.AttachmentComponent = sim.entities.attachments.get(target_id)
	if attachment_comp != null:
		attachment_comp.user_attachments[pawn_id] = mini(
			10, attachment_comp.user_attachments.get(pawn_id, 0) + 1
		)

	# Brief idle
	if action.satisfies_need_id != -1:
		var idle := Definitions.ActionDef.new()
		idle.type = Definitions.ActionType.IDLE
		idle.duration_ticks = 10
		idle.display_name = "Feeling Productive"
		idle.has_expression = true
		idle.expression = Definitions.ExpressionType.HAPPY
		idle.expression_icon_def_id = action.satisfies_need_id
		action_comp.action_queue.push_front(idle)

	action_comp.current_action = null


func _execute_pick_up(sim: Simulation, pawn_id: int, action_comp: Components.ActionComponent) -> void:
	var action := action_comp.current_action
	var inventory: Components.InventoryComponent = sim.entities.inventory.get(pawn_id)
	if inventory == null:
		return

	# Pick up from a building
	if action.target_entity != -1:
		var source_id: int = action.target_entity
		var pawn_pos: Components.PositionComponent = sim.entities.positions.get(pawn_id)
		if pawn_pos == null:
			return
		var obj_pos: Components.PositionComponent = sim.entities.positions.get(source_id)
		if obj_pos == null:
			return
		var building_comp: Components.BuildingComponent = sim.entities.buildings.get(source_id)
		if building_comp == null:
			return
		var building_def: Dictionary = sim.content.buildings[building_comp.building_def_id]

		if not _is_in_use_area(pawn_pos.coord, obj_pos.coord, building_def):
			var use_target := _find_valid_use_area(sim, obj_pos.coord, pawn_pos.coord, building_def, pawn_id)
			if use_target == Vector2i(-1, -1):
				action_comp.current_action = null
				action_comp.action_queue.clear()
				return
			action_comp.action_queue.push_front(action)
			var move_action := Definitions.ActionDef.new()
			move_action.type = Definitions.ActionType.MOVE_TO
			move_action.animation = Definitions.AnimationType.WALK
			move_action.target_coord = use_target
			move_action.display_name = "Going to pick up %s" % action.resource_type
			action_comp.current_action = move_action
			action_comp.action_start_tick = sim.time.tick
			return

		if sim.time.tick - action_comp.action_start_tick >= action.duration_ticks:
			var source_resource: Components.ResourceComponent = sim.entities.resources.get(source_id)
			if source_resource != null:
				var amount: float = action.resource_amount if action.resource_amount > 0.0 else 30.0
				var transfer: float = minf(amount, source_resource.current_amount)
				if transfer > 0.0:
					source_resource.current_amount -= transfer
					inventory.resource_type = source_resource.resource_type
					inventory.amount = transfer
			action_comp.current_action = null

	# Pick up from terrain (harvesting)
	elif action.terrain_target_coord != Vector2i(-1, -1):
		var pawn_pos: Components.PositionComponent = sim.entities.positions.get(pawn_id)
		if pawn_pos == null:
			return
		var target_coord: Vector2i = action.terrain_target_coord
		var dist: int = abs(pawn_pos.coord.x - target_coord.x) + abs(pawn_pos.coord.y - target_coord.y)

		if dist > 1:
			var adj := _find_adjacent_walkable(sim.world, target_coord, pawn_pos.coord)
			if adj == Vector2i(-1, -1):
				action_comp.current_action = null
				action_comp.action_queue.clear()
				return
			action_comp.action_queue.push_front(action)
			var move_action := Definitions.ActionDef.new()
			move_action.type = Definitions.ActionType.MOVE_TO
			move_action.animation = Definitions.AnimationType.WALK
			move_action.target_coord = adj
			move_action.display_name = "Going to harvest"
			action_comp.current_action = move_action
			action_comp.action_start_tick = sim.time.tick
			return

		if sim.time.tick - action_comp.action_start_tick >= action.duration_ticks:
			inventory.resource_type = action.resource_type
			inventory.amount = action.resource_amount if action.resource_amount > 0.0 else 30.0
			action_comp.current_action = null
	else:
		action_comp.current_action = null


func _execute_drop_off(sim: Simulation, pawn_id: int, action_comp: Components.ActionComponent) -> void:
	var action := action_comp.current_action
	if action.target_entity == -1:
		action_comp.current_action = null
		return

	var target_id: int = action.target_entity
	var inventory: Components.InventoryComponent = sim.entities.inventory.get(pawn_id)
	if inventory == null:
		return
	var pawn_pos: Components.PositionComponent = sim.entities.positions.get(pawn_id)
	if pawn_pos == null:
		return
	var obj_pos: Components.PositionComponent = sim.entities.positions.get(target_id)
	if obj_pos == null:
		return
	var building_comp: Components.BuildingComponent = sim.entities.buildings.get(target_id)
	if building_comp == null:
		return
	var building_def: Dictionary = sim.content.buildings[building_comp.building_def_id]

	if not _is_in_use_area(pawn_pos.coord, obj_pos.coord, building_def):
		var use_target := _find_valid_use_area(sim, obj_pos.coord, pawn_pos.coord, building_def, pawn_id)
		if use_target == Vector2i(-1, -1):
			action_comp.current_action = null
			action_comp.action_queue.clear()
			return
		action_comp.action_queue.push_front(action)
		var move_action := Definitions.ActionDef.new()
		move_action.type = Definitions.ActionType.MOVE_TO
		move_action.animation = Definitions.AnimationType.WALK
		move_action.target_coord = use_target
		move_action.display_name = "Going to deliver %s" % inventory.resource_type
		action_comp.current_action = move_action
		action_comp.action_start_tick = sim.time.tick
		return

	if sim.time.tick - action_comp.action_start_tick < action.duration_ticks:
		return

	# Transfer inventory to building
	var transfer_amount: float = 0.0
	var dest_resource: Components.ResourceComponent = sim.entities.resources.get(target_id)
	if dest_resource != null and inventory.resource_type == dest_resource.resource_type and inventory.amount > 0.0:
		transfer_amount = minf(inventory.amount, dest_resource.max_amount - dest_resource.current_amount)
		dest_resource.current_amount += transfer_amount
		inventory.amount -= transfer_amount
		if inventory.amount <= 0.0:
			inventory.resource_type = ""
			inventory.amount = 0.0

	# Wholesale payment from destination to source building
	if action.source_entity != -1 and transfer_amount > 0.0:
		var dest_gold: Components.GoldComponent = sim.entities.gold.get(target_id)
		var source_gold: Components.GoldComponent = sim.entities.gold.get(action.source_entity)
		if dest_gold != null and source_gold != null:
			var wholesale: int = int(transfer_amount / 10.0)
			var actual: int = mini(wholesale, dest_gold.amount)
			dest_gold.amount -= actual
			source_gold.amount += actual

	# Pawn pay/receive
	var buy_in: int = _get_work_buy_in(building_def)
	var payout: int = _get_payout(building_def)
	var pawn_gold: Components.GoldComponent = sim.entities.gold.get(pawn_id)
	if pawn_gold != null:
		pawn_gold.amount -= buy_in
		var building_gold: Components.GoldComponent = sim.entities.gold.get(target_id)
		if building_gold != null:
			building_gold.amount += buy_in
			var actual_payout: int = mini(payout, building_gold.amount)
			building_gold.amount -= actual_payout
			pawn_gold.amount += actual_payout

	# Satisfy Purpose need
	if action.satisfies_need_id != -1:
		var need_comp: Components.NeedsComponent = sim.entities.needs.get(pawn_id)
		if need_comp != null and need_comp.needs.has(action.satisfies_need_id):
			need_comp.needs[action.satisfies_need_id] = clampf(
				need_comp.needs[action.satisfies_need_id] + action.need_satisfaction_amount,
				0.0, 100.0
			)

	# Grant work buff
	var buff_comp: Components.BuffComponent = sim.entities.buffs.get(pawn_id)
	if buff_comp != null:
		buff_comp.active_buffs = buff_comp.active_buffs.filter(func(b: Definitions.BuffInstance) -> bool:
			return not (b.source == Definitions.BuffSource.WORK and b.source_id == building_def["id"])
		)
		var b := Definitions.BuffInstance.new()
		b.source = Definitions.BuffSource.WORK
		b.source_id = building_def["id"]
		b.mood_offset = 15.0
		b.start_tick = sim.time.tick
		b.end_tick = sim.time.tick + 1200
		buff_comp.active_buffs.append(b)

	# Increment attachment
	var attachment_comp: Components.AttachmentComponent = sim.entities.attachments.get(target_id)
	if attachment_comp != null:
		attachment_comp.user_attachments[pawn_id] = mini(
			10, attachment_comp.user_attachments.get(pawn_id, 0) + 1
		)

	# Brief idle
	if action.satisfies_need_id != -1:
		var idle := Definitions.ActionDef.new()
		idle.type = Definitions.ActionType.IDLE
		idle.duration_ticks = 10
		idle.display_name = "Feeling Productive"
		idle.has_expression = true
		idle.expression = Definitions.ExpressionType.HAPPY
		idle.expression_icon_def_id = action.satisfies_need_id
		action_comp.action_queue.push_front(idle)

	action_comp.current_action = null


# --- Helpers ---------------------------------------------------------------

func _is_in_use_area(pawn_coord: Vector2i, obj_coord: Vector2i, building_def: Dictionary) -> bool:
	var use_areas: Array = building_def.get("useAreas", [])
	if use_areas.is_empty():
		var dist: int = abs(pawn_coord.x - obj_coord.x) + abs(pawn_coord.y - obj_coord.y)
		return dist <= 1

	for offset in use_areas:
		if pawn_coord == obj_coord + offset:
			return true
	return false


func _find_valid_use_area(
	sim: Simulation,
	obj_coord: Vector2i,
	from_coord: Vector2i,
	building_def: Dictionary,
	exclude_pawn: int = -1
) -> Vector2i:
	var use_areas: Array = building_def.get("useAreas", [])
	if use_areas.is_empty():
		use_areas = [Vector2i(0, 1), Vector2i(0, -1), Vector2i(1, 0), Vector2i(-1, 0)]

	var best := Vector2i(-1, -1)
	var best_dist: int = 2147483647

	for offset in use_areas:
		var candidate: Vector2i = obj_coord + offset
		if not sim.world.is_in_bounds(candidate):
			continue
		if not sim.world.get_tile(candidate).walkable:
			continue
		var path := Pathfinder.find_path(sim.world, from_coord, candidate)
		if path.is_empty():
			continue
		var dist: int = abs(candidate.x - from_coord.x) + abs(candidate.y - from_coord.y)
		if dist < best_dist:
			best_dist = dist
			best = candidate

	return best


func _find_adjacent_walkable(world: World, target: Vector2i, from_coord: Vector2i) -> Vector2i:
	var dirs := [Vector2i(0, 1), Vector2i(0, -1), Vector2i(1, 0), Vector2i(-1, 0)]
	var best := Vector2i(-1, -1)
	var best_dist: int = 2147483647

	for d in dirs:
		var adj: Vector2i = target + d
		if not world.is_in_bounds(adj):
			continue
		if not world.is_walkable(adj):
			continue
		var dist: int = abs(adj.x - from_coord.x) + abs(adj.y - from_coord.y)
		if dist < best_dist:
			best_dist = dist
			best = adj

	return best


# --- Economic helpers ------------------------------------------------------

func _get_building_cost(building_def: Dictionary, level: int = 0) -> int:
	var base_cost: int = int(building_def.get("baseCost", 10))
	return int(base_cost * pow(1.15, level))


func _get_payout(building_def: Dictionary, level: int = 0) -> int:
	return int(_get_building_cost(building_def, level) * float(building_def.get("baseProduction", 2.0)))


func _get_work_buy_in(building_def: Dictionary, level: int = 0) -> int:
	var payout: int = _get_payout(building_def, level)
	if payout <= 10:
		return 0
	return payout / 2
