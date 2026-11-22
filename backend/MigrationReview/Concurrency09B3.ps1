$ErrorActionPreference = 'Stop'
$psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
$db = 'ashenfall_09b3_test'
$account = [guid]::NewGuid().ToString()
$character = [guid]::NewGuid().ToString()
$two = [guid]::NewGuid().ToString()
$off = [guid]::NewGuid().ToString()
function Query([string]$sql) {
    $result = $sql | & $psql -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d $db -A -t -f - 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($result -join "`n") }
    return ($result -join "`n")
}
try {
    if ((Query 'SELECT current_database();').Trim() -ne $db) { throw 'Unexpected database.' }
    Query "INSERT INTO accounts(id,identity_provider,provider_subject) VALUES('$account','09b3test','$account'); INSERT INTO player_characters(id,account_id,class_definition_id,character_name) VALUES('$character','$account','90000000-0000-0000-0000-000000000002','Concurrency'); INSERT INTO owned_items(id,character_id,item_definition_id,quantity) VALUES('$two','$character','90000000-0000-0000-0000-000000000012',1),('$off','$character','90000000-0000-0000-0000-000000000013',1);" | Out-Null
    $first="BEGIN; INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES('$character','main_hand','$two'); SELECT pg_sleep(1); COMMIT;"
    $second="BEGIN; INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES('$character','off_hand','$off'); COMMIT;"
    $j1=Start-Job -ScriptBlock { param($s,$p,$d) $s | & $p -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d $d -A -t -f - 2>$null; $LASTEXITCODE } -ArgumentList $first,$psql,$db
    Start-Sleep -Milliseconds 150
    $j2=Start-Job -ScriptBlock { param($s,$p,$d) $s | & $p -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d $d -A -t -f - 2>$null; $LASTEXITCODE } -ArgumentList $second,$psql,$db
    $r1=@(Receive-Job (Wait-Job $j1)); $r2=@(Receive-Job (Wait-Job $j2))
    Remove-Job $j1,$j2
    $rows=(Query "SELECT slot_code FROM character_equipment WHERE character_id='$character' ORDER BY slot_code;").Trim()
    if ($r1[-1] -ne 0 -or $r2[-1] -eq 0 -or $rows -ne 'main_hand') { throw "Opposing hand invariant failed: $rows" }
    Write-Output 'Concurrent opposing-hand attempts: one committed, incompatible off-hand rejected; PASS.'
} finally {
    Query "DELETE FROM character_equipment WHERE character_id='$character'; DELETE FROM owned_items WHERE character_id='$character'; DELETE FROM player_characters WHERE id='$character' AND account_id='$account'; DELETE FROM accounts WHERE id='$account' AND provider_subject='$account';" | Out-Null
    Write-Output "Disposable account $account removed."
}
