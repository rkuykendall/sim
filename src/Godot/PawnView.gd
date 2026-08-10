class_name PawnView
extends Node2D

const DEFAULT_MOVE_DURATION: float = 0.5  # fallback, matches sim's default tile-step time

# Character sheet layout: 24x24 frames, one animation per row.
# [row, frame_count, fps, loop]
const CHAR_FRAME_SIZE: int = 24
const CHAR_ANIMS: Dictionary = {
	"idle": [0, 8, 8.0, true],
	"walk": [1, 4, 8.0, true],
	"exertion": [3, 1, 1.0, false],
	"sit": [4, 2, 2.0, false],
}

# Overhead icons (same spot as the expression bubble) — axe swings during the AXE animation
# (tree harvesting), item icon shows whenever the pawn's inventory is carrying something with
# a matching sprite. Scaled to match the character/tile/building convention (SPRITE_SCALE),
# not the (smaller, unscaled) bubble convention.
const OVERHEAD_ICON_OFFSET: Vector2 = Vector2(0, -40)
const AXE_SWING_ANGLE: float = 0.785398  # 45 degrees, each direction (90 total sweep)
# The strike (downswing) is a sharp, fast motion; the recovery (upswing) is slower and more
# deliberate — downswing is twice as fast as upswing, not a symmetric back-and-forth.
const AXE_DOWNSWING_DURATION: float = 0.25
const AXE_UPSWING_DURATION: float = 0.5

var _sprite: AnimatedSprite2D
var _forced_sheet_key: String = ""
var _house_texture: Texture2D = null
var _axe_sprite: Sprite2D
var _axe_swing_timer: float = 0.0
var _item_sprite: Sprite2D
var _carrying_resource_type: String = ""
var _bubble_node: Node2D
var _bubble_wrapper: Sprite2D
var _bubble_icon: Sprite2D
var _target_position: Vector2
var _move_start: Vector2
var _move_elapsed: float = 0.0
var _move_duration: float = DEFAULT_MOVE_DURATION
var _bubble_time: float = 0.0
var _bubble_float_speed: float = 2.0
var _bubble_float_amount: float = 3.0
var _selected: bool = false
var _selection_rect: ColorRect


func _ready() -> void:
	_sprite = AnimatedSprite2D.new()
	_sprite.name = "Sprite"
	add_child(_sprite)

	_axe_sprite = Sprite2D.new()
	_axe_sprite.name = "Axe"
	_axe_sprite.texture = SpriteResourceManager.get_texture("axe")
	_axe_sprite.position = OVERHEAD_ICON_OFFSET
	_axe_sprite.scale = Vector2(RenderingConstants.SPRITE_SCALE, RenderingConstants.SPRITE_SCALE)
	_axe_sprite.rotation = -AXE_SWING_ANGLE
	_axe_sprite.visible = false
	add_child(_axe_sprite)

	_item_sprite = Sprite2D.new()
	_item_sprite.name = "CarriedItem"
	_item_sprite.position = OVERHEAD_ICON_OFFSET
	_item_sprite.scale = Vector2(RenderingConstants.SPRITE_SCALE, RenderingConstants.SPRITE_SCALE)
	_item_sprite.visible = false
	add_child(_item_sprite)

	_bubble_node = Node2D.new()
	_bubble_node.name = "BubbleNode"
	_bubble_node.visible = false
	_bubble_node.z_index = 100
	_bubble_node.position = Vector2(0, -40)
	add_child(_bubble_node)

	_bubble_wrapper = Sprite2D.new()
	_bubble_wrapper.name = "BubbleWrapper"
	_bubble_wrapper.centered = true
	_bubble_wrapper.z_index = 0
	_bubble_node.add_child(_bubble_wrapper)

	_bubble_icon = Sprite2D.new()
	_bubble_icon.name = "BubbleIcon"
	_bubble_icon.centered = true
	_bubble_icon.z_index = 1
	_bubble_node.add_child(_bubble_icon)

	_selection_rect = ColorRect.new()
	_selection_rect.name = "SelectionRect"
	_selection_rect.color = Color(1, 1, 1, 0.3)
	_selection_rect.size = Vector2(
		RenderingConstants.RENDERED_TILE_SIZE, RenderingConstants.RENDERED_TILE_SIZE
	)
	_selection_rect.position = Vector2(
		-RenderingConstants.RENDERED_TILE_SIZE * 0.5, -RenderingConstants.RENDERED_TILE_SIZE * 0.5
	)
	_selection_rect.visible = false
	add_child(_selection_rect)


