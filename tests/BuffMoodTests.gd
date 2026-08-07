extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")
const _Definitions = preload("res://src/Core/Definitions.gd")


func run() -> void:
	print("  [BuffMoodTests]")
	run_test("ActiveBuff_RaisesMoodImmediately", test_active_buff_raises_mood)
	run_test("TransientBuff_ExpiresAndMoodReturnsToZero", test_transient_buff_expires)
	run_test("MultipleBuffs_MoodIsSumClampedTo100", test_mood_sums_and_clamps_positive)
	run_test("MultipleBuffs_MoodIsSumClampedToNegative100", test_mood_sums_and_clamps_negative)
	run_test("NeedBelowCriticalThreshold_AppliesSingleDebuff", test_need_critical_applies_debuff)
	run_test("NeedRecoveringAboveThreshold_RemovesDebuff", test_need_recovery_removes_debuff)


func test_active_buff_raises_mood() -> void:
	var builder := _Builder.new()
	builder.add_pawn("Solo", 2, 2, {})
	var sim = builder.build()
	var pawn_id := _get_first_pawn(sim)

	_add_buff(sim, pawn_id, 20.0, -1)
	sim.tick()

	assert_approx(sim.get_mood(pawn_id), 20.0, 0.01, "Mood should reflect the active buff's offset")


func test_transient_buff_expires() -> void:
	var builder := _Builder.new()
	builder.add_pawn("Solo", 2, 2, {})
	var sim = builder.build()
	var pawn_id := _get_first_pawn(sim)

	_add_buff(sim, pawn_id, 20.0, sim.time.tick + 5)
	sim.tick()
	assert_approx(sim.get_mood(pawn_id), 20.0, 0.01, "Mood should reflect the buff while active")

	sim.run_ticks(10)  # past end_tick

	assert_approx(sim.get_mood(pawn_id), 0.0, 0.01, "Mood should return to 0 once the buff expires")
	var buff_comp = sim.entities.buffs.get(pawn_id)
	assert_eq(buff_comp.active_buffs.size(), 0, "Expired buff should be removed from active_buffs")


func test_mood_sums_and_clamps_positive() -> void:
	var builder := _Builder.new()
	builder.add_pawn("Solo", 2, 2, {})
	var sim = builder.build()
	var pawn_id := _get_first_pawn(sim)

	_add_buff(sim, pawn_id, 80.0, -1)
	_add_buff(sim, pawn_id, 80.0, -1)
	sim.tick()

	assert_approx(sim.get_mood(pawn_id), 100.0, 0.01, "Mood should clamp at 100 even if buffs sum higher")


func test_mood_sums_and_clamps_negative() -> void:
	var builder := _Builder.new()
	builder.add_pawn("Solo", 2, 2, {})
	var sim = builder.build()
	var pawn_id := _get_first_pawn(sim)

	_add_buff(sim, pawn_id, -80.0, -1)
	_add_buff(sim, pawn_id, -80.0, -1)
	sim.tick()

	assert_approx(sim.get_mood(pawn_id), -100.0, 0.01, "Mood should clamp at -100 even if buffs sum lower")


# A need that decays below its critical threshold (with no building around to satisfy it)
# should pick up exactly one NEED_CRITICAL debuff, which drags mood negative.
func test_need_critical_applies_debuff() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(6, 6)
	var hunger_id := builder.define_need("Hunger", 2.0, 15.0, 35.0, -30.0, -10.0)
	builder.add_pawn("Hungry", 2, 2, {hunger_id: 20.0})
	var sim = builder.build()

	var pawn_id := _get_first_pawn(sim)

	sim.run_ticks(10)  # 20 - 2.0*10 = 0, well past the critical threshold of 15

	assert_lt(sim.get_mood(pawn_id), 0.0, "Mood should be negative once hunger is critical")
	var buff_comp = sim.entities.buffs.get(pawn_id)
	var critical_count := 0
	for b in buff_comp.active_buffs:
		if b.source == _Definitions.BuffSource.NEED_CRITICAL and b.source_id == hunger_id:
			critical_count += 1
	assert_eq(critical_count, 1, "Should have exactly one critical debuff for the need, not stacked duplicates")


# Once a need recovers back above its low threshold, its debuff should be removed and mood
# should return to neutral.
func test_need_recovery_removes_debuff() -> void:
	var builder := _Builder.new()
	builder.add_pawn("Solo", 2, 2, {})
	var sim = builder.build()
	var pawn_id := _get_first_pawn(sim)
	var buff_comp = sim.entities.buffs.get(pawn_id)

	var need_def: Dictionary = {
		"id": 999, "decayPerTick": 0.0, "criticalThreshold": 15.0, "lowThreshold": 35.0,
		"criticalDebuff": -30.0, "lowDebuff": -10.0,
	}

	# Simulate the need dropping critical, then recovering, via the same entry point NeedsSystem uses.
	var needs_system := NeedsSystem.new()
	needs_system._update_need_debuffs(buff_comp, need_def, 5.0, sim.time.tick)
	assert_eq(buff_comp.active_buffs.size(), 1, "Should have a debuff while critical")

	needs_system._update_need_debuffs(buff_comp, need_def, 100.0, sim.time.tick)
	assert_eq(buff_comp.active_buffs.size(), 0, "Debuff should be cleared once the need recovers")


# -------------------------------------------------------------------------
# Helpers
# -------------------------------------------------------------------------

func _add_buff(sim, pawn_id: int, mood_offset: float, end_tick: int) -> void:
	var buff_comp = sim.entities.buffs.get(pawn_id)
	var b := _Definitions.BuffInstance.new()
	b.source = _Definitions.BuffSource.BUILDING
	b.source_id = -1
	b.mood_offset = mood_offset
	b.start_tick = sim.time.tick
	b.end_tick = end_tick
	buff_comp.active_buffs.append(b)


