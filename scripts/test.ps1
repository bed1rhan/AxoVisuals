param([string]$PhotoFixture, [string]$HeicFixture)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdk = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) { $sdk = (Get-Command dotnet -ErrorAction Stop).Source }
$model = Join-Path $projectRoot 'models/u2net.onnx'
if (-not (Test-Path -LiteralPath $model)) { throw 'Bundled model missing. Run scripts/fetch-model.ps1 explicitly.' }
$testArguments = @($model)
if ($PhotoFixture) { $testArguments += $PhotoFixture }
if ($HeicFixture) { if (-not $PhotoFixture) { throw 'HEIC verification also requires PhotoFixture.' }; $testArguments += $HeicFixture }
Push-Location $projectRoot
try {
    & $sdk run --project tests/AxoVisuals.Tests -c Release -- @testArguments
    if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed.' }
} finally { Pop-Location }