func _process(delta: float) -> void:
	# Interpolates over the exact duration of one tile-step (set via set_move_duration) so the
	# sprite arrives exactly when the sim does.
	if position != _target_position:
		_move_elapsed += delta
		var t: float = (
			1.0 if _move_duration <= 0.0 else clampf(_move_elapsed / _move_duration, 0.0, 1.0)
		)
		position = _move_start.lerp(_target_position, t)

		# Flip based on the step's overall direction so it stays stable for the whole step.
		if _sprite.sprite_frames != null:
			var dx: float = _target_position.x - _move_start.x
			if dx < -0.1:
				_sprite.flip_h = true
			elif dx > 0.1:
				_sprite.flip_h = false

	# Floating bubble animation
	if _bubble_node.visible:
		_bubble_time += delta * _bubble_float_speed
		_bubble_node.position = Vector2(0, -40.0 + sin(_bubble_time) * _bubble_float_amount)

	# Axe swing — a fast downswing (the strike) followed by a slower upswing (the recovery),
	# not a symmetric back-and-forth; see AXE_DOWNSWING_DURATION/AXE_UPSWING_DURATION. The
	# character's own body pose follows which leg we're on — "exertion" for the strike, "idle"
	# for the raise. flip_h mirrors the axe's texture but NOT the screen-space direction its
	# rotation turns — the same rotation sign reads as "swinging forward" for a right-facing
	# pawn and "swinging backward" for a mirrored, left-facing one, so the sweep direction has
	# to invert with facing too.
	if _axe_sprite.visible:
		_axe_sprite.flip_h = _sprite.flip_h  # face the same way as the pawn's body
		var facing_sign: float = -1.0 if _sprite.flip_h else 1.0
		_axe_swing_timer = fmod(
			_axe_swing_timer + delta, AXE_DOWNSWING_DURATION + AXE_UPSWING_DURATION
		)
		var is_downswing: bool = _axe_swing_timer < AXE_DOWNSWING_DURATION
		if is_downswing:
			_axe_sprite.rotation = (
				facing_sign
				* lerpf(
					-AXE_SWING_ANGLE, AXE_SWING_ANGLE, _axe_swing_timer / AXE_DOWNSWING_DURATION
				)
			)
		else:
			var up_t: float = (_axe_swing_timer - AXE_DOWNSWING_DURATION) / AXE_UPSWING_DURATION
			_axe_sprite.rotation = facing_sign * lerpf(AXE_SWING_ANGLE, -AXE_SWING_ANGLE, up_t)

		var body_anim: String = "exertion" if is_downswing else "idle"
		if _sprite.animation != body_anim:
			_sprite.play(body_anim)


func initialize_with_sprite(sheet_tex: Texture2D) -> void:
	var frames := SpriteFrames.new()
	for anim_name: String in CHAR_ANIMS:
		var def: Array = CHAR_ANIMS[anim_name]
		_add_animation(frames, anim_name, sheet_tex, def[0], def[1], def[2], def[3])
	_sprite.sprite_frames = frames
	_sprite.scale = Vector2(RenderingConstants.SPRITE_SCALE, RenderingConstants.SPRITE_SCALE)
	# The character art rests at the bottom-center of each (larger) frame; shift the
	# centered sprite up so its bottom edge lands on the tile's bottom edge, not the frame's.
	var frame_render_size: float = CHAR_FRAME_SIZE * RenderingConstants.SPRITE_SCALE
	_sprite.position.y = (RenderingConstants.RENDERED_TILE_SIZE - frame_render_size) / 2.0
	_sprite.play("idle")


## Syncs this pawn's rendered sheet to whatever house they're CURRENTLY using — either that
## house's own deterministic default (CharacterSheetPool.get_sheet_for_house) or its
## skin_override if one is set (see Simulation.set_building_skin_override). GameRoot only calls
## this while the pawn's live snapshot reports home_visit_building_id != -1 — i.e. they're
## actively using an isHome building right this tick (see Simulation._build_pawn_snapshots) —
## so a house's look genuinely only reaches a pawn on their next visit, not instantly for
## everyone who's ever slept there. No-op if the resolved texture is unchanged, and a permanent
## forced sheet (visitor pawns, see assign_forced_sheet) always wins over this.
func sync_house_sheet(texture: Texture2D) -> void:
	if not _forced_sheet_key.is_empty():
		return
	if texture == null or texture == _house_texture:
		return
	_house_texture = texture
	initialize_with_sprite(texture)


## Permanently locks this pawn to an explicit sheet (visitor pawns) — takes priority over
## everything else, including a home's skin override, since visitors are meant to look distinct
## from the regular colonist population. Called once at node creation.
func assign_forced_sheet(sheet_key: String) -> void:
	_forced_sheet_key = sheet_key
	var tex: Texture2D = CharacterSheetPool.get_sheet_by_name(sheet_key)
	if tex != null:
		initialize_with_sprite(tex)


