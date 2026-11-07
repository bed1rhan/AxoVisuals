# Run from an elevated PowerShell to trust this specific local test publisher and install.
# No root CA is installed; remove this certificate from TrustedPeople when finished testing.
$ErrorActionPreference='Stop'
$package=Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'AxoVisuals_*_x64.msix' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if(-not $package) { throw 'MSIX not found beside this script.' }
$certificate=[Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path $PSScriptRoot 'AxoVisuals-Test.cer'))
$expected=(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'certificate-thumbprint.txt') -Raw).Trim()
if($certificate.Thumbprint -ne $expected -or $certificate.Subject -ne 'CN=Axolotl Technology') { throw 'Unexpected signing certificate.' }
if(-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this script in an elevated PowerShell. It trusts the included test certificate in LocalMachine/TrustedPeople.' }
Import-Certificate -FilePath (Join-Path $PSScriptRoot 'AxoVisuals-Test.cer') -CertStoreLocation Cert:/LocalMachine/TrustedPeople | Out-Null
Add-AppxPackage -Path $package.FullName
Write-Output 'AxoVisuals installed. Launch it from Start. Uninstall using Settings > Apps > Installed apps.'
