$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$files = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }
$forbidden = 'HttpClient|WebClient|HttpWebRequest|TcpClient|UdpClient|System\.Net\.Sockets|Process\.Start|WebView2|ApplicationInsights|SentrySdk|DownloadFile|UploadFile'
$findings = $files | Select-String -Pattern $forbidden
if ($findings) { $findings | Format-Table Path,LineNumber,Line; throw 'Unexpected network or process API in production source.' }
$policy = Get-Content -LiteralPath (Join-Path $projectRoot 'src/AxoVisuals.Core/Imaging/ImagingRuntime.cs') -Raw
if ($policy -notmatch 'domain="delegate" rights="none"' -or $policy -notmatch 'name="disk" value="0"') { throw 'Missing native privacy policy.' }
$ai = Get-Content -LiteralPath (Join-Path $projectRoot 'src/AxoVisuals.Core/AI/BackgroundRemovalService.cs') -Raw
if ($ai -notmatch 'DisableTelemetryEvents' -or $ai -notmatch 'SHA256.HashData') { throw 'Missing model privacy/integrity control.' }
Write-Output 'PASS: no network/process APIs; delegates blocked; disk spill disabled; ONNX telemetry disabled; model integrity pinned.'
