extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")


func run() -> void:
	print("  [SimulationQueriesTests]")
	run_test("FindPawnByName_ReturnsMatchingId", test_find_pawn_by_name)
	run_test("FindPawnByName_ReturnsNegativeOneWhenNotFound", test_find_pawn_by_name_missing)
	run_test("GetNeedValue_ReturnsZeroForUnknownPawnOrNeed", test_get_need_value_unknown)
	run_test("GetMood_ReturnsZeroForUnknownPawn", test_get_mood_unknown)
	run_test("RunTicks_AdvancesExactlyThatManyTicks", test_run_ticks_exact)
	run_test("ScoreMapDiversity_MatchesCachedDiversityMap", test_score_map_diversity_matches_cache)


func test_find_pawn_by_name() -> void:
	var builder := _Builder.new()
	builder.add_pawn("Alice", 1, 1, {})
	builder.add_pawn("Bob", 2, 2, {})
	var sim := builder.build()

	var alice_id := sim.find_pawn_by_name("Alice")
	var bob_id := sim.find_pawn_by_name("Bob")

	assert_not_eq(alice_id, -1, "Should find Alice")
	assert_not_eq(bob_id, -1, "Should find Bob")
	assert_not_eq(alice_id, bob_id, "Alice and Bob should be different entities")
	assert_eq(
		sim.entities.pawns.get(alice_id).name, "Alice", "Resolved id should map back to Alice"
	)


func test_find_pawn_by_name_missing() -> void:
	var builder := _Builder.new()
	builder.add_pawn("Alice", 1, 1, {})
	var sim := builder.build()

	assert_eq(sim.find_pawn_by_name("Nobody"), -1, "Unknown name should return -1")


func test_get_need_value_unknown() -> void:
	var builder := _Builder.new()
	var hunger_id := builder.define_need("Hunger")
	builder.add_pawn("Alice", 1, 1, {hunger_id: 42.0})
	var sim := builder.build()

	var alice_id := sim.find_pawn_by_name("Alice")
	assert_approx(
		sim.get_need_value(alice_id, hunger_id), 42.0, 0.01, "Should return the actual need value"
	)
	assert_eq(sim.get_need_value(alice_id, 999999), 0.0, "Unknown need id should return 0.0")
	assert_eq(sim.get_need_value(999999, hunger_id), 0.0, "Unknown pawn id should return 0.0")


func test_get_mood_unknown() -> void:
	var builder := _Builder.new()
	builder.add_pawn("Alice", 1, 1, {})
	var sim := builder.build()

	assert_eq(sim.get_mood(999999), 0.0, "Unknown pawn id should return 0.0")


func test_run_ticks_exact() -> void:
	var builder := _Builder.new()
	var sim := builder.build()
	var start_tick := sim.time.tick

	sim.run_ticks(37)

	assert_eq(
		sim.time.tick, start_tick + 37, "run_ticks should advance by exactly the requested count"
	)


# score_map_diversity() delegates to AISystem's cached diversity map — this guards against
# that delegation drifting out of sync (e.g. someone reintroducing a separate computation).
func test_score_map_diversity_matches_cache() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	var wall_id := builder.define_terrain("Wall", false)
	var sim := builder.build()

	sim.paint_terrain(Vector2i(3, 3), wall_id)

	var expected_score := 0
	for value: int in sim.ai_system._get_diversity_map(sim.world):
		expected_score += value
	expected_score += sim.entities.all_buildings().size()

	assert_eq(
		sim.score_map_diversity(),
		expected_score,
		"score_map_diversity should match the cached diversity map's sum"
	)
