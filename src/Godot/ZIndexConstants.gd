class_name ZIndexConstants

const TILE_NODES: int = -10
const TERRAIN_NON_BLOCKING: int = 0
const TERRAIN_BLOCKING_AND_PAWNS: int = 1
# Same band as TERRAIN_BLOCKING_AND_PAWNS, not higher — Godot only y-sorts nodes that share a
# z_index, so a separate higher band would always draw buildings on top of pawns regardless of
# actual screen position, instead of letting a pawn walking "in front of" a building occlude it.
const BUILDINGS: int = 1
const UI_OVERLAY: int = 3
