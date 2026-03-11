class_name Definitions

enum BuffSource {
	NEED_CRITICAL,  # Applied when need below critical threshold
	NEED_LOW,       # Applied when need below low threshold
	BUILDING,       # Applied by building use
	WORK,           # Applied by working at a building
}

enum BuildingWorkType {
	DIRECT,              # Work creates resources at this building
	HAUL_FROM_BUILDING,  # Work = haul resources from another building
	HAUL_FROM_TERRAIN,   # Work = harvest resources from terrain tiles
}

enum ActionType {
	IDLE,
	MOVE_TO,
	USE_BUILDING,
	WORK,
	PICK_UP,   # Pick up resources from building or terrain
	DROP_OFF,  # Drop off resources at destination building
}

enum AnimationType {
	IDLE,
	WALK,
	AXE,
	PICKAXE,
	LOOK_UP,
	LOOK_DOWN,
}

enum ExpressionType {
	THOUGHT,    # Cloud bubble — wanting something
	SPEECH,     # Speech bubble — neutral/talking
	HAPPY,      # Heart bubble — satisfied/happy
	COMPLAINT,  # Jagged bubble — frustrated/angry
	QUESTION,   # Question bubble — confused/waiting
}


class BuffInstance:
	var source: Definitions.BuffSource
	var source_id: int       # NeedDef id or BuildingDef id depending on source
	var mood_offset: float
	var start_tick: int
	var end_tick: int = -1   # -1 = permanent until removed


class ActionDef:
	var type: Definitions.ActionType
	var animation: Definitions.AnimationType = Definitions.AnimationType.IDLE
	var target_coord: Vector2i = Vector2i(-1, -1)   # (-1,-1) = unset
	var target_entity: int = -1                      # -1 = unset
	var duration_ticks: int = 0
	var satisfies_need_id: int = -1                  # -1 = unset
	var need_satisfaction_amount: float = 0.0
	var display_name: String = ""

	# Expression bubble shown while performing this action
	var expression: Definitions.ExpressionType = Definitions.ExpressionType.THOUGHT
	var has_expression: bool = false
	var expression_icon_def_id: int = -1  # Need def id for icon sprite

	# Hauling context
	var terrain_target_coord: Vector2i = Vector2i(-1, -1)
	var resource_type: String = ""
	var resource_amount: float = 0.0
	var source_entity: int = -1  # Source building for wholesale payment
