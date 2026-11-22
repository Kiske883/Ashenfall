$ErrorActionPreference = 'Stop'
$psqlPath = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
$account = [guid]::NewGuid().ToString()
$first = [guid]::NewGuid().ToString()
$second = [guid]::NewGuid().ToString()
$class = '614ef7a1-a2f1-4876-9f80-e1653fd4a311'
$subject = '09b2-disposable-' + [guid]::NewGuid().ToString('N')
function Invoke-Sql([string]$sql) {
    $output = $sql | & $psqlPath -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d ashenfall_dev -A -t -f - 2>&1
    return @{ Exit = $LASTEXITCODE; Output = ($output -join "`n") }
}
try {
    $setup = Invoke-Sql "INSERT INTO accounts(id,identity_provider,provider_subject) VALUES('$account','09b2_disposable_test','$subject');"
    if ($setup.Exit -ne 0) { throw $setup.Output }
    $sql1 = "BEGIN; INSERT INTO player_characters(id,account_id,class_definition_id,character_name) VALUES('$first','$account','$class','Concurrent One'); SELECT pg_sleep(1); COMMIT;"
    $sql2 = "BEGIN; INSERT INTO player_characters(id,account_id,class_definition_id,character_name) VALUES('$second','$account','$class','Concurrent Two'); COMMIT;"
    $job1 = Start-Job -ScriptBlock { param($sql,$exe) $sql | & $exe -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d ashenfall_dev -A -t -f -; $LASTEXITCODE } -ArgumentList $sql1,$psqlPath
    Start-Sleep -Milliseconds 150
    $job2 = Start-Job -ScriptBlock { param($sql,$exe) $sql | & $exe -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d ashenfall_dev -A -t -f - 2>$null; $LASTEXITCODE } -ArgumentList $sql2,$psqlPath
    $result1 = @(Receive-Job (Wait-Job $job1)); $result2 = @(Receive-Job (Wait-Job $job2))
    Remove-Job $job1,$job2
    $count = Invoke-Sql "SELECT count(*) FROM player_characters WHERE account_id='$account' AND deleted_at IS NULL;"
    if ($count.Exit -ne 0 -or $count.Output.Trim() -ne '1') { throw 'Concurrent create invariant failed.' }
    if ($result1[-1] -eq $result2[-1]) { throw 'Concurrent create did not yield one winner and one conflict.' }
    $winner = Invoke-Sql "SELECT id FROM player_characters WHERE account_id='$account' AND deleted_at IS NULL;"
    $id = $winner.Output.Trim()
    $u1 = "BEGIN; UPDATE player_characters SET experience=1,row_revision=2 WHERE id='$id' AND row_revision=1; SELECT pg_sleep(1); COMMIT;"
    $u2 = "BEGIN; UPDATE player_characters SET experience=2,row_revision=2 WHERE id='$id' AND row_revision=1; COMMIT;"
    $job1 = Start-Job -ScriptBlock { param($sql,$exe) $sql | & $exe -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d ashenfall_dev -A -t -f -; $LASTEXITCODE } -ArgumentList $u1,$psqlPath
    Start-Sleep -Milliseconds 150
    $job2 = Start-Job -ScriptBlock { param($sql,$exe) $sql | & $exe -X -w -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d ashenfall_dev -A -t -f -; $LASTEXITCODE } -ArgumentList $u2,$psqlPath
    $result1 = @(Receive-Job (Wait-Job $job1)); $result2 = @(Receive-Job (Wait-Job $job2))
    Remove-Job $job1,$job2
    $updated = Invoke-Sql "SELECT row_revision,experience FROM player_characters WHERE id='$id';"
    if ($updated.Exit -ne 0 -or $updated.Output.Trim() -ne '2|1') { throw 'Concurrent update invariant failed.' }
    if (($result1 -join ' ') -notmatch 'UPDATE 1' -or ($result2 -join ' ') -notmatch 'UPDATE 0') { throw 'Concurrent update did not report a stale writer.' }
    Write-Output 'Concurrent create: one committed active character, one unique conflict. Concurrent revision update: one winner, one stale writer. PASS'
} finally {
    $cleanup = Invoke-Sql "BEGIN; DELETE FROM player_characters WHERE account_id='$account'; DELETE FROM accounts WHERE id='$account' AND identity_provider='09b2_disposable_test' AND provider_subject='$subject'; COMMIT;"
    if ($cleanup.Exit -ne 0) { Write-Error "Cleanup failed for disposable account $account" }
    else { Write-Output 'Disposable concurrency rows removed.' }
}
