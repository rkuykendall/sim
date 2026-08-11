class_name SaveService

const SAVE_VERSION: int = 3

# ---------------------------------------------------------------------------
# File I/O
# ---------------------------------------------------------------------------


static func save(sim: Simulation, path: String, save_name: String = "") -> void:
	var data: Dictionary = to_dict(sim, save_name)
	var json: String = JSON.stringify(data)
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		push_error(
			(
				"SaveService.save: cannot open '%s' for writing (error %d)"
				% [path, FileAccess.get_open_error()]
			)
		)
		return
	file.store_string(json)


# Returns a restored Simulation, or null on failure.
static func load_file(path: String, content: ContentRegistry) -> Simulation:
	if not FileAccess.file_exists(path):
		push_error("SaveService.load_file: file not found: %s" % path)
		return null

	var text: String = FileAccess.get_file_as_string(path)
	var parsed: Variant = JSON.parse_string(text)
	if parsed == null or not parsed is Dictionary:
		push_error("SaveService.load_file: failed to parse JSON: %s" % path)
		return null
	var data: Dictionary = parsed

	if DefUtils.get_int(data, "version", 0) != SAVE_VERSION:
		push_error(
			(
				"SaveService.load_file: incompatible save version in '%s' (expected %d)"
				% [path, SAVE_VERSION]
			)
		)
		return null

	return from_dict(data, content)


