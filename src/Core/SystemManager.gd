class_name SystemManager

# Each system must implement: func tick(sim: Simulation) -> void. No shared base class exists
# (they're otherwise unrelated), so this is duck-typed by convention rather than by interface.
var _systems: Array[Object] = []


func add(system: Object) -> void:
	_systems.append(system)


func tick_all(sim: Simulation) -> void:
	for system: Object in _systems:
		@warning_ignore("unsafe_method_access")  # duck-typed tick(sim) — see note above
		system.tick(sim)
