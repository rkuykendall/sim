class_name SaveService

const SAVE_VERSION: int = 2


# ---------------------------------------------------------------------------
# File I/O
# ---------------------------------------------------------------------------

static func save(sim: Simulation, path: String, save_name: String = "") -> void:
	var data: Dictionary = to_dict(sim, save_name)
	var json: String = JSON.stringify(data)
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		push_error("SaveService.save: cannot open '%s' for writing (error %d)" % [path, FileAccess.get_open_error()])
		return
	file.store_string(json)


# Returns a restored Simulation, or null on failure.
static func load_file(path: String, content: ContentRegistry) -> Simulation:
	if not FileAccess.file_exists(path):
		push_error("SaveService.load_file: file not found: %s" % path)
		return null

	var text: String = FileAccess.get_file_as_string(path)
	var data = JSON.parse_string(text)
	if data == null or not data is Dictionary:
		push_error("SaveService.load_file: failed to parse JSON: %s" % path)
		return null

	if int(data.get("version", 0)) != SAVE_VERSION:
		push_error("SaveService.load_file: incompatible save version in '%s' (expected %d)" % [path, SAVE_VERSION])
		return null

	return from_dict(data, content)


# Lightweight metadata read (no full parse needed, just top-level keys).
static func read_metadata(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	var text: String = FileAccess.get_file_as_string(path)
	var data = JSON.parse_string(text)
	if data == null or not data is Dictionary:
		return {}

	var current_tick: int = int(data.get("current_tick", 0))
	var day: int = (current_tick / TimeService.TICKS_PER_DAY) + 1
	var entities: Array = data.get("entities", [])
	var pawn_count: int = 0
	for e in entities:
		if e is Dictionary and e.get("type", "") == "Pawn":
			pawn_count += 1

	return {
		"version":      int(data.get("version", 0)),
		"display_name": data.get("name", ""),
		"saved_at":     data.get("saved_at", ""),
		"day":          day,
		"pawn_count":   pawn_count,
	}


# ---------------------------------------------------------------------------
# Serialization
# ---------------------------------------------------------------------------

static func to_dict(sim: Simulation, save_name: String = "") -> Dictionary:
	return {
		"version":            SAVE_VERSION,
		"name":               save_name,
		"saved_at":           Time.get_datetime_string_from_system(false, true),
		"seed":               sim.sim_seed,
		"current_tick":       sim.time.tick,
		"selected_palette_id": sim.selected_palette_id,
		"palette":            sim.palette.map(func(c: Color) -> String: return "#" + c.to_html(false)),
		"next_entity_id":     sim.entities.next_id,
		"tax_pool":           sim.tax_pool,
		"world":              _serialize_world(sim.world),
		"entities":           _serialize_entities(sim),
	}


static func _serialize_world(world: World) -> Dictionary:
	var tiles: Array = []
	for x in world.width:
		for y in world.height:
			var tile: World.Tile = world.get_tile_xy(x, y)
			var t: Dictionary = {
				"x": x,
				"y": y,
				"base_terrain_type_id":    tile.base_terrain_type_id,
				"base_variant_index":      tile.base_variant_index,
				"color_index":             tile.color_index,
				"walkability_cost":        tile.walkability_cost,
				"blocks_light":            tile.blocks_light,
				"building_blocks_movement": tile.building_blocks_movement,
			}
			# Only write overlay fields when set (saves space)
			if tile.overlay_terrain_type_id != -1:
				t["overlay_terrain_type_id"] = tile.overlay_terrain_type_id
				t["overlay_variant_index"]   = tile.overlay_variant_index
				t["overlay_color_index"]     = tile.overlay_color_index
			tiles.append(t)

	return {
		"width":  world.width,
		"height": world.height,
		"tiles":  tiles,
	}


static func _serialize_entities(sim: Simulation) -> Array:
	var out: Array = []
	var em: EntityManager = sim.entities

	# Pawns
	for pawn_id in em.all_pawns():
		var e: Dictionary = { "id": pawn_id, "type": "Pawn" }

		var pos: Components.PositionComponent = em.positions.get(pawn_id)
		if pos != null:
			e["x"] = pos.coord.x
			e["y"] = pos.coord.y

		var pawn: Components.PawnComponent = em.pawns.get(pawn_id)
		if pawn != null:
			e["name"] = pawn.name

		var need_comp: Components.NeedsComponent = em.needs.get(pawn_id)
		if need_comp != null:
			# JSON keys must be strings; store need_id as string int
			var needs_dict: Dictionary = {}
			for need_id in need_comp.needs:
				needs_dict[str(need_id)] = need_comp.needs[need_id]
			e["needs"] = needs_dict

		var mood: Components.MoodComponent = em.moods.get(pawn_id)
		if mood != null:
			e["mood"] = mood.mood

		var buff_comp: Components.BuffComponent = em.buffs.get(pawn_id)
		if buff_comp != null:
			var buffs: Array = []
			for b in buff_comp.active_buffs:
				buffs.append({
					"source":      int(b.source),
					"source_id":   b.source_id,
					"mood_offset": b.mood_offset,
					"start_tick":  b.start_tick,
					"end_tick":    b.end_tick,
				})
			e["buffs"] = buffs

		var gold: Components.GoldComponent = em.gold.get(pawn_id)
		if gold != null:
			e["gold"] = gold.amount

		var inv: Components.InventoryComponent = em.inventory.get(pawn_id)
		if inv != null:
			e["inventory"] = {
				"resource_type": inv.resource_type,
				"amount":        inv.amount,
				"max_amount":    inv.max_amount,
			}

		out.append(e)

	# Buildings
	for building_id in em.all_buildings():
		var e: Dictionary = { "id": building_id, "type": "Building" }

		var pos: Components.PositionComponent = em.positions.get(building_id)
		if pos != null:
			e["x"] = pos.coord.x
			e["y"] = pos.coord.y

		var bc: Components.BuildingComponent = em.buildings.get(building_id)
		if bc != null:
			# Save name (stable) + id (backward compat)
			var bdef: Dictionary = sim.content.buildings.get(bc.building_def_id, {})
			e["building_def_name"] = bdef.get("name", "")
			e["building_def_id"]   = bc.building_def_id
			e["building_color_index"] = bc.color_index

		var res: Components.ResourceComponent = em.resources.get(building_id)
		if res != null:
			e["resource"] = {
				"resource_type":   res.resource_type,
				"current_amount":  res.current_amount,
				"max_amount":      res.max_amount,
				"depletion_mult":  res.depletion_mult,
			}

		var ac: Components.AttachmentComponent = em.attachments.get(building_id)
		if ac != null:
			var attachments: Dictionary = {}
			for pawn_id in ac.user_attachments:
				attachments[str(pawn_id)] = ac.user_attachments[pawn_id]
			e["attachments"] = attachments

		var gold: Components.GoldComponent = em.gold.get(building_id)
		if gold != null:
			e["building_gold"] = gold.amount

		out.append(e)

	return out


# ---------------------------------------------------------------------------
# Deserialization
# ---------------------------------------------------------------------------

static func from_dict(data: Dictionary, content: ContentRegistry) -> Simulation:
	var world_data: Dictionary = data.get("world", {})
	var world_width: int  = int(world_data.get("width",  World.DEFAULT_WIDTH))
	var world_height: int = int(world_data.get("height", World.DEFAULT_HEIGHT))

	var sim := Simulation.new(
		content,
		int(data.get("seed", -1)),
		TimeService.DEFAULT_START_HOUR,
		world_width,
		world_height,
		false,   # themes enabled
		Simulation.DEFAULT_TAX_MULTIPLIER
	)

	# Restore world tiles (overwrites auto-initialized terrain)
	_restore_world(sim, world_data)

	# Restore entities — buildings first (pawns may share IDs referencing them)
	var entities: Array = data.get("entities", [])
	for e in entities:
		if e is Dictionary and e.get("type", "") == "Building":
			_restore_building(sim, e)
	for e in entities:
		if e is Dictionary and e.get("type", "") == "Pawn":
			_restore_pawn(sim, e)

	# Restore simulation-level state
	sim.entities.set_next_id(int(data.get("next_entity_id", 1)))
	sim.time.set_tick(int(data.get("current_tick", 0)))
	sim.tax_pool = int(data.get("tax_pool", 0))
	sim.selected_palette_id = int(data.get("selected_palette_id", -1))

	var palette_hexes: Array = data.get("palette", [])
	if not palette_hexes.is_empty():
		var restored_palette: Array[Color] = []
		for hex in palette_hexes:
			restored_palette.append(Color(hex))
		sim.palette = restored_palette

	return sim


static func _restore_world(sim: Simulation, world_data: Dictionary) -> void:
	var tiles: Array = world_data.get("tiles", [])
	for t in tiles:
		if not t is Dictionary:
			continue
		var x: int = int(t.get("x", 0))
		var y: int = int(t.get("y", 0))
		if not sim.world.is_in_bounds(Vector2i(x, y)):
			continue

		var tile: World.Tile = sim.world.get_tile_xy(x, y)
		tile.base_terrain_type_id = int(t.get("base_terrain_type_id", -1))
		tile.base_variant_index   = int(t.get("base_variant_index", 0))
		tile.color_index          = int(t.get("color_index", 0))
		tile.walkability_cost     = float(t.get("walkability_cost", 1.0))
		tile.blocks_light         = bool(t.get("blocks_light", false))
		tile.building_blocks_movement = bool(t.get("building_blocks_movement", false))

		# Overlay (optional)
		if t.has("overlay_terrain_type_id"):
			tile.overlay_terrain_type_id = int(t["overlay_terrain_type_id"])
			tile.overlay_variant_index   = int(t.get("overlay_variant_index", 0))
			tile.overlay_color_index     = int(t.get("overlay_color_index", 0))
		else:
			tile.overlay_terrain_type_id = -1


static func _restore_building(sim: Simulation, e: Dictionary) -> void:
	var entity_id: int = int(e.get("id", -1))
	if entity_id == -1:
		return

	var coord := Vector2i(int(e.get("x", 0)), int(e.get("y", 0)))

	# Prefer name-based lookup (stable across content reloads), fall back to saved ID
	var building_def_name: String = e.get("building_def_name", "")
	var building_def_id: int
	if not building_def_name.is_empty():
		building_def_id = sim.content.get_building_id(building_def_name)
	else:
		building_def_id = int(e.get("building_def_id", -1))

	if building_def_id == -1:
		push_warning("SaveService: unknown building '%s' — skipping entity %d" % [building_def_name, entity_id])
		return

	var pos := Components.PositionComponent.new()
	pos.coord = coord
	sim.entities.positions[entity_id] = pos

	var bc := Components.BuildingComponent.new()
	bc.building_def_id = building_def_id
	bc.color_index = int(e.get("building_color_index", 0))
	sim.entities.buildings[entity_id] = bc

	var gold := Components.GoldComponent.new()
	gold.amount = int(e.get("building_gold", 0))
	sim.entities.gold[entity_id] = gold

	# Resource component
	if e.has("resource"):
		var r: Dictionary = e["resource"]
		var rc := Components.ResourceComponent.new()
		rc.resource_type   = r.get("resource_type", "")
		rc.current_amount  = float(r.get("current_amount", 0.0))
		rc.max_amount      = float(r.get("max_amount", 100.0))
		rc.depletion_mult  = float(r.get("depletion_mult", 1.0))
		sim.entities.resources[entity_id] = rc
	else:
		# Legacy save compat: init from building def if it has a resource type
		var bdef: Dictionary = sim.content.buildings.get(building_def_id, {})
		var resource_type: String = bdef.get("resourceType", "")
		if not resource_type.is_empty():
			var max_amount: float = float(bdef.get("maxResourceAmount", 100.0))
			var rc := Components.ResourceComponent.new()
			rc.resource_type  = resource_type
			rc.current_amount = max_amount
			rc.max_amount     = max_amount
			rc.depletion_mult = float(bdef.get("depletionMult", 1.0))
			sim.entities.resources[entity_id] = rc

	# Attachment component
	var ac := Components.AttachmentComponent.new()
	if e.has("attachments"):
		var att: Dictionary = e["attachments"]
		for key in att:
			ac.user_attachments[int(key)] = int(att[key])
	sim.entities.attachments[entity_id] = ac


static func _restore_pawn(sim: Simulation, e: Dictionary) -> void:
	var entity_id: int = int(e.get("id", -1))
	if entity_id == -1:
		return

	var coord := Vector2i(int(e.get("x", 0)), int(e.get("y", 0)))

	var pos := Components.PositionComponent.new()
	pos.coord = coord
	sim.entities.positions[entity_id] = pos

	var pawn := Components.PawnComponent.new()
	pawn.name = e.get("name", "Pawn")
	sim.entities.pawns[entity_id] = pawn

	var need_comp := Components.NeedsComponent.new()
	if e.has("needs"):
		var raw: Dictionary = e["needs"]
		for key in raw:
			need_comp.needs[int(key)] = float(raw[key])
	sim.entities.needs[entity_id] = need_comp

	var mood := Components.MoodComponent.new()
	mood.mood = float(e.get("mood", 0.0))
	sim.entities.moods[entity_id] = mood

	var gold := Components.GoldComponent.new()
	gold.amount = int(e.get("gold", 0))
	sim.entities.gold[entity_id] = gold

	var buff_comp := Components.BuffComponent.new()
	if e.has("buffs"):
		for bdata in e["buffs"]:
			if not bdata is Dictionary:
				continue
			var b := Definitions.BuffInstance.new()
			b.source      = int(bdata.get("source", 0)) as Definitions.BuffSource
			b.source_id   = int(bdata.get("source_id", 0))
			b.mood_offset = float(bdata.get("mood_offset", 0.0))
			b.start_tick  = int(bdata.get("start_tick", 0))
			b.end_tick    = int(bdata.get("end_tick", -1))
			buff_comp.active_buffs.append(b)
	sim.entities.buffs[entity_id] = buff_comp

	# Clear action state — pawn will re-decide on next tick
	sim.entities.actions[entity_id] = Components.ActionComponent.new()

	var inv := Components.InventoryComponent.new()
	if e.has("inventory"):
		var inv_data: Dictionary = e["inventory"]
		inv.resource_type = inv_data.get("resource_type", "")
		inv.amount        = float(inv_data.get("amount", 0.0))
		inv.max_amount    = float(inv_data.get("max_amount", 0.0))
	sim.entities.inventory[entity_id] = inv
