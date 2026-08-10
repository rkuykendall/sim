extends "res://tests/SimTestCase.gd"

const _Builder = preload("res://tests/TestSimulationBuilder.gd")

const EXPECTED_TRACK_FILES: Array[String] = [
	"res://music/tracks/cuddle_clouds.ogg",
	"res://music/tracks/drifting_memories.ogg",
	"res://music/tracks/evening_harmony.ogg",
	"res://music/tracks/floating_dream.ogg",
	"res://music/tracks/forgotten_biomes.ogg",
	"res://music/tracks/gentle_breeze.ogg",
	"res://music/tracks/golden_gleam.ogg",
	"res://music/tracks/polar_lights.ogg",
	"res://music/tracks/strange_worlds.ogg",
	"res://music/tracks/sunlight_through_leaves.ogg",
	"res://music/tracks/wanderers_tale.ogg",
	"res://music/tracks/whispering_woods.ogg",
	"res://music/classics/minuet.ogg",
	"res://music/classics/minuet_slower.ogg",
	"res://music/classics/tales_from_the_vienna_woods.ogg",
	"res://music/classics/gymnopedie_no_1.ogg",
]


func run() -> void:
	print("  [ThemeSystemTests]")
	run_test("Roster_HasSixteenUniqueThemes_OneForEveryMusicFile", test_roster_completeness)
	run_test("Roster_ExactlyTheExpectedThemesHaveNoShadows", test_exactly_one_no_shadow_theme)
	run_test("Gymnopedie_DrainsAllPawnsEnergyOnStart", test_gymnopedie_drains_energy)
	run_test("Gymnopedie_HasShadowsFalse_ReflectedInSnapshot", test_gymnopedie_snapshot_has_shadows_false)
	run_test("Theme_DoesNotChange_WithoutMusicFinishedSignal", test_theme_persists_until_music_finished)
	run_test("Theme_ChangesToRosterMember_AfterMusicFinished", test_theme_changes_after_music_finished)
	run_test("RandomSelection_NeverImmediatelyRepeatsSameTheme", test_avoid_immediate_repeat)
	run_test("DisabledByDefault_NoCurrentThemeWithoutOptIn", test_disabled_by_default)
	run_test("StrangeWorlds_SpawnsVisitorsOnStart_RemovesOnEnd", test_strange_worlds_visitor_lifecycle)
	run_test("StrangeWorlds_SetsHomeSkinOverride_OnStart_ClearsOnEnd", test_strange_worlds_home_skin_override_lifecycle)
	run_test("HomeSkinOverride_PlumbsToPawnSnapshot_WhenPawnIsHomeThere", test_home_skin_override_plumbs_to_pawn_snapshot)
	run_test("ViennaWoods_SpawnsTwoSplitVisitorGroups_RemovesOnEnd", test_vienna_woods_visitor_lifecycle)
	run_test("ViennaWoods_SetsHomeSkinOverride_OnStart_ClearsOnEnd", test_vienna_woods_home_skin_override_lifecycle)
	run_test("PolarLights_AlternatesHomeSkinOverride_OnStart_ClearsOnEnd", test_polar_lights_home_skin_override_alternates)
	run_test("PolarLights_SpawnsMixedVisitorCrowd_RemovesOnEnd", test_polar_lights_visitor_lifecycle)
	run_test("GoldenGleam_RotatesHomeSkinOverride_OnStart_ClearsOnEnd", test_golden_gleam_home_skin_override_rotates)
	run_test("GoldenGleam_SpawnsSingleVisitor_RemovesOnEnd", test_golden_gleam_visitor_lifecycle)
	run_test("DriftingMemories_SetsHomeSkinOverride_OnStart_ClearsOnEnd", test_drifting_memories_home_skin_override_lifecycle)
	run_test("DriftingMemories_SpawnsVisitorsWithinRange_RemovesOnEnd", test_drifting_memories_visitor_lifecycle)
	run_test("GentleBreeze_SpawnsDoubleVisitorCount_RemovesOnEnd_NoSkinOverride", test_gentle_breeze_visitor_lifecycle)
	run_test("ForgottenBiomes_SpawnsMaxPawnsVisitorCount_RemovesOnEnd_NoSkinOverride", test_forgotten_biomes_visitor_lifecycle)
	run_test("FloatingDream_SetsHomeSkinOverride_AndSpawnsHalfMaxPawnsVisitors_RemovesOnEnd", test_floating_dream_lifecycle)
	run_test("FloatingDream_HasNoShadows_AndWeatherTintAndEffectKeySet", test_floating_dream_weather_properties)
	run_test("SimpleTheme_StrayCritters_TriggerRateIsRoughlyOneInFive", test_simple_theme_stray_trigger_rate)
	run_test("SimpleTheme_StrayCritters_CombinationVariesAndCleansUp", test_simple_theme_stray_combination_varies)
	run_test("Priority_FreshThemeStartsAtZero_TiersHaveExpectedGains", test_priority_defaults_and_tiers)
	run_test("Priority_PickedThemeJumpsByGain_OthersDecayTowardFloor", test_priority_lifecycle)
	run_test("Priority_NeverGoesNegative", test_priority_never_goes_negative)
	run_test("Priority_PickAlwaysComesFromTheLowestPrioritySet", test_pick_random_theme_selects_lowest_priority)
	run_test("Priority_CommonThemePickedMoreOftenThanRareTheme", test_common_theme_picked_more_often_than_rare)


