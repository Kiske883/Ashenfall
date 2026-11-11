shader_type spatial;
render_mode cull_disabled;

varying vec3 local_position;
float hash2(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise2(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash2(i), hash2(i + vec2(1, 0)), f.x),
        mix(hash2(i + vec2(0, 1)), hash2(i + vec2(1, 1)), f.x), f.y);
}
void vertex() { local_position = VERTEX; }
void fragment() {
    float weather = noise2(local_position.xz * 4.0 + local_position.y * vec2(1.3, 2.1));
    float grain = noise2(local_position.xz * 65.0 + local_position.y * vec2(5.0, 9.0));
    ALBEDO = COLOR.rgb * (0.78 + weather * 0.24 + grain * 0.08);
    ROUGHNESS = 1.0;
    SPECULAR = 0.04;
    NORMAL = normalize(NORMAL + vec3(dFdx(grain), dFdy(grain), 0.0) * 0.16);
}
