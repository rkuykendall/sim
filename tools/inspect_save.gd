extends SceneTree

## Prints a health report for a save file: need distribution, mood, and building resource
## levels — the numbers we've been checking by hand when tuning the economy.
##
## Usage:
##   godot --headless --path . --script tools/inspect_save.gd -- "Save 1"
##   godot --headless --path . --script tools/inspect_save.gd -- 1
## Defaults to the most recently saved slot if no name is given.


func _initialize() -> void:
	var content := ContentLoader.load_all()

	var slot_name := _resolve_slot_name()
	if slot_name.is_empty():
		printerr("No saves found in %s" % SaveFileManager.SAVE_DIRECTORY)
		quit(1)
		return

	var sim: Simulation = SaveFileManager.load_save(slot_name, content)
	if sim == null:
		printerr("Failed to load save '%s'" % slot_name)
		quit(1)
		return

	print("=== Save: %s (tick %d, %s) ===" % [slot_name, sim.time.tick, sim.time.time_string])
	print("Pawns: %d   Buildings: %d" % [sim.entities.all_pawns().size(), sim.entities.all_buildings().size()])
	print("")

	_print_needs(sim, content)
	_print_mood(sim)
	_print_buildings(sim, content)

	quit()


func _resolve_slot_name() -> String:
	var args := OS.get_cmdline_user_args()
	if not args.is_empty():
		var arg: String = args[0]
		return "Save %s" % arg if arg.is_valid_int() else arg

	var saves: Array = SaveFileManager.get_all_saves()
	if saves.is_empty():
		return ""
	return saves[0].get("slot_name", "")


func _print_needs(sim: Simulation, content: ContentRegistry) -> void:
	print("=== Needs ===")
	var pawn_ids: Array = sim.entities.all_pawns()
	for need_id in content.needs:
		var need_def: Dictionary = content.needs[need_id]
		var need_name: String = need_def.get("name", str(need_id))
		var critical_threshold: float = float(need_def.get("criticalThreshold", 0.0))
		var low_threshold: float = float(need_def.get("lowThreshold", 0.0))

		var values: Array = []
		var critical_count := 0
		var low_count := 0
		for pawn_id in pawn_ids:
			var v: float = sim.get_need_value(pawn_id, need_id)
			values.append(v)
			if v < critical_threshold:
				critical_count += 1
			elif v < low_threshold:
				low_count += 1

		if values.is_empty():
			continue
		values.sort()
		var total := 0.0
		for v in values:
			total += v

		print("%-10s min=%5.1f max=%5.1f avg=%5.1f  critical(<%.0f)=%d/%d  low(<%.0f)=%d/%d" % [
			need_name, values[0], values[-1], total / values.size(),
			critical_threshold, critical_count, values.size(),
			low_threshold, low_count, values.size(),
		])
	print("")


func _print_mood(sim: Simulation) -> void:
	print("=== Mood ===")
	var moods: Array = []
	for pawn_id in sim.entities.all_pawns():
		moods.append(sim.get_mood(pawn_id))
	if moods.is_empty():
		print("(no pawns)")
		print("")
		return

	moods.sort()
	var total := 0.0
	var negative_count := 0
	for m in moods:
		total += m
		if m < 0.0:
			negative_count += 1

	print("avg=%.1f min=%.1f max=%.1f  negative=%d/%d" % [
		total / moods.size(), moods[0], moods[-1], negative_count, moods.size()
	])
	print("distribution: %s" % [moods])
	print("")


func _print_buildings(sim: Simulation, content: ContentRegistry) -> void:
	print("=== Buildings ===")
	var building_ids: Array = sim.entities.all_buildings()
	building_ids.sort()
	for building_id in building_ids:
		var bc: Components.BuildingComponent = sim.entities.buildings.get(building_id)
		var pos: Components.PositionComponent = sim.entities.positions.get(building_id)
		var bdef: Dictionary = content.buildings.get(bc.building_def_id, {})
		var building_name: String = bdef.get("name", "?")
		var pos_str: String = "(%d,%d)" % [pos.coord.x, pos.coord.y] if pos != null else "(?,?)"

		var rc: Components.ResourceComponent = sim.entities.resources.get(building_id)
		var res_str := ""
		if rc != null:
			var pct: float = (rc.current_amount / rc.max_amount * 100.0) if rc.max_amount > 0 else 0.0
			var flag: String = "  [LOW]" if pct < 20.0 else ""
			res_str = "  %s %.0f/%.0f (%.0f%%)%s" % [rc.resource_type, rc.current_amount, rc.max_amount, pct, flag]

		print("id=%-3d %-12s %-10s%s" % [building_id, building_name, pos_str, res_str])
	print("")
