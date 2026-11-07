param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdk = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) { $sdk = (Get-Command dotnet -ErrorAction Stop).Source }
Push-Location $projectRoot
try {
    & $sdk restore src/AxoVisuals -p:Platform=x64 --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & $sdk build src/AxoVisuals -c Release -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if (-not $SkipTests) { & "$PSScriptRoot/test.ps1" }
    & "$PSScriptRoot/privacy-audit.ps1"
    & $sdk publish src/AxoVisuals -c Release -p:Platform=x64 --no-restore -o artifacts/AxoVisuals-win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    # The unpackaged WinUI SDK build creates the app PRI but omits it from dotnet publish.
    $resourceIndex = 'src/AxoVisuals/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/AxoVisuals.pri'
    Copy-Item -LiteralPath $resourceIndex -Destination artifacts/AxoVisuals-win-x64/AxoVisuals.pri
    Get-ChildItem -Path 'src/AxoVisuals/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/*.xbf' | Copy-Item -Destination artifacts/AxoVisuals-win-x64
    & "$PSScriptRoot/clean-publish.ps1" -OutputDirectory (Join-Path $projectRoot "artifacts/AxoVisuals-win-x64")

    Write-Output 'Ready: artifacts/AxoVisuals-win-x64/AxoVisuals.exe'
} finally { Pop-Location }

