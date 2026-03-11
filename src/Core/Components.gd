class_name Components


class PositionComponent:
	var coord: Vector2i


class PawnComponent:
	var name: String = ""


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
	# pawn_id -> attachment strength (0-10)
	var user_attachments: Dictionary[int, int] = {}


class GoldComponent:
	var amount: int = 0


class InventoryComponent:
	var resource_type: String = ""   # "" = empty hands
	var amount: float = 0.0
	var max_amount: float = 50.0
