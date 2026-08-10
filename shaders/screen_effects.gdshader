shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

// ===== THEME VISUALS =====
// warm/night/weather_tint are pre-computed by GameRoot from the current theme's own playback
// progress (see CRTShaderController.set_theme_visuals) rather than a real-world clock, so
// every theme's sunrise/sunset glow, night-dimming, or weather tint always aligns with its
// own song.
const float NIGHT_DIM = 0.5;
// Only a mild dimming at full strength — nowhere near NIGHT_DIM's full darkness, since
// weather_tint is meant to read as "overcast," not "night."
const float WEATHER_TINT_DIM = 0.8;
const vec3 WARM_TINT = vec3(1.0, 0.8, 0.50) * 1.3;
const vec3 COOL_TINT = vec3(0.5, 0.6, 1.0) * 1.3;

uniform float warm : hint_range(0.0, 1.0) = 0.0;
uniform float night : hint_range(0.0, 1.0) = 0.0;
// Independent of night: a theme (e.g. rain, or a future snow theme) can look cool/overcast
// via this alone, with a much milder dim than night's — see SimTheme.get_weather_tint.
uniform float weather_tint : hint_range(0.0, 1.0) = 0.0;

// ===== CRT SETTINGS =====
uniform bool enable_grain = true;
uniform bool enable_scanlines = true;
uniform bool enable_rgb_grid = true;
uniform bool enable_bloom = true;
uniform float brightness_multiplier : hint_range(1.0, 2.0, 0.05) = 1;
uniform float contrast : hint_range(0.8, 1.5, 0.05) = 1.2;
uniform float bloom_intensity : hint_range(0.0, 0.5, 0.05) = 0.3;
uniform float bloom_threshold : hint_range(0.3, 1.0, 0.05) = 0.6;

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
float filmGrainNoise(in float time, in vec2 uv)
{
    return fract(sin(dot(uv, vec2(12.9898, 78.233) * time)) * 43758.5453);
}

// Simple box blur for bloom effect
vec3 sample_bloom(sampler2D tex, vec2 uv, vec2 pixel_size)
{
    vec3 sum = vec3(0.0);
    // Sample a 5x5 grid with weighted contributions
    for (int x = -2; x <= 2; x++) {
        for (int y = -2; y <= 2; y++) {
            vec2 offset = vec2(float(x), float(y)) * pixel_size * 2.0;
            vec3 sample_col = texture(tex, uv + offset).rgb;
            // Only include bright pixels
            float luminance = dot(sample_col, vec3(0.299, 0.587, 0.114));
            if (luminance > bloom_threshold) {
                sum += sample_col * (luminance - bloom_threshold);
            }
        }
    }
    return sum / 25.0;
}

void fragment()
{
	vec2 xy = SCREEN_UV;
	vec4 color = texture(screen_texture, xy);

	// ===== APPLY THEME VISUALS FIRST =====
	// warm/night/weather_tint arrive pre-computed from the current theme's own progress (see
	// CRTShaderController.set_theme_visuals) — this shader just applies the tint/dim.
	// weather_tint gets its own mild dim (WEATHER_TINT_DIM) independent of night's much
	// stronger one, so an overcast rainy day reads as darker/bluer without going full-night.
	float cool = max(night, weather_tint);

	vec3 tint = mix(vec3(1.0), WARM_TINT, warm);
	tint = mix(tint, COOL_TINT, cool);

	float brightness = mix(1.0, NIGHT_DIM, night);
	brightness *= mix(1.0, WEATHER_TINT_DIM, weather_tint);

	color.rgb *= tint * brightness;

	// ===== THEN APPLY CRT EFFECTS =====

	// Film grain
	if(enable_grain)
	{
		// Remap noise from 0.0-1.0 to -1.0 to +1.0 so it can darken or brighten equally
		float noise = filmGrainNoise(TIME, SCREEN_UV) * 2.0 - 1.0;
		color.rgb += noise * grain_strength;
	}

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

	// Contrast adjustment (expand around midpoint)
	color.rgb = (color.rgb - 0.5) * contrast + 0.5;

	// Bloom effect - add glow from bright areas
	if (enable_bloom)
	{
		vec2 pixel_size = 1.0 / vec2(textureSize(screen_texture, 0));
		vec3 bloom = sample_bloom(screen_texture, xy, pixel_size);
		color.rgb += bloom * bloom_intensity;
	}

	// Final brightness adjustment
	color.rgb *= brightness_multiplier;

	// Clamp to valid range
	color.rgb = clamp(color.rgb, 0.0, 1.0);

	COLOR = color;
}
