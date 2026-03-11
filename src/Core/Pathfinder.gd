class_name Pathfinder

# Cardinal directions only — matching C# implementation.
const DIRECTIONS: Array[Vector2i] = [
	Vector2i(0, 1),
	Vector2i(0, -1),
	Vector2i(1, 0),
	Vector2i(-1, 0),
]

# Higher values make pawns more aggressively seek low-cost paths.
const COST_MULTIPLIER: float = 3.0


# Binary min-heap keyed by float priority.
# Stores [priority: float, value: Vector2i] pairs.
class MinHeap:
	var _data: Array = []

	func push(priority: float, value: Vector2i) -> void:
		_data.append([priority, value])
		_bubble_up(_data.size() - 1)

	func pop() -> Vector2i:
		var top: Vector2i = _data[0][1]
		var last = _data.pop_back()
		if not _data.is_empty():
			_data[0] = last
			_sink_down(0)
		return top

	func is_empty() -> bool:
		return _data.is_empty()

	func _bubble_up(i: int) -> void:
		while i > 0:
			var parent: int = (i - 1) / 2
			if _data[parent][0] <= _data[i][0]:
				break
			var tmp = _data[parent]
			_data[parent] = _data[i]
			_data[i] = tmp
			i = parent

	func _sink_down(i: int) -> void:
		var n: int = _data.size()
		while true:
			var left: int = 2 * i + 1
			var right: int = 2 * i + 2
			var smallest: int = i
			if left < n and _data[left][0] < _data[smallest][0]:
				smallest = left
			if right < n and _data[right][0] < _data[smallest][0]:
				smallest = right
			if smallest == i:
				break
			var tmp = _data[i]
			_data[i] = _data[smallest]
			_data[smallest] = tmp
			i = smallest


# Returns an ordered Array[Vector2i] from start to goal, or empty array if no path.
# Path includes the start position at index 0 — ActionSystem uses this as a timing mechanism.
static func find_path(world: World, start: Vector2i, goal: Vector2i) -> Array[Vector2i]:
	var open_set := MinHeap.new()
	var came_from: Dictionary[Vector2i, Vector2i] = {}
	var g_score: Dictionary[Vector2i, float] = { start: 0.0 }

	open_set.push(_heuristic(start, goal), start)

	while not open_set.is_empty():
		var current: Vector2i = open_set.pop()

		if current == goal:
			return _reconstruct_path(came_from, current)

		for dir in DIRECTIONS:
			var neighbor: Vector2i = current + dir

			if not world.is_in_bounds(neighbor):
				continue

			var tile: World.Tile = world.get_tile(neighbor)
			if not tile.walkable:
				continue

			var move_cost: float = tile.walkability_cost * COST_MULTIPLIER
			var tentative_g: float = g_score[current] + move_cost

			if not g_score.has(neighbor) or tentative_g < g_score[neighbor]:
				came_from[neighbor] = current
				g_score[neighbor] = tentative_g
				open_set.push(tentative_g + _heuristic(neighbor, goal), neighbor)

	return []


static func _heuristic(a: Vector2i, b: Vector2i) -> float:
	return float(abs(a.x - b.x) + abs(a.y - b.y))


static func _reconstruct_path(
	came_from: Dictionary[Vector2i, Vector2i],
	current: Vector2i
) -> Array[Vector2i]:
	var path: Array[Vector2i] = [current]
	while came_from.has(current):
		current = came_from[current]
		path.push_front(current)
	return path
