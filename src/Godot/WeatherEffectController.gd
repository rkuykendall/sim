class_name WeatherEffectController
extends Node2D

## World-space ambient weather particles (rain, and reusable for e.g. a future snow theme) —
## a plain world-space Node2D (not a CanvasLayer) so it pans/zooms with the camera exactly
## like tiles/buildings/pawns, and its "landed" position is a real point in the world instead
## of a screen-space coordinate disconnected from any tile. Driven entirely by GameRoot calling
## set_active_weather(key) with the current theme's snapshot.theme.weather_effect_key (see
## SimTheme.get_weather_effect_key).
##
## Each spritesheet is a grid of frame_size x frame_size frames, columns wide: frame 0 is the
## single falling-particle frame (held while a drop falls fall_height onto its own randomly
## chosen landing point somewhere in the visible world — NOT a screen-crossing fall; this is a
## top-down game, so every point in view is equally "the ground," not just the bottom edge).
## Frames 1..splash_frame_count are a one-shot "landed" animation played once at that landing
## point before the particle is freed. To add a new weather type (e.g. snow), add a profile
## below with its own sheet/motion and have a theme return its key — no other code needed.
const PROFILES: Dictionary = {
	"rain": {
		"texture": "res://sprites/effects/rain_sheet.png",
		"frame_size": 16,
		"columns": 4,
		"splash_frame_count": 7,
		"fall_velocity": Vector2(-30.0, 200.0),
		"fall_height": 64.0,  # 2 tiles — falls from just above rooftop height, not screen-height
		"splash_fps": 16.0,
		"spawn_density": 0.0006,  # drops/sec per visible world px² — keeps density zoom-consistent
	},
}

# How much a given drop's fall speed (and therefore its fall_height duration) randomly varies,
# so they don't all fall in lockstep.
const FALL_SPEED_VARIANCE: float = 0.2

@export var camera_path: NodePath = ""

var _camera: Camera2D = null
var _active_key: String = ""
var _profile: Dictionary = {}
var _shared_frames: SpriteFrames = null
var _spawn_accumulator: float = 0.0


func _ready() -> void:
	# Rain falls "through the air" above everything else in the world — render it in its own
	# top z_index band regardless of y-sort, rather than sorting per-drop against pawns/tiles.
	y_sort_enabled = false
	z_index = 100

	if not camera_path.is_empty():
		_camera = get_node_or_null(camera_path)


func _process(delta: float) -> void:
	if _active_key.is_empty():
		return
	var density: float = float(_profile.get("spawn_density", 0.0))
	var area: float = _get_visible_world_rect().get_area()
	_spawn_accumulator += delta * density * area
	while _spawn_accumulator >= 1.0:
		_spawn_accumulator -= 1.0
		_spawn_drop()


## Switches the active weather effect ("" disables spawning new particles — any already
## in-flight finish falling/splashing naturally rather than being cut off). No-op if unchanged,
## so GameRoot can call this every snapshot without rebuilding sprite frames constantly.
func set_active_weather(key: String) -> void:
	if key == _active_key:
		return
	_active_key = key
	_spawn_accumulator = 0.0

	if key.is_empty():
		_profile = {}
		_shared_frames = null
		return

	_profile = PROFILES.get(key, {})
	if _profile.is_empty():
		push_warning("WeatherEffectController: unknown weather key '%s'" % key)
		_active_key = ""
		return
	_shared_frames = _build_sprite_frames(_profile)


## The camera's currently-visible area in world coordinates — spawning within this (rather
## than raw screen pixels) is what keeps rain aligned with the world as the camera pans/zooms.
func _get_visible_world_rect() -> Rect2:
	var viewport_size: Vector2 = get_viewport().get_visible_rect().size
	if _camera == null:
		return Rect2(Vector2.ZERO, viewport_size)
	var half_extents: Vector2 = (viewport_size * 0.5) / _camera.zoom
	# global_position rather than get_screen_center_position(): the latter can lag a frame
	# behind a just-applied position change (it syncs with the viewport's render transform),
	# where a plain position read is always synchronously current.
	return Rect2(_camera.global_position - half_extents, half_extents * 2.0)


func _spawn_drop() -> void:
	if _shared_frames == null:
		return

	var world_rect: Rect2 = _get_visible_world_rect()
	var fall_height: float = float(_profile.get("fall_height", 64.0))

	# Land somewhere across the whole visible ground — not just along one screen-relative
	# edge — then work backward to where the drop must have started to fall fall_height and
	# land exactly here.
	var landing_pos := Vector2(
		randf_range(world_rect.position.x, world_rect.end.x),
		randf_range(world_rect.position.y, world_rect.end.y)
	)

	var speed_mult: float = randf_range(1.0 - FALL_SPEED_VARIANCE, 1.0 + FALL_SPEED_VARIANCE)
	var velocity: Vector2 = Vector2(_profile.get("fall_velocity", Vector2(0, 200))) * speed_mult
	var duration: float = fall_height / velocity.y
	var start_pos: Vector2 = landing_pos - velocity * duration

	var drop := AnimatedSprite2D.new()
	drop.sprite_frames = _shared_frames
	drop.scale = Vector2(RenderingConstants.SPRITE_SCALE, RenderingConstants.SPRITE_SCALE)
	drop.position = start_pos
	add_child(drop)
	drop.play("fall")

	var tween := create_tween()
	tween.tween_property(drop, "position", landing_pos, duration)
	tween.finished.connect(_on_drop_landed.bind(drop))


func _on_drop_landed(drop: AnimatedSprite2D) -> void:
	if not is_instance_valid(drop):
		return
	drop.animation_finished.connect(drop.queue_free)
	drop.play("splash")


func _build_sprite_frames(profile: Dictionary) -> SpriteFrames:
	var texture: Texture2D = load(profile["texture"])
	var frame_size: int = profile["frame_size"]
	var columns: int = profile["columns"]

	var frames := SpriteFrames.new()

	frames.add_animation("fall")
	frames.set_animation_loop("fall", true)
	frames.add_frame("fall", _atlas_frame(texture, 0, columns, frame_size))

	frames.add_animation("splash")
	frames.set_animation_loop("splash", false)
	frames.set_animation_speed("splash", float(profile.get("splash_fps", 16.0)))
	for i in int(profile.get("splash_frame_count", 0)):
		frames.add_frame("splash", _atlas_frame(texture, i + 1, columns, frame_size))

	return frames


func _atlas_frame(texture: Texture2D, index: int, columns: int, frame_size: int) -> AtlasTexture:
	var col: int = index % columns
	var row: int = index / columns
	var atlas := AtlasTexture.new()
	atlas.atlas = texture
	atlas.region = Rect2(col * frame_size, row * frame_size, frame_size, frame_size)
	return atlas
