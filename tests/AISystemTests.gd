extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")


func run() -> void:
	print("  [AISystemTests]")
	run_test(
		"DiversityMap_StableAcrossRepeatedWanderDecisions",
		test_diversity_map_stable_across_wandering
	)
	run_test("DiversityMap_RebuildsAfterPaintTerrain", test_diversity_map_rebuilds_after_paint)
	run_test("DiversityMap_RebuildsAfterDeleteAtTile", test_diversity_map_rebuilds_after_delete)
	run_test(
		"DiversityMap_ReusesSameArrayWhenWorldUnchanged", test_diversity_map_cached_reference_reused
	)
	run_test(
		"TrappedPawn_GetsFallbackAction_InsteadOfEmptyQueue", test_trapped_pawn_gets_fallback_action
	)
	run_test("TrappedPawn_DoesNotRedecideEveryTick", test_trapped_pawn_does_not_redecide_every_tick)
	run_test(
		"ConsumerAttachment_DoesNotBiasWorkScoring",
		test_consumer_attachment_does_not_bias_work_scoring
	)
	run_test(
		"WorkScoring_PenalizesTotalCrowd_NotJustTheSingleMostAttachedPawn",
		test_work_scoring_sums_other_pawns_attachment
	)
	run_test(
		"WanderHomeVisit_HappensOverTime_WithoutTouchingEnergyOrAttachment",
		test_wander_home_visit_occurs_without_side_effects
	)
	run_test("WanderHomeVisit_NeverHappens_ForVisitors", test_wander_home_visit_excludes_visitors)
	run_test(
		"WanderExpression_ShowsUnsatisfiableNeed_RegardlessOfActionability",
		test_wander_expression_shows_unsatisfiable_need
	)
	run_test(
		"WanderExpression_ShowsBestNeed_WhenMoodIsPositive",
		test_wander_expression_shows_best_need_when_happy
	)


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
	assert_true(
		is_same(first, second),
		"Repeated reads with no world change should reuse the same Array instance"
	)


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
		action_comp.action_start_tick,
		start_tick_ref,
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
		candidates[0],
		near_entity_id,
		"Nearer building should win work scoring; Social attachment to the farther one must not leak into Purpose scoring"
	)


# A building with several moderately-attached workers should look more crowded than one with
# a single more-attached worker — total headcount, not the single strongest tie, is what
# should discourage a new pawn from piling on. This is what let a single high-demand building
# (e.g. a Tavern under constant consumer draw) snowball into recruiting far more workers than
# capacity-equal alternatives, since only the single highest attachment ever counted against it.
func test_work_scoring_sums_other_pawns_attachment() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(20, 20)
	var purpose_id := builder.define_need("Purpose", 0.01)

	var crowded_id := builder.define_building(
		"Crowded", -1, 50.0, 20, 0.0, 0, [], 1, true, "stone", 100.0, "direct"
	)
	var quiet_id := builder.define_building(
		"Quiet", -1, 50.0, 20, 0.0, 0, [], 1, true, "stone", 100.0, "direct"
	)
	builder.add_building(crowded_id, 10, 8)
	builder.add_building(quiet_id, 10, 12)
	builder.add_pawn("NewWorker", 10, 10, {purpose_id: 50.0})
	var sim := builder.build()

	var crowded_entity_id := _get_building_by_def_id(sim, crowded_id)
	var quiet_entity_id := _get_building_by_def_id(sim, quiet_id)
	sim.entities.resources[crowded_entity_id].current_amount = 0.0
	sim.entities.resources[quiet_entity_id].current_amount = 0.0

	# Crowded: three other pawns each at a modest strength 4 (sum 12, max 4).
	# Quiet: one other pawn at a higher strength 6 (sum 6, max 6).
	# Under a max-only penalty, Quiet would look MORE claimed (6 > 4) and lose. Summing
	# correctly identifies Crowded as the more contested building instead.
	sim.entities.attachments[crowded_entity_id].need_attachments[purpose_id] = {
		8001: 4, 8002: 4, 8003: 4
	}
	sim.entities.attachments[quiet_entity_id].need_attachments[purpose_id] = {9001: 6}

	var pawn_id := sim.find_pawn_by_name("NewWorker")
	var candidates := sim.ai_system._find_work_candidates(sim, pawn_id, purpose_id)

	assert_eq(
		candidates[0],
		quiet_entity_id,
		"A new worker should prefer the building with fewer total attached workers, not just a lower single strongest attachment"
	)


