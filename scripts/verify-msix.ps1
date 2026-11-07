param([string]$PackagePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Security.Cryptography.Pkcs
$archive=[IO.Compression.ZipFile]::OpenRead((Resolve-Path $PackagePath))
try {
    foreach($name in @('AxoVisuals.exe','AxoVisuals.pri','resources.pri','Microsoft.UI.Xaml.dll','coreclr.dll','onnxruntime.dll','Magick.Native-Q8-x64.dll','models/u2net.onnx','AppxSignature.p7x')) { if(-not ($archive.Entries | Where-Object FullName -eq $name)) { throw "Missing package content: $name" } }
    if(@($archive.Entries | Where-Object { $_.FullName -match '\.(pdb|lib|pfx|p12)$' -or $_.FullName -like 'docs/*' }).Count -gt 0) { throw 'Developer/private-key artifacts found.' }
    $stream=$archive.GetEntry('models/u2net.onnx').Open()
    try { $sha=[Security.Cryptography.SHA256]::Create(); $hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','');$sha.Dispose() } finally { $stream.Dispose() }
    if($hash -ne '8D10D2F3BB75AE3B6D527C77944FC5E7DCD94B29809D47A739A7A728A912B491') { throw 'Model hash mismatch.' }
    $signature=[IO.MemoryStream]::new();$stream=$archive.GetEntry('AppxSignature.p7x').Open()
    try { $stream.CopyTo($signature);$bytes=$signature.ToArray() } finally { $stream.Dispose();$signature.Dispose() }
    $cms=[Security.Cryptography.Pkcs.SignedCms]::new()
    $cms.Decode([byte[]]$bytes[4..($bytes.Length-1)]);$cms.CheckSignature($true)
    if($cms.SignerInfos[0].Certificate.Subject -ne 'CN=Axolotl Technology') { throw 'Unexpected signer.' }
    Write-Output 'PASS: MSIX required runtime/codecs/model present; model SHA-256 matches; no PDB/LIB/private keys/docs; CMS signature cryptographically valid (trust-chain installation not tested).'
} finally { $archive.Dispose() }
