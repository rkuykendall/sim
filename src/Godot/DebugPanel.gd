class_name DebugPanel
extends PanelContainer

enum _DisplayMode { TIME, PAWN, BUILDING }

var _debug_mode: bool = false
var _mode: _DisplayMode = _DisplayMode.TIME
var _sim: Simulation = null
var _content: ContentRegistry = null

# Time
var _current_speed: String = "1x"
var _current_time: Dictionary = {}

# UI nodes
var _main_container: VBoxContainer
var _time_container: VBoxContainer
var _pawn_container: VBoxContainer
var _building_container: VBoxContainer

var _time_label: Label
var _palette_button: Button

var _pawn_name_label: Label
var _pawn_mood_label: Label
var _pawn_gold_label: Label
var _pawn_action_label: Label
var _pawn_needs_container: VBoxContainer
var _pawn_buffs_container: VBoxContainer
var _pawn_attachments_container: VBoxContainer
var _need_bars: Dictionary = {}   # need_id -> ProgressBar
var _buff_labels: Array = []
var _pawn_attachment_labels: Array = []

var _building_name_label: Label
var _building_status_label: Label
var _building_desc_label: Label
var _building_debug_container: VBoxContainer
var _building_debug_labels: Array = []


func _ready() -> void:
	_build_ui()
	visible = false


func _build_ui() -> void:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.1, 0.1, 0.1, 0.9)
	style.set_corner_radius_all(4)
	style.set_content_margin_all(10)
	add_theme_stylebox_override("panel", style)
	custom_minimum_size = Vector2(250, 0)

	_main_container = VBoxContainer.new()
	add_child(_main_container)

	_time_container = VBoxContainer.new()
	_time_container.name = "TimeContainer"
	_main_container.add_child(_time_container)

	_time_label = Label.new()
	_time_label.add_theme_font_size_override("font_size", 20)
	_time_container.add_child(_time_label)

	_palette_button = Button.new()
	_palette_button.text = "Cycle Palette"
	_palette_button.pressed.connect(_on_palette_button_pressed)
	_time_container.add_child(_palette_button)

	_pawn_container = VBoxContainer.new()
	_pawn_container.name = "PawnContainer"
	_main_container.add_child(_pawn_container)
	_build_pawn_ui()

	_building_container = VBoxContainer.new()
	_building_container.name = "BuildingContainer"
	_main_container.add_child(_building_container)
	_build_building_ui()

	_update_container_visibility()


func _build_pawn_ui() -> void:
	_pawn_name_label = _add_label(_pawn_container, "", 20)
	_pawn_mood_label = _add_label(_pawn_container, "", 16)
	_pawn_gold_label = _add_label(_pawn_container, "", 16)
	_pawn_action_label = _add_label(_pawn_container, "", 16)

	_add_header(_pawn_container, "Needs:")
	_pawn_needs_container = VBoxContainer.new()
	_pawn_container.add_child(_pawn_needs_container)

	_add_header(_pawn_container, "Buffs:")
	_pawn_buffs_container = VBoxContainer.new()
	_pawn_container.add_child(_pawn_buffs_container)

	_add_header(_pawn_container, "Attachments:")
	_pawn_attachments_container = VBoxContainer.new()
	_pawn_container.add_child(_pawn_attachments_container)


func _build_building_ui() -> void:
	_building_name_label = _add_label(_building_container, "", 20)
	_building_status_label = _add_label(_building_container, "", 16)
	_building_desc_label = _add_label(_building_container, "", 16)

	_add_header(_building_container, "Details:")
	_building_debug_container = VBoxContainer.new()
	_building_container.add_child(_building_debug_container)


func initialize(content: ContentRegistry) -> void:
	_content = content


func set_simulation(sim: Simulation) -> void:
	_sim = sim


func set_debug_mode(enabled: bool) -> void:
	_debug_mode = enabled
	visible = _debug_mode


func update_time(time: Dictionary) -> void:
	_current_time = time
	if _mode == _DisplayMode.TIME:
		_time_label.text = "%s (%s)" % [time.get("time_string", ""), _current_speed]


func update_speed(speed: String) -> void:
	_current_speed = speed
	if _mode == _DisplayMode.TIME:
		_time_label.text = "%s (%s)" % [_current_time.get("time_string", ""), _current_speed]


func show_pawn(pawn: Dictionary, sim: Simulation) -> void:
	_sim = sim
	_mode = _DisplayMode.PAWN
	_update_container_visibility()
	visible = _debug_mode

	var pawn_id: int = pawn.get("id", -1)
	_pawn_name_label.text = sim.format_entity_id(pawn_id)

	var mood: float = pawn.get("mood", 0.0)
	_pawn_mood_label.text = "Mood: %+d" % int(mood)
	_pawn_mood_label.modulate = Color.LIME if mood > 20 else (Color.RED if mood < -20 else Color.WHITE)

	var gold: int = pawn.get("gold", 0)
	_pawn_gold_label.text = "Gold: %d" % gold
	_pawn_gold_label.modulate = Color(1, 0.84, 0) if gold >= 100 else (Color.YELLOW if gold >= 50 else (Color.WHITE if gold > 0 else Color.RED))

	_pawn_action_label.text = pawn.get("current_action", "Idle")

	_update_needs_display(pawn_id)
	_update_buffs_display(pawn_id)
	_update_pawn_attachments_display(pawn)