# A colonist with no urgent needs (so _wander_randomly always drives their decisions) should
# eventually take AISystem's occasional cosmetic home detour — this is what actually lets a
# house's skin_override (see Simulation.set_building_skin_override) reach colonists during
# normal daytime play, since real Energy-driven visits only happen once Energy is genuinely
# low. Must have zero side effects on the real Energy need or attachment (see
# ActionSystem._execute_use_building's satisfies_need_id gating).
func test_wander_home_visit_occurs_without_side_effects() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(20, 20)
	var energy_id := builder.define_need("Energy", 0.0)  # zero decay: never becomes urgent
	var home_id := builder.define_building(
		"TestHome",
		energy_id,
		50.0,
		20,
		20.0,
		800,
		[],
		1,
		false,
		"",
		100.0,
		"direct",
		"",
		"",
		true,
		1,
		true
	)
	builder.add_building(home_id, 10, 10)
	builder.add_pawn("Idle", 10, 8, {energy_id: 100.0})
	var sim := builder.build()

	var home_building_id := _get_building_by_def_id(sim, home_id)
	var pawn_id := sim.find_pawn_by_name("Idle")
	var action_comp = sim.entities.actions.get(pawn_id)

	var visited := false
	# Deterministic given TestSimulationBuilder's fixed seed — enough ticks for a hundred-plus
	# wander decisions at a 6% chance each. Budget is large because SIT_AND_EMOTE_CHANCE sit
	# durations (up to 2500 ticks) make each wander cycle much longer on average than a plain idle.
	for _i in 80000:
		sim.tick()
		if (
			action_comp.current_action != null
			and action_comp.current_action.type == Definitions.ActionType.USE_BUILDING
			and action_comp.current_action.target_entity == home_building_id
		):
			visited = true
			break

	assert_true(visited, "Colonist should eventually take a cosmetic home visit while idle")
	assert_eq(
		sim.get_need_value(pawn_id, energy_id),
		100.0,
		"A cosmetic visit must not touch the real Energy need"
	)

	var ac = sim.entities.attachments.get(home_building_id)
	assert_eq(
		ac.get_strength(energy_id, pawn_id),
		0,
		"A cosmetic visit must not build real Energy attachment"
	)


# Visitors (see Definitions.PawnMembership) only ever wander — a purely cosmetic home detour
# would be exactly the kind of building-seeking behavior they're specifically excluded from.
func test_wander_home_visit_excludes_visitors() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(20, 20)
	var energy_id := builder.define_need("Energy", 0.0)
	var home_id := builder.define_building(
		"TestHome",
		energy_id,
		50.0,
		20,
		20.0,
		800,
		[],
		1,
		false,
		"",
		100.0,
		"direct",
		"",
		"",
		true,
		1,
		true
	)
	builder.add_building(home_id, 10, 10)
	var sim := builder.build()

	var home_building_id := _get_building_by_def_id(sim, home_id)
	var visitor_id := sim.spawn_visitor_pawn("special_4_v2", {}, "Visitor")
	var action_comp = sim.entities.actions.get(visitor_id)

	for _i in 6000:
		sim.tick()
		assert_false(
			(
				action_comp.current_action != null
				and action_comp.current_action.type == Definitions.ActionType.USE_BUILDING
				and action_comp.current_action.target_entity == home_building_id
			),
			"A visitor must never take the cosmetic home-visit detour"
		)