func _add_animation(
	frames: SpriteFrames,
	anim_name: String,
	texture: Texture2D,
	row: int,
	frame_count: int,
	fps: float,
	loop: bool
) -> void:
	if texture == null:
		return
	if frames.has_animation(anim_name):
		frames.remove_animation(anim_name)
	frames.add_animation(anim_name)
	frames.set_animation_loop(anim_name, loop)
	frames.set_animation_speed(anim_name, fps)
	for i in frame_count:
		var atlas := AtlasTexture.new()
		atlas.atlas = texture
		atlas.region = Rect2(
			i * CHAR_FRAME_SIZE, row * CHAR_FRAME_SIZE, CHAR_FRAME_SIZE, CHAR_FRAME_SIZE
		)
		frames.add_frame(anim_name, atlas)


func set_initial_position(pos: Vector2) -> void:
	position = pos
	_move_start = pos
	_target_position = pos


func set_target_position(pos: Vector2) -> void:
	if pos == _target_position:
		return
	_move_start = position
	_move_elapsed = 0.0
	_target_position = pos


## Sets how long one tile-step should take to visually traverse, in seconds.
## Passed in each frame by GameRoot since it depends on the sim's tick rate and current game speed.
func set_move_duration(seconds: float) -> void:
	_move_duration = seconds


func set_current_animation(animation: Definitions.AnimationType) -> void:
	if _sprite.sprite_frames == null:
		return

	var show_axe: bool = animation == Definitions.AnimationType.AXE
	if show_axe and not _axe_sprite.visible:
		# Restart from the raised position instead of popping in mid-arc.
		_axe_swing_timer = 0.0
		_axe_sprite.rotation = (-1.0 if _sprite.flip_h else 1.0) * -AXE_SWING_ANGLE
	_axe_sprite.visible = show_axe

	if show_axe:
		return  # body pose is driven by the swing phase in _process() instead

	var anim_name: String
	match animation:
		Definitions.AnimationType.WALK:
			anim_name = "walk"
		Definitions.AnimationType.PICKAXE:
			anim_name = "exertion"
		Definitions.AnimationType.SIT:
			anim_name = "sit"
		_:
			anim_name = "idle"

	if _sprite.animation != anim_name:
		_sprite.play(anim_name)


## Shows an overhead icon for what the pawn's inventory currently holds. Only resource types
## listed here have an icon — every other haulSourceResourceType in buildings.json (add one as
## its sprite is ready) is silently hidden rather than looked up, so an unmapped type never
## spams a missing-texture warning every frame.
const CARRY_ICON_RESOURCE_TYPES: Array[String] = ["wood", "food"]


func set_carrying(resource_type: String) -> void:
	if resource_type == _carrying_resource_type:
		return
	_carrying_resource_type = resource_type
	if resource_type in CARRY_ICON_RESOURCE_TYPES:
		_item_sprite.texture = SpriteResourceManager.get_texture(resource_type)
		_item_sprite.visible = true
	else:
		_item_sprite.visible = false


func set_mood(mood: float) -> void:
	if mood > 20:
		_sprite.modulate = Color(0.8, 1.0, 0.8)
	elif mood < -20:
		_sprite.modulate = Color(1.0, 0.7, 0.7)
	else:
		_sprite.modulate = Color.WHITE


func set_selected(selected: bool) -> void:
	_selected = selected
	_selection_rect.visible = selected


## Sets the expression bubble. Pass has_expression=false to hide.
func set_expression(
	has_expression: bool,
	expression: Definitions.ExpressionType,
	expression_icon_def_id: int,
	content: ContentRegistry
) -> void:
	if not has_expression:
		_bubble_node.visible = false
		return

	# Select bubble wrapper texture based on expression type
	var bubble_key: String
	match expression:
		Definitions.ExpressionType.HAPPY:
			bubble_key = "bubble_happy"
		Definitions.ExpressionType.COMPLAINT:
			bubble_key = "bubble_complaint"
		Definitions.ExpressionType.SPEECH:
			bubble_key = "bubble_speech"
		Definitions.ExpressionType.QUESTION:
			bubble_key = "bubble_question"
		_:
			bubble_key = "bubble_thought"

	var bubble_tex: Texture2D = SpriteResourceManager.get_texture(bubble_key)
	_bubble_wrapper.texture = bubble_tex

	# Load need icon if we have one
	if expression_icon_def_id != -1 and content != null:
		var need_def: Dictionary = content.needs.get(expression_icon_def_id, {})
		var icon_key: String = need_def.get("spriteKey", "")
		if not icon_key.is_empty():
			_bubble_icon.texture = SpriteResourceManager.get_texture(icon_key)
			_bubble_icon.visible = true
		else:
			_bubble_icon.visible = false
	else:
		_bubble_icon.visible = false

	_bubble_node.visible = true
	_bubble_time = 0.0
