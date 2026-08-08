extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")


func run() -> void:
	print("  [AISystemTests]")
	run_test("DiversityMap_StableAcrossRepeatedWanderDecisions", test_diversity_map_stable_across_wandering)
	run_test("DiversityMap_RebuildsAfterPaintTerrain", test_diversity_map_rebuilds_after_paint)
	run_test("DiversityMap_RebuildsAfterDeleteAtTile", test_diversity_map_rebuilds_after_delete)
	run_test("DiversityMap_ReusesSameArrayWhenWorldUnchanged", test_diversity_map_cached_reference_reused)
	run_test("TrappedPawn_GetsFallbackAction_InsteadOfEmptyQueue", test_trapped_pawn_gets_fallback_action)
	run_test("TrappedPawn_DoesNotRedecideEveryTick", test_trapped_pawn_does_not_redecide_every_tick)
	run_test("ConsumerAttachment_DoesNotBiasWorkScoring", test_consumer_attachment_does_not_bias_work_scoring)


# A lone pawn with no needs wanders continuously; the cached diversity map must stay
# bit-for-bit identical across many wander cycles when the world itself hasn't changed.
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


# A pawn fully boxed in by walls (no reachable buildings, no walkable wander candidate
# anywhere nearby or in the random global sample) must still get queued *something* —
# a fallback idle/wait — rather than being left with an empty queue.
func test_trapped_pawn_gets_fallback_action() -> void:
	var sim := _build_trapped_pawn_scenario()
	var pawn_id := sim.find_pawn_by_name("Trapped")
	var action_comp = sim.entities.actions.get(pawn_id)

	sim.tick()  # AISystem's first decision attempt for this pawn

	assert_true(
		action_comp.current_action != null or not action_comp.action_queue.is_empty(),
		"A pawn with no valid action should get a fallback action, not an empty queue"
	)


# An empty queue never trips AISystem's "already has something to do" skip, so without a
# persistent fallback it would re-run the full decision every tick. Verified via
# action_start_tick rather than wall-clock timing, since a synthetic scenario is too cheap
# for timing to be a reliable, non-flaky signal.
func test_trapped_pawn_does_not_redecide_every_tick() -> void:
	var sim := _build_trapped_pawn_scenario()
	var pawn_id := sim.find_pawn_by_name("Trapped")
	var action_comp = sim.entities.actions.get(pawn_id)

	sim.tick()  # AISystem queues the fallback idle (current_action is still null this tick —
	            # ActionSystem, which pops the queue into current_action, runs before AISystem)
	sim.tick()  # ActionSystem pops it into current_action and sets action_start_tick

	assert_not_null(action_comp.current_action, "Fallback idle should be the active action by now")
	var action_ref = action_comp.current_action
	var start_tick_ref: int = action_comp.action_start_tick

	sim.run_ticks(5)  # well within the fallback's retry-delay duration

	assert_eq(
		action_comp.action_start_tick, start_tick_ref,
		"A fresh decision was made on a later tick — the fallback should persist instead of retrying every tick"
	)
	assert_true(
		is_same(action_comp.current_action, action_ref),
		"current_action was replaced by a new decision instead of the fallback persisting"
	)


# Attachment is tracked per need. A pawn with max attachment to a building for Social (built
# purely by visiting it as a consumer) should not have that carry over to Purpose (work)
# scoring for the same building — a nearer, equally-eligible building should still win.
func test_consumer_attachment_does_not_bias_work_scoring() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(20, 20)
	var social_id := builder.define_need("Social", 0.01)
	var purpose_id := builder.define_need("Purpose", 0.01)

	var far_id := builder.define_building(
		"FarTavern", social_id, 50.0, 20, 0.0, 0, [], 1, true, "stone", 100.0, "direct"
	)
	var near_id := builder.define_building(
		"NearMine", -1, 50.0, 20, 0.0, 0, [], 1, true, "stone", 100.0, "direct"
	)
	builder.add_building(far_id, 18, 10)
	builder.add_building(near_id, 8, 10)
	builder.add_pawn("Worker", 10, 10, {social_id: 100.0, purpose_id: 50.0})
	var sim := builder.build()

	var far_entity_id := _get_building_by_def_id(sim, far_id)
	var near_entity_id := _get_building_by_def_id(sim, near_id)
	sim.entities.resources[far_entity_id].current_amount = 0.0
	sim.entities.resources[near_entity_id].current_amount = 0.0

	var pawn_id := sim.find_pawn_by_name("Worker")
	# Max attachment to the far building, but only for Social — never worked or delivered there.
	sim.entities.attachments[far_entity_id].need_attachments[social_id] = {pawn_id: 10}

	var candidates := sim.ai_system._find_work_candidates(sim, pawn_id, purpose_id)

	assert_eq(
		candidates[0], near_entity_id,
		"Nearer building should win work scoring; Social attachment to the farther one must not leak into Purpose scoring"
	)


func _build_trapped_pawn_scenario() -> Simulation:
	var builder := _Builder.new()
	builder.with_world_bounds(4, 4)
	var wall_id := builder.define_terrain("Wall", false)
	builder.add_pawn("Trapped", 2, 2, {})
	var sim := builder.build()

	# Wall off every tile except the pawn's own tile — no wander candidate, random or nearby,
	# can ever be walkable.
	for x in 5:
		for y in 5:
			if Vector2i(x, y) != Vector2i(2, 2):
				sim.paint_terrain(Vector2i(x, y), wall_id)

	return sim
