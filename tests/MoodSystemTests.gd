extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")


func run() -> void:
	print("  [MoodSystemTests]")
	run_test("NeedBelowCriticalThreshold_AppliesCriticalPenalty", test_need_critical_applies_penalty)
	run_test("NeedBelowLowThreshold_AppliesLowPenalty", test_need_low_applies_penalty)
	run_test("NeedFullySatisfied_ContributesMaxBonus", test_need_satisfied_contributes_max_bonus)
	run_test("NeedRightAtLowThreshold_ContributesZero", test_need_at_low_threshold_contributes_zero)
	run_test("PenaltyCeiling_IsFarLargerThanBonusCeiling", test_penalty_ceiling_exceeds_bonus_ceiling)
	run_test("NeedRecovering_MoodUpdatesImmediately_NoLag", test_need_recovery_updates_mood_immediately)
	run_test("MultipleNeeds_MoodIsAverageAcrossThem", test_multiple_needs_average)
	run_test("MultipleNeeds_MoodClampsAtExtremes", test_mood_clamps_at_extremes)


# All thresholds/penalties now live as shared constants in MoodSystem.gd, not per-need content —
# define_need's own critical/low args are just inert positional params at this point.
func test_need_critical_applies_penalty() -> void:
	var builder := _Builder.new()
	var hunger_id := builder.define_need("Hunger", 0.0)  # zero decay — value set directly below
	builder.add_pawn("Hungry", 2, 2, {hunger_id: 0.0})
	var sim := builder.build()
	var pawn_id := _get_first_pawn(sim)

	sim.tick()

	assert_approx(sim.get_mood(pawn_id), MoodSystem.CRITICAL_PENALTY, 0.01, "A need below the shared critical threshold should apply exactly the shared critical penalty")


func test_need_low_applies_penalty() -> void:
	var builder := _Builder.new()
	var hunger_id := builder.define_need("Hunger", 0.0)
	builder.add_pawn("Peckish", 2, 2, {hunger_id: 25.0})
	var sim := builder.build()
	var pawn_id := _get_first_pawn(sim)

	sim.tick()

	assert_approx(sim.get_mood(pawn_id), MoodSystem.LOW_PENALTY, 0.01, "A need below the shared low threshold (but not critical) should apply exactly the shared low penalty")


func test_need_satisfied_contributes_max_bonus() -> void:
	var builder := _Builder.new()
	var hunger_id := builder.define_need("Hunger", 0.0)
	builder.add_pawn("WellFed", 2, 2, {hunger_id: 100.0})
	var sim := builder.build()
	var pawn_id := _get_first_pawn(sim)

	sim.tick()

	assert_approx(sim.get_mood(pawn_id), MoodSystem.MAX_SATISFACTION_BONUS, 0.01, "A fully satisfied need should contribute exactly the max satisfaction bonus")


func test_need_at_low_threshold_contributes_zero() -> void:
	var builder := _Builder.new()
	var hunger_id := builder.define_need("Hunger", 0.0)
	builder.add_pawn("Neutral", 2, 2, {hunger_id: MoodSystem.LOW_THRESHOLD})
	var sim := builder.build()
	var pawn_id := _get_first_pawn(sim)

	sim.tick()

	assert_approx(sim.get_mood(pawn_id), 0.0, 0.01, "A need sitting exactly at the shared low threshold should contribute neither a penalty nor a bonus")


# The whole point of bringing threshold/penalty shaping back: a handful of well-met needs
# shouldn't be able to casually cancel out one need in real crisis (see MultipleNeeds_MoodIsAverageAcrossThem).
func test_penalty_ceiling_exceeds_bonus_ceiling() -> void:
	assert_lt(MoodSystem.CRITICAL_PENALTY, -MoodSystem.MAX_SATISFACTION_BONUS, "The critical penalty's magnitude should be larger than the max satisfaction bonus")


# Unlike a timed buff, mood is recomputed fresh from current need values every tick — no
# lingering penalty after the underlying need actually recovers.
func test_need_recovery_updates_mood_immediately() -> void:
	var builder := _Builder.new()
	var hunger_id := builder.define_need("Hunger", 0.0)
	builder.add_pawn("Recovering", 2, 2, {hunger_id: 0.0})
	var sim := builder.build()
	var pawn_id := _get_first_pawn(sim)

	sim.tick()
	assert_lt(sim.get_mood(pawn_id), 0.0, "Mood should be negative while the need is critical")

	sim.entities.needs[pawn_id].needs[hunger_id] = 100.0
	sim.tick()
	assert_gt(sim.get_mood(pawn_id), 0.0, "Mood should reflect the recovered need on the very next tick, with no lingering penalty")


func test_multiple_needs_average() -> void:
	var builder := _Builder.new()
	var hunger_id := builder.define_need("Hunger", 0.0)
	var energy_id := builder.define_need("Energy", 0.0)
	builder.add_pawn("Mixed", 2, 2, {hunger_id: 0.0, energy_id: 100.0})
	var sim := builder.build()
	var pawn_id := _get_first_pawn(sim)

	sim.tick()

	var expected: float = (MoodSystem.CRITICAL_PENALTY + MoodSystem.MAX_SATISFACTION_BONUS) / 2.0
	assert_approx(sim.get_mood(pawn_id), expected, 0.01, "Mood should be the average of every need's contribution, not the sum")


func test_mood_clamps_at_extremes() -> void:
	var builder := _Builder.new()
	var a_id := builder.define_need("A", 0.0)
	var b_id := builder.define_need("B", 0.0)
	builder.add_pawn("Content", 2, 2, {a_id: 100.0, b_id: 100.0})
	var sim := builder.build()
	var pawn_id := _get_first_pawn(sim)

	sim.tick()

	assert_approx(sim.get_mood(pawn_id), MoodSystem.MAX_SATISFACTION_BONUS, 0.01, "Two fully satisfied needs should average to the max bonus, well under the +100 clamp")
