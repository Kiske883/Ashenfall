# Milestone 09C.1 — read-only character integration

## Architecture

The optional `res://scenes/world/CharacterPersistencePreview.tscn` instances the
existing dwarf playable demo and adds a small status label. Its C# presentation
calls `PersistedCharacterClient`, a separate, typed HTTP client. The client calls
the existing development-only `GET /api/v1/dev/character/` endpoint. That API
selects the active character for its explicitly configured development subject;
the client requires an explicit expected character GUID and rejects a mismatch.
The response DTO carries ID, class ID, name, XP, allocated attributes and row
revision. This phase displays name and revision only: it does not apply persisted
stats to gameplay or write equipment, inventory or character data.

The API owns all PostgreSQL access. The preview accepts only a loopback HTTP base
URL, requires a development token and ID from the process environment, uses a
five-second timeout and a 64 KiB response limit. Missing configuration, an
unavailable API, missing character, incorrect ID and invalid response are shown
in the label. The preview runs only in Godot development builds. This gate and
the existing API identity are development tooling, not production authentication.

## Configuration and manual review

1. Configure and start the existing API in Development with its explicitly
   enabled local character endpoint, private token and subject, and point it at
   an appropriate database containing an existing character for that subject.
   Do not create or alter development-database data for this review without
   separate authorization. Use an isolated fixture if needed.
2. Launch Godot from a process with `ASHENFALL_CHARACTER_API_URL` set to the
   local API base URL, `ASHENFALL_CHARACTER_ID` set to that character's exact
   GUID, and `ASHENFALL_CHARACTER_DEV_TOKEN` set privately to the same token as
   the API. Never save a token in a scene, project file, source or this report.
   For example, enter it privately in PowerShell using `Read-Host -AsSecureString`
   and pass it to the process environment through your local secret workflow.
3. Open `scenes/world/CharacterPersistencePreview.tscn` in Godot .NET and press
   F6. The label should show the expected character and revision. Move with
   WASD and click-to-move; verify the camera and Guardian encounter still work.
4. Restart the scene with a different GUID and verify an error is shown; test an
   absent account and an offline API likewise. Restore the correct configuration
   for later review. No character or equipment writes occur from this scene.

## Validation

- Backend and Godot builds: zero warnings/errors. Backend live suite: **53 passed,
  0 failed, 4 skipped**. The four skips are the opt-in publisher tests against
  the development catalog; see the 09B.5 report.
- The typed client retrieved a real disposable character through the actual
  ASP.NET in-process HTTP pipeline and isolated PostgreSQL. An absent subject
  returned 404; a mismatched configured ID was rejected. The test host checks
  both EF connection strings target only the isolated database before requests.
- A real closed loopback socket and mocked transport tests verified offline and
  missing responses. These do not constitute a visual Godot gameplay session.
- Godot headless import and preview startup succeeded. A headless preview check
  confirmed missing configuration produces a visible, mouse-ignoring label;
  existing player melee regression reported zero failures. Manual F6 and
  external HTTP socket success remain to be tested by the owner.
- Isolated database test rows were cleaned up. No migrations, development data
  changes, Git operations or secrets in project files were introduced.

## Files and rollback

Created `scripts/characters/PersistedCharacterClient.cs`,
`scripts/characters/CharacterPersistencePreview.cs`,
`scenes/world/CharacterPersistencePreview.tscn`,
`backend/Ashenfall.Tests/PersistedCharacterClientTests.cs`,
`docs/validate_character_persistence.gd`, and this report. Godot may create
`.uid` sidecars. Modified `backend/Ashenfall.Tests/Ashenfall.Tests.csproj` and
`CharacterConcurrencyLiveTests.cs` to link/test the client and harden isolation;
updated the 09B.5 report. To roll back, remove only these new files and their
generated sidecars, and revert only the described additions in the three existing
files while preserving all earlier uncommitted work. No database rollback is
needed. Do not perform Git cleanup or reset.
