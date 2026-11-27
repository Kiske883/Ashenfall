# 09C.2B.1 — read-only gameplay snapshot preview

The existing preview now fetches `GET /api/v1/dev/character/gameplay-snapshot`
and displays the configured character GUID, backend character revision, level,
maximum HP and fixed damage. These are display-only values; the existing
playable scene, health initialization, combat and movement are unchanged. The
endpoint's actual revision field is `characterRevision`. Its snapshot contract
does not include the character's name.

`PersistedCharacterClient.LoadSnapshotAsync` checks the loopback-only HTTP URL,
explicit ID and token, request cancellation, five-second timeout, 64 KiB response
limit, all required fields and types, schema version 1, matching nonempty ID,
positive revision, class/release IDs, version and lowercase SHA-256 shape, level,
health and damage bounds. It distinguishes authentication, unavailable service,
missing character, invalid response and timeout in the status label. It does not
verify that the catalog hash covers progression or recompute that hash. There is
no refresh or cache: a failed load shows an error rather than reporting old data
as loaded. The preview does not persist or apply the response to combat.

Changed files: `scripts/characters/PersistedCharacterClient.cs`,
`scripts/characters/CharacterPersistencePreview.cs`,
`backend/Ashenfall.Tests/PersistedCharacterClientTests.cs`,
`docs/architecture/MILESTONE_09C_1_MANUAL_PREVIEW_SETUP.md`, and this report.

Validation: Godot C# build passed without warnings; headless editor import exited
successfully. Isolated backend build and full test run: 60 passed, 0 failed,
9 skipped (five opt-in database tests and four protected development-catalog
tests). The seven focused client tests include valid, malformed, mismatched and
HTTP error cases. A separately launched local API on loopback port 5098, backed
by `ashenfall_09b3_test`, returned HTTP 200 with the expected identity, release
version/hash and revisions 1 and 2; the level-2 character had 105/27 without
equipment and 121/30 after a real HTTP equip. The test used unique disposable
account, character and item IDs; exact-ID cleanup completed, and a subsequent
read-only query found zero player rows. The initial TCP attempt produced no
evidence and was repeated successfully. Authentication and failure responses
were tested with mocked HTTP, not via the external TCP run. No production or
development database or Git operations were used.

For manual F6 validation use the updated 09C.1 manual setup guide. Confirm the
GUID and stats, move and fight, then test an offline API and an incorrect ID.
Manual rendering and gameplay validation remain outstanding.
Rollback by selectively reverting only the changes listed above; preserve prior
uncommitted work. No database rollback is required for this implementation.