# Every music file in music/tracks/ and music/classics/ should have exactly one theme, and
# vice versa — catches typos/renames/orphaned tracks early.
func test_roster_completeness() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var sim := builder.build()

	var themes: Array = sim.theme_system._available_themes
	assert_eq(themes.size(), 16, "Roster should have exactly 16 themes, one per music file")

	var names: Dictionary = {}
	var files: Dictionary = {}
	for t in themes:
		names[t.get_name()] = true
		files[t.get_music_file()] = true
	assert_eq(names.size(), 16, "All 16 theme names should be unique")
	assert_eq(files.size(), 16, "All 16 music files should be unique")

	for expected_file in EXPECTED_TRACK_FILES:
		assert_true(files.has(expected_file), "Roster should include %s" % expected_file)
		assert_true(ResourceLoader.exists(expected_file), "%s should exist on disk" % expected_file)


func test_exactly_one_no_shadow_theme() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var sim := builder.build()

	var no_shadow_names: Array[String] = []
	for t in sim.theme_system._available_themes:
		if not t.has_shadows():
			no_shadow_names.append(t.get_name())

	no_shadow_names.sort()
	assert_eq(
		no_shadow_names, ["Floating Dream", "Gymnopédie No. 1"],
		"Only Gymnopédie No. 1 (night) and Floating Dream (rain) should have no shadows"
	)


func test_gymnopedie_drains_energy() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var energy_id := builder.define_need("Energy", 0.0)
	builder.add_pawn("Sleepy", 2, 2, {energy_id: 80.0})
	var sim := builder.build()

	var pawn_id := sim.find_pawn_by_name("Sleepy")
	assert_eq(sim.get_need_value(pawn_id, energy_id), 80.0, "Setup should start with non-zero Energy")

	var gymnopedie = _find_theme_by_name(sim, "Gymnopédie No. 1")
	assert_not_null(gymnopedie, "Roster should contain Gymnopédie No. 1")
	sim.theme_system._start_theme(sim, gymnopedie)

	assert_eq(sim.get_need_value(pawn_id, energy_id), 0.0, "Gymnopédie starting should drain Energy to 0")


func test_gymnopedie_snapshot_has_shadows_false() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var sim := builder.build()

	var gymnopedie = _find_theme_by_name(sim, "Gymnopédie No. 1")
	sim.theme_system._start_theme(sim, gymnopedie)

	var snapshot: Dictionary = sim.create_render_snapshot()
	assert_false(snapshot.get("theme", {}).get("has_shadows", true), "Snapshot should reflect Gymnopédie's has_shadows=false")


func test_theme_persists_until_music_finished() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var sim := builder.build()

	sim.run_ticks(1)
	var first_name: String = sim.theme_system.current_theme.get_name()

	sim.run_ticks(2000)
	assert_eq(sim.theme_system.current_theme.get_name(), first_name, "Theme should not change without on_music_finished being called")


func test_theme_changes_after_music_finished() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var sim := builder.build()

	sim.run_ticks(1)

	sim.theme_system.on_music_finished(sim)
	assert_not_null(sim.theme_system.current_theme, "A new theme should be selected after music finishes")

	var found := false
	for t in sim.theme_system._available_themes:
		if t.get_name() == sim.theme_system.current_theme.get_name():
			found = true
	assert_true(found, "The new current theme should be a roster member")


# No explicit "exclude the current theme" rule exists anymore — this now falls out of the
# priority/cooldown selection in _pick_random_theme (see ThemeSystem.gd): a just-picked
# theme's priority is always above 0 afterward, and since there are more themes (16) than the
# largest gain (10, "rare"), some other theme is always sitting at 0 and wins instead.
func test_avoid_immediate_repeat() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var sim := builder.build()

	sim.run_ticks(1)
	var previous_name: String = sim.theme_system.current_theme.get_name()
	for i in 40:
		sim.theme_system.on_music_finished(sim)
		var current_name: String = sim.theme_system.current_theme.get_name()
		assert_not_eq(current_name, previous_name, "Theme should never immediately repeat the previous one")
		previous_name = current_name


func test_disabled_by_default() -> void:
	var builder := _Builder.new()  # themes NOT enabled
	var sim := builder.build()

	sim.run_ticks(100)
	assert_true(sim.theme_system.current_theme == null, "Themes should stay disabled unless explicitly enabled")


