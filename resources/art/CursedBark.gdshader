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
    float grain = noise3(local_position * vec3(18.0, 1.8, 18.0));
    float weather = noise3(local_position * 3.5);
    float crevice = smoothstep(0.4, 0.7, grain);
    vec3 bark = mix(vec3(0.042, 0.035, 0.03), vec3(0.16, 0.135, 0.10), crevice);
    bark = mix(bark, vec3(0.12, 0.125, 0.11), weather * 0.22);
    float soil_contact = 1.0 - smoothstep(-0.02, 0.4, local_position.y);
    bark = mix(bark, vec3(0.055, 0.047, 0.033), soil_contact * 0.65);
    ALBEDO = bark * COLOR.rgb;
    ROUGHNESS = 0.98;
    SPECULAR = 0.1;
    NORMAL = normalize(NORMAL + vec3(dFdx(grain), dFdy(grain), 0.0) * 0.12);
}
