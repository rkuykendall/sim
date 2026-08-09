extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")
const _Definitions = preload("res://src/Core/Definitions.gd")


func run() -> void:
	print("  [VisitorPawnTests]")
	run_test("Visitor_WithEmptyNeeds_WandersForever_NeverSeeksBuilding", test_visitor_wanders_forever)
	run_test("Visitor_ExcludedFromColonistCount_PopulationChecksIgnoreThem", test_visitor_excluded_from_population)
	run_test("RemoveAllVisitors_SendsEveryVisitorAway", test_remove_all_visitors_sends_them_away)
	run_test("RemoveAllVisitors_NoOpsCleanly_WithZeroVisitors", test_remove_all_visitors_noop)
	run_test("FindLeastInterestingPawn_NeverPicksAVisitor", test_find_least_interesting_pawn_skips_visitors)


# A needs-less visitor should never target the building that satisfies a real need — with no
# needs, AISystem's "nothing urgent" fallback should keep it wandering indefinitely.
func test_visitor_wanders_forever() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(20, 20)
	var need_id := builder.define_need("Hunger", 0.02, 15.0, 35.0, -5.0, -1.0, "food")
	var canteen_id := builder.define_building(
		"Canteen", need_id, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 1, false
	)
	builder.add_building(canteen_id, 10, 10)
	var sim := builder.build()

	var visitor_id := sim.spawn_visitor_pawn("special_4_v2", {}, "Wanderer")
	assert_not_eq(visitor_id, -1, "Visitor should spawn on a walkable edge tile")

	var needs_comp = sim.entities.needs.get(visitor_id)
	assert_true(needs_comp.needs.is_empty(), "Visitor should start with a genuinely empty needs dict")

	sim.run_ticks(600)

	assert_true(needs_comp.needs.is_empty(), "Visitor's needs dict should stay empty — nothing should populate it")
	var action_comp = sim.entities.actions.get(visitor_id)
	var targeted_canteen: bool = false
	if action_comp.current_action != null and action_comp.current_action.type == _Definitions.ActionType.USE_BUILDING:
		targeted_canteen = true
	assert_false(targeted_canteen, "A needs-less visitor should never target a need-satisfying building")


# Capacity-1 home already at capacity with one real colonist, plus several visitors — the
# visitors' presence must not trigger a spawn (looks under capacity) or an emigration (looks
# over capacity), since they're excluded from the colonist headcount entirely.
func test_visitor_excluded_from_population() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(20, 20)
	var home_id := builder.define_building(
		"TinyHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 1, true
	)
	builder.add_building(home_id, 10, 10)
	builder.add_pawn("Colonist", 10, 9, {})
	var sim := builder.build()

	assert_eq(sim.get_max_pawns(), 1, "Capacity-1 home should cap population at 1")

	for i in 3:
		sim.spawn_visitor_pawn("special_4_v2", {}, "Visitor %d" % (i + 1))

	assert_eq(sim.entities.all_pawns().size(), 4, "Setup should have 1 colonist + 3 visitors")

	sim.run_ticks(Simulation.PAWN_SPAWN_INTERVAL + 10)

	assert_eq(sim.entities.all_pawns().size(), 4, "Visitors present shouldn't trigger a spawn or an emigration")
	assert_not_eq(sim.find_pawn_by_name("Colonist"), -1, "The real colonist should still be present")


func test_remove_all_visitors_sends_them_away() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(20, 20)
	var sim := builder.build()

	var visitor_ids: Array = []
	for i in 3:
		visitor_ids.append(sim.spawn_visitor_pawn("special_4_v2", {}, "Visitor %d" % (i + 1)))

	sim.remove_all_visitors()

	for visitor_id in visitor_ids:
		var action_comp = sim.entities.actions.get(visitor_id)
		assert_not_null(action_comp.current_action, "Each visitor should have an active leave-town action")
		assert_eq(
			action_comp.current_action.type, _Definitions.ActionType.MOVE_TO,
			"Visitor should be walking toward the map edge"
		)
		var queued_leave: bool = false
		for queued in action_comp.action_queue:
			if queued.type == _Definitions.ActionType.LEAVE_TOWN:
				queued_leave = true
		assert_true(queued_leave, "A LEAVE_TOWN action should be queued after the walk")

	sim.run_ticks(2000)
	for visitor_id in visitor_ids:
		assert_false(sim.entities.all_pawns().has(visitor_id), "Visitor should be gone after enough time to walk off")


func test_remove_all_visitors_noop() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(20, 20)
	builder.add_pawn("Colonist", 10, 10, {})
	var sim := builder.build()

	sim.remove_all_visitors()

	assert_eq(sim.entities.all_pawns().size(), 1, "remove_all_visitors should no-op cleanly with zero visitors")
	assert_not_eq(sim.find_pawn_by_name("Colonist"), -1, "The lone colonist should be unaffected")


# Regression test for a real bug: a needs-less visitor always scores exactly 0.0 (no mood
# debuffs, no attachment), which can look "safer" to evict than a real, HAPPY colonist —
# whose positive mood score is actually higher (better) than the visitor's guaranteed 0.
# Without the membership check, _find_least_interesting_pawn would wrongly pick the visitor
# (lower score) over the happy colonist.
func test_find_least_interesting_pawn_skips_visitors() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(20, 20)
	var home_id := builder.define_building(
		"TinyHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 1, true
	)
	builder.add_building(home_id, 10, 10)
	builder.add_pawn("Happy", 10, 9, {})
	var sim := builder.build()

	var happy_id := sim.find_pawn_by_name("Happy")
	var buff_comp = sim.entities.buffs.get(happy_id)
	var b := _Definitions.BuffInstance.new()
	b.source = _Definitions.BuffSource.BUILDING
	b.source_id = -1
	b.mood_offset = 80.0
	b.start_tick = sim.time.tick
	b.end_tick = -1
	buff_comp.active_buffs.append(b)
	sim.run_ticks(1)  # MoodSystem only applies active_buffs into MoodComponent.mood on tick

	var visitor_id := sim.spawn_visitor_pawn("special_4_v2", {}, "Visitor")

	var worst_id: int = sim._find_least_interesting_pawn()
	assert_eq(worst_id, happy_id, "With visitors excluded, the sole real colonist is the only eligible candidate")
	assert_not_eq(worst_id, visitor_id, "A visitor's guaranteed-0 score must never make it eligible for capacity-driven emigration")
