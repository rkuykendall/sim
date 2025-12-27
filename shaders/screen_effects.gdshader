shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

// ===== TIME OF DAY SETTINGS =====
// Tunable time bands (fractions of a 24h day)
// Sunrise ~5:30a–7:40a, Sunset ~4:48p–7:55p
const float SUNRISE_LEN = 0.1;
const float SUNRISE_START = 0.08;
const float SUNRISE_END = SUNRISE_START + SUNRISE_LEN;
const float SUNSET_START = 1.0 - SUNRISE_START - SUNRISE_LEN;
const float SUNSET_END = 1.0 - SUNRISE_START;

// Night plateau/falloff (fractions of a 24h day)
// Full darkness within ~1h of midnight, fade out over ~6h
const float NIGHT_INNER = 0.05;
const float NIGHT_OUTER = 0.1;

// Color/brightness tweaks
const float NIGHT_DIM = 0.5;
const vec3 WARM_TINT = vec3(1.0, 0.8, 0.50) * 1.3;
const vec3 COOL_TINT = vec3(0.5, 0.6, 1.0) * 1.3;

// 0.0 = midnight, 0.5 = noon, 1.0 = next midnight
uniform float time_of_day : hint_range(0.0, 1.0) = 0.5;
// Wider than 1.0 slows transitions, lower than 1.0 speeds them up
uniform float transition_speed : hint_range(0.01, 40.0) = 0.5;

// ===== CRT SETTINGS =====
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

// ===== HELPER FUNCTIONS =====
float band_ease(float t, float start, float end, float speed)
{
    float band = end - start;
    float span = min(band * 0.45, (band * 0.5) / max(speed, 0.01));
    float ease_in = smoothstep(start, start + span, t);
    float ease_out = smoothstep(end - span, end, t);
    return clamp(ease_in - ease_out, 0.0, 1.0);
}

float filmGrainNoise(in float time, in vec2 uv)
{
    return fract(sin(dot(uv, vec2(12.9898, 78.233) * time)) * 43758.5453);
}

void fragment()
{
	vec2 xy = SCREEN_UV;
	vec4 color = texture(screen_texture, xy);

	// ===== APPLY TIME OF DAY EFFECTS FIRST =====
	float t = time_of_day;

	// Warm glows around sunrise/sunset with wider overlap to avoid neutral gaps
	float warm = band_ease(t, SUNRISE_START, SUNRISE_END, transition_speed)
		+ band_ease(t, SUNSET_START, SUNSET_END, transition_speed);
	warm = clamp(warm, 0.0, 1.0);

	// Cool tint + dim only deep at night, wrap-aware around midnight
	// Hold full darkness near midnight, then fade toward morning/evening
	float midnight_dist = min(t, 1.0 - t); // shortest distance to midnight
	float night_falloff = smoothstep(NIGHT_INNER, NIGHT_OUTER, midnight_dist);
	float night = 1.0 - night_falloff;
	float cool = night;

	vec3 tint = mix(vec3(1.0), WARM_TINT, warm);
	tint = mix(tint, COOL_TINT, cool);

	float brightness = 1.0;
	brightness = mix(brightness, NIGHT_DIM, night);

	// Apply time-of-day tint and brightness
	color.rgb *= tint * brightness;

	// ===== THEN APPLY CRT EFFECTS =====

	// Film grain
	if(enable_grain)
		color.rgb = mix(color.rgb ,vec3(0.0), filmGrainNoise(TIME, SCREEN_UV) * grain_strength);

	// RGB grid
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

	// Scanlines
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

	// Final brightness adjustment
	color.rgb *= brightness_multiplier;

	COLOR = color;
}
