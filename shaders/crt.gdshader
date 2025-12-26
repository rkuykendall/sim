shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform bool enable_grain = true;
uniform bool enable_scanlines = true;
uniform bool enable_rgb_grid = true;
uniform float brightness_multiplier:hint_range(1.0,1.5, 0.05) = 1.05;

// RGB grid scale - how many screen pixels per RGB pattern (lower = finer grain)
uniform float rgb_grid_scale : hint_range(1.0, 8.0, 1.0) = 2.0;
// RGB grid softness - brightness of "off" channels (0.0 = hard, 0.5 = soft)
uniform float rgb_grid_softness : hint_range(0.0, 0.8, 0.05) = 0.3;

group_uniforms scanlines_settings;
uniform float scanlines_interval: hint_range(1.0, 20.0, 1.0) = 4.0;
uniform float scanline_opacity: hint_range(0.0, 1.0, 0.05) = 0.3;
uniform float scanline_thickness: hint_range(0.0, 10.0, 1.0) = 2.0;

group_uniforms grain_settings;
uniform float grain_strength: hint_range(0.0, 1.0, 0.05) = 0.25;

float filmGrainNoise(in float time, in vec2 uv)
{
    return fract(sin(dot(uv, vec2(12.9898, 78.233) * time)) * 43758.5453);
}

void fragment()
{
	vec2 xy = SCREEN_UV;

	vec4 color = texture(screen_texture, xy);

	if(enable_grain)
		color.rgb = mix(color.rgb ,vec3(0.0), filmGrainNoise(TIME, SCREEN_UV) * grain_strength);

	if(enable_rgb_grid)
	{
		// Use screen pixels scaled by rgb_grid_scale
		// This keeps the bands at a fixed pixel size regardless of window scaling
		int pixel_x = int(FRAGCOORD.x / rgb_grid_scale);
		int rgbIndex = pixel_x % 3;

		float r;
		float g;
		float b;
		if(rgbIndex == 0)
			r = 1.0;
		else
			r = rgb_grid_softness;
		if(rgbIndex == 1)
			g = 1.0;
		else
			g = rgb_grid_softness;
		if(rgbIndex == 2)
			b = 1.0;
		else
			b = rgb_grid_softness;
		vec3 mask = vec3(r, g, b);

		// Luminance preservation: calculate average mask brightness
		float mask_avg = (mask.r + mask.g + mask.b) / 3.0;

		// Compensate for mask darkness
		color.rgb /= mask_avg;

		// Calculate target color and handle overflow
		vec3 target_color = color.rgb;
		vec3 primary_col = clamp(target_color, 0.0, 1.0);

		// Capture overflow (bright pixels that would clip)
		vec3 overflow = target_color - primary_col;

		// Apply mask to primary color
		primary_col *= mask;

		// Redistribute overflow to "off" channels (mimics phosphor bleed)
		if(mask_avg > 0.0)
		{
			overflow /= (1.0 / mask_avg - 1.0);
			primary_col += overflow * (1.0 - mask);
		}

		color.rgb = primary_col;
	}

	if(enable_scanlines)
	{
		// Determine which scanline we're on
		float scanline_index = floor(FRAGCOORD.y / scanlines_interval);

		// Alternate between brightening and darkening
		float is_bright_line = mod(scanline_index, 2.0);

		// Create the scanline effect
		if(is_bright_line > 0.5)
		{
			// Brighten this line
			color.rgb *= 1.0 + scanline_opacity;
		}
		else
		{
			// Darken this line
			color.rgb *= 1.0 - scanline_opacity;
		}
	}

	color.rgb *= brightness_multiplier;

	COLOR = color;
}
