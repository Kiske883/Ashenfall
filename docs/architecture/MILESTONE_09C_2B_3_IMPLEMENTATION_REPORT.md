# 09C.2B.3 — floating damage feedback

The existing `AdventureCombatFeedback` system is now present in the dwarf demo
and therefore also in the persistence preview. The first adventure inherits the
same single instance. Both combat controllers report the actual HP reduction in
their existing `DamageApplied` signal after health accepts a hit. A missed,
interrupted, duplicate or rejected impact produces no successful event. A
lethal overkill reports only the target's remaining HP. The health component,
damage formulas, attack timing and snapshot logic are unchanged.

The existing camera-facing `Label3D` feedback has an outline, warm ivory for
damage dealt and muted red for damage received, rises 0.34 world units and fades
over one second. Its initial position is above the actor; each label and tween
is independently cleaned up. The presentation distinguishes dealt, critical and
received events, with configurable colors and optional critical label. Existing
development-only critical experimentation predates this milestone; no critical
rolls or mechanics were added here. Flash cleanup now reuses a list per frame.

## Files

- `scripts/combat/MeleeDamageController.cs` and
  `scripts/combat/EnemyMeleeDamageController.cs`: emit actual health reduction.
- `scripts/world/AdventureCombatFeedback.cs`: reusable configurable number
  styling and lifecycle.
- `scenes/world/AshenfallDwarfPlayableDemo.tscn`: add feedback to the demo.
- `scenes/world/AshenfallFirstAdventure.tscn`: inherit that instance, avoiding
  duplicate feedback.
- `docs/validate_health_damage_death.gd`: checks 25 and overkill display.
- `docs/validate_milestone_08.gd`: waits for the new one-second lifetime.
- This report.

## Validation and remaining review

Godot C# build passed with zero warnings/errors. The headless
`validate_health_damage_death.gd` passed with zero failures; it checked a real
25-damage hit and a 40-damage attack against 25 remaining HP displaying 25,
duplicate events, misses, death and collision. `validate_milestone_08.gd` passed
with zero failures, including Guardian damage feedback, transient cleanup and
restart. The latter was rerun after the flash-list change. Both tests ran with
the catalog endpoint unset; no PostgreSQL or Git operations were performed.

The persistence-backed 27-damage path was not executed: a suitable verified API
process was not available for this validation, and a prior automatic policy
review rejected launching a mock. Multiple overlapping labels and rendered
camera readability have not been independently validated. Headless checks do
not establish visual placement or typography quality.

For manual validation, run `AshenfallDwarfPlayableDemo.tscn` with F6, hit a
Guardian and inspect the ivory 25; let the Guardian strike and inspect the red
number. For persistence mode, start the already documented, verified test-only
API and open `CharacterPersistencePreview.tscn`: verify 105/105, hit for 27,
and check its HUD. Review repeated hits, lethal hits, camera visibility and
cleanup after restarting. Wait for owner visual approval.

Rollback by selectively reverting the listed changes while preserving unrelated
work. No database rollback is required.
