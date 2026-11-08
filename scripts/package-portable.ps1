param([string]$Version = '0.1.1', [switch]$Rebuild)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$portable = Join-Path $projectRoot 'artifacts/AxoVisuals-win-x64'
if ($Rebuild -or -not (Test-Path -LiteralPath (Join-Path $portable 'AxoVisuals.exe'))) {
    & "$PSScriptRoot/build.ps1"
}

$required = @('AxoVisuals.exe', 'AxoVisuals.pri', 'App.xbf', 'MainWindow.xbf', 'Microsoft.UI.Xaml.dll', 'coreclr.dll', 'onnxruntime.dll', 'Magick.Native-Q8-x64.dll', 'models/u2net.onnx', 'licenses/U-2-Net-LICENSE.txt')
foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $portable $file))) { throw "Missing portable runtime file: $file" }
}

$modelHash = (Get-FileHash -LiteralPath (Join-Path $portable 'models/u2net.onnx') -Algorithm SHA256).Hash
$expectedModelHash = '8D10D2F3BB75AE3B6D527C77944FC5E7DCD94B29809D47A739A7A728A912B491'
if ($modelHash -ne $expectedModelHash) { throw 'Bundled U2-Net model checksum mismatch.' }

$output = Join-Path $projectRoot 'artifacts/release'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$name = "AxoVisuals-$Version-win-x64"
$zip = Join-Path $output "$name.zip"
$checksum = "$zip.sha256"
$stage = Join-Path $output ("staging-" + [Guid]::NewGuid().ToString('N'))

try {
    $root = Join-Path $stage $name
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    Get-ChildItem -LiteralPath $portable -Force | Copy-Item -Destination $root -Recurse
    @"
AxoVisuals $Version

Extract this folder completely, then run AxoVisuals.exe.
No installer, administrator permission, .NET SDK, or Visual Studio is required.

The bundled U2-Net model enables offline background removal. Do not move or delete the models folder.
Third-party license and notice files are in the licenses folder.
"@ | Set-Content -LiteralPath (Join-Path $root 'RUNNING.txt') -Encoding utf8

    Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path $root -DestinationPath $zip -CompressionLevel Optimal
    $entries = [IO.Compression.ZipFile]::OpenRead($zip).Entries.FullName
    foreach ($file in $required) {
        if ($entries -notcontains "$name/$file") { throw "Archive validation failed: $file" }
    }
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    "$hash *$(Split-Path -Leaf $zip)" | Set-Content -LiteralPath $checksum -Encoding ascii
    Write-Output "Ready: $zip"
    Write-Output "SHA256: $hash"
} finally {
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
