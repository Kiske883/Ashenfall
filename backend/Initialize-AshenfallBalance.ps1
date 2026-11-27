# Dot-source this file to register Invoke-AshenfallBalance in the current session.
# Loading it performs no build, database access, publication or environment-file read.
$balanceWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$balanceProject = Join-Path $PSScriptRoot 'Ashenfall.BalanceCli\Ashenfall.BalanceCli.csproj'
$balanceHasher = [Security.Cryptography.SHA256]::Create()
try {
    $balanceWorkspaceKey = ([BitConverter]::ToString($balanceHasher.ComputeHash(
        [Text.Encoding]::UTF8.GetBytes($balanceWorkspace.ToLowerInvariant())))).Replace('-', '').Substring(0, 16)
} finally { $balanceHasher.Dispose() }
$balanceBuildRoot = Join-Path ([IO.Path]::GetTempPath()) ('AshenfallBalanceCli-' + $balanceWorkspaceKey)

$balanceInvoke = {
    $ErrorActionPreference = 'Stop'
    # PowerShell 7 can otherwise turn a nonzero native exit into a terminating error
    # before our explicit exit-code check. This assignment is function-local.
    $PSNativeCommandUseErrorActionPreference = $false
    $balanceArguments = @($args)
    $balanceDotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
    Push-Location -LiteralPath $balanceWorkspace
    try {
        $balanceSdkVersion = & $balanceDotnet --version
        $balanceSdkExit = $LASTEXITCODE
    } finally { Pop-Location }
    if ($balanceSdkExit -ne 0) { throw "Cannot resolve the .NET SDK (exit $balanceSdkExit); no command was run." }
    $balanceAssemblyDirectory = Join-Path $balanceBuildRoot 'Debug\net8.0'
    $balanceAssembly = Join-Path $balanceAssemblyDirectory 'Ashenfall.BalanceCli.dll'
    $balanceStamp = Join-Path $balanceBuildRoot 'balance-input.sha256'
    $balanceInputs = @(
        foreach ($balanceDirectory in @('Ashenfall.Api', 'Ashenfall.CatalogPublisher', 'Ashenfall.BalanceCli')) {
            Get-ChildItem -LiteralPath (Join-Path $balanceWorkspace ('backend\' + $balanceDirectory)) -File -Recurse |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
                    $_.Extension -in @('.cs', '.csproj', '.props', '.targets', '.resx') }
        }
        foreach ($balanceParent in @($balanceWorkspace, (Join-Path $balanceWorkspace 'backend'))) {
            foreach ($balanceName in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'global.json')) {
                $balanceInputPath = Join-Path $balanceParent $balanceName
                if (Test-Path -LiteralPath $balanceInputPath -PathType Leaf) { Get-Item -LiteralPath $balanceInputPath }
            }
        }
    ) | Sort-Object FullName -Unique
    $balanceInputLines = @(
        'balance-initializer-v1'
        'sdk:' + $balanceSdkVersion
        foreach ($balanceInput in $balanceInputs) {
            $balanceRelativeName = $balanceInput.FullName.Substring($balanceWorkspace.Length)
            $balanceRelativeName + ':' + (Get-FileHash -LiteralPath $balanceInput.FullName -Algorithm SHA256).Hash
        }
    )
    $balanceHash = [Security.Cryptography.SHA256]::Create()
    try {
        $balanceSignature = [BitConverter]::ToString($balanceHash.ComputeHash(
            [Text.Encoding]::UTF8.GetBytes(($balanceInputLines -join "`n")))).Replace('-', '')
    } finally { $balanceHash.Dispose() }
    $balanceOutputsPresent = $true
    foreach ($balanceOutputFile in @('Ashenfall.BalanceCli.dll', 'Ashenfall.BalanceCli.deps.json',
        'Ashenfall.BalanceCli.runtimeconfig.json', 'Ashenfall.CatalogPublisher.dll', 'Ashenfall.Api.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $balanceAssemblyDirectory $balanceOutputFile) -PathType Leaf)) {
            $balanceOutputsPresent = $false
        }
    }
    $balanceCachedSignature = if (Test-Path -LiteralPath $balanceStamp -PathType Leaf) {
        [IO.File]::ReadAllText($balanceStamp)
    } else { '' }
    if (-not $balanceOutputsPresent -or $balanceCachedSignature -ne $balanceSignature) {
        Write-Host 'Building Ashenfall Balance CLI (isolated output; no database operation)...'
        # Keep build messages off the function's JSON success-output stream.
        Push-Location -LiteralPath $balanceWorkspace
        try {
            & $balanceDotnet build $balanceProject "-p:BaseOutputPath=$balanceBuildRoot\" -p:UseAppHost=false | Out-Host
            $balanceBuildExit = $LASTEXITCODE
        } finally { Pop-Location }
        if ($balanceBuildExit -ne 0) { throw "Balance CLI build failed (exit $balanceBuildExit); no command was run." }
        if (-not (Test-Path -LiteralPath $balanceAssembly -PathType Leaf)) { throw 'Build produced no CLI assembly.' }
        [IO.File]::WriteAllText($balanceStamp, $balanceSignature)
    }
    & $balanceDotnet $balanceAssembly @balanceArguments
    $balanceCommandExit = $LASTEXITCODE
    if ($balanceCommandExit -ne 0) { throw "Balance CLI command failed (exit $balanceCommandExit); stop and inspect the CLI error." }
}.GetNewClosure()
Set-Item -Path Function:Invoke-AshenfallBalance -Value $balanceInvoke
