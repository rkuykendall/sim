extends SceneTree

## Advances a save by N in-game days and WRITES THE RESULT BACK to the same slot — used to
## fast-forward a save to its economy equilibrium (e.g. after tuning balance constants) so
## play resumes from a settled state instead of wherever the last session happened to stop.
##
## This overwrites the save. Run tools/economy_report.gd first if you just want to check
## health without touching it.
##
## Usage:
##   godot --headless --path . --script tools/advance_save.gd -- "Save 1" 100
##   godot --headless --path . --script tools/advance_save.gd -- 1 100
## Slot defaults to the most recently saved slot if omitted; days defaults to 10.

const DAY_TICKS: int = 14400
const DEFAULT_DAYS: int = 10


func _initialize() -> void:
	var content := ContentLoader.load_all()

	var args := OS.get_cmdline_user_args()
	var slot_name := _resolve_slot_name(args)
	if slot_name.is_empty():
		printerr("No saves found in %s" % SaveFileManager.SAVE_DIRECTORY)
		quit(1)
		return
	var days := int(args[1]) if args.size() > 1 and args[1].is_valid_int() else DEFAULT_DAYS

	var sim: Simulation = SaveFileManager.load_save(slot_name, content)
	if sim == null:
		printerr("Failed to load save '%s'" % slot_name)
		quit(1)
		return

	var meta: Dictionary = SaveService.read_metadata(SaveFileManager.SAVE_DIRECTORY + slot_name + ".json")
	var display_name: String = meta.get("display_name", slot_name)

	print("Advancing '%s' by %d day(s) from tick %d..." % [slot_name, days, sim.time.tick])
	for day in days:
		sim.run_ticks(DAY_TICKS)
		print("  day %d/%d done (pop=%d)" % [day + 1, days, sim.entities.all_pawns().size()])

	SaveFileManager.write_save(slot_name, sim, display_name)
	print("Saved '%s' at tick %d (day %d)" % [slot_name, sim.time.tick, sim.time.tick / DAY_TICKS + 1])

	quit()


func _resolve_slot_name(args: Array) -> String:
	if not args.is_empty():
		var arg: String = args[0]
		return "Save %s" % arg if arg.is_valid_int() else arg

	var saves: Array = SaveFileManager.get_all_saves()
	if saves.is_empty():
		return ""
	return saves[0].get("slot_name", "")
