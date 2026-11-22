"""Author isolated combat locomotion and Impact C; never writes approved resources."""
from pathlib import Path
import re
import sys
from math import sin, pi

sys.dont_write_bytecode = True
import author_dwarf_approved_axe_attack as approved

# Model-unit offsets relative to the approved Impact A. Intentionally small.
IMPACT_C_EXTRA_AXE_DROP = 0.025
IMPACT_C_EXTRA_TORSO_LEAN = 0.025
IMPACT_C_EXTRA_KNEE_CROUCH = 0.008

impact_c_controls = list(approved.impact_controls)
impact_c_controls[0] = (0, -.07 - IMPACT_C_EXTRA_KNEE_CROUCH, -.008)
impact_c_controls[1] = (-.07 - IMPACT_C_EXTRA_TORSO_LEAN, 0, .01)
impact_c_controls[3] = (.01, -.03 - IMPACT_C_EXTRA_AXE_DROP, -.035)
impact_c = approved.approved_pose(impact_c_controls, 90.0)

KNOTS = [(t, impact_c if abs(t - .94) < 1e-7 or abs(t - 1.10) < 1e-7 else pose)
         for t, pose in approved.KNOTS]


def sample_attack(t):
    for i in range(len(KNOTS) - 1):
        a_time, a_pose = KNOTS[i]
        b_time, b_pose = KNOTS[i + 1]
        if t <= b_time + 1e-8:
            u = max(0, min(1, (t - a_time) / (b_time - a_time)))
            return approved.blend(a_pose, b_pose, u * u * (3 - 2 * u))
    return KNOTS[-1][1]


def grounded_track(name):
    source = Path('resources/animations/DwarfGroundedExperiment.tres').read_text()
    walk = source.split('[sub_resource type="Animation" id="Walk"]', 1)[1]
    pattern = r'tracks/\d+/path = NodePath\("\.:' + name + r'"\).*?"values": \[(.*?)\]'
    match = re.search(pattern, walk, re.S)
    if not match:
        raise ValueError('Missing grounded Walk track: ' + name)
    values = match.group(1)
    if 'Vector3' in values:
        return [tuple(map(float, p.split(','))) for p in re.findall(r'Vector3\(([^)]+)\)', values)]
    return [float(v) for v in values.split(',')]


def combat_walk():
    # Exact foot target and ankle curves from the approved grounded Walk.
    # Its pelvis motion is retained as a delta around Guard B so the upper
    # body starts and ends in precisely the same stance as Combat/GuardB.
    ground = {name: grounded_track(name) for name in
              ('BodyOffset', 'LeftFootOffset', 'RightFootOffset', 'LeftFootPitch', 'RightFootPitch')}
    count = len(ground['BodyOffset'])
    if count != 201 or any(len(v) != count for v in ground.values()):
        raise ValueError('Grounded Walk key count changed')
    first = ground['BodyOffset'][0]
    frames = []
    for i in range(count):
        phase = 2 * pi * i / (count - 1)
        pose = list(approved.guard_b)
        delta = tuple(a - b for a, b in zip(ground['BodyOffset'][i], first))
        pose[0] = tuple(a + b for a, b in zip(pose[0], delta))
        # One tiny synchronized shoulder/body sway; axe and grips remain one
        # rigid torso attachment and retain the exact Guard B handle roll.
        pose[1] = approved.multiply(pose[1], approved.orientation((0, 0.007 * sin(phase), 0)))
        pose[5] = tuple(a + b for a, b in zip(pose[5], (0, 0, 0.004 * sin(phase))))
        pose[6], pose[7] = ground['LeftFootOffset'][i], ground['RightFootOffset'][i]
        pose[8], pose[9] = ground['LeftFootPitch'][i], ground['RightFootPitch'][i]
        frames.append(tuple(pose))
    return [i * .003 for i in range(count)], frames


def main():
    times = [i * .01 for i in range(171)]
    walk_times, walk_frames = combat_walk()
    lines = ['[gd_resource type="AnimationLibrary" load_steps=4 format=3]', '']
    lines += approved.animation('AxeAttack', 1.7, times, [sample_attack(t) for t in times], False)
    lines += ['']
    lines += approved.animation('GuardB', 1.0, [0.0, 1.0], [approved.guard_b] * 2, True)
    lines += ['']
    lines += approved.animation('CombatWalk', .6, walk_times, walk_frames, True)
    lines += ['', '[resource]', '_data = { &"AxeAttack": SubResource("AxeAttack"), &"GuardB": SubResource("GuardB"), &"CombatWalk": SubResource("CombatWalk") }', '']
    Path('resources/animations/DwarfCombatGuardImpactC.tres').write_text('\n'.join(lines), encoding='utf-8')
    print('Wrote isolated CombatWalk (201 keys) and Impact C attack (171 keys)')


if __name__ == '__main__':
    main()
