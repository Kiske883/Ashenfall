# Milestone 09B.4 — Godot combat stability

**09B.4 — GODOT COMBAT STABILITY VALIDATED** for the headless checks below.

## Reproduction and correction

On Godot 4.7.2 .NET, `--headless --path . --script
res://docs/validate_bidirectional_combat.gd` reproduced six
`Handle is not initialized` managed errors while assertions reported zero
failures. The full pre-fix output is in `09B4_combat_before.log`; stack traces
point to `ScriptManagerBridge.SwapGCHandleForType` while the next fixture is
instantiated after the occlusion fixture.

The fixture teardown queued the scene for deletion and resumed on
`process_frame`, which is emitted before queued frees are processed. The next
fixture could consequently be instantiated during the old scene's teardown.
`docs/validate_bidirectional_combat.gd` now awaits the old scene's `tree_exited`
signal instead. The evidence supports this fixture-lifecycle ordering as the
trigger; it does not establish a deeper engine-internal cause. No gameplay code,
combat checks or timing changed.

Three consecutive post-fix runs exited zero, with zero assertion failures,
zero `ERROR:` lines and zero `WARNING:` lines. Full outputs are in
`09B4_combat_after1.log`, `09B4_combat_after2.log`, and
`09B4_combat_after3.log`. The first run also explicitly counted zero handle
errors. A previous 09B.2 session had reproduced six errors on a repeated run.

## Regression checks

`dotnet build --no-restore -v:q` passed with zero warnings/errors. Godot headless
editor import and startup passed. Headless scripts `validate_first_adventure.gd`,
`validate_player_melee.gd`, `validate_catalog_gameplay.gd` and
`validate_milestone_08g.gd` exited zero and reported zero failures. These check
encounters, click movement, melee, catalog gameplay, animation and restart.
Commands use the existing Godot console executable with `--headless --path .`;
append `--script res://docs/<script>` for each script.

This is headless validation; no new manual visual gameplay session is claimed.
