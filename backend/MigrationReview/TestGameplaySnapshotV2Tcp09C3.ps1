# Read-only real TCP validation for the existing manual character; no database writes.
param(
    [Parameter(Mandatory=$true)][string]$ApiDll,
    [int]$Port = 5098
)
$ErrorActionPreference = 'Stop'
$psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
$character = 'd9a62a3c-4996-4276-8ab8-2c4d6f2ec988'
$class = '90000000-0000-0000-0000-000000000002'
$release = '91000000-0000-0000-0000-000000000001'
$hash = 'd1ef0a212b83df17300088210d081a63951621b2602e7ff35cf2a711c4de7d4d'
$api = $null

function Test-IsolatedState {
    $result = & $psql -X -w -q -A -t -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d ashenfall_09b3_test -c "BEGIN READ ONLY; SELECT current_database()='ashenfall_09b3_test' AND inet_server_port()=5432 AND (inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr()='::1'::inet) AND (SELECT system_identifier::text FROM pg_control_system())='7708337984335018212' AND EXISTS (SELECT 1 FROM player_characters WHERE id='$character' AND class_definition_id='$class' AND experience=100 AND row_revision=1 AND deleted_at IS NULL) AND (SELECT count(*) FROM catalog_releases WHERE status='published')=1 AND EXISTS (SELECT 1 FROM catalog_releases r JOIN catalog_v2_manifest m ON m.release_id=r.id WHERE r.id='$release' AND r.status='published' AND m.content_sha256='$hash') AND NOT EXISTS (SELECT 1 FROM owned_items WHERE character_id='$character') AND NOT EXISTS (SELECT 1 FROM character_equipment WHERE character_id='$character'); COMMIT;"
    if ($LASTEXITCODE -ne 0 -or ([string]($result -join '')).Trim() -ne 't') { throw 'Isolated database, catalog or character preflight failed.' }
}

try {
    if ($Port -lt 1024 -or $Port -gt 65535 -or -not (Test-Path -LiteralPath $ApiDll)) { throw 'Invalid API binary or port.' }
    if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) { throw 'Requested TCP port is occupied.' }
    Test-IsolatedState
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"
    $env:ASHENFALL_DB_CONNECTION = 'Host=localhost;Port=5432;Database=ashenfall_09b3_test;Username=ashenfall_app'
    $env:ASHENFALL_CHARACTERS_ENABLED = '1'
    $env:ASHENFALL_DEV_IDENTITY_SUBJECT = 'manual-09c2b1-d9a62a3c-4996-4276-8ab8-2c4d6f2ec988'
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $env:ASHENFALL_DEV_IDENTITY_TOKEN = [Convert]::ToBase64String($bytes)
    # The token is inherited in memory, never passed in process arguments or written to files.
    $api = Start-Process -FilePath 'dotnet' -ArgumentList @(('"' + (Resolve-Path -LiteralPath $ApiDll).Path + '"')) -WorkingDirectory (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path -WindowStyle Hidden -PassThru
    $ready = $false
    for ($n=0; $n -lt 60; $n++) {
        if ($api.HasExited) { throw 'The isolated API exited during startup.' }
        if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.OwningProcess -eq $api.Id }) { $ready=$true; break }
        Start-Sleep -Milliseconds 200
    }
    if (-not $ready) { throw 'The isolated API did not own the expected listener.' }
    $url = "http://127.0.0.1:$Port/api/v1/dev/character/gameplay-snapshot"
    $headers = @{ 'X-Ashenfall-Dev-Token' = $env:ASHENFALL_DEV_IDENTITY_TOKEN }
    $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Headers $headers -TimeoutSec 10
    if ($response.StatusCode -ne 200) { throw 'Expected HTTP 200.' }
    $value = $response.Content | ConvertFrom-Json
    if ($value.schemaVersion -ne 2 -or $value.characterId -ne $character -or $value.classDefinitionId -ne $class -or
        $value.characterRevision -ne 1 -or $value.catalogReleaseId -ne $release -or $value.catalogVersion -ne 'snapshot-09c3-v2' -or
        $value.catalogHash -ne $hash -or $value.effectiveStats.level -ne 2 -or $value.effectiveStats.maxHealth -ne 105 -or
        $value.effectiveStats.fixedDamage -ne 27 -or $value.effectiveStats.minDamage -ne 27 -or $value.effectiveStats.maxDamage -ne 27 -or
        $value.effectiveStats.criticalChancePercent -ne 5 -or $value.effectiveStats.criticalDamageBonusPercent -ne 100) { throw 'V2 snapshot values do not match the published fixture.' }
    $unauthorized = $false
    try { Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec 5 | Out-Null }
    catch { if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 401) { $unauthorized=$true } else { throw } }
    if (-not $unauthorized) { throw 'Unauthenticated request was not rejected.' }
    # The catalog API independently reloads and recomputes the V1 base hashes for historical/current definitions.
    $baseUrl = "http://127.0.0.1:$Port"
    $historical = Invoke-RestMethod -Uri "$baseUrl/api/v1/catalog/snapshot?version=snapshot-09c2a1-test" -TimeoutSec 10
    if ($historical.catalogVersion -ne 'snapshot-09c2a1-test' -or $historical.contentSha256 -ne 'a59cb3897f7764923b3e2c41c08d0ae618981e1cd8187835ceb428b6062c8438') { throw 'Historical catalog failed verification.' }
    $current = Invoke-RestMethod -Uri "$baseUrl/api/v1/catalog/snapshot?version=snapshot-09c3-v2" -TimeoutSec 10
    if ($current.catalogVersion -ne 'snapshot-09c3-v2' -or $current.contentSha256 -ne 'a33262049a29f4bde6d8e62d2f1d3837ed474a79d163b806917c4dde7fc6e5e1') { throw 'Current base catalog failed verification.' }
    Test-IsolatedState
    Write-Output 'Real authenticated TCP snapshot: HTTP 200; unauthenticated request: HTTP 401.'
    Write-Output 'Historical V1 and current V2 base catalog snapshots: HTTP 200 and independently hash-verified.'
    $value | ConvertTo-Json -Depth 5
}
finally {
    if ($api -and -not $api.HasExited) { Stop-Process -Id $api.Id }
    Remove-Item Env:ASHENFALL_DEV_IDENTITY_TOKEN -ErrorAction SilentlyContinue
}
