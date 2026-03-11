class_name MoodSystem


func tick(sim: Simulation) -> void:
	for pawn_id in sim.entities.all_pawns():
		var mood_comp: Components.MoodComponent = sim.entities.moods.get(pawn_id)
		if mood_comp == null:
			continue
		var buff_comp: Components.BuffComponent = sim.entities.buffs.get(pawn_id)
		if buff_comp == null:
			continue

		var mood: float = 0.0
		for buff in buff_comp.active_buffs:
			mood += buff.mood_offset

		mood_comp.mood = clampf(mood, -100.0, 100.0)