# Lightweight metadata read (no full parse needed, just top-level keys).
static func read_metadata(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	var text: String = FileAccess.get_file_as_string(path)
	var parsed: Variant = JSON.parse_string(text)
	if parsed == null or not parsed is Dictionary:
		return {}
	var data: Dictionary = parsed

	var current_tick: int = DefUtils.get_int(data, "current_tick", 0)
	var day: int = (current_tick / TimeService.TICKS_PER_DAY) + 1
	var entities: Array = DefUtils.get_array(data, "entities", [])
	var pawn_count: int = 0
	for e: Variant in entities:
		if not e is Dictionary:
			continue
		var ed: Dictionary = e
		if DefUtils.get_string(ed, "type", "") == "Pawn":
			pawn_count += 1

	return {
		"version": DefUtils.get_int(data, "version", 0),
		"display_name": DefUtils.get_string(data, "name", ""),
		"saved_at": DefUtils.get_string(data, "saved_at", ""),
		"day": day,
		"pawn_count": pawn_count,
	}


# ---------------------------------------------------------------------------
# Serialization
# ---------------------------------------------------------------------------


static func to_dict(sim: Simulation, save_name: String = "") -> Dictionary:
	return {
		"version": SAVE_VERSION,
		"name": save_name,
		"saved_at": Time.get_datetime_string_from_system(false, true),
		"seed": sim.sim_seed,
		"current_tick": sim.time.tick,
		"selected_palette_id": sim.selected_palette_id,
		"palette": sim.palette.map(func(c: Color) -> String: return "#" + c.to_html(false)),
		"next_entity_id": sim.entities.next_id,
		"world": _serialize_world(sim.world),
		"entities": _serialize_entities(sim),
		"current_theme_name":
		sim.theme_system.current_theme.get_name() if sim.theme_system.current_theme != null else "",
		"current_theme_start_tick": sim.theme_system.get_current_theme_start_tick(),
	}


static func _serialize_world(world: World) -> Dictionary:
	var tiles: Array = []
	for x in world.width:
		for y in world.height:
			var tile: World.Tile = world.get_tile_xy(x, y)
			var t: Dictionary = {
				"x": x,
				"y": y,
				"base_terrain_type_id": tile.base_terrain_type_id,
				"base_variant_index": tile.base_variant_index,
				"color_index": tile.color_index,
				"walkability_cost": tile.walkability_cost,
				"blocks_light": tile.blocks_light,
				"building_blocks_movement": tile.building_blocks_movement,
			}
			# Only write overlay fields when set (saves space)
			if tile.overlay_terrain_type_id != -1:
				t["overlay_terrain_type_id"] = tile.overlay_terrain_type_id
				t["overlay_variant_index"] = tile.overlay_variant_index
				t["overlay_color_index"] = tile.overlay_color_index
			tiles.append(t)

	return {
		"width": world.width,
		"height": world.height,
		"tiles": tiles,
	}


static func _serialize_entities(sim: Simulation) -> Array:
	var out: Array = []
	var em: EntityManager = sim.entities

	# Pawns
	for pawn_id in em.all_pawns():
		var e: Dictionary = {"id": pawn_id, "type": "Pawn"}

		var pos: Components.PositionComponent = em.positions.get(pawn_id)
		if pos != null:
			e["x"] = pos.coord.x
			e["y"] = pos.coord.y

		var pawn: Components.PawnComponent = em.pawns.get(pawn_id)
		if pawn != null:
			e["name"] = pawn.name
			e["membership"] = int(pawn.membership)
			e["forced_sheet_key"] = pawn.forced_sheet_key

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

		var inv: Components.InventoryComponent = em.inventory.get(pawn_id)
		if inv != null:
			e["inventory"] = {
				"resource_type": inv.resource_type,
				"amount": inv.amount,
				"max_amount": inv.max_amount,
			}

		out.append(e)

	# Buildings
	for building_id in em.all_buildings():
		var e: Dictionary = {"id": building_id, "type": "Building"}

		var pos: Components.PositionComponent = em.positions.get(building_id)
		if pos != null:
			e["x"] = pos.coord.x
			e["y"] = pos.coord.y

		var bc: Components.BuildingComponent = em.buildings.get(building_id)
		var bdef: Dictionary = {}
		if bc != null:
			# Save name (stable) + id (backward compat)
			bdef = sim.content.buildings.get(bc.building_def_id, {})
			e["building_def_name"] = bdef.get("name", "")
			e["building_def_id"] = bc.building_def_id
			e["building_color_index"] = bc.color_index
			e["skin_override"] = bc.skin_override

		# Only persist a resource block if the building's CURRENT content definition still
		# calls for one. A stale ResourceComponent can linger in memory after a content change
		# drops a building's resourceType (e.g. loaded from an older save) — saving is the
		# strict side of this pair (loading stays lenient for backward compat), so writing the
		# save back out is what lets data self-correct to match current content, rather than
		# propagating vestigial fields forward indefinitely.
		var res: Components.ResourceComponent = em.resources.get(building_id)
		if res != null and not DefUtils.get_string(bdef, "resourceType", "").is_empty():
			e["resource"] = {
				"resource_type": res.resource_type,
				"current_amount": res.current_amount,
				"max_amount": res.max_amount,
				"depletion_mult": res.depletion_mult,
			}

		var ac: Components.AttachmentComponent = em.attachments.get(building_id)
		if ac != null:
			var attachments: Dictionary = {}
			for need_id in ac.need_attachments:
				var per_pawn: Dictionary = ac.need_attachments[need_id]
				var per_pawn_out: Dictionary = {}
				for pawn_id: int in per_pawn:
					per_pawn_out[str(pawn_id)] = per_pawn[pawn_id]
				attachments[str(need_id)] = per_pawn_out
			e["attachments"] = attachments

		out.append(e)

	return out


# ---------------------------------------------------------------------------
# Deserialization
# ---------------------------------------------------------------------------


static func from_dict(data: Dictionary, content: ContentRegistry) -> Simulation:
	var world_data: Dictionary = DefUtils.get_dict(data, "world", {})
	var world_width: int = DefUtils.get_int(world_data, "width", World.DEFAULT_WIDTH)
	var world_height: int = DefUtils.get_int(world_data, "height", World.DEFAULT_HEIGHT)

	var sim := Simulation.new(
		content,
		DefUtils.get_int(data, "seed", -1),
		TimeService.DEFAULT_START_HOUR,
		world_width,
		world_height,
		false  # themes enabled
	)

	# Restore world tiles (overwrites auto-initialized terrain)
	_restore_world(sim, world_data)

	# Restore entities — buildings first (pawns may share IDs referencing them)
	var entities: Array = DefUtils.get_array(data, "entities", [])
	for e: Variant in entities:
		if not e is Dictionary:
			continue
		var ed: Dictionary = e
		if DefUtils.get_string(ed, "type", "") == "Building":
			_restore_building(sim, ed)
	for e: Variant in entities:
		if not e is Dictionary:
			continue
		var ed: Dictionary = e
		if DefUtils.get_string(ed, "type", "") == "Pawn":
			_restore_pawn(sim, ed)

	# Restore simulation-level state
	sim.entities.set_next_id(DefUtils.get_int(data, "next_entity_id", 1))
	sim.time.set_tick(DefUtils.get_int(data, "current_tick", 0))
	sim.selected_palette_id = DefUtils.get_int(data, "selected_palette_id", -1)

	# Additive/optional — older saves without this just fall through to picking a fresh theme
	# on the next tick, same as any new game. Without this, a theme's visitors/skin overrides
	# (already correctly restored above as plain pawn/building data) would be orphaned under
	# whatever unrelated theme gets randomly picked next.
	sim.theme_system.restore_current_theme(
		DefUtils.get_string(data, "current_theme_name", ""),
		DefUtils.get_int(data, "current_theme_start_tick", sim.time.tick)
	)

	var palette_hexes: Array = DefUtils.get_array(data, "palette", [])
	if not palette_hexes.is_empty():
		var restored_palette: Array[Color] = []
		for hex: String in palette_hexes:
			restored_palette.append(Color(hex))
		sim.palette = restored_palette

	return sim


static func _restore_world(sim: Simulation, world_data: Dictionary) -> void:
	var tiles: Array = DefUtils.get_array(world_data, "tiles", [])
	for t: Variant in tiles:
		if not t is Dictionary:
			continue
		var td: Dictionary = t
		var x: int = DefUtils.get_int(td, "x", 0)
		var y: int = DefUtils.get_int(td, "y", 0)
		if not sim.world.is_in_bounds(Vector2i(x, y)):
			continue

		var tile: World.Tile = sim.world.get_tile_xy(x, y)
		tile.base_terrain_type_id = DefUtils.get_int(td, "base_terrain_type_id", -1)
		tile.base_variant_index = DefUtils.get_int(td, "base_variant_index", 0)
		tile.color_index = DefUtils.get_int(td, "color_index", 0)
		tile.walkability_cost = DefUtils.get_float(td, "walkability_cost", 1.0)
		tile.blocks_light = DefUtils.get_bool(td, "blocks_light", false)
		tile.building_blocks_movement = DefUtils.get_bool(td, "building_blocks_movement", false)

		# Overlay (optional)
		if td.has("overlay_terrain_type_id"):
			tile.overlay_terrain_type_id = DefUtils.get_int(td, "overlay_terrain_type_id", -1)
			tile.overlay_variant_index = DefUtils.get_int(td, "overlay_variant_index", 0)
			tile.overlay_color_index = DefUtils.get_int(td, "overlay_color_index", 0)
		else:
			tile.overlay_terrain_type_id = -1


static func _restore_building(sim: Simulation, e: Dictionary) -> void:
	var entity_id: int = DefUtils.get_int(e, "id", -1)
	if entity_id == -1:
		return

	var coord := Vector2i(DefUtils.get_int(e, "x", 0), DefUtils.get_int(e, "y", 0))

	# Prefer name-based lookup (stable across content reloads), fall back to saved ID
	var building_def_name: String = DefUtils.get_string(e, "building_def_name", "")
	var building_def_id: int
	if not building_def_name.is_empty():
		building_def_id = sim.content.get_building_id(building_def_name)
	else:
		building_def_id = DefUtils.get_int(e, "building_def_id", -1)

	if building_def_id == -1:
		push_warning(
			(
				"SaveService: unknown building '%s' — skipping entity %d"
				% [building_def_name, entity_id]
			)
		)
		return

	var pos := Components.PositionComponent.new()
	pos.coord = coord
	sim.entities.positions[entity_id] = pos

	var bc := Components.BuildingComponent.new()
	bc.building_def_id = building_def_id
	bc.color_index = DefUtils.get_int(e, "building_color_index", 0)
	bc.skin_override = DefUtils.get_string(e, "skin_override", "")
	sim.entities.buildings[entity_id] = bc

	# Resource component
	if e.has("resource"):
		var r: Dictionary = DefUtils.get_dict(e, "resource", {})
		var rc := Components.ResourceComponent.new()
		rc.resource_type = DefUtils.get_string(r, "resource_type", "")
		rc.current_amount = DefUtils.get_float(r, "current_amount", 0.0)
		rc.max_amount = DefUtils.get_float(r, "max_amount", 100.0)
		rc.depletion_mult = DefUtils.get_float(r, "depletion_mult", 1.0)
		sim.entities.resources[entity_id] = rc
	else:
		# Legacy save compat: init from building def if it has a resource type
		var bdef: Dictionary = sim.content.buildings.get(building_def_id, {})
		var resource_type: String = DefUtils.get_string(bdef, "resourceType", "")
		if not resource_type.is_empty():
			var max_amount: float = DefUtils.get_float(bdef, "maxResourceAmount", 100.0)
			var rc := Components.ResourceComponent.new()
			rc.resource_type = resource_type
			rc.current_amount = max_amount
			rc.max_amount = max_amount
			rc.depletion_mult = DefUtils.get_float(bdef, "depletionMult", 1.0)
			sim.entities.resources[entity_id] = rc

	# Attachment component. Values are per-need pawn maps ({"need_id": {"pawn_id": strength}}).
	# Older saves stored a flat {"pawn_id": strength} with no need context — that data can't be
	# attributed to a need, so it's dropped rather than guessed; attachments rebuild through play.
	var ac := Components.AttachmentComponent.new()
	if e.has("attachments"):
		var att: Dictionary = DefUtils.get_dict(e, "attachments", {})
		for key: String in att:
			var value: Variant = att[key]
			if value is Dictionary:
				var per_pawn_data: Dictionary = value
				var need_id: int = int(key)
				var per_pawn_out: Dictionary = {}
				for pawn_key: String in per_pawn_data:
					per_pawn_out[int(pawn_key)] = DefUtils.get_int(per_pawn_data, pawn_key, 0)
				ac.need_attachments[need_id] = per_pawn_out
	sim.entities.attachments[entity_id] = ac


static func _restore_pawn(sim: Simulation, e: Dictionary) -> void:
	var entity_id: int = DefUtils.get_int(e, "id", -1)
	if entity_id == -1:
		return

	var coord := Vector2i(DefUtils.get_int(e, "x", 0), DefUtils.get_int(e, "y", 0))

	var pos := Components.PositionComponent.new()
	pos.coord = coord
	sim.entities.positions[entity_id] = pos

	var pawn := Components.PawnComponent.new()
	pawn.name = DefUtils.get_string(e, "name", "Pawn")
	# Additive/optional — old saves never had visitors, so defaulting to COLONIST/"" is correct.
	pawn.membership = DefUtils.get_int(e, "membership", 0) as Definitions.PawnMembership
	pawn.forced_sheet_key = DefUtils.get_string(e, "forced_sheet_key", "")
	sim.entities.pawns[entity_id] = pawn

	var need_comp := Components.NeedsComponent.new()
	if e.has("needs"):
		var raw: Dictionary = DefUtils.get_dict(e, "needs", {})
		for key: String in raw:
			need_comp.needs[int(key)] = DefUtils.get_float(raw, key, 0.0)
	sim.entities.needs[entity_id] = need_comp

	var mood := Components.MoodComponent.new()
	mood.mood = DefUtils.get_float(e, "mood", 0.0)
	sim.entities.moods[entity_id] = mood

	# Clear action state — pawn will re-decide on next tick
	sim.entities.actions[entity_id] = Components.ActionComponent.new()

	var inv := Components.InventoryComponent.new()
	if e.has("inventory"):
		var inv_data: Dictionary = DefUtils.get_dict(e, "inventory", {})
		inv.resource_type = DefUtils.get_string(inv_data, "resource_type", "")
		inv.amount = DefUtils.get_float(inv_data, "amount", 0.0)
		inv.max_amount = DefUtils.get_float(inv_data, "max_amount", 0.0)
	sim.entities.inventory[entity_id] = inv
