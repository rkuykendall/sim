class_name NeedsSystem


func tick(sim: Simulation) -> void:
	for pawn_id in sim.entities.pawns:
		var need_comp: Components.NeedsComponent = sim.entities.needs.get(pawn_id)
		if need_comp == null:
			continue

		for need_id in need_comp.needs.keys():
			var need_def: Dictionary = sim.content.needs.get(need_id, {})
			if need_def.is_empty():
				continue

			var decay: float = float(need_def["decayPerTick"])

			var old_value: float = need_comp.needs[need_id]
			var new_value: float = clampf(old_value - decay, 0.0, 100.0)
			need_comp.needs[need_id] = new_value
