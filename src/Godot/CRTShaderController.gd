class_name CRTShaderController
extends CanvasLayer

@export var shader_rect_path: NodePath = ""

var _shader_rect: ColorRect = null
var _enabled: bool = true


func _ready() -> void:
	if not shader_rect_path.is_empty():
		_shader_rect = get_node_or_null(shader_rect_path)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed:
		if event.keycode == KEY_F4:
			_enabled = not _enabled
			if _shader_rect != null:
				_shader_rect.visible = _enabled


func set_time_of_day(day_fraction: float) -> void:
	if _shader_rect == null:
		return

	var mat: ShaderMaterial = _shader_rect.material as ShaderMaterial
	if mat != null:
		mat.set_shader_parameter("time_of_day", day_fraction)