# Regression test for the real bug this fixes: a need with no building anywhere to satisfy it
# (e.g. Hygiene with no Well built) used to never surface a complaint, since _decide_next_action
# always finds something else actionable to go do first, and the old buff-driven expression only
# ever reflected needs the pawn was actively acting on. No "Energy" need is registered at all, so
# _try_queue_home_visit's random detour can never trigger. The sit-and-emote itself is now gated
# behind SIT_AND_EMOTE_CHANCE (most wanders are just a brief plain idle) rather than guaranteed
# on every wander, so this retries until that branch is hit — deterministic given the fixed seed.
func test_wander_expression_shows_unsatisfiable_need() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(20, 20)
	var hygiene_id := builder.define_need("Hygiene", 0.0)  # zero decay — value set directly below
	builder.add_pawn("Grimy", 10, 10, {hygiene_id: 0.0})
	var sim := builder.build()

	var pawn_id := sim.find_pawn_by_name("Grimy")
	sim.entities.moods[pawn_id].mood = -50.0  # deterministic "unhappy" — _wander_randomly is called directly below, without a sim.tick() that would let MoodSystem recompute this from needs
	var action_comp = sim.entities.actions.get(pawn_id)

	var sit_action = _find_sit_action_by_wandering(sim, pawn_id, action_comp)

	assert_not_null(sit_action, "Should eventually trigger the sit-and-observe emote")
	assert_eq(sit_action.animation, Definitions.AnimationType.SIT, "Should play the sit animation")
	assert_true(
		sit_action.has_expression,
		"Should show an expression even though nothing can satisfy Hygiene"
	)
	assert_eq(
		sit_action.expression, Definitions.ExpressionType.COMPLAINT, "Unhappy pawn should complain"
	)
	assert_eq(
		sit_action.expression_icon_def_id,
		hygiene_id,
		"Complaint should point at the pawn's actual worst need"
	)


func test_wander_expression_shows_best_need_when_happy() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(20, 20)
	var hygiene_id := builder.define_need("Hygiene", 0.0)
	var purpose_id := builder.define_need("Purpose", 0.0)
	builder.add_pawn("Content", 10, 10, {hygiene_id: 40.0, purpose_id: 90.0})
	var sim := builder.build()

	var pawn_id := sim.find_pawn_by_name("Content")
	sim.entities.moods[pawn_id].mood = 50.0  # deterministic "happy"
	var action_comp = sim.entities.actions.get(pawn_id)

	var sit_action = _find_sit_action_by_wandering(sim, pawn_id, action_comp)

	assert_not_null(sit_action, "Should eventually trigger the sit-and-observe emote")
	assert_eq(sit_action.animation, Definitions.AnimationType.SIT, "Should play the sit animation")
	assert_true(sit_action.has_expression, "A happy pawn should still show an expression")
	assert_eq(
		sit_action.expression,
		Definitions.ExpressionType.HAPPY,
		"Positive mood should show a happy expression"
	)
	assert_eq(
		sit_action.expression_icon_def_id,
		purpose_id,
		"Happy expression should point at the pawn's actual best need"
	)


# Repeatedly calls _wander_randomly (clearing the queue each time) until it produces a sit
# action, per AISystem.SIT_AND_EMOTE_CHANCE. Returns the sit ActionDef, or null if it never
# triggered within the attempt budget.
func _find_sit_action_by_wandering(
	sim: Simulation, pawn_id: int, action_comp: Components.ActionComponent
):
	for _i in 200:
		action_comp.action_queue.clear()
		sim.ai_system._wander_randomly(sim, pawn_id, action_comp)
		if (
			action_comp.action_queue.size() == 2
			and action_comp.action_queue[1].animation == Definitions.AnimationType.SIT
		):
			return action_comp.action_queue[1]
	return null


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
