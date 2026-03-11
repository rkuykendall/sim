class_name SpriteResourceManager

# Sprite path map: key -> res:// path
const _SPRITE_PATH_MAP: Dictionary = {
	# Tile sprites
	"flat":               "res://sprites/tiles/flat.png",
	"water":              "res://sprites/tiles/water.png",
	"grass":              "res://sprites/tiles/grass.png",
	"dirt":               "res://sprites/tiles/dirt.png",
	"stone":              "res://sprites/tiles/stone.png",
	"path":               "res://sprites/tiles/path.png",
	"trees":              "res://sprites/tiles/trees.png",
	"block":              "res://sprites/tiles/block.png",
	"wall":               "res://sprites/tiles/wall.png",
	"wood_floor":         "res://sprites/tiles/wood_floor.png",
	"rock":               "res://sprites/tiles/rock.png",
	"plant":              "res://sprites/tiles/plant.png",
	# Building sprites
	"home":               "res://sprites/buildings/home.png",
	"well":               "res://sprites/buildings/well.png",
	"farm":               "res://sprites/buildings/farm.png",
	"lumber_mill":        "res://sprites/buildings/lumber_mill.png",
	"tavern":             "res://sprites/buildings/tavern.png",
	"market":             "res://sprites/buildings/market.png",
	# Character sprites
	"character_walk":     "res://sprites/characters/walk_strip8.png",
	"character_idle":     "res://sprites/characters/idle_strip3.png",
	"character_axe":      "res://sprites/characters/axe_strip5.png",
	"character_pickaxe":  "res://sprites/characters/pickaxe_strip5.png",
	"character_look_down":"res://sprites/characters/look_down.png",
	"character_look_up":  "res://sprites/characters/look_up.png",
	# Bubble sprites
	"bubble_thought":     "res://sprites/ui/bubbles/thought_bubble.png",
	"bubble_happy":       "res://sprites/ui/bubbles/heart_bubble.png",
	"bubble_complaint":   "res://sprites/ui/bubbles/complaint_bubble.png",
	"bubble_speech":      "res://sprites/ui/bubbles/thought_bubble.png",
	"bubble_question":    "res://sprites/ui/bubbles/thought_bubble.png",
	# Need icon sprites
	"hunger":             "res://sprites/ui/icons/hunger.png",
	"energy":             "res://sprites/ui/icons/energy.png",
	"social":             "res://sprites/ui/icons/social.png",
	"hygiene":            "res://sprites/ui/icons/hygiene.png",
	"purpose":            "res://sprites/ui/icons/purpose.png",
}

static var _cache: Dictionary = {}


static func get_texture(sprite_key: String) -> Texture2D:
	if _cache.has(sprite_key):
		return _cache[sprite_key]

	var path: String = ""
	if _SPRITE_PATH_MAP.has(sprite_key):
		path = _SPRITE_PATH_MAP[sprite_key]
	else:
		# Try direct res:// path as fallback
		path = "res://sprites/" + sprite_key + ".png"

	if not ResourceLoader.exists(path):
		push_warning("SpriteResourceManager: texture not found for key '%s' at '%s'" % [sprite_key, path])
		return null

	var texture: Texture2D = load(path)
	_cache[sprite_key] = texture
	return texture


## Returns a texture cropped to the given source region.
## Used for toolbar icons so multi-variant/multi-tile sheets show one clean region.
static func get_icon_texture(sprite_key: String, region: Rect2 = Rect2()) -> Texture2D:
	var tex: Texture2D = get_texture(sprite_key)
	if tex == null:
		return null
	var r: Rect2 = region if region.size != Vector2.ZERO else Rect2(0, 0, tex.get_width(), tex.get_height())
	if r.size.x >= tex.get_width() and r.size.y >= tex.get_height():
		return tex
	var atlas := AtlasTexture.new()
	atlas.atlas = tex
	atlas.region = r
	return atlas


static func clear_cache() -> void:
	_cache.clear()
