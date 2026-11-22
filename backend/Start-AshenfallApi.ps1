# Starts the local read-only catalog API. Connection settings come from private
# Development user secrets; this script never reads or prints their contents.
$ErrorActionPreference = 'Stop'

if ($env:ASPNETCORE_ENVIRONMENT -and $env:ASPNETCORE_ENVIRONMENT -ne 'Development') {
    throw 'ASPNETCORE_ENVIRONMENT is already set to a different value. Resolve it explicitly before starting the development API.'
}
if ($env:DOTNET_ENVIRONMENT -and $env:DOTNET_ENVIRONMENT -ne 'Development') {
    throw 'DOTNET_ENVIRONMENT is already set to a different value. Resolve it explicitly before starting the development API.'
}
if ($env:ASHENFALL_DB_CONNECTION) {
    throw 'ASHENFALL_DB_CONNECTION is already set. This helper uses Development user secrets; remove the process variable explicitly or start the API manually.'
}

$existingListener = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($existingListener) {
    throw "Port 5000 is already in use by process $($existingListener.OwningProcess). Close the prior API instance in its own terminal before restarting."
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$project = Join-Path $PSScriptRoot 'Ashenfall.Api\Ashenfall.Api.csproj'
Write-Host 'Starting Ashenfall catalog API in Development. Verify /api/v1/catalog/version after startup.'
dotnet run --project $project --no-launch-profile
exit $LASTEXITCODE