func test_priority_defaults_and_tiers() -> void:
	var simple = SimTheme.SimpleTheme.new("Trial", "res://music/tracks/cuddle_clouds.ogg")
	assert_eq(simple.priority, 0, "A fresh theme should start at priority 0")
	assert_eq(simple.get_priority_gain(), 6, "SimpleTheme should inherit the default tier's gain (6)")

	assert_eq(SimTheme.GymnopedieTheme.new().get_priority_gain(), 3, "Gymnopédie should be the common tier (gain 3)")
	assert_eq(SimTheme.StrangeWorldsTheme.new().get_priority_gain(), 10, "Strange Worlds should be the rare tier (gain 10)")


# Isolates the mechanism directly: a picked theme's priority should jump to its own gain, every
# other theme should be left untouched by that same pick (only decayed on the *next* pick), and
# an un-picked theme should decay by exactly 1 per subsequent pick.
func test_priority_lifecycle() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var sim := builder.build()

	var themes: Array = sim.theme_system._available_themes
	for t in themes:
		t.priority = 0

	var picked_first = sim.theme_system._pick_random_theme()
	assert_eq(picked_first.priority, picked_first.get_priority_gain(), "Picked theme's priority should jump to its own gain")

	for t in themes:
		if t != picked_first:
			assert_eq(t.priority, 0, "Non-picked themes should be untouched by someone else's pick (still at the floor they started at)")

	var priority_after_first_pick: int = picked_first.priority
	sim.theme_system._pick_random_theme()  # a second, unrelated pick — decays everyone again
	assert_eq(picked_first.priority, priority_after_first_pick - 1, "An un-picked theme should decay by exactly 1 on the next pick")


func test_priority_never_goes_negative() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var sim := builder.build()

	for i in 20:
		sim.theme_system._pick_random_theme()

	for t in sim.theme_system._available_themes:
		assert_ge(float(t.priority), 0.0, "Priority should never decay below 0")


func test_pick_random_theme_selects_lowest_priority() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var sim := builder.build()

	var themes: Array = sim.theme_system._available_themes
	for t in themes:
		t.priority = 5
	var target = themes[3]
	target.priority = 0

	var picked = sim.theme_system._pick_random_theme()
	assert_eq(picked, target, "Should always pick the uniquely lowest-priority theme, even after this pick's own decay step")


# Over many rounds, a common theme (gain 3, cools down fast) should come up noticeably more
# often than a rare one (gain 10, cools down slowly) — this is the actual point of the tiers.
func test_common_theme_picked_more_often_than_rare() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var sim := builder.build()

	var gymnopedie = _find_theme_by_name(sim, "Gymnopédie No. 1")
	var strange_worlds = _find_theme_by_name(sim, "Strange Worlds")
	assert_not_null(gymnopedie, "Roster should contain Gymnopédie No. 1")
	assert_not_null(strange_worlds, "Roster should contain Strange Worlds")

	var gymnopedie_count := 0
	var strange_worlds_count := 0
	for i in 500:
		var picked = sim.theme_system._pick_random_theme()
		if picked == gymnopedie:
			gymnopedie_count += 1
		elif picked == strange_worlds:
			strange_worlds_count += 1

	assert_gt(
		float(gymnopedie_count), float(strange_worlds_count),
		"Common theme (%d picks) should be picked more often than rare theme (%d picks) over many rounds" % [gymnopedie_count, strange_worlds_count]
	)


# StrangeWorldsTheme should spawn as many visitor pawns as the colony's pawn goal (get_max_pawns,
# capacity-4 home here) the moment it starts (each carrying its forced sheet key), and send them
# all away the moment it ends.
func test_strange_worlds_visitor_lifecycle() -> void:
	var builder := _Builder.new().with_themes_enabled()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TestHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 4, true
	)
	builder.add_building(home_id, 5, 5)
	var sim := builder.build()

	assert_eq(sim.get_max_pawns(), 4, "Setup should give a pawn goal of 4")

	var theme = SimTheme.StrangeWorldsTheme.new()
	sim.theme_system._start_theme(sim, theme)

	var visitor_count := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			visitor_count += 1
			assert_eq(sim.entities.pawns[pawn_id].forced_sheet_key, "special_4_v2", "Spawned visitor should carry Strange Worlds' forced sheet key")
	assert_eq(visitor_count, sim.get_max_pawns(), "Theme should spawn exactly get_max_pawns() visitors on start")

	sim.theme_system._start_theme(sim, _find_theme_by_name(sim, "Gymnopédie No. 1"))
	sim.run_ticks(2000)

	var remaining_visitors := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			remaining_visitors += 1
	assert_eq(remaining_visitors, 0, "All visitors should have left after the theme ended and enough time passed")


