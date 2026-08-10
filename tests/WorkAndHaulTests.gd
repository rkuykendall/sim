extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")
const _Definitions = preload("res://src/Core/Definitions.gd")


func run() -> void:
	print("  [WorkAndHaulTests]")
	run_test("DirectWork_SatisfiesPurposeAndReplenishesResource", test_direct_work)
	run_test("HaulFromBuilding_TransfersResourceAndSatisfiesPurpose", test_haul_from_building)
	run_test("HaulFromTerrain_HarvestsAndDelivers", test_haul_from_terrain)
	run_test(
		"HaulFromBuilding_NoSource_FallsBackToWander", test_haul_from_building_no_source_falls_back
	)
	run_test(
		"HaulFromTerrain_NoTerrain_FallsBackToWander", test_haul_from_terrain_no_terrain_falls_back
	)
	run_test(
		"HaulFromBuilding_NoSource_SpillsOverToOtherWork",
		test_haul_from_building_no_source_spills_over
	)
	run_test(
		"DirectWork_UsesBuildingsOwnProductionAmount",
		test_direct_work_uses_custom_production_amount
	)
	run_test(
		"HaulFromTerrain_StaysWorkEligible_EvenWhenStockIsNearlyFull",
		test_haul_from_terrain_stays_eligible_when_full
	)
	run_test(
		"HaulFromTerrain_WorksWithNoResourceStorageAtAll",
		test_haul_from_terrain_without_resource_component
	)
	run_test(
		"Snapshot_HaulFromTerrainPickup_HasNoBuildingTarget",
		test_snapshot_haul_from_terrain_pickup_has_no_building_target
	)
	run_test(
		"Snapshot_HaulFromBuildingPickup_HasBuildingTarget",
		test_snapshot_haul_from_building_pickup_has_building_target
	)
	run_test(
		"HaulFromTerrain_ClearsInventory_EvenWhenDestinationHasNoStorage",
		test_haul_from_terrain_clears_inventory_without_storage
	)


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

	assert_gt(
		sim.get_need_value(pawn_id, purpose_id), 50.0, "Purpose should increase after working"
	)
	assert_gt(
		sim.entities.resources[mine_entity_id].current_amount,
		10.0,
		"Working should replenish the mine's stock"
	)


# Each direct-work building can set its own productionAmount (e.g. a Farm and a Tavern
# shouldn't be forced to produce at the same rate just because they share workType "direct").
func test_direct_work_uses_custom_production_amount() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	var purpose_id := builder.define_need("Purpose", 0.01)
	var mine_id := builder.define_building(
		"SlowMine",
		-1,
		50.0,
		20,
		0.0,
		0,
		[],
		1,
		true,
		"stone",
		100.0,
		"direct",
		"",
		"",
		true,
		1,
		false,
		5.0
	)
	builder.add_building(mine_id, 4, 4)
	builder.add_pawn("Miner", 3, 4, {purpose_id: 50.0})
	var sim = builder.build()

	var mine_entity_id := _get_building_by_def_id(sim, mine_id)
	sim.entities.resources[mine_entity_id].current_amount = 10.0

	sim.run_ticks(2300)

	assert_approx(
		sim.entities.resources[mine_entity_id].current_amount,
		15.0,
		0.01,
		"A custom productionAmount of 5.0 should add exactly 5 to the starting stock of 10, not the default 30"
	)


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
		"Tavern",
		purpose_id,
		40.0,
		20,
		0.0,
		0,
		[],
		1,
		true,
		"wood",
		100.0,
		"haulFromBuilding",
		"wood"
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

	assert_lt(
		sim.entities.resources[source_entity_id].current_amount,
		source_before,
		"Source stock should decrease after hauling"
	)
	assert_gt(
		sim.entities.resources[dest_entity_id].current_amount,
		10.0,
		"Destination stock should increase after delivery"
	)
	assert_gt(
		sim.get_need_value(pawn_id, purpose_id),
		50.0,
		"Purpose should increase after completing the haul"
	)


