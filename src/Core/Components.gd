class_name Components


class PositionComponent:
	var coord: Vector2i


class PawnComponent:
	var name: String = ""
	var membership: Definitions.PawnMembership = Definitions.PawnMembership.COLONIST
	var forced_sheet_key: String = ""  # "" = no per-pawn forced sheet (orthogonal to membership)


class NeedsComponent:
	# need_id (int) -> value (float) 0..100
	var needs: Dictionary[int, float] = {}


class MoodComponent:
	var mood: float = 0.0  # -100..100


class BuffComponent:
	var active_buffs: Array[Definitions.BuffInstance] = []


class ActionComponent:
	var current_action: Definitions.ActionDef = null
	var action_start_tick: int = 0
	var action_queue: Array[Definitions.ActionDef] = []
	var current_path: Array[Vector2i] = []
	var path_index: int = 0


class BuildingComponent:
	var building_def_id: int = -1
	var color_index: int = 0
	# Sheet key (e.g. "character_7_v2") this building hands out to any colonist who calls it
	# home, in place of the deterministic per-house default (see CharacterSheetPool.get_sheet_
	# for_house) — "" means no override. Only meaningful on isHome buildings, but harmless
	# otherwise. Set via Simulation.set_building_skin_override; themes are the expected caller
	# (see Theme.gd's StrangeWorldsTheme), but any future system can drive it the same way.
	var skin_override: String = ""

	# Is any pawn currently targeting this building?
	# Computed from pawn ActionComponents — not stored.
	func in_use(entities: EntityManager, building_id: int) -> bool:
		for pawn_id in entities.all_pawns():
			var action_comp: Components.ActionComponent = entities.actions.get(pawn_id)
			if action_comp == null:
				continue
			if action_comp.current_action != null and action_comp.current_action.target_entity == building_id:
				return true
			for queued in action_comp.action_queue:
				if queued.target_entity == building_id:
					return true
		return false

	# Which pawn is currently using this building (-1 if none)?
	func used_by(entities: EntityManager, building_id: int) -> int:
		for pawn_id in entities.all_pawns():
			var action_comp: Components.ActionComponent = entities.actions.get(pawn_id)
			if action_comp == null:
				continue
			if action_comp.current_action != null and action_comp.current_action.target_entity == building_id:
				return pawn_id
			for queued in action_comp.action_queue:
				if queued.target_entity == building_id:
					return pawn_id
		return -1


class ResourceComponent:
	var resource_type: String = ""
	var current_amount: float = 100.0
	var max_amount: float = 100.0
	var depletion_mult: float = 1.0  # 0 = never depletes


class AttachmentComponent:
	# need_id -> (pawn_id -> attachment strength, 0-10). Kept separate per need so, e.g.,
	# visiting a building to satisfy Social doesn't carry over to Purpose (work) scoring there.
	var need_attachments: Dictionary[int, Dictionary] = {}

	func get_strength(need_id: int, pawn_id: int) -> int:
		var per_pawn: Dictionary = need_attachments.get(need_id, {})
		return per_pawn.get(pawn_id, 0)

	func increment(need_id: int, pawn_id: int) -> void:
		if not need_attachments.has(need_id):
			need_attachments[need_id] = {}
		var per_pawn: Dictionary = need_attachments[need_id]
		per_pawn[pawn_id] = mini(10, per_pawn.get(pawn_id, 0) + 1)

	## Pivots need_attachments into pawn_id -> {need_id: strength} — for display/snapshot
	## purposes only; AI scoring always reads a specific need's bucket via get_strength().
	func per_pawn_breakdown() -> Dictionary:
		var breakdown: Dictionary = {}
		for need_id in need_attachments:
			var per_pawn: Dictionary = need_attachments[need_id]
			for pawn_id in per_pawn:
				if not breakdown.has(pawn_id):
					breakdown[pawn_id] = {}
				breakdown[pawn_id][need_id] = per_pawn[pawn_id]
		return breakdown


class InventoryComponent:
	var resource_type: String = ""   # "" = empty hands
	var amount: float = 0.0
	var max_amount: float = 50.0
