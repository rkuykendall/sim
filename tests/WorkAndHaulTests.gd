extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")
const _Definitions = preload("res://src/Core/Definitions.gd")


func run() -> void:
	print("  [WorkAndHaulTests]")
	run_test("DirectWork_SatisfiesPurposeAndReplenishesResource", test_direct_work)
	run_test("HaulFromBuilding_TransfersResourceAndSatisfiesPurpose", test_haul_from_building)
	run_test("HaulFromTerrain_HarvestsAndDelivers", test_haul_from_terrain)
	run_test("HaulFromBuilding_NoSource_FallsBackToWander", test_haul_from_building_no_source_falls_back)
	run_test("HaulFromTerrain_NoTerrain_FallsBackToWander", test_haul_from_terrain_no_terrain_falls_back)
	run_test("HaulFromBuilding_NoSource_SpillsOverToOtherWork", test_haul_from_building_no_source_spills_over)


# workType "direct": a pawn works in place at a low-stock building, which should both
# satisfy its Purpose need and replenish the building's own resource stock.
func test_direct_work() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	var purpose_id := builder.define_need("Purpose", 0.01)
	var mine_id := builder.define_building(
		"Mine", -1, 50.0, 20, 0.0, 0, [], 1, true, "stone", 100.0, "direct"
	)
	builder.add_building(mine_id, 4, 4)
	builder.add_pawn("Miner", 3, 4, {purpose_id: 50.0})
	var sim = builder.build()

	var mine_entity_id := _get_building_by_def_id(sim, mine_id)
	sim.entities.resources[mine_entity_id].current_amount = 10.0  # below 0.8 of max -> eligible for work
	var pawn_id := sim.find_pawn_by_name("Miner")

	sim.run_ticks(2300)  # WORK duration is AISystem.WORK_DURATION_TICKS (2000) + buffer

	assert_gt(sim.get_need_value(pawn_id, purpose_id), 50.0, "Purpose should increase after working")
	assert_gt(sim.entities.resources[mine_entity_id].current_amount, 10.0, "Working should replenish the mine's stock")


# workType "haulFromBuilding": a pawn should pick resources up from a full source building
# and deliver them to a depleted destination building, satisfying Purpose in the process.
func test_haul_from_building() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	var purpose_id := builder.define_need("Purpose", 0.01)
	var source_id := builder.define_building(
		"LumberMill", -1, 50.0, 20, 0.0, 0, [], 1, false, "wood", 100.0
	)
	var dest_id := builder.define_building(
		"Tavern", purpose_id, 40.0, 20, 0.0, 0, [], 1, true, "wood", 100.0,
		"haulFromBuilding", "wood"
	)
	builder.add_building(source_id, 1, 4)
	builder.add_building(dest_id, 6, 4)
	builder.add_pawn("Hauler", 4, 4, {purpose_id: 50.0})
	var sim = builder.build()

	var source_entity_id := _get_building_by_def_id(sim, source_id)
	var dest_entity_id := _get_building_by_def_id(sim, dest_id)
	sim.entities.resources[dest_entity_id].current_amount = 10.0  # below 0.8 of max -> eligible for work
	var pawn_id := sim.find_pawn_by_name("Hauler")

	var source_before: float = sim.entities.resources[source_entity_id].current_amount

	sim.run_ticks(500)

	assert_lt(sim.entities.resources[source_entity_id].current_amount, source_before, "Source stock should decrease after hauling")
	assert_gt(sim.entities.resources[dest_entity_id].current_amount, 10.0, "Destination stock should increase after delivery")
	assert_gt(sim.get_need_value(pawn_id, purpose_id), 50.0, "Purpose should increase after completing the haul")


# workType "haulFromTerrain": a pawn should harvest from the nearest matching terrain tile
# and deliver it to the requesting building.
func test_haul_from_terrain() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 9)
	var purpose_id := builder.define_need("Purpose", 0.01)
	builder.define_terrain("Floor", true, "flat")
	var trees_id := builder.define_terrain("Trees", true)
	var dest_id := builder.define_building(
		"Sawmill", purpose_id, 40.0, 20, 0.0, 0, [], 1, true, "wood", 100.0,
		"haulFromTerrain", "", "Trees"
	)
	builder.add_building(dest_id, 4, 4)
	builder.add_pawn("Woodcutter", 4, 5, {purpose_id: 50.0})
	var sim = builder.build()

	# Paint a harvestable tile a couple steps from the building.
	sim.paint_terrain(Vector2i(6, 4), trees_id)

	var dest_entity_id := _get_building_by_def_id(sim, dest_id)
	sim.entities.resources[dest_entity_id].current_amount = 10.0
	var pawn_id := sim.find_pawn_by_name("Woodcutter")

	sim.run_ticks(2200)  # PICK_UP duration is a fixed 1500 ticks (see AISystem._queue_haul_from_terrain)

	assert_gt(sim.entities.resources[dest_entity_id].current_amount, 10.0, "Destination stock should increase after harvesting delivery")
	assert_gt(sim.get_need_value(pawn_id, purpose_id), 50.0, "Purpose should increase after completing the harvest")


