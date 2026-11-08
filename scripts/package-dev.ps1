$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
& "$PSScriptRoot/build.ps1"
$source = Join-Path $projectRoot 'artifacts/AxoVisuals-win-x64'
$destination = Join-Path $projectRoot 'artifacts/dev-v0.2.0'
if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
Copy-Item -LiteralPath $source -Destination $destination -Recurse
"AxoVisuals v0.2.0 development build. This is a portable test copy and does not modify the installed MSIX application." | Set-Content -LiteralPath (Join-Path $destination 'DEV-BUILD.txt') -Encoding utf8
Write-Output "Ready: $(Join-Path $destination 'AxoVisuals.exe')"
