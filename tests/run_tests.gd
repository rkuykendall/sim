extends SceneTree

# Preload forces the class_name registry to be populated before instantiation.
const _IntegrationTests = preload("res://tests/SimulationIntegrationTests.gd")
const _PathfindingTests = preload("res://tests/PathfindingWallTests.gd")
const _LifecycleTests   = preload("res://tests/PawnLifecycleTests.gd")
const _SaveLoadTests    = preload("res://tests/SaveLoadTests.gd")


func _init() -> void:
	print("=== SimGame Test Runner ===")
	print("")

	var total_pass := 0
	var total_fail := 0

	for suite_script in [_IntegrationTests, _PathfindingTests, _LifecycleTests, _SaveLoadTests]:
		var suite = suite_script.new()
		suite.run()
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
