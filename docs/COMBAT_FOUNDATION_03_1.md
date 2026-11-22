# Combat Foundation 03.1 — one melee order

This is the requested combat-roadmap 03.1. The repository's earlier Milestone 03 documents describe tree art; this file uses a distinct name to avoid replacing them. Work remains local on `feature/milestone-05-animation` at HEAD `2735828`. The pre-existing uncommitted 06D art, animation, tests and captures are retained. Nothing was staged, committed, pushed, reset or deleted.
Before editing, Git showed 7 modified tracked paths and 83 untracked entries. `DwarfPlayerPresentation.cs` and the definitive dwarf scene already had 06D edits, so the integration extends them with narrow patches. At final validation Git shows 9 modified tracked paths and 90 untracked entries, including the retained 06D work and new 03.1 files.

## Play

Open `res://scenes/world/AshenfallDwarfPlayableDemo.tscn` in Godot 4.7.2 Mono and press **F6**.

- Left-click or hold on terrain: existing click-to-move and held destination updates.
- Left-click the Fallen Guardian: replace any ground order with **one** approach and **one** melee attack. The player follows a navigation path, stops within a configurable center-based range, faces the target and plays the approved 1.70 s Impact C attack. There is no automatic repeat.
- WASD: existing manual movement and target-order cancellation.
- Space: issue a new attack order against the selected valid target. Without a selected target it retains the prior visual practice swing, now with the same logical movement lock.
- During a committed attack, horizontal movement is locked for the configured duration; gravity and collision remain active. A ground click can set the next navigation destination, which starts moving when the lock ends.

There is no damage, health, death, hitbox, hit detection, enemy reaction, loot, networking or attack loop. The Fallen Guardian's own AI attack signal remains unchanged.

## Architecture

`PlayerMeleeController` owns the command and state (`Idle`, `Approach`, `Attack`), target validation, timed repathing, range/line-of-sight/facing checks, a configurable `AttackDuration` (initially 1.70 s), and the temporary movement lock. `IssueAttack(MeleeTarget)` is callable without mouse input and does not reference the dwarf, mesh or animation player. A passive `MeleeTarget` marker lives on the Guardian's collision actor; another physics actor can reuse it.

The existing `PlayerController` remains the sole mover and navigation-agent owner. It provides a small optional physics-time click resolver: the target ray uses layers 1 and 2, excludes the player, and honors intervening solid geometry. When that ray hits a target, the click is consumed before ground acquisition; otherwise the original ground ray and plane fallback run unchanged. The target controller uses the actor's existing `TrySetDestination` and `CancelDestination`. Its destination lies on the last segment of an existing navigation route, one arrival tolerance inside the requested melee radius. It does not rebuild navigation or bypass physical collision. Invalid, removed, blocked or unreachable targets cancel the order.

The logical `AttackStarted(target, sequence)` signal is the only new attack-start event. `DwarfPlayerPresentation` observes it, scales the approved visual clip to the configured logical duration and faces the target during the final turn and swing. Animation completion does not produce another logical attack or damage event. A future transport can send an actor/target-ID command to an authoritative host, which can call `IssueAttack` after validation; local `Node3D` references in this prototype are **not** network identifiers, and no network authority is implemented yet.

## Files

Created:

- `scripts/combat/MeleeTarget.cs` and `PlayerMeleeController.cs`, plus Godot `.uid` sidecars.
- `docs/validate_player_melee.gd` and its `.uid` sidecar.
- `docs/combat_03_1_{approach,windup,recovery}.png` — Forward+ gameplay-camera captures.
- This document.

Modified with narrow additions:

- `scripts/characters/PlayerController.cs` — optional physics-time click arbitration and horizontal movement lock; original terrain targeting/navigation remain the fallback.
- `scripts/characters/DwarfPlayerPresentation.cs` — one-way combat signal subscription, target-facing override and duration-scaled visual playback; prior 06D curves remain unchanged.
- `scenes/enemies/FallenGuardian.tscn` — passive target marker; existing collider, AI and presentation remain unchanged.
- `scenes/world/AshenfallDwarfPlayableDemo.tscn` — connects the generic melee component to the existing player and dwarf adapter.
- `scenes/world/AshenfallDwarfLocomotionExperiment.tscn` and `AshenfallDwarfFiveMeterComparison.tscn` — disable the inherited melee component so their previous input and locomotion behavior remain intact.

The two previously modified 06D files and other untracked 06D work were extended in place or preserved. Approved animation `.tres` files, geometry, navigation resources, camera, world generator and enemy AI source were not changed.

## Validation performed

- `dotnet build --nologo`: passed, 0 warnings and 0 errors.
- Godot 4.7.2 Mono headless editor import and bounded startup of the definitive scene: passed.
- New `validate_player_melee.gd`: passed with zero failures. It injects real viewport clicks, held ground drag/release, enemy selection, approach, range stop, one attack, lock, recovery, input-independent repeat command, manual cancellation, unreachable target, configurable duration and queued ground order.
- Existing `validate_dwarf_combat_playable.gd`, `validate_dwarf_animation.gd`, `validate_dwarf_combat_guard_variant.gd`, `validate_enemy_attack.gd`: passed with zero failures.
- Existing `validate_dwarf_playable.gd` and its 5 m/s historical comparison, `validate_playable_demo.gd`, `validate_mouse_navigation.gd`, `validate_gate_navigation.gd` and `inspect_terrain_edges.gd`: passed with zero failures. The original grip tolerance was not changed.
- Vulkan Forward+ rendered approach, windup and recovery at the gameplay camera; the images were inspected. The characters are small at orthographic size 22, so fine grip/contact judgments still need the close preview and manual gameplay review.

## Limits and manual review

The attack order is one-shot. A target that runs away after the swing needs another click or Space. Space without a target produces a visual practice swing. A ground click during the lock is queued; WASD during the lock cancels target selection but cannot move until the attack completes. There is no hit result or enemy reaction. The line-of-sight and navigation policy is designed for this single-floor prototype; complex multi-floor combat and network IDs belong to later milestones.

Manually test enemy clicks from each side, intervening trees/rocks/gate pillars, repeat orders, terrain clicks during the swing, held mouse navigation, WASD cancellation, both arches and terrain edges. Observe the timing and facing at gameplay scale. Automated navigation regressions passed, but manual melee routing around every obstacle was not performed in this task.

## Rollback

Keep the pre-existing 06D work. Revert only the 03.1 additions in the six modified scene/script files above and remove only the new 03.1 combat scripts, test, images, documentation and their `.uid` files after review. Do not reset or clean the working tree, because it contains unrelated uncommitted work.