# Strange Worlds is per-house, not global — on_start should stamp every isHome building's
# skin_override, and on_end should clear every one of them back to "".
func test_strange_worlds_home_skin_override_lifecycle() -> void:
	var builder := _Builder.new().with_themes_enabled()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TestHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 1, true
	)
	builder.add_building(home_id, 2, 2)
	builder.add_building(home_id, 5, 5)
	var sim := builder.build()

	var home_ids: Array[int] = sim.get_home_building_ids()
	assert_eq(home_ids.size(), 2, "Setup should have two home buildings")

	var theme = SimTheme.StrangeWorldsTheme.new()
	sim.theme_system._start_theme(sim, theme)

	for building_id in home_ids:
		assert_eq(
			sim.entities.buildings[building_id].skin_override, "character_7_v2",
			"Every home should receive Strange Worlds' skin override on start"
		)

	sim.theme_system._start_theme(sim, _find_theme_by_name(sim, "Gymnopédie No. 1"))

	for building_id in home_ids:
		assert_eq(
			sim.entities.buildings[building_id].skin_override, "",
			"Every home's override should be cleared once the theme ends"
		)


# A pawn ACTIVELY using an isHome building right now (see Simulation._build_pawn_snapshots'
# home_visit_building_id) should reflect that house's CURRENT skin_override in its own snapshot
# entry — this is what lets PawnView.sync_house_sheet pick it up without Godot needing to
# cross-reference buildings itself. Being merely attached to a home (having slept there before)
# is NOT enough — only an in-progress visit counts, which is what actually gates the update.
func test_home_skin_override_plumbs_to_pawn_snapshot() -> void:
	var builder := _Builder.new()
	builder.with_world_bounds(10, 10)
	var energy_id := builder.define_need("Energy", 0.0)
	var home_id := builder.define_building(
		"TestHome", energy_id, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 1, true
	)
	builder.add_building(home_id, 2, 2)
	builder.add_pawn("Sleeper", 2, 2, {})
	var sim := builder.build()

	var home_building_id := _get_building_by_def_id(sim, home_id)
	var pawn_id := sim.find_pawn_by_name("Sleeper")

	# Directly put the pawn mid-USE_BUILDING at their home (see PopulationTests/AttachmentTests
	# for the same pattern) rather than running the full AI loop — this test is about snapshot
	# plumbing, not pathing/AI decisions.
	var use_action := Definitions.ActionDef.new()
	use_action.type = Definitions.ActionType.USE_BUILDING
	use_action.target_entity = home_building_id
	use_action.satisfies_need_id = energy_id
	var action_comp = sim.entities.actions.get(pawn_id)
	action_comp.current_action = use_action

	var snapshot_before: Dictionary = sim.create_render_snapshot()
	var pawn_snap_before: Dictionary = _find_pawn_snapshot(snapshot_before, pawn_id)
	assert_eq(pawn_snap_before.get("home_visit_building_id", -1), home_building_id, "Pawn should be resolved as visiting this house right now")
	assert_eq(pawn_snap_before.get("home_visit_skin_override", ""), "", "No override set yet")

	sim.set_building_skin_override(home_building_id, "character_7_v2")

	var snapshot_after: Dictionary = sim.create_render_snapshot()
	var pawn_snap_after: Dictionary = _find_pawn_snapshot(snapshot_after, pawn_id)
	assert_eq(pawn_snap_after.get("home_visit_skin_override", ""), "character_7_v2", "Pawn snapshot should reflect the home's new override while still visiting")

	# Once they're no longer actively using the building, the visit-gated fields should clear —
	# even though they're still just as attached/"living" there as before.
	action_comp.current_action = null
	var snapshot_after_leaving: Dictionary = sim.create_render_snapshot()
	var pawn_snap_after_leaving: Dictionary = _find_pawn_snapshot(snapshot_after_leaving, pawn_id)
	assert_eq(pawn_snap_after_leaving.get("home_visit_building_id", -1), -1, "Once the visit ends, the pawn should no longer be resolved as home")

	# Merely targeting the home building isn't enough either — it has to specifically be a
	# USE_BUILDING action (the "sleeping there" action), not some other action type that happens
	# to reference the same building id (e.g. a hypothetical WORK shift there).
	var work_action := Definitions.ActionDef.new()
	work_action.type = Definitions.ActionType.WORK
	work_action.target_entity = home_building_id
	action_comp.current_action = work_action
	var snapshot_during_work: Dictionary = sim.create_render_snapshot()
	var pawn_snap_during_work: Dictionary = _find_pawn_snapshot(snapshot_during_work, pawn_id)
	assert_eq(pawn_snap_during_work.get("home_visit_building_id", -1), -1, "A non-USE_BUILDING action targeting the home building must not count as a home visit")


