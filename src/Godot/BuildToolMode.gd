class_name BuildToolMode

enum Mode {
	PLACE_BUILDING,
	PLACE_TERRAIN,
	FILL_SQUARE,
	OUTLINE_SQUARE,
	FLOOD_FILL,
	DELETE,
	SELECT,
}

# Global build tool state — accessed as BuildToolMode.current_mode etc.
static var current_mode: BuildToolMode.Mode = BuildToolMode.Mode.PLACE_TERRAIN
static var selected_building_def_id: int = -1
static var selected_terrain_def_id: int = -1   # -1 = delete mode
static var selected_color_index: int = 3