func show_building(building: Dictionary, sim: Simulation) -> void:
	_sim = sim
	_mode = _DisplayMode.BUILDING
	_update_container_visibility()
	visible = _debug_mode

	var building_id: int = building.get("id", -1)
	_building_name_label.text = sim.format_entity_id(building_id)

	var in_use: bool = building.get("in_use", false)
	var used_by_name: String = building.get("used_by_name", "")
	if in_use and not used_by_name.is_empty():
		_building_status_label.text = "In use by %s" % used_by_name
		_building_status_label.modulate = Color.YELLOW
	else:
		_building_status_label.text = "Available"
		_building_status_label.modulate = Color.LIME

	if _content != null:
		var bdef: Dictionary = _content.buildings.get(building.get("building_def_id", -1), {})
		if not bdef.is_empty():
			var need_name: String = "nothing"
			var need_id: int = int(bdef.get("satisfiesNeedId", -1))
			if need_id != -1:
				var ndef: Dictionary = _content.needs.get(need_id, {})
				if not ndef.is_empty():
					need_name = ndef.get("name", "nothing")
			var amount: float = float(bdef.get("needSatisfactionAmount", 0))
			_building_desc_label.text = "Satisfies: %s (+%d)" % [need_name, int(amount)]

	_update_building_debug_display(building)


func clear_selection() -> void:
	_mode = _DisplayMode.TIME
	_update_container_visibility()
	visible = _debug_mode
	_time_label.text = "%s (%s)" % [_current_time.get("time_string", ""), _current_speed]


func _on_palette_button_pressed() -> void:
	if _sim != null:
		_sim.cycle_palette()


func _update_container_visibility() -> void:
	_time_container.visible     = _mode == _DisplayMode.TIME
	_pawn_container.visible     = _mode == _DisplayMode.PAWN
	_building_container.visible = _mode == _DisplayMode.BUILDING


func _update_needs_display(pawn_id: int) -> void:
	if _content == null or _sim == null:
		return
	var need_comp: Components.NeedsComponent = _sim.entities.needs.get(pawn_id)
	if need_comp == null:
		return
	for need_id in need_comp.needs.keys():
		var ndef: Dictionary = _content.needs.get(need_id, {})
		if ndef.is_empty():
			continue
		var bar: ProgressBar
		if _need_bars.has(need_id):
			bar = _need_bars[need_id]
		else:
			bar = _create_need_bar(ndef.get("name", str(need_id)))
			_need_bars[need_id] = bar
		var value: float = need_comp.needs[need_id]
		bar.value = value
		var crit: float = float(ndef.get("criticalThreshold", 20.0))
		var low: float  = float(ndef.get("lowThreshold", 40.0))
		bar.modulate = Color.RED if value < crit else (Color.YELLOW if value < low else Color.LIME)


func _create_need_bar(need_name: String) -> ProgressBar:
	var hbox := HBoxContainer.new()
	var lbl := Label.new()
	lbl.text = need_name
	lbl.custom_minimum_size = Vector2(70, 0)
	lbl.add_theme_font_size_override("font_size", 16)
	var bar := ProgressBar.new()
	bar.custom_minimum_size = Vector2(50, 10)
	bar.max_value = 100
	bar.show_percentage = false
	hbox.add_child(lbl)
	hbox.add_child(bar)
	_pawn_needs_container.add_child(hbox)
	return bar


func _update_buffs_display(pawn_id: int) -> void:
	for lbl in _buff_labels:
		lbl.queue_free()
	_buff_labels.clear()

	if _content == null or _sim == null:
		return
	var buff_comp: Components.BuffComponent = _sim.entities.buffs.get(pawn_id)
	if buff_comp == null or buff_comp.active_buffs.is_empty():
		var lbl := _add_label(_pawn_buffs_container, "(none)", 16)
		lbl.modulate = Color.GRAY
		_buff_labels.append(lbl)
		return

	for inst in buff_comp.active_buffs:
		var buff_name: String
		match inst.source:
			Definitions.BuffSource.BUILDING:
				var bdef: Dictionary = _content.buildings.get(inst.source_id, {})
				buff_name = bdef.get("name", "Building")
			Definitions.BuffSource.WORK:
				buff_name = "Productive"
			Definitions.BuffSource.NEED_CRITICAL:
				var ndef: Dictionary = _content.needs.get(inst.source_id, {})
				buff_name = "%s (Critical)" % ndef.get("name", "Critical Need")
			Definitions.BuffSource.NEED_LOW:
				var ndef: Dictionary = _content.needs.get(inst.source_id, {})
				buff_name = "%s (Low)" % ndef.get("name", "Low Need")
			_:
				buff_name = "Unknown"
		var lbl := _add_label(_pawn_buffs_container, "%s (%+d)" % [buff_name, int(inst.mood_offset)], 16)
		lbl.modulate = Color.LIME if inst.mood_offset > 0 else (Color.ORANGE if inst.mood_offset < 0 else Color.WHITE)
		_buff_labels.append(lbl)