# ViennaWoodsTheme should spawn TWO visitor groups on start, each sized get_max_pawns() / 2 —
# spring_2_v2 and spring_3_v2 — and send everyone away once it ends.
func test_vienna_woods_visitor_lifecycle() -> void:
	var builder := _Builder.new().with_themes_enabled()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TestHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 6, true
	)
	builder.add_building(home_id, 5, 5)
	var sim := builder.build()

	assert_eq(sim.get_max_pawns(), 6, "Setup should give a pawn goal of 6")

	var theme = SimTheme.ViennaWoodsTheme.new()
	sim.theme_system._start_theme(sim, theme)

	var sheet_counts: Dictionary = {"spring_2_v2": 0, "spring_3_v2": 0}
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			var sheet: String = sim.entities.pawns[pawn_id].forced_sheet_key
			assert_true(sheet_counts.has(sheet), "Visitor should carry one of Vienna Woods' two forced sheet keys")
			sheet_counts[sheet] += 1

	var expected_group_size: int = sim.get_max_pawns() / 2
	assert_eq(sheet_counts["spring_2_v2"], expected_group_size, "spring_2_v2 group should be sized get_max_pawns() / 2")
	assert_eq(sheet_counts["spring_3_v2"], expected_group_size, "spring_3_v2 group should be sized get_max_pawns() / 2")

	sim.theme_system._start_theme(sim, _find_theme_by_name(sim, "Gymnopédie No. 1"))
	sim.run_ticks(2000)

	var remaining_visitors := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			remaining_visitors += 1
	assert_eq(remaining_visitors, 0, "All visitors should have left after the theme ended and enough time passed")


func test_vienna_woods_home_skin_override_lifecycle() -> void:
	var builder := _Builder.new().with_themes_enabled()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TestHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 1, true
	)
	builder.add_building(home_id, 2, 2)
	var sim := builder.build()

	var home_ids: Array[int] = sim.get_home_building_ids()

	var theme = SimTheme.ViennaWoodsTheme.new()
	sim.theme_system._start_theme(sim, theme)

	for building_id in home_ids:
		assert_eq(
			sim.entities.buildings[building_id].skin_override, "spring_1_v2",
			"Every home should receive Vienna Woods' skin override on start"
		)

	sim.theme_system._start_theme(sim, _find_theme_by_name(sim, "Gymnopédie No. 1"))

	for building_id in home_ids:
		assert_eq(
			sim.entities.buildings[building_id].skin_override, "",
			"Every home's override should be cleared once the theme ends"
		)


# Polar Lights alternates by build order (winter_1_v2, winter_2_v2, winter_1_v2, ...) rather
# than applying one override to every home like Strange Worlds/Vienna Woods.
func test_polar_lights_home_skin_override_alternates() -> void:
	var builder := _Builder.new().with_themes_enabled()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TestHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 1, true
	)
	builder.add_building(home_id, 1, 1)
	builder.add_building(home_id, 3, 3)
	builder.add_building(home_id, 5, 5)
	builder.add_building(home_id, 7, 7)
	var sim := builder.build()

	var home_ids: Array[int] = sim.get_home_building_ids()
	assert_eq(home_ids.size(), 4, "Setup should have four home buildings")

	var theme = SimTheme.PolarLightsTheme.new()
	sim.theme_system._start_theme(sim, theme)

	for i in home_ids.size():
		var expected: String = "winter_1_v2" if i % 2 == 0 else "winter_2_v2"
		assert_eq(
			sim.entities.buildings[home_ids[i]].skin_override, expected,
			"Home %d should alternate to %s" % [i, expected]
		)

	sim.theme_system._start_theme(sim, _find_theme_by_name(sim, "Gymnopédie No. 1"))

	for building_id in home_ids:
		assert_eq(
			sim.entities.buildings[building_id].skin_override, "",
			"Every home's override should be cleared once the theme ends"
		)


# Unlike Vienna Woods' exact even split, Polar Lights' visitor crowd is a random per-visitor
# mix of winter_3_v2/winter_4_v2 — only the total count and valid sheet membership are
# deterministic invariants worth asserting.
func test_polar_lights_visitor_lifecycle() -> void:
	var builder := _Builder.new().with_themes_enabled()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TestHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 6, true
	)
	builder.add_building(home_id, 5, 5)
	var sim := builder.build()

	assert_eq(sim.get_max_pawns(), 6, "Setup should give a pawn goal of 6")

	var theme = SimTheme.PolarLightsTheme.new()
	sim.theme_system._start_theme(sim, theme)

	var visitor_count := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			visitor_count += 1
			var sheet: String = sim.entities.pawns[pawn_id].forced_sheet_key
			assert_true(
				sheet == "winter_3_v2" or sheet == "winter_4_v2",
				"Visitor should carry one of Polar Lights' two forced sheet keys"
			)
	assert_eq(visitor_count, sim.get_max_pawns(), "Theme should spawn exactly get_max_pawns() visitors on start")

	sim.theme_system._start_theme(sim, _find_theme_by_name(sim, "Gymnopédie No. 1"))
	sim.run_ticks(2000)

	var remaining_visitors := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			remaining_visitors += 1
	assert_eq(remaining_visitors, 0, "All visitors should have left after the theme ended and enough time passed")


