extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")


func run() -> void:
	print("  [AISystemTests]")
	run_test("DiversityMap_StableAcrossRepeatedWanderDecisions", test_diversity_map_stable_across_wandering)
	run_test("DiversityMap_RebuildsAfterPaintTerrain", test_diversity_map_rebuilds_after_paint)
	run_test("DiversityMap_RebuildsAfterDeleteAtTile", test_diversity_map_rebuilds_after_delete)
	run_test("DiversityMap_ReusesSameArrayWhenWorldUnchanged", test_diversity_map_cached_reference_reused)


# A lone pawn with no needs wanders continuously. The per-decision "cap nearby diversity"
# logic used to mutate the shared cached array in place — this guards against that bug
# resurfacing: the cache must be bit-for-bit identical before and after many wander cycles
# that occur with no actual terrain change in between.
func test_diversity_map_stable_across_wandering() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	builder.add_pawn("Wanderer", 4, 4, {})
	var sim = builder.build()

	var before: Array = sim.ai_system._get_diversity_map(sim.world).duplicate()

	# Long enough for a lone idle pawn to run through several wander/idle cycles.
	sim.run_ticks(400)

	var after: Array = sim.ai_system._get_diversity_map(sim.world)
	assert_eq(after, before, "Cached diversity map must not be mutated by wander decisions")


# Painting a tile changes its terrain hash, which should invalidate the cache so the next
# read reflects the change rather than serving stale scores.
func test_diversity_map_rebuilds_after_paint() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	var wall_id := builder.define_terrain("Wall", false)
	var sim = builder.build()

	var before: Array = sim.ai_system._get_diversity_map(sim.world).duplicate()

	sim.paint_terrain(Vector2i(4, 4), wall_id)

	var after: Array = sim.ai_system._get_diversity_map(sim.world)
	var fresh: Array = sim.ai_system._compute_diversity_map(sim.world)
	assert_not_eq(after, before, "Diversity map should change after painting a differing tile")
	assert_eq(after, fresh, "Cached map should match a fresh computation after invalidation")


# delete_at_tile is a second terrain-mutation entry point (separate from paint_terrain) and
# must invalidate the cache too.
func test_diversity_map_rebuilds_after_delete() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	# spriteKey "flat" is what delete_at_tile() resets a cleared tile back to
	# (see Simulation._get_flat_terrain_id()) — needed for delete to actually change anything.
	builder.define_terrain("Flat", true, "flat")
	var floor_id := builder.define_terrain("Floor", true)
	var sim = builder.build()

	sim.paint_terrain(Vector2i(4, 4), floor_id)
	var before: Array = sim.ai_system._get_diversity_map(sim.world).duplicate()

	sim.delete_at_tile(Vector2i(4, 4))

	var after: Array = sim.ai_system._get_diversity_map(sim.world)
	var fresh: Array = sim.ai_system._compute_diversity_map(sim.world)
	assert_not_eq(after, before, "Diversity map should change after deleting a painted tile")
	assert_eq(after, fresh, "Cached map should match a fresh computation after invalidation")


# When nothing about the world has changed, repeated reads should return the exact same
# Array object rather than recomputing — this is the whole point of the cache.
func test_diversity_map_cached_reference_reused() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(8, 8)
	var sim = builder.build()

	var first: Array = sim.ai_system._get_diversity_map(sim.world)
	var second: Array = sim.ai_system._get_diversity_map(sim.world)
	assert_true(first == second, "Repeated reads with no world change should be equal")
	assert_true(is_same(first, second), "Repeated reads with no world change should reuse the same Array instance")
