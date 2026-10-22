shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_linear;
uniform float amount : hint_range(0.0, 1.0) = 0.0;
uniform float blur_pixels = 7.0;
uniform float darkness = 0.5;

void fragment() {
    vec2 radius = SCREEN_PIXEL_SIZE * blur_pixels * amount;
    vec3 sum = texture(screen_tex, SCREEN_UV).rgb;
    float total = 1.0;
    for (int i = 0; i < 24; i++) {
        float angle = float(i) * 2.399963;
        float distance_scale = sqrt((float(i) + 0.5) / 24.0);
        sum += texture(screen_tex, SCREEN_UV + vec2(cos(angle), sin(angle)) * distance_scale * radius).rgb;
        total += 1.0;
    }
    vec3 color = sum / total;
    float edge = smoothstep(0.3, 0.95, distance(SCREEN_UV, vec2(0.5)));
    color *= (1.0 - darkness * amount) * (1.0 - edge * 0.4 * amount);
    COLOR = vec4(color, 1.0);
}
