class_name EntityManager

var _next_id: int = 1

# Component tables — keyed by entity id (int)
var positions: Dictionary[int, Components.PositionComponent] = {}
var pawns: Dictionary[int, Components.PawnComponent] = {}
var needs: Dictionary[int, Components.NeedsComponent] = {}
var moods: Dictionary[int, Components.MoodComponent] = {}
var buffs: Dictionary[int, Components.BuffComponent] = {}
var actions: Dictionary[int, Components.ActionComponent] = {}
var buildings: Dictionary[int, Components.BuildingComponent] = {}
var resources: Dictionary[int, Components.ResourceComponent] = {}
var attachments: Dictionary[int, Components.AttachmentComponent] = {}
var gold: Dictionary[int, Components.GoldComponent] = {}
var inventory: Dictionary[int, Components.InventoryComponent] = {}


func _create() -> int:
	var id := _next_id
	_next_id += 1
	return id


var next_id: int:
	get: return _next_id


func set_next_id(id: int) -> void:
	_next_id = id


func create_pawn(
	coord: Vector2i,
	pawn_name: String = "Pawn",
	starting_needs: Dictionary = {},
	starting_gold: int = 100
) -> int:
	var id := _create()

	var pos := Components.PositionComponent.new()
	pos.coord = coord
	positions[id] = pos

	var pawn := Components.PawnComponent.new()
	pawn.name = pawn_name
	pawns[id] = pawn

	var mood := Components.MoodComponent.new()
	moods[id] = mood

	var need_comp := Components.NeedsComponent.new()
	for k in starting_needs:
		need_comp.needs[k] = float(starting_needs[k])
	needs[id] = need_comp

	buffs[id] = Components.BuffComponent.new()
	actions[id] = Components.ActionComponent.new()

	var g := Components.GoldComponent.new()
	g.amount = starting_gold
	gold[id] = g

	inventory[id] = Components.InventoryComponent.new()

	return id


func create_building(coord: Vector2i, building_def_id: int, color_index: int) -> int:
	var id := _create()

	var pos := Components.PositionComponent.new()
	pos.coord = coord
	positions[id] = pos

	var building := Components.BuildingComponent.new()
	building.building_def_id = building_def_id
	building.color_index = color_index
	buildings[id] = building

	var g := Components.GoldComponent.new()
	gold[id] = g

	return id


func destroy(id: int) -> void:
	positions.erase(id)
	pawns.erase(id)
	needs.erase(id)
	moods.erase(id)
	buffs.erase(id)
	actions.erase(id)
	buildings.erase(id)
	resources.erase(id)
	attachments.erase(id)
	gold.erase(id)
	inventory.erase(id)


func all_pawns() -> Array[int]:
	return Array(pawns.keys(), TYPE_INT, "", null)


func all_buildings() -> Array[int]:
	return Array(buildings.keys(), TYPE_INT, "", null)
