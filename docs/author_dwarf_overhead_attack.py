"""Author the isolated overhead attack AnimationLibrary from grip-centred poses.

This offline tool uses only the Python standard library. The approved rig, axe,
Idle and Walk resources are never edited. Run from the project root.
"""

from math import cos, sin
from pathlib import Path


DURATION = 1.7
STEP = 0.01
# Each pose: time, axe centre offset, short (X,Z) lift/chop rotation, torso XYZ,
# body translation, head XYZ, beard XYZ. A short sagittal tilt and a modest
# lateral adjustment move one rigid axe with both grips; the centre moves
# vertically and fore/aft with the hands.
POSES = [
    (0.00, (0, 0, 0), (0, 0), (0, 0, 0), (0, 0, 0), (0, 0, 0), (0, 0, 0)),
    (0.18, (-0.015, -0.025, 0.025), (-0.03, -0.05), (-0.01, -0.025, 0), (0, -0.025, 0.012), (0.01, 0, 0), (0.008, 0, 0)),
    (0.42, (-0.07, 0.23, 0.09), (0.50, 0.20), (0.055, 0, 0), (0, -0.035, 0), (-0.025, 0, 0), (-0.01, 0, 0)),
    (0.67, (-0.16, 0.35, 0.23), (0.95, 0.35), (0.075, 0.025, 0), (0, -0.04, -0.01), (-0.045, 0, 0), (-0.025, 0, 0)),
    (0.74, (-0.16, 0.35, 0.23), (0.95, 0.35), (0.075, 0.025, 0), (0, -0.04, -0.01), (-0.045, 0, 0), (-0.025, 0, 0)),
    (0.84, (-0.18, 0.27, 0.18), (0.60, 0.14), (0.00, 0, 0), (0, -0.04, 0), (0, 0, 0), (0, 0, 0)),
    (0.96, (0.025, -0.08, 0.07), (-0.25, -0.43), (-0.11, -0.025, 0), (0, -0.055, 0.015), (0.045, 0, 0), (0.022, 0, 0)),
    (1.12, (0.025, -0.11, 0.07), (-0.20, -0.37), (-0.095, -0.015, 0), (0, -0.055, 0.015), (0.035, 0, 0), (0.015, 0, 0)),
    (1.35, (0.005, -0.04, 0.03), (-0.10, -0.18), (-0.025, 0, 0), (0, -0.025, 0), (0.012, 0, 0), (0.006, 0, 0)),
    (1.70, (0, 0, 0), (0, 0), (0, 0, 0), (0, 0, 0), (0, 0, 0), (0, 0, 0)),
]

TRACKS = [
    "BodyOffset", "TorsoRotation", "HeadRotation", "WeaponOffset",
    "WeaponRotation", "BeardRotation", "LeftFootOffset", "RightFootOffset",
    "LeftFootPitch", "RightFootPitch", "ForearmTwistWeight",
]


def smooth(a, b, t):
    f = (t - a[0]) / (b[0] - a[0])
    f = f * f * (3 - 2 * f)
    return [
        tuple(x + (y - x) * f for x, y in zip(a[i], b[i]))
        for i in range(1, 7)
    ]


def sample(t):
    for i in range(len(POSES) - 1):
        if t <= POSES[i + 1][0] + 1e-8:
            offset, angles, torso, body, head, beard = smooth(POSES[i], POSES[i + 1], t)
            # The two hand targets come from one rigid axe transform. Neither
            # hand gets an independent weapon track or a separate rotation.
            twist = min(0.5, abs(angles[0]) * 0.55)
            return [body, torso, head, offset, (angles[0], 0, angles[1]), beard,
                    (0, 0, 0), (0, 0, 0), 0, 0, twist]
    raise ValueError(t)


def godot_value(value):
    if isinstance(value, tuple):
        return "Vector3(" + ", ".join(f"{v:.9f}" for v in value) + ")"
    return f"{value:.9f}"


def main():
    times = [i * STEP for i in range(round(DURATION / STEP) + 1)]
    frames = [sample(t) for t in times]
    # A quick author-time grip-order check: the left grip must stay to the
    # dwarf's left through this short overhead arc (no crossed hands).
    for frame in frames:
        angle = frame[4][2]
        left_x = -.1472 * cos(angle) + .168 * sin(angle)
        right_x = .1472 * cos(angle) - .168 * sin(angle)
        assert left_x < right_x, "Grip order crossed during overhead raise"
    lines = [
        '[gd_resource type="AnimationLibrary" load_steps=2 format=3]',
        '[sub_resource type="Animation" id="AxeAttack"]',
        'resource_name = "AxeAttack"',
        f'length = {DURATION}',
    ]
    for idx, name in enumerate(TRACKS):
        values = ", ".join(godot_value(frame[idx]) for frame in frames)
        lines += [
            f'tracks/{idx}/type = "value"',
            f'tracks/{idx}/path = NodePath(".:{name}")',
            f'tracks/{idx}/interp = 1',
            f'tracks/{idx}/keys = {{',
            '"times": PackedFloat32Array(' + ", ".join(f"{t:.6f}" for t in times) + '),',
            '"transitions": PackedFloat32Array(' + ", ".join("1" for _ in times) + '),',
            '"update": 0,',
            '"values": [' + values + ']',
            '}',
        ]
    lines += ['', '[resource]', '_data = { &"AxeAttack": SubResource("AxeAttack") }', '']
    Path('resources/animations/DwarfAxeOverhead.tres').write_text("\n".join(lines), encoding='utf-8')


if __name__ == '__main__':
    main()