func _update_pawn_attachments_display(pawn: Dictionary) -> void:
	for lbl in _pawn_attachment_labels:
		lbl.queue_free()
	_pawn_attachment_labels.clear()

	var attachments: Dictionary = pawn.get("attachments", {})
	if attachments.is_empty():
		var lbl := _add_label(_pawn_attachments_container, "(none)", 14)
		lbl.modulate = Color.GRAY
		_pawn_attachment_labels.append(lbl)
		return

	for building_id in attachments.keys():
		var strength: int = attachments[building_id]
		var formatted_id: String = _sim.format_entity_id(building_id) if _sim != null else str(building_id)
		var lbl := _add_label(_pawn_attachments_container, "%s: %d/10" % [formatted_id, strength], 14)
		lbl.modulate = Color.LIME if strength >= 8 else (Color.YELLOW if strength >= 5 else Color.WHITE)
		_pawn_attachment_labels.append(lbl)


func _update_building_debug_display(building: Dictionary) -> void:
	for lbl in _building_debug_labels:
		lbl.queue_free()
	_building_debug_labels.clear()

	var gold: int = building.get("gold", 0)
	var gold_lbl := _add_label(_building_debug_container, "Gold: %d" % gold, 14)
	gold_lbl.modulate = Color(1, 0.84, 0) if gold >= 100 else (Color.YELLOW if gold >= 50 else (Color.WHITE if gold > 0 else Color.GRAY))
	_building_debug_labels.append(gold_lbl)

	var capacity: int = building.get("capacity", 1)
	var current_users: int = building.get("current_users", 0)
	var phase: int = building.get("phase", 0)
	var phase_str: String = " (phase %d)" % phase if phase > 0 else ""
	var cap_lbl := _add_label(_building_debug_container, "Capacity: %d/%d%s" % [current_users, capacity, phase_str], 14)
	cap_lbl.modulate = Color.ORANGE if current_users >= capacity else (Color.YELLOW if current_users > 0 else Color.WHITE)
	_building_debug_labels.append(cap_lbl)

	var cost: int = building.get("cost", 0)
	if cost > 0:
		var cost_lbl := _add_label(_building_debug_container, "Use: %dg" % cost, 14)
		cost_lbl.modulate = Color.CYAN
		_building_debug_labels.append(cost_lbl)

	var resource_type: String = building.get("resource_type", "")
	var current_res: float = building.get("current_resource", -1.0)
	var max_res: float = building.get("max_resource", -1.0)
	if not resource_type.is_empty() and current_res >= 0.0:
		var res_lbl := _add_label(_building_debug_container,
			"Resources: %.0f/%.0f %s" % [current_res, max_res, resource_type], 14)
		var pct: float = current_res / max_res if max_res > 0 else 0.0
		res_lbl.modulate = Color.RED if pct < 0.2 else (Color.ORANGE if pct < 0.5 else Color.LIME)
		_building_debug_labels.append(res_lbl)

		if building.get("can_be_worked_at", false):
			var buy_in: int = building.get("work_buy_in", 0)
			var payout: int = building.get("payout", 0)
			var work_text: String = "Work: %dg in / %dg out" % [buy_in, payout] if buy_in > 0 else "Work: %dg out" % payout
			var work_lbl := _add_label(_building_debug_container, work_text, 14)
			work_lbl.modulate = Color.GREEN
			_building_debug_labels.append(work_lbl)

	var attach: Dictionary = building.get("attachments", {})
	if not attach.is_empty():
		var hdr := _add_label(_building_debug_container, "Attachments:", 14)
		hdr.modulate = Color.GRAY
		_building_debug_labels.append(hdr)
		for pawn_id in attach.keys():
			var strength: int = attach[pawn_id]
			var formatted: String = _sim.format_entity_id(pawn_id) if _sim != null else str(pawn_id)
			var lbl := _add_label(_building_debug_container, "  %s: %d/10" % [formatted, strength], 14)
			lbl.modulate = Color.LIME if strength >= 8 else (Color.YELLOW if strength >= 5 else Color.WHITE)
			_building_debug_labels.append(lbl)


func _add_label(parent: Control, text: String, font_size: int) -> Label:
	var lbl := Label.new()
	lbl.text = text
	lbl.add_theme_font_size_override("font_size", font_size)
	parent.add_child(lbl)
	return lbl


func _add_header(parent: Control, text: String) -> void:
	var lbl := _add_label(parent, text, 16)
	lbl.modulate = Color.GRAY
