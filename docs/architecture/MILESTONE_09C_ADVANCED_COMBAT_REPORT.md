# Milestone 09C — development variable damage and critical hits

## Implementation

`scripts/combat/CombatDamageCalculator.cs` is a pure, injectable calculation service.
It rolls an **inclusive** integer range, tests a `[0,1)` random value with strict
`chance < probability`, applies the critical multiplier, then rounds midpoint away
from zero. Valid profiles cannot produce negative final damage. A positive roll has
minimum final damage 1; a zero roll has final damage 0 and is not applied. Armor is
carried in the result but deliberately has **no mitigation** because no armor formula
has been approved. Attack-cycle modifiers are represented in the profile but do not
change approved animation/impact timing. Actual damage remains clamped by the existing
`HealthComponent`, including overkill and one-time death.

Both existing melee damage controllers use the shared calculator only when a Debug
build has `ASHENFALL_DEV_VARIABLE_DAMAGE=1` or their explicit exported development
toggle is enabled. Defaults remain the published fixed **25 dwarf / 10 Guardian**.
The development profile defaults to dwarf 20–30, 10% critical, 1.5× and Guardian
8–12, 5%, 1.5×. The established logical attack sequence and impact validation still
consume each attack once; random values are drawn only after a valid impact. No
animation, AI, movement, dodge, health, catalog row or attack timing was changed.
`AdventureCombatFeedback` displays a restrained gold `CRIT -N` damage number for a
successful critical hit; existing successful-hit feedback still fires once.

## Validation

- Backend unit tests cover inclusive bounds, strict probability boundary, zero chance,
  deterministic critical roll, rounding and invalid random/profile rejection.
- `docs/validate_variable_damage.gd` exercised a dwarf critical hit (20 × 1.5 = 30)
  and a Guardian critical hit (8 × 1.5 = 12) in the actual first-adventure scene,
  verified one feedback event and the distinct number. Exit 0, zero failures.
- The focused Godot test also passed at fixed 30, 60 and 120 FPS. Rendered Vulkan
  Forward+ startup on the GTX 650 passed with zero focused failures.
- Existing catalog reload/gameplay, first adventure, melee, health/death, combat feel,
  Milestone 08/08G and mouse navigation regression scripts exited 0 with zero test
  failures. `validate_bidirectional_combat.gd` reported zero assertion failures but
  intermittently emitted a headless `Handle is not initialized` Godot error despite
  exit 0; this remains a diagnostic limitation, not a clean pass claim.
- Backend: 40 passed, 4 live database tests skipped; Godot and backend C# builds had
  zero warnings/errors. No catalog data or PostgreSQL schema was changed.

Created `scripts/combat/CombatDamageCalculator.cs` and
`docs/validate_variable_damage.gd`. Modified the two existing melee damage
controllers, `scripts/world/AdventureCombatFeedback.cs`, and backend test project/
focused tests. No scene or animation resource changed.

Manual review: start the first adventure in a Debug build, set
`ASHENFALL_DEV_VARIABLE_DAMAGE=1` for that Godot process, and observe varied values
and occasional gold critical numbers on both combatants. Remove the variable to
return immediately to published fixed values on the next launch. Confirm attack
timing, interruptions, deaths and victory/defeat in the GUI. A catalog-authoritative
variable profile requires a separately approved schema/catalog release; none was
published or executed here.
