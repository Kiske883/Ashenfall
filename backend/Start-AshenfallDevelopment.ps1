# One command opens the catalog adventure development workflow. No credentials
# are read by this script; the API uses its existing private Development setup.
param(
    [switch]$NoEditor,
    [string]$GodotPath = 'C:\Propio\tools\Godot\Godot_v4.7.2-stable_mono_win64.exe',
    [string]$ExpectedCatalogVersion = 'prototype-1',
    [string]$ExpectedCatalogHash = 'a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$versionUri = 'http://localhost:5000/api/v1/catalog/version'
$expectedVersion = $ExpectedCatalogVersion
$expectedHash = $ExpectedCatalogHash

function Test-AshenfallApi {
    try {
        $response = Invoke-RestMethod -Uri $versionUri -TimeoutSec 2
        if ($response.catalogVersion -cne $expectedVersion -or
            $response.contentSha256 -cne $expectedHash -or
            $response.schemaMajor -ne 1) { return $false }
        $snapshot = Invoke-RestMethod -Uri "http://localhost:5000/api/v1/catalog/snapshot?version=$expectedVersion" -TimeoutSec 2
        return ($snapshot.catalogVersion -ceq $expectedVersion -and
            $snapshot.contentSha256 -ceq $expectedHash -and
            $snapshot.classes.Count -eq 1 -and $snapshot.enemies.Count -eq 1 -and
            $snapshot.items.Count -eq 2)
    } catch { return $false }
}

$listener = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($listener) {
    if (-not (Test-AshenfallApi)) {
        throw "Port 5000 is occupied by PID $($listener.OwningProcess), but it does not serve the expected Ashenfall catalog. Close or inspect that process yourself."
    }
    Write-Host 'Reusing the verified Ashenfall API on port 5000.'
} else {
    if ($env:ASPNETCORE_ENVIRONMENT -and $env:ASPNETCORE_ENVIRONMENT -ne 'Development') {
        throw 'A conflicting ASPNETCORE_ENVIRONMENT is set.'
    }
    if ($env:DOTNET_ENVIRONMENT -and $env:DOTNET_ENVIRONMENT -ne 'Development') {
        throw 'A conflicting DOTNET_ENVIRONMENT is set.'
    }
    if ($env:ASHENFALL_DB_CONNECTION) {
        throw 'ASHENFALL_DB_CONNECTION is already set. Resolve it explicitly before using this helper.'
    }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $apiProject = Join-Path $PSScriptRoot 'Ashenfall.Api\Ashenfall.Api.csproj'
    $api = Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', $apiProject, '--no-launch-profile') -WindowStyle Hidden -PassThru
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if (Test-AshenfallApi) { $ready = $true; break }
        if ($api.HasExited) { break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $ready) {
        throw "Ashenfall API did not serve the expected catalog within 15 seconds (launcher PID $($api.Id)). Check the Development configuration and API logs. The process was not terminated automatically."
    }
    Write-Host "Started and verified Ashenfall API (launcher PID $($api.Id))."
}

if (-not $NoEditor) {
    if (-not (Test-Path -LiteralPath $GodotPath)) { throw "Godot executable was not found: $GodotPath" }
    Start-Process -FilePath $GodotPath -ArgumentList @('--editor', '--path', $root) -WorkingDirectory $root
    Write-Host 'Godot editor opened. Run scenes/world/AshenfallCatalogAdventure.tscn with F6.'
}