# workType "haulFromTerrain": a pawn should harvest from the nearest matching terrain tile
# and deliver it to the requesting building.
func test_haul_from_terrain() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 9)
	var purpose_id := builder.define_need("Purpose", 0.01)
	builder.define_terrain("Floor", true, "flat")
	var trees_id := builder.define_terrain("Trees", true)
	var dest_id := builder.define_building(
		"Sawmill",
		purpose_id,
		40.0,
		20,
		0.0,
		0,
		[],
		1,
		true,
		"wood",
		100.0,
		"haulFromTerrain",
		"wood",
		"Trees"
	)
	builder.add_building(dest_id, 4, 4)
	builder.add_pawn("Woodcutter", 4, 5, {purpose_id: 50.0})
	var sim = builder.build()

	# Paint a harvestable tile a couple steps from the building.
	sim.paint_terrain(Vector2i(6, 4), trees_id)

	var dest_entity_id := _get_building_by_def_id(sim, dest_id)
	sim.entities.resources[dest_entity_id].current_amount = 10.0
	var pawn_id := sim.find_pawn_by_name("Woodcutter")

	sim.run_ticks(2200)  # PICK_UP duration is AISystem.HARVEST_DURATION_TICKS (750) + travel/buffer

	assert_gt(
		sim.entities.resources[dest_entity_id].current_amount,
		10.0,
		"Destination stock should increase after harvesting delivery"
	)
	assert_gt(
		sim.get_need_value(pawn_id, purpose_id),
		50.0,
		"Purpose should increase after completing the harvest"
	)


# haulFromTerrain sources (e.g. Trees) never run dry, unlike a haulFromBuilding source that
# can genuinely be depleted. A haulFromTerrain destination building must stay work-eligible
# even once its own stock is nearly full — otherwise it fills up once, permanently drops out
# of every future candidate list, and silently stops offering work for the rest of the game
# (this is exactly what happened to the real LumberMill building). haulFromBuilding should
# still respect the stock-based eligibility gate, since its supply is genuinely finite.
func test_haul_from_terrain_stays_eligible_when_full() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 9)
	var purpose_id := builder.define_need("Purpose", 0.01)
	builder.define_terrain("Floor", true, "flat")
	var trees_id := builder.define_terrain("Trees", true)
	var sawmill_id := builder.define_building(
		"Sawmill",
		purpose_id,
		40.0,
		20,
		0.0,
		0,
		[],
		1,
		true,
		"wood",
		100.0,
		"haulFromTerrain",
		"",
		"Trees"
	)
	var warehouse_id := builder.define_building(
		"Warehouse",
		purpose_id,
		40.0,
		20,
		0.0,
		0,
		[],
		1,
		true,
		"stone",
		100.0,
		"haulFromBuilding",
		"stone"
	)
	builder.add_building(sawmill_id, 4, 4)
	builder.add_building(warehouse_id, 4, 6)
	builder.add_pawn("Worker", 4, 5, {purpose_id: 50.0})
	var sim = builder.build()

	var sawmill_entity_id := _get_building_by_def_id(sim, sawmill_id)
	var warehouse_entity_id := _get_building_by_def_id(sim, warehouse_id)
	sim.entities.resources[sawmill_entity_id].current_amount = 95.0
	sim.entities.resources[warehouse_entity_id].current_amount = 95.0

	var pawn_id := sim.find_pawn_by_name("Worker")
	var candidates: Array = sim.ai_system._find_work_candidates(sim, pawn_id, purpose_id)

	assert_true(
		candidates.has(sawmill_entity_id),
		"A nearly-full haulFromTerrain building should still be a work candidate"
	)
	assert_false(
		candidates.has(warehouse_entity_id),
		"A nearly-full haulFromBuilding destination should NOT be a work candidate"
	)