# Golden Gleam rotates through all four dg_knight sheets by build order, wrapping via modulo
# once there are more homes than sheets — the 5th home should cycle back to dg_knight_1_v2.
func test_golden_gleam_home_skin_override_rotates() -> void:
	var builder := _Builder.new().with_themes_enabled()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TestHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 1, true
	)
	builder.add_building(home_id, 1, 1)
	builder.add_building(home_id, 3, 3)
	builder.add_building(home_id, 5, 5)
	builder.add_building(home_id, 7, 7)
	builder.add_building(home_id, 9, 9)
	var sim := builder.build()

	var home_ids: Array[int] = sim.get_home_building_ids()
	assert_eq(home_ids.size(), 5, "Setup should have five home buildings")

	var theme = SimTheme.GoldenGleamTheme.new()
	sim.theme_system._start_theme(sim, theme)

	var expected_skins: Array[String] = [
		"dg_knight_1_v2", "dg_knight_2_v2", "dg_knight_3_v2", "dg_knight_4_v2", "dg_knight_1_v2",
	]
	for i in home_ids.size():
		assert_eq(
			sim.entities.buildings[home_ids[i]].skin_override, expected_skins[i],
			"Home %d should rotate to %s (wrapping via modulo)" % [i, expected_skins[i]]
		)

	sim.theme_system._start_theme(sim, _find_theme_by_name(sim, "Gymnopédie No. 1"))

	for building_id in home_ids:
		assert_eq(
			sim.entities.buildings[building_id].skin_override, "",
			"Every home's override should be cleared once the theme ends"
		)


# Unlike the other visitor-bearing themes, Golden Gleam brings in exactly one visitor,
# regardless of colony size.
func test_golden_gleam_visitor_lifecycle() -> void:
	var builder := _Builder.new().with_themes_enabled()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TestHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 6, true
	)
	builder.add_building(home_id, 5, 5)
	var sim := builder.build()

	assert_eq(sim.get_max_pawns(), 6, "Setup should give a pawn goal of 6 (unrelated to visitor count here)")

	var theme = SimTheme.GoldenGleamTheme.new()
	sim.theme_system._start_theme(sim, theme)

	var visitor_count := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			visitor_count += 1
			assert_eq(sim.entities.pawns[pawn_id].forced_sheet_key, "character_15_v2", "The single visitor should carry Golden Gleam's forced sheet key")
	assert_eq(visitor_count, 1, "Theme should spawn exactly one visitor regardless of colony size")

	sim.theme_system._start_theme(sim, _find_theme_by_name(sim, "Gymnopédie No. 1"))
	sim.run_ticks(2000)

	var remaining_visitors := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			remaining_visitors += 1
	assert_eq(remaining_visitors, 0, "The visitor should have left after the theme ended and enough time passed")


func test_drifting_memories_home_skin_override_lifecycle() -> void:
	var builder := _Builder.new().with_themes_enabled()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TestHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 1, true
	)
	builder.add_building(home_id, 2, 2)
	builder.add_building(home_id, 5, 5)
	var sim := builder.build()

	var home_ids: Array[int] = sim.get_home_building_ids()
	assert_eq(home_ids.size(), 2, "Setup should have two home buildings")

	var theme = SimTheme.DriftingMemoriesTheme.new()
	sim.theme_system._start_theme(sim, theme)

	for building_id in home_ids:
		assert_eq(
			sim.entities.buildings[building_id].skin_override, "character_17_v2",
			"Every home should receive Drifting Memories' skin override on start"
		)

	sim.theme_system._start_theme(sim, _find_theme_by_name(sim, "Gymnopédie No. 1"))

	for building_id in home_ids:
		assert_eq(
			sim.entities.buildings[building_id].skin_override, "",
			"Every home's override should be cleared once the theme ends"
		)


# Visitor count is randi_range(1, get_max_pawns() * 2), not a fixed number — only the range
# bound, sheet membership, and cleanup are deterministic invariants worth asserting.
func test_drifting_memories_visitor_lifecycle() -> void:
	var builder := _Builder.new().with_themes_enabled()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TestHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 4, true
	)
	builder.add_building(home_id, 5, 5)
	var sim := builder.build()

	assert_eq(sim.get_max_pawns(), 4, "Setup should give a pawn goal of 4")

	var theme = SimTheme.DriftingMemoriesTheme.new()
	sim.theme_system._start_theme(sim, theme)

	var visitor_count := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			visitor_count += 1
			assert_eq(sim.entities.pawns[pawn_id].forced_sheet_key, "character_18_v2", "Spawned visitor should carry Drifting Memories' forced sheet key")
	assert_ge(visitor_count, 1, "Should spawn at least one visitor")
	assert_true(visitor_count <= sim.get_max_pawns() * 2, "Should never spawn more than get_max_pawns() * 2 visitors")

	sim.theme_system._start_theme(sim, _find_theme_by_name(sim, "Gymnopédie No. 1"))
	sim.run_ticks(2000)

	var remaining_visitors := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			remaining_visitors += 1
	assert_eq(remaining_visitors, 0, "All visitors should have left after the theme ended and enough time passed")


