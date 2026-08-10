extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")
const _Definitions = preload("res://src/Core/Definitions.gd")


func run() -> void:
	print("  [MultiPawnTests]")
	run_test("CapacityOne_NeverServesTwoPawnsSimultaneously", test_capacity_one_serializes_pawns)
	run_test("CapacityTwo_BothPawnsEventuallyServed", test_capacity_two_serves_both)
	run_test(
		"SecondPawn_FallsBackToWander_WhenOnlyBuildingIsFull", test_second_pawn_wanders_when_full
	)


# Two hungry pawns, one market with the default capacity of 1: at no point should both
# pawns simultaneously have the market as their current or queued target.
func test_capacity_one_serializes_pawns() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	var hunger_id := builder.define_need("Hunger", 0.05, 15.0, 35.0, 0.0, -5.0)
	var market_id := builder.define_building(
		"Market", hunger_id, 50.0, 20, 0.0, 0, [Vector2i(-1, 0), Vector2i(1, 0)]
	)
	builder.add_building(market_id, 4, 4)
	builder.add_pawn("Alice", 0, 4, {hunger_id: 50.0})
	builder.add_pawn("Bob", 7, 4, {hunger_id: 50.0})
	var sim = builder.build()

	var building_id: int = sim.entities.all_buildings()[0]
	var max_simultaneous := 0

	for _tick in 600:
		sim.tick()
		var count := _count_targeting(sim, building_id)
		max_simultaneous = maxi(max_simultaneous, count)

	assert_eq(
		max_simultaneous,
		1,
		"Capacity-1 building should never be targeted by more than 1 pawn at once"
	)


# Same setup, but capacity 2 — both pawns should be able to get fed without waiting each
# other out.
func test_capacity_two_serves_both() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	var hunger_id := builder.define_need("Hunger", 0.05, 15.0, 35.0, 0.0, -5.0)
	var market_id := builder.define_building(
		"Market",
		hunger_id,
		50.0,
		20,
		0.0,
		0,
		[Vector2i(-1, 0), Vector2i(1, 0)],
		1,
		false,
		"",
		100.0,
		"direct",
		"",
		"",
		true,
		2
	)
	builder.add_building(market_id, 4, 4)
	builder.add_pawn("Alice", 0, 4, {hunger_id: 20.0})
	builder.add_pawn("Bob", 7, 4, {hunger_id: 20.0})
	var sim = builder.build()

	var alice_id := sim.find_pawn_by_name("Alice")
	var bob_id := sim.find_pawn_by_name("Bob")

	sim.run_ticks(600)

	assert_gt(sim.get_need_value(alice_id, hunger_id), 40.0, "Alice should have been fed")
	assert_gt(sim.get_need_value(bob_id, hunger_id), 40.0, "Bob should have been fed")


# With capacity 1 and only one building that satisfies the need, the second pawn to decide
# should fall back to wandering rather than queueing up behind the first.
func test_second_pawn_wanders_when_full() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	var hunger_id := builder.define_need("Hunger", 0.05, 15.0, 35.0, 0.0, -5.0)
	var market_id := builder.define_building(
		"Market", hunger_id, 50.0, 20, 0.0, 0, [Vector2i(-1, 0), Vector2i(1, 0)]
	)
	builder.add_building(market_id, 4, 4)
	builder.add_pawn("Alice", 3, 4, {hunger_id: 50.0})
	builder.add_pawn("Bob", 5, 4, {hunger_id: 50.0})
	var sim = builder.build()

	var building_id: int = sim.entities.all_buildings()[0]
	var bob_id := sim.find_pawn_by_name("Bob")

	# One tick is enough for both pawns' empty-queue decisions to run; Alice (iterated
	# first, since she was added first) claims the market and Bob should not queue behind her.
	sim.tick()

	var bob_action_comp = sim.entities.actions.get(bob_id)
	var bob_targets_market := false
	if bob_action_comp != null:
		if (
			bob_action_comp.current_action != null
			and bob_action_comp.current_action.target_entity == building_id
		):
			bob_targets_market = true
		for queued in bob_action_comp.action_queue:
			if queued.target_entity == building_id:
				bob_targets_market = true
	assert_false(
		bob_targets_market,
		"Second pawn should not queue behind a capacity-1 building already claimed"
	)


# -------------------------------------------------------------------------
# Helpers
# -------------------------------------------------------------------------


func _count_targeting(sim, building_id: int) -> int:
	var count := 0
	for pawn_id in sim.entities.all_pawns():
		var action_comp = sim.entities.actions.get(pawn_id)
		if action_comp == null:
			continue
		var targets := false
		if (
			action_comp.current_action != null
			and action_comp.current_action.target_entity == building_id
		):
			targets = true
		for queued in action_comp.action_queue:
			if queued.target_entity == building_id:
				targets = true
		if targets:
			count += 1
	return count
