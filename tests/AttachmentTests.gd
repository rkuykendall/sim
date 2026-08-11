extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")


func run() -> void:
	print("  [AttachmentTests]")
	run_test("Decay_ReducesStrongAttachment_NotJustWeakOnes", test_decay_reduces_strong_attachment)
	run_test("Decay_RemovesAttachment_OnceItReachesZero", test_decay_removes_attachment_at_zero)
	run_test(
		"DestroyEntity_RemovesPawnsAttachmentEntries_FromEveryBuilding",
		test_destroy_entity_cleans_up_attachments
	)


# A "regular" (strength above the old decay threshold of 5) must still decay — otherwise
# attachment scores saturate permanently for any pawn who visits a building more than a
# handful of times, and the whole point of attachment (differentiating who's tied down) is
# lost. If it's genuinely still a regular, repeat visits (increment()) will bump it back up.
func test_decay_reduces_strong_attachment() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(5, 5)
	# Zero decay so the pawn's AI never actually needs to revisit the Tavern during the run —
	# a real visit would re-increment attachment and mask the pure decay signal being tested.
	var need_id := builder.define_need("Social", 0.0)
	var building_id := builder.define_building("Tavern", need_id)
	builder.add_building(building_id, 2, 2)
	builder.add_pawn("Regular", 2, 3, {})
	var sim := builder.build()

	var tavern_id := _get_building_by_def_id(sim, building_id)
	var pawn_id := sim.find_pawn_by_name("Regular")
	var ac: Components.AttachmentComponent = sim.entities.attachments.get(tavern_id)
	for i in 10:
		ac.increment(need_id, pawn_id)
	assert_eq(ac.get_strength(need_id, pawn_id), 10, "Setup should reach max strength")

	sim.run_ticks(Simulation.ATTACHMENT_DECAY_INTERVAL + 1)

	assert_eq(
		ac.get_strength(need_id, pawn_id),
		9,
		"A single decay interval should reduce even a maxed-out attachment by 1"
	)


func test_decay_removes_attachment_at_zero() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(5, 5)
	var need_id := builder.define_need("Social", 0.0)
	var building_id := builder.define_building("Tavern", need_id)
	builder.add_building(building_id, 2, 2)
	builder.add_pawn("OneTimer", 2, 3, {})
	var sim := builder.build()

	var tavern_id := _get_building_by_def_id(sim, building_id)
	var pawn_id := sim.find_pawn_by_name("OneTimer")
	var ac: Components.AttachmentComponent = sim.entities.attachments.get(tavern_id)
	ac.increment(need_id, pawn_id)
	assert_eq(
		ac.get_strength(need_id, pawn_id), 1, "Setup should have a single point of attachment"
	)

	sim.run_ticks(Simulation.ATTACHMENT_DECAY_INTERVAL + 1)

	assert_eq(
		ac.get_strength(need_id, pawn_id),
		0,
		"A single point of attachment should fully decay away after one interval"
	)
	var remaining_after_decay: Dictionary = ac.need_attachments.get(need_id, {})
	assert_false(
		remaining_after_decay.has(pawn_id),
		"Decayed-to-zero entries should be erased, not left at 0"
	)


func test_destroy_entity_cleans_up_attachments() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(5, 5)
	var need_id := builder.define_need("Social")
	var building_id := builder.define_building("Tavern", need_id)
	builder.add_building(building_id, 2, 2)
	builder.add_pawn("Leaving", 2, 3, {})
	var sim := builder.build()

	var tavern_id := _get_building_by_def_id(sim, building_id)
	var pawn_id := sim.find_pawn_by_name("Leaving")
	var ac: Components.AttachmentComponent = sim.entities.attachments.get(tavern_id)
	ac.increment(need_id, pawn_id)
	assert_eq(ac.get_strength(need_id, pawn_id), 1, "Setup should have an attachment entry")

	sim.destroy_entity(pawn_id)

	var remaining: Dictionary = ac.need_attachments.get(need_id, {})
	assert_false(
		remaining.has(pawn_id),
		"Destroying a pawn should remove its attachment entries from buildings it visited"
	)
