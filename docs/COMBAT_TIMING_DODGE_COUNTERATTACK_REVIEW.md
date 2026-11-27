# Combat timing and counterattack review

## Cause and scope

`CharacterPersistencePreview.tscn` instances the existing
`AshenfallDwarfPlayableDemo.tscn`; it adds a read-only character status label
and does not change enemy or player combat. The dwarf demo inherited the
Guardian's original 0.60 s windup, whereas Milestone 07.1 explicitly set
1.20 s on the three Guardians in `AshenfallFirstAdventure.tscn`. Thus the
reported behavior was a pre-existing difference between scenes, not a database
or persistence regression. At near-simultaneous starts, the Guardian's former
0.70 s damage attempt preceded the dwarf's 0.94 s impact. Dodging before that
impact deliberately cancels the dwarf's pending strike.

The dwarf demo now overrides its Guardian windup to 1.20 s. The persistence
preview inherits that setting. The separate original placeholder demo and the
Guardian prefab are unchanged. The first adventure already had 1.20 s windup;
its catalog bootstrap can still apply its own catalog-derived values. There is
no database or API change.

## Timing

| Timing | Dwarf demo / preview before | Dwarf demo / preview after |
| --- | ---: | ---: |
| Guardian preparation before `AttackExecuted` | 0.60 s | 1.20 s |
| Guardian damage attempt after execution | 0.10 s | 0.10 s |
| Guardian damage after windup start | 0.70 s | 1.30 s |
| Guardian recovery after execution | 1.00 s | 1.00 s |
| Earliest next damage after previous execution, if still in range | 1.70 s | 2.30 s |
| Dwarf impact / total attack | 0.94 / 1.70 s | 0.94 / 1.70 s |

The existing one-second recovery was retained: the scripted counterattack
starts in that phase, and the next windup supplies further time before the next
damage check. Facing, physics ticks and approach distance can add delays. The
Guardian animation controller maps the existing clip windup onto the configured
1.20 s and keeps its recovery mapping; the logical impact remains during the
strike. Damage values, ranges, movement, collision and cancellation logic are
unchanged. There is no invulnerability during retreat or attack.

## Validation

- `dotnet build --no-restore -v:q`: zero warnings/errors. Godot headless import
  and preview startup succeeded.
- `validate_counterattack.gd` used real movement, target approach, health and
  attack states. In separate scene runs, both the dwarf demo and persistence
  preview completed four dodge/counter cycles: Guardian 100 → 0 HP, dwarf
  remained at 100 HP. The command is issued during Guardian recovery; each hit
  lands before its next damage attempt. The test begins with a controlled
  starting position; this is not a claim that every terrain position is safe.
- The preview counterattack test also passed Godot `--fixed-fps` 30, 60 and
  120 runs. Physics remains on its existing fixed tick; these headless simulated
  rates are not measured graphics performance or human reaction tests.
- `validate_combat_feel.gd` passed: in-range late retreat still takes 10 damage,
  pre-impact WASD/ground-click movement cancels the dwarf hit, and visual
  strike and logical Guardian impact overlap. `validate_player_melee.gd`,
  `validate_bidirectional_combat.gd`, `validate_health_damage_death.gd` and
  `validate_dwarf_playable.gd` reported zero failures.

## Files and manual review

Modified `scenes/world/AshenfallDwarfPlayableDemo.tscn` (one scene override),
`docs/validate_bidirectional_combat.gd` and `docs/validate_dwarf_playable.gd`
(updated expected scene timing). Created `docs/validate_counterattack.gd` and
this report; Godot may generate a script `.uid`. No Git operations were used.

In the persistence preview, approach the Guardian, wait for the sword windup,
move outside its reach, then click the Guardian during recovery. Repeat while
monitoring both health bars. Also deliberately remain in range for a strike and
confirm it still costs 10 HP; interrupt an attack before the axe impact and
confirm it deals no damage. Verify readability, collision-constrained retreat,
camera and movement feel in manual gameplay. No new visual or human gameplay
approval is claimed by the automated tests.
