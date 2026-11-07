# Explicit development-only download. The application never executes this script.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$modelDirectory = Join-Path $projectRoot 'models'
New-Item -ItemType Directory -Path $modelDirectory -Force | Out-Null
$destination = Join-Path $modelDirectory 'u2net.onnx'
$expected = '8D10D2F3BB75AE3B6D527C77944FC5E7DCD94B29809D47A739A7A728A912B491'
if (Test-Path -LiteralPath $destination) {
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -eq $expected) { Write-Output 'Verified model already exists.'; exit 0 }
    throw 'An unexpected model already exists. Preserve or remove it explicitly before retrying.'
}
$temporary = Join-Path $modelDirectory ('download-' + [Guid]::NewGuid().ToString('N') + '.tmp')
try {
    Invoke-WebRequest -Uri 'https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2net.onnx' -OutFile $temporary
    if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $expected) { throw 'Model checksum mismatch.' }
    Move-Item -LiteralPath $temporary -Destination $destination
    Write-Output 'Model downloaded and verified. Inference will run offline.'
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
