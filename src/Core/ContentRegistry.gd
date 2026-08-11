class_name ContentRegistry

# Each store maps:
#   name (String) -> id (int)
#   id (int)      -> def (Dictionary)

var palettes: Dictionary[int, Dictionary] = {}  # id -> { name, colors: Array }
var palette_ids: Dictionary[String, int] = {}  # name -> id

var needs: Dictionary[int, Dictionary] = {}  # id -> NeedDef dict
var need_ids: Dictionary[String, int] = {}  # name -> id

var terrains: Dictionary[int, Dictionary] = {}  # id -> TerrainDef dict
var terrain_ids: Dictionary[String, int] = {}  # name -> id

var buildings: Dictionary[int, Dictionary] = {}  # id -> BuildingDef dict
var building_ids: Dictionary[String, int] = {}  # name -> id

var _next_id: int = 1


func _alloc_id() -> int:
	var id := _next_id
	_next_id += 1
	return id


func register_palette(key: String, def: Dictionary) -> int:
	var id := _alloc_id()
	def["id"] = id
	def["name"] = key
	palettes[id] = def
	palette_ids[key] = id
	return id


func register_need(key: String, def: Dictionary) -> int:
	var id := _alloc_id()
	def["id"] = id
	needs[id] = def
	need_ids[key] = id
	return id


func register_terrain(key: String, def: Dictionary) -> int:
	var id := _alloc_id()
	def["id"] = id
	terrains[id] = def
	terrain_ids[key] = id
	return id


func register_building(key: String, def: Dictionary) -> int:
	var id := _alloc_id()
	def["id"] = id
	def["name"] = key
	buildings[id] = def
	building_ids[key] = id
	return id


func get_need_id(name: String) -> int:
	return need_ids.get(name, -1)


func get_terrain_id(name: String) -> int:
	return terrain_ids.get(name, -1)


func get_building_id(name: String) -> int:
	return building_ids.get(name, -1)


func get_palette_id(name: String) -> int:
	return palette_ids.get(name, -1)
