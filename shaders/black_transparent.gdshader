shader_type canvas_item;

void fragment() {
    vec4 tex_color = texture(TEXTURE, UV);

    // If the pixel is pure black (R=0, G=0, B=0), make it transparent
    if (tex_color.r == 0.0 && tex_color.g == 0.0 && tex_color.b == 0.0) {
        COLOR = vec4(0.0, 0.0, 0.0, 0.0);
    } else {
        // Otherwise, apply normal modulation
        COLOR = tex_color * COLOR;
    }
}
