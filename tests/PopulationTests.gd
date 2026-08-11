extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")


func run() -> void:
	print("  [PopulationTests]")
	run_test(
		"Emigration_RemovesLeastInterestingPawn_WhenOverCapacity",
		test_emigration_removes_least_interesting_pawn
	)
	run_test("Emigration_DoesNotTrigger_AtOrBelowCapacity", test_no_emigration_within_capacity)
	run_test("Emigration_DoesNotTrigger_WhenNoHomesExist", test_no_emigration_without_homes)
	run_test("Emigration_DoesNotRetargetPawn_MidWalk", test_emigration_does_not_retarget_mid_walk)
	run_test(
		"RealContent_HomeCapacitiesMatchExpectedHalvedValues", test_real_content_home_capacities
	)
	run_test(
		"RealContent_MaxPawnsMatchesActualHomeLayout", test_real_content_max_pawns_for_known_layout
	)


# One capacity-1 home, two pawns already present (over capacity). The unhappier, unattached
# pawn should be the one who leaves, not the happier one — even though both start equally
# unattached, mood alone should be enough to pick the sadder pawn deterministically.
func test_emigration_removes_least_interesting_pawn() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TinyHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 1, true
	)
	builder.add_building(home_id, 5, 5)
	var mood_need_id := builder.define_need("Contentment", 0.0, 15.0, 35.0, -50.0, -20.0)
	builder.add_pawn("Happy", 5, 4, {mood_need_id: 100.0})
	builder.add_pawn("Sad", 5, 6, {mood_need_id: 0.0})
	var sim := builder.build()

	assert_eq(sim.get_max_pawns(), 1, "Capacity-1 home should cap population at 1")
	assert_eq(sim.entities.all_pawns().size(), 2, "Test setup should start over capacity")

	# Long enough for the population check (every Simulation.PAWN_SPAWN_INTERVAL ticks) to
	# fire, the chosen pawn to walk to the map edge, and the pending removal to be processed.
	sim.run_ticks(Simulation.PAWN_SPAWN_INTERVAL + 300)

	assert_eq(sim.entities.all_pawns().size(), 1, "Population should have dropped to capacity")
	assert_not_eq(sim.find_pawn_by_name("Happy"), -1, "Happier pawn should remain")
	assert_eq(sim.find_pawn_by_name("Sad"), -1, "Unhappier pawn should have emigrated")


# Population already at capacity — nothing should be removed.
func test_no_emigration_within_capacity() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TinyHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 2, true
	)
	builder.add_building(home_id, 5, 5)
	builder.add_pawn("Alice", 5, 4, {})
	builder.add_pawn("Bob", 5, 6, {})
	var sim := builder.build()

	assert_eq(sim.get_max_pawns(), 2, "Capacity-2 home should cap population at 2")

	sim.run_ticks(Simulation.PAWN_SPAWN_INTERVAL + 300)

	assert_eq(sim.entities.all_pawns().size(), 2, "Population at capacity should be left alone")


# get_max_pawns() floors at 1 even with zero homes (so a fresh town can still grow to one
# pawn) — but that floor must NOT make emigration treat "no homes built yet" as "1 over
# capacity". A town that never built housing shouldn't have pawns evicted for it.
func test_no_emigration_without_homes() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(10, 10)
	builder.add_pawn("Alice", 4, 5, {})
	builder.add_pawn("Bob", 6, 5, {})
	var sim := builder.build()

	assert_eq(sim.get_max_pawns(), 1, "Floor should still report 1 with no homes")

	sim.run_ticks(Simulation.PAWN_SPAWN_INTERVAL + 300)

	assert_eq(
		sim.entities.all_pawns().size(),
		2,
		"Pawns should not be evicted when no homes have ever been built"
	)


# The map-edge walk (~40 tiles here, ~400 ticks) takes longer than the 300-tick interval
# between population checks. A second check firing mid-walk must not retarget the same pawn
# to a new random edge — that would reset their progress and, since it happens every
# interval, mean they never actually arrive anywhere.
func test_emigration_does_not_retarget_mid_walk() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(80, 80)
	var home_id := builder.define_building(
		"TinyHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 1, true
	)
	builder.add_building(home_id, 40, 40)
	builder.add_pawn("Alice", 40, 39, {})
	builder.add_pawn("Bob", 40, 41, {})
	var sim := builder.build()

	sim.run_ticks(Simulation.PAWN_SPAWN_INTERVAL + 10)
	assert_eq(sim.entities.all_pawns().size(), 2, "Nobody should have reached the edge yet")

	var emigrant_id := _find_emigrating_pawn(sim)
	assert_not_eq(emigrant_id, -1, "One pawn should be mid-emigration by now")
	var target_after_first_check: Vector2i = (
		sim.entities.actions.get(emigrant_id).current_action.target_coord
	)

	# A second population check fires here (another PAWN_SPAWN_INTERVAL), while the same pawn
	# is still walking — the ~400-tick walk isn't done yet.
	sim.run_ticks(Simulation.PAWN_SPAWN_INTERVAL)

	assert_eq(sim.entities.all_pawns().size(), 2, "Pawn should still be en route, not yet removed")
	var action_comp: Components.ActionComponent = sim.entities.actions.get(emigrant_id)
	assert_not_null(
		action_comp.current_action, "Emigrating pawn should still have an active action"
	)
	assert_eq(
		action_comp.current_action.target_coord,
		target_after_first_check,
		"A second population check should not retarget a pawn already emigrating"
	)


func _find_emigrating_pawn(sim: Simulation) -> int:
	for pawn_id: int in sim.entities.all_pawns():
		var action_comp: Components.ActionComponent = sim.entities.actions.get(pawn_id)
		if (
			action_comp != null
			and action_comp.current_action != null
			and action_comp.current_action.display_name == "Leaving town"
		):
			return pawn_id
	return -1


# Everything above exercises the emigration MECHANISM against synthetic content — none of it
# would catch buildings.json itself getting reverted or fat-fingered. These two load the real
# production content directly.
func test_real_content_home_capacities() -> void:
	var content := ContentLoader.load_all()
	var expected: Dictionary[String, int] = {
		"Home1": 1, "Home2": 1, "Home3": 1, "Home4": 2, "Home5": 4
	}
	for home_name: String in expected:
		var building_id: int = content.get_building_id(home_name)
		assert_not_eq(building_id, -1, "%s should exist in production content" % home_name)
		var bdef: Dictionary = content.buildings.get(building_id, {})
		assert_true(
			DefUtils.get_bool(bdef, "isHome", false), "%s should be marked isHome" % home_name
		)
		assert_eq(
			DefUtils.get_int(bdef, "capacity", -1),
			expected[home_name],
			"%s capacity should be %d" % [home_name, expected[home_name]]
		)


# Mirrors an actual house layout (1x Home1, 2x Home2, 2x Home3, 1x Home4, 1x Home5) against
# real content and checks the full get_max_pawns() computation, not just individual fields.
func test_real_content_max_pawns_for_known_layout() -> void:
	var content := ContentLoader.load_all()
	var sim := Simulation.new(content)

	var layout: Array[String] = ["Home1", "Home2", "Home2", "Home3", "Home3", "Home4", "Home5"]
	for i in layout.size():
		var building_id: int = content.get_building_id(layout[i])
		sim.create_building(building_id, Vector2i(i * 3, 0))

	assert_eq(sim.get_max_pawns(), 11, "1+1+1+1+1+2+4 across this layout should total 11")
