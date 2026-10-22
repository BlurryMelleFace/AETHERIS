shader_type canvas_item;

uniform float wind_time = 0.0;
uniform float wind_strength = 0.0;
uniform float tree_phase = 0.0;
uniform float sun_direction = 0.0;
uniform float daylight = 1.0;
uniform float sway_pixels = 9.0;
varying vec4 tree_color;

void vertex() {
    tree_color = COLOR;
    float height_weight = 1.0 - UV.y;
    // The lowest tenth of the tree is rooted and never moves.
    float bend = max(height_weight - 0.1, 0.0) / 0.9;
    VERTEX.x += bend * bend * wind_strength * sway_pixels;
}

void fragment() {
    float height_weight = 1.0 - UV.y;
    float canopy = smoothstep(0.04, 0.28, height_weight);
    float branch = sin(wind_time * 1.37 + tree_phase + UV.y * 18.0)
        + 0.45 * sin(wind_time * 2.13 - tree_phase + UV.y * 31.0);
    float needles = sin(wind_time * 3.1 + UV.y * 57.0 + UV.x * 23.0 + tree_phase);
    float displacement = (branch * 0.008 + needles * 0.0025)
        * (0.15 + abs(wind_strength)) * canopy * height_weight;
    vec2 sample_uv = UV + vec2(displacement, 0.0);
    vec4 foliage = texture(TEXTURE, sample_uv);
    foliage.a *= step(0.0, sample_uv.x) * step(sample_uv.x, 1.0);
    float shaded_side = mix(UV.x, 1.0 - UV.x, smoothstep(-0.2, 0.2, sun_direction));
    float shade = smoothstep(0.2, 0.9, shaded_side) * (0.08 + daylight * 0.12);
    float tier_shade = (0.5 + 0.5 * sin(UV.y * 36.0 + displacement * 28.0)) * canopy * 0.045;
    float edge = max(0.0, foliage.a - texture(TEXTURE,
        sample_uv + vec2(sign(sun_direction) * TEXTURE_PIXEL_SIZE.x, 0.0)).a);
    vec3 color = foliage.rgb * (1.0 - shade - tier_shade);
    color += vec3(0.10, 0.11, 0.065) * edge * daylight * 0.25;
    COLOR = vec4(color, foliage.a) * tree_color;
}