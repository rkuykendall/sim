extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")
const _Definitions = preload("res://src/Core/Definitions.gd")


func run() -> void:
	print("  [PawnLifecycleTests]")
	run_test("Pawn_SeeksFoodWhenHasDebuff", test_seeks_food_when_debuff)
	run_test("Pawn_WandersWhenNoDebuffs", test_wanders_when_no_debuffs)
	run_test("Pawn_SatisfiesNeed_Wanders_ThenReturns", test_lifecycle_eat_wander_return)
	run_test("Pawn_SurvivesLongTerm", test_survives_long_term)


# Pawn below debuff threshold should seek market within a few ticks.
func test_seeks_food_when_debuff() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(4, 0)
	var hunger_id := builder.define_need("Hunger", 0.01, 15.0, 35.0, 0.0, -5.0)
	var market_id := builder.define_building(
		"Market", hunger_id, 50.0, 20, 0.0, 0, [Vector2i(-1, 0)]
	)
	builder.add_building(market_id, 4, 0)
	builder.add_pawn("TestPawn", 0, 0, {hunger_id: 30.0})
	var sim = builder.build()

	var pawn_id := _get_first_pawn(sim)
	assert_not_eq(pawn_id, -1, "Should have a pawn")

	_run_ticks(sim, 5)

	# After 5 ticks pawn should be en route to or queued for Market
	var action_comp = sim.entities.actions.get(pawn_id)
	var is_going_to_market := false
	if action_comp != null:
		if action_comp.current_action != null:
			var name: String = action_comp.current_action.display_name
			if "Market" in name or "market" in name:
				is_going_to_market = true
		for queued in action_comp.action_queue:
			var qname: String = queued.display_name
			if "Market" in qname or "market" in qname:
				is_going_to_market = true
	assert_true(is_going_to_market, "Pawn with hunger=30 (below debuff threshold 35) should be going to Market")


# Pawn with high hunger should wander, not seek market.
func test_wanders_when_no_debuffs() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(4, 0)
	var hunger_id := builder.define_need("Hunger", 0.001, 15.0, 35.0, 0.0, -5.0)
	var market_id := builder.define_building("Market", hunger_id, 50.0, 20)
	builder.add_building(market_id, 4, 0)
	builder.add_pawn("TestPawn", 0, 0, {hunger_id: 95.0})
	var sim = builder.build()

	var pawn_id := _get_first_pawn(sim)
	assert_not_eq(pawn_id, -1, "Should have a pawn")

	_run_ticks(sim, 5)

	var action_comp = sim.entities.actions.get(pawn_id)
	var is_going_to_market := false
	if action_comp != null:
		if action_comp.current_action != null:
			var name: String = action_comp.current_action.display_name
			if "Market" in name or "market" in name:
				is_going_to_market = true
	assert_false(is_going_to_market, "Pawn with high hunger should not seek Market")


# Full lifecycle: eat, wander when satisfied, return when hungry again.
func test_lifecycle_eat_wander_return() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 9)
	var hunger_id := builder.define_need("Hunger", 0.5, 15.0, 35.0, 0.0, -5.0)
	var market_id := builder.define_building(
		"Market", hunger_id, 50.0, 20
	)
	builder.add_building(market_id, 5, 5)
	builder.add_pawn("TestPawn", 0, 0, {hunger_id: 10.0})
	var sim = builder.build()

	var pawn_id := _get_first_pawn(sim)
	assert_not_eq(pawn_id, -1, "Should have a pawn")

	var times_used_market := 0
	var was_wandering := false
	var went_back_to_market := false
	var last_hunger := 10.0

	for tick in 500:
		sim.tick()
		var hunger := _get_need_value(sim, pawn_id, hunger_id)
		var action_comp = sim.entities.actions.get(pawn_id)

		# Detect market use (hunger jumps up significantly)
		if hunger > last_hunger + 10.0:
			times_used_market += 1
			if was_wandering and times_used_market >= 2:
				went_back_to_market = true

		# Detect wandering (high hunger + wander action)
		if hunger > 50.0 and action_comp != null and action_comp.current_action != null:
			var aname: String = action_comp.current_action.display_name
			if "Wander" in aname or "wander" in aname or action_comp.current_action.type == _Definitions.ActionType.IDLE:
				was_wandering = true

		last_hunger = hunger

		if went_back_to_market:
			break

	assert_ge(float(times_used_market), 1.0, "Pawn should have used market at least once")
	assert_true(was_wandering, "Pawn should have wandered when hunger was high")
	assert_ge(float(times_used_market), 2.0, "Pawn should have used market at least twice")
	assert_true(went_back_to_market, "Pawn should return to market after wandering")


# Long-term survival: pawn eats and wanders repeatedly over 1000 ticks.
func test_survives_long_term() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 9)
	var hunger_id := builder.define_need("Hunger", 0.3, 15.0, 35.0, 0.0, -5.0)
	var market_id := builder.define_building(
		"Market", hunger_id, 60.0, 20
	)
	builder.add_building(market_id, 5, 5)
	builder.add_pawn("TestPawn", 0, 0, {hunger_id: 50.0})
	var sim = builder.build()

	var pawn_id := _get_first_pawn(sim)
	assert_not_eq(pawn_id, -1, "Should have a pawn")

	var times_used_market := 0
	var min_hunger := 50.0
	var last_hunger := 50.0

	for _tick in 1000:
		sim.tick()
		var hunger := _get_need_value(sim, pawn_id, hunger_id)
		if hunger > last_hunger + 10.0:
			times_used_market += 1
		if hunger < min_hunger:
			min_hunger = hunger
		last_hunger = hunger

	var final_hunger := _get_need_value(sim, pawn_id, hunger_id)

	assert_ge(float(times_used_market), 3.0, "Pawn should have eaten at least 3 times over 1000 ticks")
	assert_gt(min_hunger, 0.0, "Pawn should never have starved (hunger never hit 0)")
	assert_gt(final_hunger, 0.0, "Pawn should still be alive at end")


# -------------------------------------------------------------------------
# Helpers
# -------------------------------------------------------------------------

func _get_first_pawn(sim) -> int:
	var pawns = sim.entities.all_pawns()
	return pawns[0] if not pawns.is_empty() else -1


func _get_need_value(sim, pawn_id: int, need_id: int) -> float:
	var need_comp = sim.entities.needs.get(pawn_id)
	if need_comp == null:
		return 0.0
	return need_comp.needs.get(need_id, 0.0)


func _run_ticks(sim, count: int) -> void:
	for _i in count:
		sim.tick()
