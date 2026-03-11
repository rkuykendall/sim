class_name PawnView
extends Node2D

const SOURCE_SIZE: int = RenderingConstants.SOURCE_TILE_SIZE
const LERP_SPEED: float = 300.0   # pixels per second

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
	_selection_rect.size = Vector2(SOURCE_SIZE * RenderingConstants.SPRITE_SCALE,
		SOURCE_SIZE * RenderingConstants.SPRITE_SCALE)
	_selection_rect.position = Vector2(
		-SOURCE_SIZE * RenderingConstants.SPRITE_SCALE * 0.5,
		-SOURCE_SIZE * RenderingConstants.SPRITE_SCALE * 0.5
	)
	_selection_rect.visible = false
	add_child(_selection_rect)


func _process(delta: float) -> void:
	# Smooth position lerp
	if position.distance_to(_target_position) > 0.5:
		position = position.move_toward(_target_position, LERP_SPEED * delta)
	else:
		position = _target_position

	# Floating bubble animation
	if _bubble_node.visible:
		_bubble_time += delta * _bubble_float_speed
		_bubble_node.position = Vector2(0, -40.0 + sin(_bubble_time) * _bubble_float_amount)


func initialize_with_sprite(
	walk_tex: Texture2D,
	idle_tex: Texture2D,
	axe_tex: Texture2D,
	pickaxe_tex: Texture2D,
	look_down_tex: Texture2D,
	look_up_tex: Texture2D
) -> void:
	var frames := SpriteFrames.new()
	_add_animation(frames, "walk",      walk_tex,      8,  8.0,  true)
	_add_animation(frames, "idle",      idle_tex,      3,  3.0,  true)
	_add_animation(frames, "axe",       axe_tex,       5,  8.0,  true)
	_add_animation(frames, "pickaxe",   pickaxe_tex,   5,  8.0,  true)
	_add_animation(frames, "look_down", look_down_tex, 1,  1.0,  false)
	_add_animation(frames, "look_up",   look_up_tex,   1,  1.0,  false)
	_sprite.sprite_frames = frames
	_sprite.scale = Vector2(RenderingConstants.SPRITE_SCALE, RenderingConstants.SPRITE_SCALE)
	_sprite.play("idle")


func _add_animation(
	frames: SpriteFrames,
	anim_name: String,
	texture: Texture2D,
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
		atlas.region = Rect2(i * SOURCE_SIZE, 0, SOURCE_SIZE, SOURCE_SIZE)
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
		Definitions.AnimationType.WALK:      anim_name = "walk"
		Definitions.AnimationType.AXE:       anim_name = "axe"
		Definitions.AnimationType.PICKAXE:   anim_name = "pickaxe"
		Definitions.AnimationType.LOOK_DOWN: anim_name = "look_down"
		Definitions.AnimationType.LOOK_UP:   anim_name = "look_up"
		_:                                   anim_name = "idle"

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
