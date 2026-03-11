class_name SystemManager

# Each system must implement: func tick(sim: Simulation) -> void
var _systems: Array = []


func add(system) -> void:
	_systems.append(system)


func tick_all(sim: Simulation) -> void:
	for system in _systems:
		system.tick(sim)
