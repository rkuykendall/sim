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
	run_test("Roster_ExactlyOneThemeHasNoShadows", test_exactly_one_no_shadow_theme)
	run_test("Gymnopedie_DrainsAllPawnsEnergyOnStart", test_gymnopedie_drains_energy)
	run_test("Gymnopedie_HasShadowsFalse_ReflectedInSnapshot", test_gymnopedie_snapshot_has_shadows_false)
	run_test("Theme_DoesNotChange_WithoutMusicFinishedSignal", test_theme_persists_until_music_finished)
	run_test("Theme_ChangesToRosterMember_AfterMusicFinished", test_theme_changes_after_music_finished)
	run_test("RandomSelection_NeverImmediatelyRepeatsSameTheme", test_avoid_immediate_repeat)
	run_test("DisabledByDefault_NoCurrentThemeWithoutOptIn", test_disabled_by_default)


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

	var no_shadow_count := 0
	var no_shadow_name := ""
	for t in sim.theme_system._available_themes:
		if not t.has_shadows():
			no_shadow_count += 1
			no_shadow_name = t.get_name()

	assert_eq(no_shadow_count, 1, "Exactly one theme should have no shadows")
	assert_eq(no_shadow_name, "Gymnopédie No. 1", "The no-shadow theme should be Gymnopédie No. 1")


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


func _find_theme_by_name(sim, theme_name: String):
	for t in sim.theme_system._available_themes:
		if t.get_name() == theme_name:
			return t
	return null
