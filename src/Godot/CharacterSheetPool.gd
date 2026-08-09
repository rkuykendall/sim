class_name CharacterSheetPool

## All interchangeable "Basic character v2"-layout sheets (same 24x24 frame/row layout as
## the default character_sheet.png) a house can hand out. Picked deterministically per house
## id — see get_sheet_for_house. Pawns who haven't slept in a house yet keep the plain default
## (character_sheet, loaded separately via SpriteResourceManager) instead of drawing from here.
## Sheets a theme reserves for itself (see Theme.gd) are excluded so a house can't already be
## wearing "special" clothes on an ordinary day. special_1_v2/special_2_v2 are commented out
## for an unrelated reason — they just don't fit the rest of the pool; revisit later.
const SHEET_NAMES: Array[String] = [
	"autumn_2_v2", "autumn_3_v2", "autumn_4_v2",
	"basic_bunny_character_v2", "basic_character_v2",
	"character_1_v2", "character_2_v2", "character_3_v2", "character_4_v2",
	"character_5_v2", "character_6_v2", "character_8_v2",
	"character_9_v2", "character_10_v2", "character_11_v2", "character_12_v2",
	"character_13_v2", "character_14_v2", "character_16_v2",
	"character_19_v2", "character_20_v2",
	"character_21_v2",
	"main_character_v2",
	# "special_1_v2", "special_2_v2",
	"spring_4_v2",
	"summer_1_v2", "summer_2_v2", "summer_3_v2", "summer_4_v2",
]

static var _cache: Dictionary = {}


## Deterministic: the same house id always yields the same sheet. -1 (no house yet) returns
## null so the caller can fall back to the default sheet.
static func get_sheet_for_house(house_id: int) -> Texture2D:
	if house_id < 0 or SHEET_NAMES.is_empty():
		return null
	var sheet_name: String = SHEET_NAMES[house_id % SHEET_NAMES.size()]
	return get_sheet_by_name(sheet_name)


## Looks up a sheet by its explicit name (e.g. "character_7_v2"), for theme-driven skin
## overrides and forced-sheet visitor pawns — not tied to the house-id-modulo scheme at all.
static func get_sheet_by_name(name: String) -> Texture2D:
	return _load(name)


static func _load(sheet_name: String) -> Texture2D:
	if _cache.has(sheet_name):
		return _cache[sheet_name]

	var path: String = "res://sprites/characters/%s.png" % sheet_name
	if not ResourceLoader.exists(path):
		push_warning("CharacterSheetPool: missing texture at '%s'" % path)
		return null

	var tex: Texture2D = load(path)
	_cache[sheet_name] = tex
	return tex
