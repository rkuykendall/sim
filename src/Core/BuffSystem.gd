class_name BuffSystem


func tick(sim: Simulation) -> void:
	var now: int = sim.time.tick

	for buff_comp in sim.entities.buffs.values():
		buff_comp.active_buffs = buff_comp.active_buffs.filter(
			func(b: Definitions.BuffInstance) -> bool:
				return not (b.end_tick > 0 and b.end_tick <= now)
		)