# Gentle Breeze is visitor-only — no home skin override, unlike every other special theme so
# far. Visitor count is a fixed get_max_pawns() * 2, not randomized or split.
func test_gentle_breeze_visitor_lifecycle() -> void:
	var builder := _Builder.new().with_themes_enabled()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TestHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 3, true
	)
	builder.add_building(home_id, 5, 5)
	var sim := builder.build()

	assert_eq(sim.get_max_pawns(), 3, "Setup should give a pawn goal of 3")
	var home_building_id := _get_building_by_def_id(sim, home_id)

	var theme = SimTheme.GentleBreezeTheme.new()
	sim.theme_system._start_theme(sim, theme)

	assert_eq(sim.entities.buildings[home_building_id].skin_override, "", "Gentle Breeze should not touch any home's skin override")

	var visitor_count := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			visitor_count += 1
			assert_eq(sim.entities.pawns[pawn_id].forced_sheet_key, "autumn_1_v2", "Spawned visitor should carry Gentle Breeze's forced sheet key")
	assert_eq(visitor_count, sim.get_max_pawns() * 2, "Theme should spawn exactly get_max_pawns() * 2 visitors on start")

	sim.theme_system._start_theme(sim, _find_theme_by_name(sim, "Gymnopédie No. 1"))
	sim.run_ticks(2000)

	var remaining_visitors := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			remaining_visitors += 1
	assert_eq(remaining_visitors, 0, "All visitors should have left after the theme ended and enough time passed")


# Forgotten Biomes is also visitor-only — no home skin override. Visitor count is a fixed
# get_max_pawns() (not doubled like Gentle Breeze).
func test_forgotten_biomes_visitor_lifecycle() -> void:
	var builder := _Builder.new().with_themes_enabled()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TestHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 3, true
	)
	builder.add_building(home_id, 5, 5)
	var sim := builder.build()

	assert_eq(sim.get_max_pawns(), 3, "Setup should give a pawn goal of 3")
	var home_building_id := _get_building_by_def_id(sim, home_id)

	var theme = SimTheme.ForgottenBiomesTheme.new()
	sim.theme_system._start_theme(sim, theme)

	assert_eq(sim.entities.buildings[home_building_id].skin_override, "", "Forgotten Biomes should not touch any home's skin override")

	var visitor_count := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			visitor_count += 1
			assert_eq(sim.entities.pawns[pawn_id].forced_sheet_key, "special_3_v2", "Spawned visitor should carry Forgotten Biomes' forced sheet key")
	assert_eq(visitor_count, sim.get_max_pawns(), "Theme should spawn exactly get_max_pawns() visitors on start")

	sim.theme_system._start_theme(sim, _find_theme_by_name(sim, "Gymnopédie No. 1"))
	sim.run_ticks(2000)

	var remaining_forgotten_visitors := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			remaining_forgotten_visitors += 1
	assert_eq(remaining_forgotten_visitors, 0, "All visitors should have left after the theme ended and enough time passed")


# Floating Dream sets a home skin override (like Strange Worlds) AND spawns visitors sized
# get_max_pawns() / 2 (like Vienna Woods' per-group count, but a single sheet here).
func test_floating_dream_lifecycle() -> void:
	var builder := _Builder.new().with_themes_enabled()
	builder.with_world_bounds(10, 10)
	var home_id := builder.define_building(
		"TestHome", -1, 50.0, 20, 0.0, 0, [], 1, false, "", 100.0, "direct", "", "", true, 6, true
	)
	builder.add_building(home_id, 5, 5)
	var sim := builder.build()

	assert_eq(sim.get_max_pawns(), 6, "Setup should give a pawn goal of 6")
	var home_building_id := _get_building_by_def_id(sim, home_id)

	var theme = SimTheme.FloatingDreamTheme.new()
	sim.theme_system._start_theme(sim, theme)

	assert_eq(sim.entities.buildings[home_building_id].skin_override, "summer_1_v2", "Home should receive Floating Dream's skin override on start")

	var visitor_count := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			visitor_count += 1
			assert_eq(sim.entities.pawns[pawn_id].forced_sheet_key, "special_1_v2", "Spawned visitor should carry Floating Dream's forced sheet key")
	assert_eq(visitor_count, sim.get_max_pawns() / 2, "Theme should spawn exactly get_max_pawns() / 2 visitors on start")

	sim.theme_system._start_theme(sim, _find_theme_by_name(sim, "Gymnopédie No. 1"))
	sim.run_ticks(2000)

	assert_eq(sim.entities.buildings[home_building_id].skin_override, "", "Home's override should be cleared once the theme ends")
	var remaining_dream_visitors := 0
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			remaining_dream_visitors += 1
	assert_eq(remaining_dream_visitors, 0, "All visitors should have left after the theme ended and enough time passed")


