
shader_type canvas_item;
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

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

float band_ease(float t, float start, float end, float speed)
{
    float band = end - start;
    float span = min(band * 0.45, (band * 0.5) / max(speed, 0.01));
    float ease_in = smoothstep(start, start + span, t);
    float ease_out = smoothstep(end - span, end, t);
    return clamp(ease_in - ease_out, 0.0, 1.0);
}

void fragment()
{
    vec4 col = texture(SCREEN_TEXTURE, SCREEN_UV);
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

    col.rgb *= tint * brightness;
    COLOR = col;
}
