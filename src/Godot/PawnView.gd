class_name PawnView
extends Node2D

const LERP_SPEED: float = 0.01  # fraction per frame at 60fps (matches C# behavior)

# Character sheet layout: 24x24 frames, one animation per row.
# [row, frame_count, fps, loop]
const CHAR_FRAME_SIZE: int = 24
const CHAR_ANIMS: Dictionary = {
	"idle":     [0, 8, 8.0, true],
	"walk":     [1, 4, 8.0, true],
	"exertion": [3, 1, 1.0, false],
}

var _sprite: AnimatedSprite2D
var _bubble_node: Node2D
var _bubble_wrapper: Sprite2D
var _bubble_icon: Sprite2D
var _target_position: Vector2
var _bubble_time: float = 0.0
var _bubble_float_speed: float = 2.0
var _bubble_float_amount: float = 3.0
var _selected: bool = false
var _selection_rect: ColorRect


func _ready() -> void:
	_sprite = AnimatedSprite2D.new()
	_sprite.name = "Sprite"
	add_child(_sprite)

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
	_selection_rect.size = Vector2(RenderingConstants.RENDERED_TILE_SIZE,
		RenderingConstants.RENDERED_TILE_SIZE)
	_selection_rect.position = Vector2(
		-RenderingConstants.RENDERED_TILE_SIZE * 0.5,
		-RenderingConstants.RENDERED_TILE_SIZE * 0.5
	)
	_selection_rect.visible = false
	add_child(_selection_rect)


func _process(delta: float) -> void:
	# Smooth position lerp — exponential ease-out matching C# Lerp(0.01) at 60fps
	position = position.lerp(_target_position, 1.0 - pow(1.0 - LERP_SPEED, delta * 60.0))

	# Flip sprite to face movement direction
	if _sprite.sprite_frames != null:
		var velocity: Vector2 = _target_position - position
		if velocity.length_squared() > 0.1:
			if velocity.x < 0:
				_sprite.flip_h = true
			elif velocity.x > 0:
				_sprite.flip_h = false

	# Floating bubble animation
	if _bubble_node.visible:
		_bubble_time += delta * _bubble_float_speed
		_bubble_node.position = Vector2(0, -40.0 + sin(_bubble_time) * _bubble_float_amount)


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
		atlas.region = Rect2(i * CHAR_FRAME_SIZE, row * CHAR_FRAME_SIZE, CHAR_FRAME_SIZE, CHAR_FRAME_SIZE)
		frames.add_frame(anim_name, atlas)


func set_initial_position(pos: Vector2) -> void:
	position = pos
	_target_position = pos


func set_target_position(pos: Vector2) -> void:
	_target_position = pos


func set_current_animation(animation: Definitions.AnimationType) -> void:
	if _sprite.sprite_frames == null:
		return

	var anim_name: String
	match animation:
		Definitions.AnimationType.WALK:    anim_name = "walk"
		Definitions.AnimationType.AXE:     anim_name = "exertion"
		Definitions.AnimationType.PICKAXE: anim_name = "exertion"
		_:                                 anim_name = "idle"

	if _sprite.animation != anim_name:
		_sprite.play(anim_name)


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
		Definitions.ExpressionType.HAPPY:     bubble_key = "bubble_happy"
		Definitions.ExpressionType.COMPLAINT: bubble_key = "bubble_complaint"
		Definitions.ExpressionType.SPEECH:    bubble_key = "bubble_speech"
		Definitions.ExpressionType.QUESTION:  bubble_key = "bubble_question"
		_:                                    bubble_key = "bubble_thought"

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
