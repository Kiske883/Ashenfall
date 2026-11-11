shader_type spatial;
render_mode cull_disabled;

varying vec3 local_position;

float hash(vec3 p) {
    return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}
float noise3(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix(hash(i), hash(i + vec3(1,0,0)), f.x),
                   mix(hash(i + vec3(0,1,0)), hash(i + vec3(1,1,0)), f.x), f.y),
               mix(mix(hash(i + vec3(0,0,1)), hash(i + vec3(1,0,1)), f.x),
                   mix(hash(i + vec3(0,1,1)), hash(i + vec3(1,1,1)), f.x), f.y), f.z);
}
void vertex() { local_position = VERTEX; }
void fragment() {
    float grain = noise3(local_position * vec3(25.0, 2.5, 25.0));
    float weather = noise3(local_position * 3.5);
    float crevice = smoothstep(0.35, 0.72, grain);
    vec3 bark = mix(vec3(0.12, 0.105, 0.09), vec3(0.32, 0.28, 0.225), crevice);
    bark = mix(bark, vec3(0.30, 0.31, 0.27), weather * 0.3);
    ALBEDO = bark * COLOR.rgb;
    ROUGHNESS = 0.94;
    SPECULAR = 0.22;
    NORMAL = normalize(NORMAL + vec3(dFdx(grain), dFdy(grain), 0.0) * 0.5);
}