# The real LumberMill has no resourceType at all now (the stock display was purely vestigial —
# nothing ever consumed it) — a haulFromTerrain building must work end-to-end with no
# ResourceComponent whatsoever, not just tolerate a full one.
func test_haul_from_terrain_without_resource_component() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 9)
	var purpose_id := builder.define_need("Purpose", 0.01)
	builder.define_terrain("Floor", true, "flat")
	var trees_id := builder.define_terrain("Trees", true)
	# No resource_type passed -> no ResourceComponent gets created for this building.
	var mill_id := builder.define_building(
		"Mill", purpose_id, 40.0, 20, 0.0, 0, [], 1, true, "", 100.0, "haulFromTerrain", "", "Trees"
	)
	builder.add_building(mill_id, 4, 4)
	builder.add_pawn("Woodcutter", 4, 5, {purpose_id: 50.0})
	var sim = builder.build()

	sim.paint_terrain(Vector2i(6, 4), trees_id)

	var mill_entity_id := _get_building_by_def_id(sim, mill_id)
	assert_true(
		sim.entities.resources.get(mill_entity_id) == null,
		"Setup should have no ResourceComponent for this building"
	)

	var pawn_id := sim.find_pawn_by_name("Woodcutter")
	var candidates: Array = sim.ai_system._find_work_candidates(sim, pawn_id, purpose_id)
	assert_true(
		candidates.has(mill_entity_id),
		"A haulFromTerrain building with no ResourceComponent should still be a work candidate"
	)

	sim.run_ticks(2200)  # PICK_UP duration is AISystem.HARVEST_DURATION_TICKS (750) + travel/buffer

	assert_gt(
		sim.get_need_value(pawn_id, purpose_id),
		50.0,
		"Purpose should increase after completing the harvest, even with nowhere to store the result"
	)


# The real LumberMill has no ResourceComponent to receive deliveries into (see the previous
# test), but pawns still carry a resource_type/amount in their own InventoryComponent between
# pickup and drop-off. Completing the delivery must always empty the pawn's hands, whether or
# not the destination could actually store what they were carrying — otherwise the pawn (and
# the carried-item icon in the UI, driven by this same field) stays stuck "carrying wood"
# forever, since nothing else ever clears it.
func test_haul_from_terrain_clears_inventory_without_storage() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 9)
	var purpose_id := builder.define_need("Purpose", 0.01)
	builder.define_terrain("Floor", true, "flat")
	var trees_id := builder.define_terrain("Trees", true)
	# No resource_type (storage) but a haul_source_resource_type (carry label) — matches the
	# real LumberMill's shape.
	var mill_id := builder.define_building(
		"Mill",
		purpose_id,
		40.0,
		20,
		0.0,
		0,
		[],
		1,
		true,
		"",
		100.0,
		"haulFromTerrain",
		"wood",
		"Trees"
	)
	builder.add_building(mill_id, 4, 4)
	builder.add_pawn("Woodcutter", 4, 5, {purpose_id: 50.0})
	var sim = builder.build()

	sim.paint_terrain(Vector2i(6, 4), trees_id)
	var pawn_id := sim.find_pawn_by_name("Woodcutter")

	sim.run_ticks(2200)  # long enough for pickup + delivery to fully complete (see sibling test)

	var inv: Components.InventoryComponent = sim.entities.inventory.get(pawn_id)
	assert_eq(
		inv.resource_type,
		"",
		"Pawn's inventory should be empty after completing delivery, even with nowhere to store the wood"
	)
	assert_eq(inv.amount, 0.0, "Pawn's carried amount should be zero after completing delivery")


