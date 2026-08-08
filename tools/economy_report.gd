extends SceneTree

## Runs a save forward in memory (never writes it back) and prints economy health stats —
## the numbers we've been checking by hand when A/B testing balance constants like
## AISystem.HAUL_AMOUNT and ActionSystem.CONSUMER_DEPLETION_AMOUNT.
##
## Usage:
##   godot --headless --path . --script tools/economy_report.gd -- "Save 1" 40
##   godot --headless --path . --script tools/economy_report.gd -- 1 40
## Both args are optional: slot defaults to the most recently saved slot, days defaults to 40.

const DAY_TICKS: int = 14400
const DEFAULT_DAYS: int = 40


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

	var hunger_id: int = content.get_need_id("Hunger")
	var hunger_samples: Array = []
	var mood_samples: Array = []
	var critical_hunger_days := 0
	# building name -> { stock_samples: Array, empty_days: int }
	var building_stats: Dictionary = {}

	for _day in days:
		sim.run_ticks(DAY_TICKS)

		var pop: int = sim.entities.all_pawns().size()
		var hsum := 0.0
		var msum := 0.0
		var crit := 0
		for pawn_id in sim.entities.all_pawns():
			var h: float = sim.get_need_value(pawn_id, hunger_id)
			hsum += h
			msum += sim.get_mood(pawn_id)
			if h < 15.0:
				crit += 1
		hunger_samples.append(hsum / max(pop, 1))
		mood_samples.append(msum / max(pop, 1))
		if crit > 0:
			critical_hunger_days += 1

		var seen_this_day: Dictionary = {}
		for building_id in sim.entities.all_buildings():
			var rc: Components.ResourceComponent = sim.entities.resources.get(building_id)
			if rc == null:
				continue
			var bc: Components.BuildingComponent = sim.entities.buildings.get(building_id)
			var bdef: Dictionary = content.buildings.get(bc.building_def_id, {})
			var name: String = bdef.get("name", "?")
			if not building_stats.has(name):
				building_stats[name] = {"stock_samples": [], "empty_days": 0}
			building_stats[name]["stock_samples"].append(rc.current_amount)
			if rc.current_amount <= 0.0:
				seen_this_day[name] = true
		for name in seen_this_day:
			building_stats[name]["empty_days"] += 1

	print("=== %s: %d-day economy report (pop=%d) ===" % [slot_name, days, sim.entities.all_pawns().size()])
	print("avgHunger  mean=%.1f  min=%.1f  max=%.1f" % [_mean(hunger_samples), hunger_samples.min(), hunger_samples.max()])
	print("avgMood    mean=%.1f  min=%.1f  max=%.1f" % [_mean(mood_samples), mood_samples.min(), mood_samples.max()])
	print("criticalHungerDays=%d/%d" % [critical_hunger_days, days])
	print("")
	print("=== Building stock (per building-type, summed across instances sampled daily) ===")
	for name in building_stats:
		var stats: Dictionary = building_stats[name]
		var samples: Array = stats["stock_samples"]
		print("%-12s mean=%6.1f  min=%6.1f  max=%6.1f  emptyDays=%d/%d" % [
			name, _mean(samples), samples.min(), samples.max(), stats["empty_days"], days
		])

	quit()


func _resolve_slot_name(args: Array) -> String:
	if not args.is_empty():
		var arg: String = args[0]
		return "Save %s" % arg if arg.is_valid_int() else arg

	var saves: Array = SaveFileManager.get_all_saves()
	if saves.is_empty():
		return ""
	return saves[0].get("slot_name", "")


func _mean(values: Array) -> float:
	if values.is_empty():
		return 0.0
	var total := 0.0
	for v in values:
		total += v
	return total / values.size()
