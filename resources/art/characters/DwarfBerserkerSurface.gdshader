shader_type spatial;
render_mode cull_disabled;

uniform vec4 base_color : source_color = vec4(0.57, 0.52, 0.42, 1.0);
uniform vec4 weather_color : source_color = vec4(0.27, 0.23, 0.16, 1.0);
uniform float roughness_value = 0.95;
uniform float metalness = 0.0;
uniform float weather_amount = 0.35;
uniform vec3 grain_scale = vec3(30.0, 8.0, 30.0);
varying vec3 local_position;
float hash3(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
float noise3(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix(hash3(i), hash3(i + vec3(1,0,0)), f.x),
        mix(hash3(i + vec3(0,1,0)), hash3(i + vec3(1,1,0)), f.x), f.y),
        mix(mix(hash3(i + vec3(0,0,1)), hash3(i + vec3(1,0,1)), f.x),
        mix(hash3(i + vec3(0,1,1)), hash3(i + vec3(1,1,1)), f.x), f.y), f.z);
}
void vertex() { local_position = VERTEX; }
void fragment() {
    float patches = noise3(local_position * 14.0);
    float grain = noise3(local_position * grain_scale);
    float wear = smoothstep(0.35, 0.72, patches) * weather_amount;
    ALBEDO = mix(base_color.rgb, weather_color.rgb, wear) * COLOR.rgb * (0.9 + grain * 0.15);
    ROUGHNESS = clamp(roughness_value + wear * 0.15, 0.0, 1.0);
    METALLIC = metalness * (1.0 - wear);
    SPECULAR = 0.22;
    NORMAL = normalize(NORMAL + vec3(dFdx(grain), dFdy(grain), 0.0) * 0.13);
}
