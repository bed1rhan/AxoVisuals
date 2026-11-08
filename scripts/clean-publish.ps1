param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$allowed=[IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts/AxoVisuals-win-x64'))
$target=[IO.Path]::GetFullPath($OutputDirectory)
if($target -ne $allowed) { throw 'Cleanup is restricted to the portable publish directory.' }
$removed=@()
# PDBs are debugging symbols. ONNX import libraries are linker inputs, not loaded DLLs.
Get-ChildItem -LiteralPath $target -File -Recurse | Where-Object { $_.Extension -eq '.pdb' -or $_.Name -in @('onnxruntime.lib','onnxruntime_providers_shared.lib') } | ForEach-Object { $removed += $_.FullName.Substring($target.Length+1); Remove-Item -LiteralPath $_.FullName }
$docs=Join-Path $target 'docs'
if(Test-Path -LiteralPath $docs) { $removed += 'docs/'; Remove-Item -LiteralPath $docs -Recurse }
$readme=Join-Path $target 'README.md'
if(Test-Path -LiteralPath $readme) { Remove-Item -LiteralPath $readme; $removed += 'README.md (developer instructions)' }
# Only MUI-only culture directories may be removed. Keep English fallback and Turkish.
Get-ChildItem -LiteralPath $target -Directory | Where-Object { $_.Name -match '^[a-z]{2,3}-' -and $_.Name -notin @('en-us','en-GB','tr-TR') } | ForEach-Object {
    $files=@(Get-ChildItem -LiteralPath $_.FullName -Recurse -File)
    if($files.Count -gt 0 -and @($files | Where-Object { $_.Extension -ne '.mui' }).Count -eq 0) { $removed += $_.Name+'/ (MUI only)'; Remove-Item -LiteralPath $_.FullName -Recurse }
}
foreach($required in @('AxoVisuals.exe','AxoVisuals.pri','Microsoft.UI.Xaml.dll','coreclr.dll','onnxruntime.dll','Magick.Native-Q8-x64.dll','models/u2net.onnx')) { if(-not(Test-Path -LiteralPath (Join-Path $target $required))) { throw "Missing runtime dependency: $required" } }
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'artifacts/verification') -Force | Out-Null
$removed | Set-Content (Join-Path $projectRoot 'artifacts/verification/release-cleanup.txt')
Write-Output "Audited cleanup: $($removed.Count) entries removed. Runtime DLLs, PRI files, licenses and model retained."
