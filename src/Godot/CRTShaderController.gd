class_name CRTShaderController
extends CanvasLayer

@export var shader_rect_path: NodePath = ""

var _shader_rect: ColorRect = null
var _enabled: bool = true


func _ready() -> void:
	if not shader_rect_path.is_empty():
		_shader_rect = get_node_or_null(shader_rect_path)


func _unhandled_input(event: InputEvent) -> void:
	if not event is InputEventKey:
		return
	var key_event: InputEventKey = event
	if key_event.pressed and key_event.keycode == KEY_F4:
		_enabled = not _enabled
		if _shader_rect != null:
			_shader_rect.visible = _enabled


## warm/night/weather_tint are pre-computed by GameRoot from the current theme's playback
## progress and has_shadows/weather_tint (see GameRoot._compute_theme_visuals) — this shader
## just applies them.
func set_theme_visuals(warm: float, night: float, weather_tint: float) -> void:
	if _shader_rect == null:
		return

	var mat: ShaderMaterial = _shader_rect.material as ShaderMaterial
	if mat != null:
		mat.set_shader_parameter("warm", warm)
		mat.set_shader_parameter("night", night)
		mat.set_shader_parameter("weather_tint", weather_tint)
