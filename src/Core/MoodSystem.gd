class_name MoodSystem

## Same shape as the old per-need criticalThreshold/lowThreshold/criticalDebuff/lowDebuff
## tuning, but shared across every need instead of defined need-by-need in content — a need
## below CRITICAL_THRESHOLD takes a flat, disproportionately large hit; between critical and
## LOW_THRESHOLD a smaller flat hit; at or above LOW_THRESHOLD a bonus that scales up only to
## MAX_SATISFACTION_BONUS at full satisfaction — deliberately a much smaller ceiling than the
## penalties, so a handful of well-met needs can't casually outweigh one in real crisis.
## CRITICAL_PENALTY tuned against a real struggling save (39 colonists, every one missing a
## Well) to land around -6 average mood, matching the old per-need-tuned formula's result there.
const CRITICAL_THRESHOLD: float = 15.0
const LOW_THRESHOLD: float = 35.0
const CRITICAL_PENALTY: float = -45.0
const LOW_PENALTY: float = -10.0
const MAX_SATISFACTION_BONUS: float = 10.0


func tick(sim: Simulation) -> void:
	for pawn_id in sim.entities.pawns:
		var mood_comp: Components.MoodComponent = sim.entities.moods.get(pawn_id)
		if mood_comp == null:
			continue
		var need_comp: Components.NeedsComponent = sim.entities.needs.get(pawn_id)
		if need_comp == null or need_comp.needs.is_empty():
			continue

		var total: float = 0.0
		for need_id in need_comp.needs:
			total += _need_mood_contribution(need_comp.needs[need_id])
		var avg: float = total / need_comp.needs.size()

		mood_comp.mood = clampf(avg, -100.0, 100.0)


func _need_mood_contribution(value: float) -> float:
	if value < CRITICAL_THRESHOLD:
		return CRITICAL_PENALTY
	if value < LOW_THRESHOLD:
		return LOW_PENALTY

	var satisfaction: float = (value - LOW_THRESHOLD) / (100.0 - LOW_THRESHOLD)
	return MAX_SATISFACTION_BONUS * satisfaction