# When a haulFromBuilding destination can't find any source building (all matching sources
# missing or depleted), the pawn must still end up with something queued — an empty queue
# leaves AISystem re-running the full building search and haul lookup every single tick.
func test_haul_from_building_no_source_falls_back() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	var purpose_id := builder.define_need("Purpose", 0.01)
	# No source building of the required resource type exists anywhere on the map.
	var dest_id := builder.define_building(
		"Tavern", purpose_id, 40.0, 20, 0.0, 0, [], 1, true, "wood", 100.0,
		"haulFromBuilding", "wood"
	)
	builder.add_building(dest_id, 4, 4)
	builder.add_pawn("Hauler", 3, 4, {purpose_id: 50.0})
	var sim = builder.build()

	var dest_entity_id := _get_building_by_def_id(sim, dest_id)
	sim.entities.resources[dest_entity_id].current_amount = 10.0  # below 0.8 of max -> eligible for work
	var pawn_id := sim.find_pawn_by_name("Hauler")
	var action_comp = sim.entities.actions.get(pawn_id)

	sim.tick()

	assert_true(
		action_comp.current_action != null or not action_comp.action_queue.is_empty(),
		"Pawn should fall back to wandering when no haul source exists, not be left with an empty queue"
	)


# Same invariant, for haulFromTerrain: no matching terrain anywhere near the building.
func test_haul_from_terrain_no_terrain_falls_back() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 9)
	var purpose_id := builder.define_need("Purpose", 0.01)
	builder.define_terrain("Floor", true, "flat")
	# "Trees" terrain is registered but never painted anywhere on the map.
	builder.define_terrain("Trees", true)
	var dest_id := builder.define_building(
		"Sawmill", purpose_id, 40.0, 20, 0.0, 0, [], 1, true, "wood", 100.0,
		"haulFromTerrain", "", "Trees"
	)
	builder.add_building(dest_id, 4, 4)
	builder.add_pawn("Woodcutter", 4, 5, {purpose_id: 50.0})
	var sim = builder.build()

	var dest_entity_id := _get_building_by_def_id(sim, dest_id)
	sim.entities.resources[dest_entity_id].current_amount = 10.0
	var pawn_id := sim.find_pawn_by_name("Woodcutter")
	var action_comp = sim.entities.actions.get(pawn_id)

	sim.tick()

	assert_true(
		action_comp.current_action != null or not action_comp.action_queue.is_empty(),
		"Pawn should fall back to wandering when no matching terrain exists, not be left with an empty queue"
	)


# A pawn whose top-scoring work candidate can't actually yield work (e.g. a haul job with no
# source) should spill over to the next-best candidate rather than just wandering. The pawn is
# given maximum attachment to Market so it's unambiguously the top-scoring candidate, isolating
# the spillover behavior from scoring coincidence.
func test_haul_from_building_no_source_spills_over() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	var purpose_id := builder.define_need("Purpose", 0.01)

	# Market-equivalent: haulFromBuilding, but no source building exists anywhere -> can never
	# actually be worked, no matter how it scores.
	var market_id := builder.define_building(
		"Market", purpose_id, 40.0, 20, 0.0, 0, [], 1, true, "food", 100.0,
		"haulFromBuilding", "food"
	)
	# Farm-equivalent: direct work, always yields a job once selected.
	var farm_id := builder.define_building(
		"Farm", -1, 50.0, 20, 0.0, 0, [], 1, true, "food", 100.0, "direct"
	)
	builder.add_building(market_id, 4, 4)
	builder.add_building(farm_id, 4, 6)
	builder.add_pawn("Worker", 4, 5, {purpose_id: 50.0})
	var sim = builder.build()

	var market_entity_id := _get_building_by_def_id(sim, market_id)
	var farm_entity_id := _get_building_by_def_id(sim, farm_id)
	sim.entities.resources[market_entity_id].current_amount = 0.0  # eligible, but no source exists
	sim.entities.resources[farm_entity_id].current_amount = 0.0    # equally eligible, and workable

	var pawn_id := sim.find_pawn_by_name("Worker")
	sim.entities.attachments[market_entity_id].need_attachments[purpose_id] = {pawn_id: 10}  # max attachment

	var action_comp = sim.entities.actions.get(pawn_id)

	sim.tick()

	assert_eq(action_comp.action_queue.size(), 1, "Pawn should have queued exactly one action")
	assert_eq(action_comp.action_queue[0].type, _Definitions.ActionType.WORK, "Queued action should be real work, not a wander")
	assert_eq(action_comp.action_queue[0].target_entity, farm_entity_id, "Pawn should spill over to the Farm since Market can't actually be worked")
