class_name NeedsSystem


func tick(sim: Simulation) -> void:
	var is_night: bool = sim.time.is_night
	var is_sleep_time: bool = sim.time.is_sleep_time
	var energy_need_id: int = sim.content.get_need_id("Energy")

	for pawn_id in sim.entities.all_pawns():
		var need_comp: Components.NeedsComponent = sim.entities.needs.get(pawn_id)
		if need_comp == null:
			continue
		var buff_comp: Components.BuffComponent = sim.entities.buffs.get(pawn_id)
		if buff_comp == null:
			continue

		for need_id in need_comp.needs.keys():
			var need_def: Dictionary = sim.content.needs.get(need_id, {})
			if need_def.is_empty():
				continue

			var decay: float = float(need_def["decayPerTick"])

			# Energy decays faster at night
			if need_id == energy_need_id and is_night:
				decay *= 2.5 if is_sleep_time else 1.5

			var old_value: float = need_comp.needs[need_id]
			var new_value: float = clampf(old_value - decay, 0.0, 100.0)
			need_comp.needs[need_id] = new_value

			_update_need_debuffs(buff_comp, need_def, new_value, sim.time.tick)


func _update_need_debuffs(
	buff_comp: Components.BuffComponent,
	need_def: Dictionary,
	value: float,
	current_tick: int
) -> void:
	var need_id: int = need_def["id"]

	# Remove existing debuffs for this need
	buff_comp.active_buffs = buff_comp.active_buffs.filter(func(b: Definitions.BuffInstance) -> bool:
		return not (
			(b.source == Definitions.BuffSource.NEED_CRITICAL
			or b.source == Definitions.BuffSource.NEED_LOW)
			and b.source_id == need_id
		)
	)

	var critical_threshold: float = float(need_def["criticalThreshold"])
	var low_threshold: float = float(need_def["lowThreshold"])
	var critical_debuff: float = float(need_def["criticalDebuff"])
	var low_debuff: float = float(need_def["lowDebuff"])

	if value < critical_threshold and critical_debuff != 0.0:
		var b := Definitions.BuffInstance.new()
		b.source = Definitions.BuffSource.NEED_CRITICAL
		b.source_id = need_id
		b.mood_offset = critical_debuff
		b.start_tick = current_tick
		b.end_tick = -1
		buff_comp.active_buffs.append(b)
	elif value < low_threshold and low_debuff != 0.0:
		var b := Definitions.BuffInstance.new()
		b.source = Definitions.BuffSource.NEED_LOW
		b.source_id = need_id
		b.mood_offset = low_debuff
		b.start_tick = current_tick
		b.end_tick = -1
		buff_comp.active_buffs.append(b)