# has_shadows/weather_tint/weather_effect_key all plumb through to the render snapshot exactly
# as the theme declares them — this is what GameRoot reads to drive the CRT shader tint and
# the weather particle controller (see WeatherEffectController).
func test_floating_dream_weather_properties() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var sim := builder.build()

	var theme = SimTheme.FloatingDreamTheme.new()
	assert_false(theme.has_shadows(), "Floating Dream should have no shadows")
	assert_gt(theme.get_weather_tint(), 0.0, "Floating Dream should have a non-zero weather tint")
	assert_eq(theme.get_weather_effect_key(), "rain", "Floating Dream should declare the 'rain' weather effect")

	sim.theme_system._start_theme(sim, theme)
	var snapshot: Dictionary = sim.create_render_snapshot()
	assert_false(snapshot.get("theme", {}).get("has_shadows", true), "Snapshot should reflect no shadows")
	assert_gt(float(snapshot.get("theme", {}).get("weather_tint", 0.0)), 0.0, "Snapshot should carry a non-zero weather_tint")
	assert_eq(snapshot.get("theme", {}).get("weather_effect_key", ""), "rain", "Snapshot should carry the 'rain' weather_effect_key")


# Statistical: over many independent trials, roughly 1-in-5 SimpleTheme starts should spawn
# any stray critters at all — "should feel pretty rare". Wide tolerance since this is a real
# random sample, not a fixed sequence; a fixed builder seed still makes the run reproducible.
# Each trial destroys any spawned critters directly (bypassing remove_all_visitors' walk-to-edge
# animation, which needs real ticks to resolve — irrelevant to what this test is measuring and
# far too slow to run out 500 times) so trials stay independent and fast.
func test_simple_theme_stray_trigger_rate() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var sim := builder.build()

	var trials := 500
	var triggered := 0
	for i in trials:
		var theme = SimTheme.SimpleTheme.new("Trial", "res://music/tracks/cuddle_clouds.ogg")
		theme.on_start(sim)
		if _count_and_destroy_visitors(sim) > 0:
			triggered += 1

	var rate: float = float(triggered) / float(trials)
	assert_true(rate > 0.1 and rate < 0.3, "Trigger rate %.2f should be roughly 1-in-5 (0.1–0.3 tolerance band)" % rate)


# "Any combination is possible" — across enough trials, both small (single-critter) and
# larger (multi-critter) results should occur, and every spawned sheet should come from the
# stray pool. See test_simple_theme_stray_trigger_rate for why cleanup bypasses ticks.
func test_simple_theme_stray_combination_varies() -> void:
	var builder := _Builder.new().with_themes_enabled()
	var sim := builder.build()

	var saw_single := false
	var saw_multiple := false
	var stray_pool: Array[String] = SimTheme.SimpleTheme.STRAY_POOL

	for i in 500:
		var theme = SimTheme.SimpleTheme.new("Trial", "res://music/tracks/cuddle_clouds.ogg")
		theme.on_start(sim)

		for pawn_id in sim.entities.pawns:
			if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
				var sheet: String = sim.entities.pawns[pawn_id].forced_sheet_key
				assert_true(stray_pool.has(sheet), "Spawned critter sheet %s should come from STRAY_POOL" % sheet)

		var visitor_count: int = _count_and_destroy_visitors(sim)
		if visitor_count == 1:
			saw_single = true
		elif visitor_count > 1:
			saw_multiple = true

	assert_true(saw_single, "At least one trial should have spawned exactly one critter")
	assert_true(saw_multiple, "At least one trial should have spawned more than one critter")


## Directly destroys every current visitor pawn (bypassing remove_all_visitors' walk-to-edge
## animation, which only resolves over real ticks) and returns how many there were —
## deliberately not the theme's real on_end path, since these tests are about spawn
## distribution, not despawn behavior (already covered by other tests).
func _count_and_destroy_visitors(sim) -> int:
	var visitor_ids: Array = []
	for pawn_id in sim.entities.pawns:
		if sim.entities.pawns[pawn_id].membership == Definitions.PawnMembership.VISITOR:
			visitor_ids.append(pawn_id)
	for pawn_id in visitor_ids:
		sim.destroy_entity(pawn_id)
	return visitor_ids.size()


func _find_pawn_snapshot(snapshot: Dictionary, pawn_id: int) -> Dictionary:
	for p in snapshot.get("pawns", []):
		if p.get("id", -1) == pawn_id:
			return p
	return {}


func _get_building_by_def_id(sim, def_id: int) -> int:
	for building_id in sim.entities.buildings:
		if sim.entities.buildings[building_id].building_def_id == def_id:
			return building_id
	return -1


func _find_theme_by_name(sim, theme_name: String):
	for t in sim.theme_system._available_themes:
		if t.get_name() == theme_name:
			return t
	return null
