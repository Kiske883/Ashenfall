"""Build the isolated GUARD B -> WINDUP -> IMPACT A native animation.

The three endpoints are copied from the approved static controls. Rotations
are interpolated by normalized shortest-arc quaternion slerp. This offline
authoring tool writes only DwarfApprovedAxeAttack.tres.
"""

from math import acos, cos, sin, sqrt
from pathlib import Path
import sys

sys.dont_write_bytecode = True
from author_dwarf_overhead_attack import sample as prior_sample


def multiply(a, b):
    x, y, z, w = a
    X, Y, Z, W = b
    return (w*X + x*W + y*Z - z*Y,
            w*Y - x*Z + y*W + z*X,
            w*Z + x*Y - y*X + z*W,
            w*W - x*X - y*Y - z*Z)


def orientation(euler):
    # Godot Basis.FromEuler uses YXZ. The animation property setter converts
    # the sampled quaternion back to that same order before applying the rig.
    x, y, z = euler
    qx = (sin(x/2), 0, 0, cos(x/2))
    qy = (0, sin(y/2), 0, cos(y/2))
    qz = (0, 0, sin(z/2), cos(z/2))
    return multiply(multiply(qy, qx), qz)


def slerp(a, b, t):
    dot = sum(x*y for x, y in zip(a, b))
    if dot < 0:
        b = tuple(-v for v in b)
        dot = -dot
    if dot > 0.9995:
        values = tuple(x + (y-x)*t for x, y in zip(a, b))
    else:
        angle = acos(max(-1, min(1, dot)))
        denominator = sin(angle)
        left = sin((1-t)*angle)/denominator
        right = sin(t*angle)/denominator
        values = tuple(x*left + y*right for x, y in zip(a, b))
    size = sqrt(sum(v*v for v in values))
    return tuple(v/size for v in values)


def approved_pose(original, blade_roll):
    body, torso, head, offset, weapon, beard, left, right, left_pitch, right_pitch, twist = original
    return (body, orientation(torso), head, offset, orientation(weapon),
            beard, left, right, left_pitch, right_pitch, twist, blade_roll)


guard_b = approved_pose(prior_sample(.96), 109.5)
windup = approved_pose(prior_sample(.70), 90.0)
# The proven shoulder-side passage from the prior static investigation keeps
# the shaft clear of the head. It is a trajectory guide, not a new endpoint.
clearance = approved_pose(prior_sample(.84), 90.0)
impact_controls = list(prior_sample(0.0))
impact_controls[0] = (0, -.07, -.008)
impact_controls[1] = (-.07, 0, .01)
impact_controls[2] = (.025, 0, 0)
impact_controls[3] = (.01, -.03, -.035)
impact_controls[4] = (0, 0, 0)
impact_controls[5] = (.015, 0, 0)
impact_controls[10] = 0.0
impact_a = approved_pose(impact_controls, 90.0)

TRACKS = [
    "BodyOffset", "TorsoOrientation", "HeadRotation", "WeaponOffset",
    "WeaponOrientation", "BeardRotation", "LeftFootOffset", "RightFootOffset",
    "LeftFootPitch", "RightFootPitch", "ForearmTwistWeight", "BladeRollDegrees",
]


def blend(a, b, t):
    values = []
    for i, (x, y) in enumerate(zip(a, b)):
        if i in (1, 4):
            values.append(slerp(x, y, t))
        elif isinstance(x, tuple):
            values.append(tuple(v + (w-v)*t for v, w in zip(x, y)))
        else:
            values.append(x + (y-x)*t)
    return tuple(values)


# The middle of the lift is deliberately slow. The strike reaches Impact A
# in 0.18 s, versus 0.68 s for preparation. The three named anchors are exact.
KNOTS = [
    (0.00, guard_b),
    (0.12, blend(guard_b, clearance, .08)),
    (0.40, clearance),
    (0.68, windup),
    (0.76, windup),
    (0.84, clearance),
    (0.94, impact_a),
    (1.10, impact_a),
    (1.70, guard_b),
]


def sample(t):
    for i in range(len(KNOTS)-1):
        left_time, left = KNOTS[i]
        right_time, right = KNOTS[i+1]
        if t <= right_time + 1e-8:
            u = (t-left_time)/(right_time-left_time)
            u = max(0, min(1, u))
            u = u*u*(3-2*u)
            return blend(left, right, u)
    raise ValueError(t)


def format_value(value, index):
    if index in (1, 4):
        return "Quaternion(" + ", ".join(f"{v:.9f}" for v in value) + ")"
    if isinstance(value, tuple):
        return "Vector3(" + ", ".join(f"{v:.9f}" for v in value) + ")"
    return f"{value:.9f}"


def animation(name, duration, times, frames, looping):
    lines = [f'[sub_resource type="Animation" id="{name}"]',
             f'resource_name = "{name}"', f'length = {duration}']
    if looping:
        lines.append('loop_mode = 1')
    for i, track in enumerate(TRACKS):
        lines += [
            f'tracks/{i}/type = "value"',
            f'tracks/{i}/path = NodePath(".:{track}")',
            f'tracks/{i}/interp = 1',
            f'tracks/{i}/keys = {{',
            '"times": PackedFloat32Array(' + ", ".join(f"{t:.6f}" for t in times) + '),',
            '"transitions": PackedFloat32Array(' + ", ".join("1" for _ in times) + '),',
            '"update": 0,',
            '"values": [' + ", ".join(format_value(frame[i], i) for frame in frames) + ']',
            '}',
        ]
    return lines


def main():
    times = [i*.01 for i in range(171)]
    frames = [sample(t) for t in times]
    lines = ['[gd_resource type="AnimationLibrary" load_steps=3 format=3]', '']
    lines += animation('AxeAttack', 1.7, times, frames, False)
    lines += ['']
    lines += animation('GuardB', 1.0, [0.0, 1.0], [guard_b, guard_b], True)
    lines += ['', '[resource]', '_data = { &"AxeAttack": SubResource("AxeAttack"), &"GuardB": SubResource("GuardB") }', '']
    Path('resources/animations/DwarfApprovedAxeAttack.tres').write_text("\n".join(lines), encoding='utf-8')


if __name__ == '__main__':
    main()
