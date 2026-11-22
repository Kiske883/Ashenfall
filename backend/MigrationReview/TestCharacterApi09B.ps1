param([int]$Port = 5007, [string]$ApiPath = 'C:\Temp\Ashenfall09B2Build\Debug\net8.0\Ashenfall.Api.exe')
$ErrorActionPreference = 'Stop'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASHENFALL_CHARACTERS_ENABLED = '1'
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$env:ASHENFALL_DEV_IDENTITY_TOKEN = [Convert]::ToBase64String($bytes)
$env:ASHENFALL_DEV_IDENTITY_SUBJECT = '09b2-disposable-' + [guid]::NewGuid().ToString('N')
$env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"
$psqlPath = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
$api = $null
$createdAccount = $false
$logPath = Join-Path $env:TEMP ('ashenfall-09b2-api-' + [guid]::NewGuid().ToString('N') + '.log')
try {
    $api = Start-Process -FilePath $ApiPath -WindowStyle Hidden -PassThru -RedirectStandardOutput $logPath
    $uri = "http://127.0.0.1:$Port/api/v1/dev/character/"
    $headers = @{ 'X-Ashenfall-Dev-Token' = $env:ASHENFALL_DEV_IDENTITY_TOKEN }
    $ready = $false
    for ($i = 0; $i -lt 50; $i++) {
        try {
            $socket = New-Object System.Net.Sockets.TcpClient
            $socket.Connect('127.0.0.1', $Port)
            $socket.Dispose()
            $ready = $true
            break
        } catch {
            if ($api.HasExited) { throw 'Development API process exited before listening.' }
        }
        Start-Sleep -Milliseconds 200
    }
    if (-not $ready) { throw 'Development character endpoint did not start.' }
    $classId = '614ef7a1-a2f1-4876-9f80-e1653fd4a311'
    $body = @{classDefinitionId=$classId;name='Live Dwarf'} | ConvertTo-Json -Compress
    $createdAccount = $true
    $created = Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -ContentType 'application/json' -Body $body
    if ($created.rowRevision -ne 1) { throw 'Create revision incorrect.' }
    $loaded = Invoke-RestMethod -Uri $uri -Headers $headers
    if ($loaded.id -ne $created.id) { throw 'Load did not return created character.' }
    $award = @{expectedRevision=1;experience=12;allocatedStrength=0;allocatedDexterity=0;allocatedVitality=0} | ConvertTo-Json -Compress
    try {
        Invoke-WebRequest -Uri $uri -Method Put -Headers $headers -ContentType 'application/json' -Body $award -UseBasicParsing | Out-Null
        throw 'Client experience award was accepted.'
    } catch {
        if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 400) { throw }
    }
    $save = @{expectedRevision=1;experience=0;allocatedStrength=0;allocatedDexterity=0;allocatedVitality=0} | ConvertTo-Json -Compress
    $saved = Invoke-RestMethod -Uri $uri -Method Put -Headers $headers -ContentType 'application/json' -Body $save
    if ($saved.rowRevision -ne 2 -or $saved.experience -ne 0) { throw 'Save failed.' }
    try {
        Invoke-WebRequest -Uri $uri -Method Put -Headers $headers -ContentType 'application/json' -Body $save -UseBasicParsing | Out-Null
        throw 'Stale revision was accepted.'
    } catch {
        if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 409) { throw }
    }
    try {
        Invoke-WebRequest -Uri $uri -UseBasicParsing | Out-Null
        throw 'Missing token was accepted.'
    } catch {
        if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 401) { throw }
    }
    # Admin insertion stands in for a trusted server-side grant; no public grant endpoint exists.
    $ownedId = [guid]::NewGuid().ToString()
    $grantSql = "INSERT INTO owned_items(id,character_id,item_definition_id,quantity) VALUES('$ownedId','$($created.id)','270e4972-d576-479b-a2c5-51452bf2702e',1);"
    $grantSql | & $psqlPath -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d ashenfall_dev -A -t -f -
    if ($LASTEXITCODE -ne 0) { throw 'Trusted test item insertion failed.' }
    $inventory = Invoke-RestMethod -Uri ($uri+'inventory') -Headers $headers
    if (@($inventory).Count -ne 1 -or $inventory[0].id -ne $ownedId) { throw 'Inventory did not persist the item.' }
    $equip = @{ownedItemId=$ownedId;slot='main_hand';expectedRevision=2} | ConvertTo-Json -Compress
    $equipResult = Invoke-WebRequest -Uri ($uri+'equipment') -Method Put -Headers $headers -ContentType 'application/json' -Body $equip -UseBasicParsing
    if ($equipResult.StatusCode -ne 204) { throw 'Equip failed.' }
    $equipment = Invoke-RestMethod -Uri ($uri+'equipment') -Headers $headers
    if (@($equipment).Count -ne 1 -or $equipment[0].ownedItemId -ne $ownedId) { throw 'Equipped item was not persisted.' }
    $unequipResult = Invoke-WebRequest -Uri ($uri+'equipment/main_hand?expectedRevision=3') -Method Delete -Headers $headers -UseBasicParsing
    if ($unequipResult.StatusCode -ne 204) { throw 'Unequip failed.' }
    $stats = Invoke-RestMethod -Uri ($uri+'stats') -Headers $headers
    if ($null -eq $stats) { throw 'Stats endpoint failed.' }
    $deleted = Invoke-WebRequest -Uri ($uri+'?expectedRevision=4') -Method Delete -Headers $headers -UseBasicParsing
    if ($deleted.StatusCode -ne 204) { throw 'Soft delete failed.' }
    $replacement = Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -ContentType 'application/json' -Body (@{classDefinitionId=$classId;name='Replacement'} | ConvertTo-Json -Compress)
    if ($replacement.rowRevision -ne 1) { throw 'Replacement create failed.' }
    Write-Output 'API create/load/save/stale-revision/unauthorized/reject-client-XP/inventory/equip/unequip/stats/delete/replacement: PASS'
} catch {
    if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Tail 100 }
    throw
} finally {
    if ($api -and -not $api.HasExited) { Stop-Process -Id $api.Id }
    if ($createdAccount) {
        $subject = $env:ASHENFALL_DEV_IDENTITY_SUBJECT
        $sql = "BEGIN; DELETE FROM player_characters WHERE account_id IN (SELECT id FROM accounts WHERE identity_provider='local_development' AND provider_subject='$subject'); DELETE FROM accounts WHERE identity_provider='local_development' AND provider_subject='$subject'; COMMIT;"
        $sql | & $psqlPath -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d ashenfall_dev -A -t -f -
        if ($LASTEXITCODE -ne 0) { Write-Error "Cleanup failed for disposable subject $subject" }
    }
    Remove-Item Env:ASHENFALL_DEV_IDENTITY_TOKEN -ErrorAction SilentlyContinue
    Remove-Item Env:ASHENFALL_DEV_IDENTITY_SUBJECT -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath }
}
