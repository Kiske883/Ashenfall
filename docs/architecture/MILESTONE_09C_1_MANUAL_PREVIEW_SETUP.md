# 09C.1 — local manual preview setup

## Current accepted state — 09C.4 closure (2026-11-26)

The owner has completed the 5% -> 100% -> 5% HTTP/Godot acceptance workflow.
The active release is `a5897962-2202-4fe1-b7bb-99a12597ab5d`, version
`critical-validation-restored-20261125`, V2 hash
`af01cb185a781e87a1619ca10d44f3550d841bdf904cb1c9de63c91f9fdd979e`.
The existing manual character remains `d9a62a3c-4996-4276-8ab8-2c4d6f2ec988`,
historical class `90000000-0000-0000-0000-000000000002`, XP 100, revision 1,
level 2, maximum HP 105, damage 27–27, critical chance 5%, bonus +100%.
Earlier release/provisioning text below is historical; **do not create or delete
a character to recheck this accepted instance**.

Development requests use **`X-Ashenfall-Dev-Token`**, never `Authorization: Bearer`.
For the existing character's authenticated GET, read the private token from the
Windows user variable `ASHENFALL_DEV_IDENTITY_TOKEN` and call
`http://127.0.0.1:5093/api/v1/dev/character/gameplay-snapshot` using the complete
secret-safe PowerShell example in
[the CLI guide](CATALOG_BALANCE_CLI_GUIDE.md#authenticated-development-http-verification).
Do not print the token, use it in command arguments or persist it in files.
The existing verified API process must target only `ashenfall_09b3_test` and
use the configured subject for this character. Keep the already accepted private
API/editor token pairing; no authentication implementation changes are required.

## Historical setup instructions

The isolated database now has active release `snapshot-09c3-v2` and retains the
existing manual character `d9a62a3c-4996-4276-8ab8-2c4d6f2ec988` at level 2,
revision 1. Its original class identity is resolved through the published mapping;
do not recreate it. Reusing it requires the subject
`manual-09c2b1-d9a62a3c-4996-4276-8ab8-2c4d6f2ec988` and the same private
process token in the API and newly launched Godot editor. Expected V2 stats are
105 maximum HP, range 27–27, 5% critical chance and +100% critical bonus.

The opt-in procedure below creates a **different** disposable level-1 character
using the active V2 class. It is not required to reuse the existing manual
character. Never point it at `ashenfall_dev`. Use a fresh backend build: an older
API process may not support V2 or historical class mappings.

## Configuration

The API reads these **process environment variables** at startup:

- `ASPNETCORE_ENVIRONMENT=Development` and `DOTNET_ENVIRONMENT=Development`.
- `ASPNETCORE_URLS=http://127.0.0.1:5093` for this separate local instance.
- `ASHENFALL_DB_CONNECTION`: local PostgreSQL settings for database
  `ashenfall_09b3_test`, user `ashenfall_app`, with no password in the string.
- `ASHENFALL_CHARACTERS_ENABLED=1`.
- `ASHENFALL_DEV_IDENTITY_SUBJECT`: a unique temporary account subject.
- `ASHENFALL_DEV_IDENTITY_TOKEN`: a private token of at least 32 characters.

The Godot editor **process environment**, inherited by its F6 game process,
must contain `ASHENFALL_CHARACTER_API_URL`, `ASHENFALL_CHARACTER_ID`, and
`ASHENFALL_CHARACTER_DEV_TOKEN`. The last value must equal the API token. The
preview only accepts an HTTP loopback URL and a specific character GUID. The
endpoint derives the account from the API's configured subject; it does not
accept an account choice from Godot.

PostgreSQL passwords come from the owner's existing private Windows pgpass
configuration. The steps below generate a random token in memory. Do not echo
the token, paste it into chat, put it in source, `.env.local`, user secrets,
command-line arguments or Git, or run this inside a recorded transcript. The
token is inherited by the API and Godot processes and lasts for this local test.
Close those processes and the PowerShell session after use.

## Start an isolated API and create one disposable character

Run the following in a new PowerShell window at the repository root. These
commands **will insert one account and one character in the isolated database**
only when you choose to execute them. The local port check prevents contacting
an existing listener. Review the whole procedure, including cleanup, first.

```powershell
Set-Location 'C:\Propio\Proyectos\Ashenfall'
$testPort = 5093
if (Get-NetTCPConnection -LocalPort $testPort -State Listen -ErrorAction SilentlyContinue) {
    throw 'Port 5093 is already in use; choose a free port and update the URL below.'
}

# Verify the target and the synthetic class without writing data.
& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U ashenfall_app -d ashenfall_09b3_test -c "SELECT current_database(), current_user; SELECT c.id, c.code FROM character_classes c JOIN catalog_releases r ON r.id=c.catalog_release_id WHERE c.code='dwarf_warrior' AND r.status='published';"
if ($LASTEXITCODE -ne 0) { throw 'Isolated database preflight failed.' }

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5093'
$env:ASHENFALL_DB_CONNECTION = 'Host=localhost;Port=5432;Database=ashenfall_09b3_test;Username=ashenfall_app'
$env:ASHENFALL_CHARACTERS_ENABLED = '1'
$env:ASHENFALL_DEV_IDENTITY_SUBJECT = '09c1-manual-' + [guid]::NewGuid().ToString('N')
$randomBytes = New-Object byte[] 32
$randomGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $randomGenerator.GetBytes($randomBytes) } finally { $randomGenerator.Dispose() }
$env:ASHENFALL_DEV_IDENTITY_TOKEN = [Convert]::ToBase64String($randomBytes)
$env:ASHENFALL_CHARACTER_DEV_TOKEN = $env:ASHENFALL_DEV_IDENTITY_TOKEN
$env:ASHENFALL_CHARACTER_API_URL = 'http://127.0.0.1:5093/'

# Isolated output avoids replacing binaries of an API already running elsewhere.
$buildOutput = Join-Path $env:TEMP 'Ashenfall09C1ManualBuild'
dotnet build backend/Ashenfall.Api/Ashenfall.Api.csproj --no-restore "-p:BaseOutputPath=$buildOutput/"
if ($LASTEXITCODE -ne 0) { throw 'API build failed.' }
$apiExe = Join-Path $buildOutput 'Debug\net8.0\Ashenfall.Api.exe'
$apiProcess = Start-Process -FilePath $apiExe -WorkingDirectory (Get-Location).Path -WindowStyle Hidden -PassThru

# Wait for this exact process to own the listener; do not send a request to an
# unrelated process if startup fails or another program claims the port.
$ownedListener = $false
for ($attempt = 0; $attempt -lt 50; $attempt++) {
    if ($apiProcess.HasExited) { throw 'The isolated API process exited during startup.' }
    $listener = Get-NetTCPConnection -LocalPort $testPort -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.OwningProcess -eq $apiProcess.Id }
    if ($listener) { $ownedListener = $true; break }
    Start-Sleep -Milliseconds 200
}
if (-not $ownedListener) { throw 'The isolated API did not bind the expected port.' }

# This POST uses a newly generated subject and the existing synthetic class.
# It creates a disposable account and character; no equipment or inventory.
$requestHeaders = @{ 'X-Ashenfall-Dev-Token' = $env:ASHENFALL_DEV_IDENTITY_TOKEN }
$requestBody = @{
    classDefinitionId = '91000000-0000-0000-0000-000000000002'
    name = 'Preview Dwarf'
} | ConvertTo-Json -Compress
$createdCharacter = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:5093/api/v1/dev/character/' `
    -Headers $requestHeaders -ContentType 'application/json' -Body $requestBody
if (-not [guid]::TryParse([string]$createdCharacter.id, [ref]([guid]::Empty))) {
    throw 'The API did not return a valid character ID; use the cleanup section.'
}
$env:ASHENFALL_CHARACTER_ID = [string]$createdCharacter.id

# Open a new editor process so it inherits the three preview variables.
Start-Process -FilePath 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64.exe' `
    -ArgumentList @('--editor', '--path', 'C:\Propio\Proyectos\Ashenfall')
```

Keep this PowerShell window open: it holds the generated subject needed for
precise cleanup. If the POST fails after account creation, run cleanup anyway.
Do not use the ordinary `Start-AshenfallApi.ps1` helper for this test: it rejects
an explicit connection variable and uses the existing development user secrets.
The optional catalog autoload has separate configuration
`ASHENFALL_CATALOG_API_URL`; the hash-verified synthetic test catalog is not a
production catalog. The preview still uses the existing playable demo.

## F6 checklist

1. In the newly opened Godot .NET editor, open
   `res://scenes/world/CharacterPersistencePreview.tscn` and press F6.
2. Confirm the label shows the configured character GUID, revision, level,
   maximum HP and fixed damage. The request is read-only; persistence mode uses
   the accepted stats to initialize the encounter before gameplay starts. This
   newly created character starts at level 1.
3. Check WASD, click-to-move, camera following and the Fallen Guardian encounter.
4. Stop the API process launched above and rerun F6: the label should report
   that the character API is unavailable while gameplay remains usable.
5. To check a mismatched character, restart the isolated API with
   `$apiProcess = Start-Process -FilePath $apiExe -WorkingDirectory (Get-Location).Path -WindowStyle Hidden -PassThru`,
   set `ASHENFALL_CHARACTER_ID` to a different GUID, then launch a **new editor
   process** as above and confirm the error label. Environment changes do not
   propagate to an already running editor. Keep `$cleanupSubject` or the original
   subject environment variable for cleanup; the ID need not be restored.

External HTTP V2 validation is recorded in the 09C.3 implementation report.
Manual visual gameplay acceptance remains the owner's check.

## Remove only this test's records

Stop the F6 game, close the Godot editor you launched for this test, and stop
only `$apiProcess` if it is still running. Then, in the **same PowerShell
window**, run the following. It checks the generated subject format and connects
explicitly to the isolated database. The DELETE statements are scoped to that
unique subject. They are intended for the account/character created above,
which has no equipment or inventory. If cleanup fails, stop and report the
subject and error; do not broaden the DELETE.

```powershell
if ($apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id }
$cleanupSubject = $env:ASHENFALL_DEV_IDENTITY_SUBJECT
if ($cleanupSubject -notmatch '^09c1-manual-[0-9a-f]{32}$') { throw 'Unexpected subject; cleanup stopped.' }
$cleanupSql = @"
BEGIN;
DELETE FROM player_characters
 WHERE account_id IN (SELECT id FROM accounts WHERE identity_provider='local_development' AND provider_subject='$cleanupSubject');
DELETE FROM accounts WHERE identity_provider='local_development' AND provider_subject='$cleanupSubject';
COMMIT;
"@
$cleanupSql | & 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d ashenfall_09b3_test -f -
if ($LASTEXITCODE -ne 0) { throw "Cleanup failed for subject $cleanupSubject; stop and investigate." }
Remove-Item Env:ASHENFALL_DEV_IDENTITY_TOKEN, Env:ASHENFALL_CHARACTER_DEV_TOKEN, Env:ASHENFALL_DEV_IDENTITY_SUBJECT, Env:ASHENFALL_CHARACTER_ID -ErrorAction SilentlyContinue
```

This cleanup is the rollback for the disposable data; no schema rollback or
migration is involved. If you close the PowerShell window before cleanup, the
unique subject is lost from that session: identify the exact test record before
deleting anything. Preserve all other accounts and catalog data.
