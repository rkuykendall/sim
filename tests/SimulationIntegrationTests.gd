extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")


func run() -> void:
	print("  [SimulationIntegrationTests]")
	run_test("Pawn_WithLowHunger_UsesMarket_AndGetsFed", test_low_hunger_uses_market)
	run_test("Pawn_WithFullHunger_DoesNotSeekMarket", test_full_hunger_no_market)
	run_test("Simulation_TicksAdvanceTime", test_ticks_advance_time)
	run_test("Needs_DecayOverTime", test_needs_decay)
	run_test("Pawn_NavigatesToBuilding_AndUsesIt", test_navigates_to_building)
	run_test("DestroyEntity_RestoresTileWalkability", test_destroy_restores_walkability)


# Pawn starts hungry next to a market; after enough ticks should have eaten.
func test_low_hunger_uses_market() -> void:
	var builder := _Builder.new()
	var hunger_id := builder.define_need("Hunger", 0.02)
	var market_id := builder.define_building(
		"Market",
		hunger_id,          # satisfies_need_id
		50.0,               # satisfaction_amount
		20,                 # interaction_duration
		15.0,               # grants_buff
		2400,               # buff_duration
		[Vector2i(-1, 0)]   # use_areas: pawn must be left of market
	)
	builder.add_building(market_id, 4, 0)
	builder.add_pawn("TestPawn", 0, 0, {hunger_id: 0.0})
	var sim = builder.build()

	var pawn_id := _get_first_pawn(sim)
	assert_not_null(pawn_id, "Should have a pawn")
	var initial_hunger := _get_need_value(sim, pawn_id, hunger_id)
	assert_approx(initial_hunger, 0.0, 0.1, "Initial hunger should be ~0")

	# Walk ~4 tiles + use building + buffer
	_run_ticks(sim, 100)

	var final_hunger := _get_need_value(sim, pawn_id, hunger_id)
	assert_gt(final_hunger, initial_hunger, "Hunger should increase after eating")
	assert_gt(final_hunger, 40.0, "Hunger should be > 40 after eating")


# Pawn with full hunger should not seek market.
func test_full_hunger_no_market() -> void:
	var builder := _Builder.new()
	var hunger_id := builder.define_need("Hunger", 0.001)
	var market_id := builder.define_building(
		"Market",
		hunger_id,
		50.0, 20, 15.0, 2400,
		[Vector2i(-1, 0)]
	)
	builder.add_building(market_id, 4, 0)
	builder.add_pawn("TestPawn", 0, 0, {hunger_id: 100.0})
	var sim = builder.build()

	var pawn_id := _get_first_pawn(sim)
	assert_not_null(pawn_id, "Should have a pawn")

	_run_ticks(sim, 50)

	var hunger := _get_need_value(sim, pawn_id, hunger_id)
	assert_gt(hunger, 95.0, "Hunger should remain high (~100) with slow decay and no trip to market")


# Ticks advance the simulation time counter.
func test_ticks_advance_time() -> void:
	var builder := _Builder.new()
	var sim = builder.build()
	var initial_tick = sim.time.tick

	_run_ticks(sim, 100)

	assert_eq(sim.time.tick, initial_tick + 100, "Tick counter should advance by 100")


# Needs decay over time without any buildings to satisfy them.
func test_needs_decay() -> void:
	var builder := _Builder.new()
	var hunger_id := builder.define_need("Hunger", 0.5)
	builder.add_pawn("TestPawn", 2, 2, {hunger_id: 100.0})
	var sim = builder.build()

	var pawn_id := _get_first_pawn(sim)
	assert_not_null(pawn_id, "Should have a pawn")
	var initial_hunger := _get_need_value(sim, pawn_id, hunger_id)
	assert_approx(initial_hunger, 100.0, 0.1, "Initial hunger should be 100")

	_run_ticks(sim, 100)

	var final_hunger := _get_need_value(sim, pawn_id, hunger_id)
	assert_lt(final_hunger, initial_hunger, "Hunger should decay over time")
	# 0.5 decay/tick × 100 ticks = 50 points decay → ~50 remaining
	assert_lt(final_hunger, 60.0, "Hunger should have decayed to at most 60")


# Pawn navigates to a distant building and uses it.
func test_navigates_to_building() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 0)
	var hunger_id := builder.define_need("Hunger", 0.01)
	var market_id := builder.define_building(
		"Market", hunger_id, 50.0, 20, 15.0, 2400, [Vector2i(-1, 0)]
	)
	builder.add_building(market_id, 9, 0)
	builder.add_pawn("TestPawn", 0, 0, {hunger_id: 10.0})
	var sim = builder.build()

	var pawn_id := _get_first_pawn(sim)
	assert_not_null(pawn_id, "Should have a pawn")

	# 9 tiles × 10 ticks/tile + 20 ticks interaction + buffer
	_run_ticks(sim, 200)

	var final_hunger := _get_need_value(sim, pawn_id, hunger_id)
	assert_gt(final_hunger, 40.0, "Pawn should have eaten (hunger > 40)")


# Destroying a building restores tile walkability.
func test_destroy_restores_walkability() -> void:
	var builder := _Builder.new()
	var hunger_id := builder.define_need("Hunger")
	var market_id := builder.define_building("Market", hunger_id)
	builder.add_building(market_id, 2, 2)
	var sim = builder.build()

	var building_id = sim.entities.all_buildings()[0]
	var coord := Vector2i(2, 2)

	# Tile should be blocked by the building
	assert_false(sim.world.get_tile(coord).walkable, "Tile should not be walkable with building on it")

	sim.destroy_entity(building_id)

	assert_true(sim.world.get_tile(coord).walkable, "Tile should be walkable after building destruction")
	assert_false(sim.entities.buildings.has(building_id), "Building should be removed from entities")


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
