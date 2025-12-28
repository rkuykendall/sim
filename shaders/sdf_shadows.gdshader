shader_type canvas_item;
render_mode unshaded;

// Sun direction in degrees (0 = right, 90 = down, 180 = left, 270 = up)
uniform float sun_angle : hint_range(0.0, 360.0) = 45.0;

// Shadow parameters
uniform vec4 shadow_color : source_color = vec4(0.0, 0.0, 0.0, 0.6);
uniform float max_shadow_distance : hint_range(0.0, 200.0) = 32.0;

// Gradient texture for hard/soft shadow control
// For hard shadows: use a step gradient (0.0-0.9 = white, 0.9-1.0 = black)
// For soft shadows: use a linear gradient
uniform sampler2D shadow_gradient : hint_default_white;

// Max raymarch steps
uniform int max_steps : hint_range(1, 128) = 32;

void fragment() {
	// Convert sun angle to direction vector
	// We march FROM the pixel AWAY from the light source (opposite direction)
	float angle_rad = radians(sun_angle);
	vec2 light_dir = -vec2(cos(angle_rad), sin(angle_rad));

	// Convert screen UV to SDF coordinates
	vec2 at = screen_uv_to_sdf(SCREEN_UV);

	// Sphere march away from the light to find occluders
	float accum = 0.0;
	while(accum < max_shadow_distance) {
		float d = texture_sdf(at);
		accum += d;

		// If we're very close to an occluder, we're in shadow
		if (d < 0.01) {
			break;
		}

		// Step away from the light
		at += d * light_dir;

		// Safety check: don't exceed max steps
		if (accum >= max_shadow_distance) {
			break;
		}
	}

	// Calculate shadow alpha based on distance traveled
	// Short distance = hit nearby occluder = dark shadow (alpha = 1.0)
	// Long distance = hit far occluder or no hit = light/no shadow (alpha = 0.0)
	float alpha = 1.0 - min(1.0, accum / max_shadow_distance);

	// Apply gradient texture to control hard vs soft shadows
	alpha = texture(shadow_gradient, vec2(alpha, 0.5)).r;

	// Output shadow color with calculated alpha
	COLOR = vec4(shadow_color.rgb, alpha * shadow_color.a);
}
