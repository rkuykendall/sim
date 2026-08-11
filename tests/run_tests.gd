extends SceneTree

# Preload forces the class_name registry to be populated before instantiation.
const _IntegrationTests = preload("res://tests/SimulationIntegrationTests.gd")
const _PathfindingTests = preload("res://tests/PathfindingWallTests.gd")
const _LifecycleTests = preload("res://tests/PawnLifecycleTests.gd")
const _SaveLoadTests = preload("res://tests/SaveLoadTests.gd")
const _AISystemTests = preload("res://tests/AISystemTests.gd")
const _MultiPawnTests = preload("res://tests/MultiPawnTests.gd")
const _WorkAndHaulTests = preload("res://tests/WorkAndHaulTests.gd")
const _MoodSystemTests = preload("res://tests/MoodSystemTests.gd")
const _PaintingTests = preload("res://tests/TerrainPaintingTests.gd")
const _QueriesTests = preload("res://tests/SimulationQueriesTests.gd")
const _PopulationTests = preload("res://tests/PopulationTests.gd")
const _AttachmentTests = preload("res://tests/AttachmentTests.gd")
const _ThemeSystemTests = preload("res://tests/ThemeSystemTests.gd")
const _VisitorPawnTests = preload("res://tests/VisitorPawnTests.gd")


func _init() -> void:
	print("=== Paint Town Test Runner ===")
	print("")

	var total_pass := 0
	var total_fail := 0

	for suite_script: GDScript in [
		_IntegrationTests,
		_PathfindingTests,
		_LifecycleTests,
		_SaveLoadTests,
		_AISystemTests,
		_MultiPawnTests,
		_WorkAndHaulTests,
		_MoodSystemTests,
		_PaintingTests,
		_QueriesTests,
		_PopulationTests,
		_AttachmentTests,
		_ThemeSystemTests,
		_VisitorPawnTests
	]:
		# Each suite extends SimTestCase but defines its own run() — not on the shared base —
		# so this stays duck-typed rather than statically resolved (see SystemManager.gd).
		var suite: SimTestCase = suite_script.new()
		@warning_ignore("unsafe_method_access")
		suite.run()
		@warning_ignore("unsafe_method_access")
		var results: Dictionary = suite.run_all()
		total_pass += results["pass"]
		total_fail += results["fail"]

	print("")
	print("==========================")
	print("Results: %d passed, %d failed" % [total_pass, total_fail])
	if total_fail == 0:
		print("ALL TESTS PASSED")
	else:
		print("SOME TESTS FAILED")
	print("")

	quit(1 if total_fail > 0 else 0)
