class_name CameraController
extends Camera2D

const MIN_ZOOM: float = 0.5
const MAX_ZOOM: float = 8.0
const ZOOM_SPEED: float = 10.0
const PAN_SPEED_BASE: float = 1000.0

var _world_width: int = 0
var _world_height: int = 0
var _zoom_target: Vector2


func _ready() -> void:
	_zoom_target = zoom


func _process(delta: float) -> void:
	zoom = zoom.slerp(_zoom_target, ZOOM_SPEED * delta)
	_handle_pan(delta)
	_clamp_to_bounds()


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("camera_zoom_in"):
		_zoom_target = (_zoom_target * 2.0).clampf(MIN_ZOOM, MAX_ZOOM)
	elif event.is_action_pressed("camera_zoom_out"):
		_zoom_target = (_zoom_target * 0.5).clampf(MIN_ZOOM, MAX_ZOOM)


func set_world_bounds(world_pixel_width: int, world_pixel_height: int) -> void:
	_world_width = world_pixel_width
	_world_height = world_pixel_height


func _handle_pan(delta: float) -> void:
	var pan_speed: float = PAN_SPEED_BASE / zoom.x
	var move := Vector2.ZERO

	if Input.is_action_pressed("ui_left"):
		move.x -= pan_speed * delta
	if Input.is_action_pressed("ui_right"):
		move.x += pan_speed * delta
	if Input.is_action_pressed("ui_up"):
		move.y -= pan_speed * delta
	if Input.is_action_pressed("ui_down"):
		move.y += pan_speed * delta

	if move != Vector2.ZERO:
		position += move
		_clamp_to_bounds()


func _clamp_to_bounds() -> void:
	if _world_width == 0 or _world_height == 0:
		return

	var viewport_size: Vector2 = get_viewport_rect().size / zoom
	var half_vp := viewport_size * 0.5

	var min_x: float = half_vp.x
	var max_x: float = maxf(_world_width - half_vp.x, half_vp.x)
	var min_y: float = half_vp.y
	var max_y: float = maxf(_world_height - half_vp.y, half_vp.y)

	position.x = clampf(position.x, min_x, max_x)
	position.y = clampf(position.y, min_y, max_y)