# When a haulFromBuilding destination can't find any source building (all matching sources
# missing or depleted), the pawn must still end up with something queued — an empty queue
# leaves AISystem re-running the full building search and haul lookup every single tick.
func test_haul_from_building_no_source_falls_back() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	var purpose_id := builder.define_need("Purpose", 0.01)
	# No source building of the required resource type exists anywhere on the map.
	var dest_id := builder.define_building(
		"Tavern",
		purpose_id,
		40.0,
		20,
		0.0,
		0,
		[],
		1,
		true,
		"wood",
		100.0,
		"haulFromBuilding",
		"wood"
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
		"Sawmill",
		purpose_id,
		40.0,
		20,
		0.0,
		0,
		[],
		1,
		true,
		"wood",
		100.0,
		"haulFromTerrain",
		"",
		"Trees"
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
		"Market",
		purpose_id,
		40.0,
		20,
		0.0,
		0,
		[],
		1,
		true,
		"food",
		100.0,
		"haulFromBuilding",
		"food"
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
	sim.entities.resources[farm_entity_id].current_amount = 0.0  # equally eligible, and workable

	var pawn_id := sim.find_pawn_by_name("Worker")
	sim.entities.attachments[market_entity_id].need_attachments[purpose_id] = {pawn_id: 10}  # max attachment

	var action_comp = sim.entities.actions.get(pawn_id)

	sim.tick()

	assert_eq(action_comp.action_queue.size(), 1, "Pawn should have queued exactly one action")
	assert_eq(
		action_comp.action_queue[0].type,
		_Definitions.ActionType.WORK,
		"Queued action should be real work, not a wander"
	)
	assert_eq(
		action_comp.action_queue[0].target_entity,
		farm_entity_id,
		"Pawn should spill over to the Farm since Market can't actually be worked"
	)


# GameRoot hides a pawn's sprite while PICK_UP targets a building (they're inside it), but a
# PICK_UP harvesting from open terrain (e.g. chopping trees) has no building at all — the
# render snapshot must expose enough to tell the two apart, or terrain harvesting incorrectly
# hides the pawn the whole time they're chopping.
func test_snapshot_haul_from_terrain_pickup_has_no_building_target() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(9, 9)
	var purpose_id := builder.define_need("Purpose", 0.01)
	builder.define_terrain("Floor", true, "flat")
	var trees_id := builder.define_terrain("Trees", true)
	var mill_id := builder.define_building(
		"Sawmill",
		purpose_id,
		40.0,
		20,
		0.0,
		0,
		[],
		1,
		true,
		"",
		100.0,
		"haulFromTerrain",
		"",
		"Trees"
	)
	builder.add_building(mill_id, 4, 4)
	builder.add_pawn("Woodcutter", 4, 5, {purpose_id: 50.0})
	var sim = builder.build()
	sim.paint_terrain(Vector2i(6, 4), trees_id)

	var pawn_id := sim.find_pawn_by_name("Woodcutter")
	assert_true(
		_run_until_action_type(sim, pawn_id, _Definitions.ActionType.PICK_UP, 500),
		"Setup should reach the harvesting phase within 500 ticks"
	)

	var snapshot: Dictionary = sim.create_render_snapshot()
	var pawn_snap: Dictionary = _find_pawn_snapshot(snapshot, pawn_id)
	assert_eq(
		pawn_snap.get("current_action_type"),
		_Definitions.ActionType.PICK_UP,
		"Should be captured mid-harvest"
	)
	assert_false(
		pawn_snap.get("has_building_target", true),
		"Harvesting from terrain should have no building target"
	)


func test_snapshot_haul_from_building_pickup_has_building_target() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	var purpose_id := builder.define_need("Purpose", 0.01)
	var source_id := builder.define_building(
		"LumberMill", -1, 50.0, 20, 0.0, 0, [], 1, false, "wood", 100.0
	)
	var dest_id := builder.define_building(
		"Tavern",
		purpose_id,
		40.0,
		20,
		0.0,
		0,
		[],
		1,
		true,
		"wood",
		100.0,
		"haulFromBuilding",
		"wood"
	)
	builder.add_building(source_id, 1, 4)
	builder.add_building(dest_id, 6, 4)
	builder.add_pawn("Hauler", 4, 4, {purpose_id: 50.0})
	var sim = builder.build()

	var dest_entity_id := _get_building_by_def_id(sim, dest_id)
	sim.entities.resources[dest_entity_id].current_amount = 10.0
	var pawn_id := sim.find_pawn_by_name("Hauler")

	assert_true(
		_run_until_action_type(sim, pawn_id, _Definitions.ActionType.PICK_UP, 500),
		"Setup should reach the pickup phase within 500 ticks"
	)

	var snapshot: Dictionary = sim.create_render_snapshot()
	var pawn_snap: Dictionary = _find_pawn_snapshot(snapshot, pawn_id)
	assert_eq(
		pawn_snap.get("current_action_type"),
		_Definitions.ActionType.PICK_UP,
		"Should be captured mid-pickup"
	)
	assert_true(
		pawn_snap.get("has_building_target", false),
		"Hauling from a building should have a building target"
	)


func _run_until_action_type(sim, pawn_id: int, action_type: int, max_ticks: int) -> bool:
	for i in max_ticks:
		sim.run_ticks(1)
		var action_comp = sim.entities.actions.get(pawn_id)
		if (
			action_comp != null
			and action_comp.current_action != null
			and action_comp.current_action.type == action_type
		):
			return true
	return false


func _find_pawn_snapshot(snapshot: Dictionary, pawn_id: int) -> Dictionary:
	for pawn_snap in snapshot.get("pawns", []):
		if int(pawn_snap.get("id", -1)) == pawn_id:
			return pawn_snap
	return {}
