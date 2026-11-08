param([string]$Version='1.2.0.0',[switch]$Rebuild)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$portable=Join-Path $projectRoot 'artifacts/AxoVisuals-win-x64'
if($Rebuild -or -not(Test-Path (Join-Path $portable 'AxoVisuals.exe'))) { & "$PSScriptRoot/build.ps1" }
$bin=Join-Path $env:USERPROFILE '.nuget/packages/microsoft.windows.sdk.buildtools/10.0.26100.4654/bin/10.0.26100.0/x64'
foreach($tool in @('makeappx.exe','signtool.exe')) { if(-not(Test-Path (Join-Path $bin $tool))) { throw 'Restore the pinned Windows SDK BuildTools package first.' } }
$out=Join-Path $projectRoot 'artifacts/msix'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$stage=Join-Path $out ('staging-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    Get-ChildItem -LiteralPath $portable | Copy-Item -Destination $stage -Recurse
    # WinUI's unpackaged output names its resource index after the executable.
    # MSIX resource resolution requires the package primary resource index at this name.
    Copy-Item -LiteralPath (Join-Path $stage 'AxoVisuals.pri') -Destination (Join-Path $stage 'resources.pri') -Force
    $assets=Join-Path $stage 'Assets'
    Add-Type -AssemblyName System.Drawing
    foreach($size in @(44,50,150)) {
        $bitmap=[Drawing.Bitmap]::new($size,$size)
        $graphics=[Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([Drawing.Color]::FromArgb(25,27,30))
            $graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $pen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(141,216,197),[single]($size*0.07))
            try { $graphics.DrawLines($pen,[Drawing.PointF[]]@([Drawing.PointF]::new($size*.23,$size*.77),[Drawing.PointF]::new($size*.5,$size*.2),[Drawing.PointF]::new($size*.77,$size*.77)));$graphics.DrawLine($pen,[single]($size*.35),[single]($size*.56),[single]($size*.65),[single]($size*.56)) } finally { $pen.Dispose() }
            $bitmap.Save((Join-Path $assets "Logo$size.png"),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose();$bitmap.Dispose() }
    }
    [xml]$manifest=@"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap rescap">
 <Identity Name="AxolotlTechnology.AxoVisuals" Publisher="CN=Axolotl Technology" Version="$Version" ProcessorArchitecture="x64"/>
 <Properties><DisplayName>AxoVisuals</DisplayName><PublisherDisplayName>Axolotl Technology</PublisherDisplayName><Logo>Assets\Logo50.png</Logo></Properties>
 <Resources><Resource Language="en-US"/><Resource Language="tr-TR"/></Resources>
 <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0"/></Dependencies>
 <Applications><Application Id="App" Executable="AxoVisuals.exe" EntryPoint="Windows.FullTrustApplication"><uap:VisualElements DisplayName="AxoVisuals" Description="Local image utility" BackgroundColor="#191B1E" Square150x150Logo="Assets\Logo150.png" Square44x44Logo="Assets\Logo44.png"/></Application></Applications>
 <Capabilities><rescap:Capability Name="runFullTrust"/></Capabilities>
</Package>
"@
    # Merge in-process WinRT activation registrations from the exact bundled SDK framework.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $runtime=Join-Path $env:USERPROFILE '.nuget/packages/microsoft.windowsappsdk.runtime/1.8.260804001/tools/MSIX/win10-x64/Microsoft.WindowsAppRuntime.1.8.msix'
    $archive=[IO.Compression.ZipFile]::OpenRead($runtime)
    try { $reader=[IO.StreamReader]::new($archive.GetEntry('AppxManifest.xml').Open()); try { [xml]$runtimeManifest=$reader.ReadToEnd() } finally { $reader.Dispose() } } finally { $archive.Dispose() }
    $extensions=$manifest.CreateElement('Extensions',$manifest.DocumentElement.NamespaceURI)
    foreach($extension in $runtimeManifest.Package.Extensions.Extension) {
        if($extension.Category -eq 'windows.activatableClass.inProcessServer') {
            $dll=[string]$extension.InProcessServer.Path
            if(-not(Test-Path -LiteralPath (Join-Path $stage $dll))) { throw "Missing activation server $dll" }
            [void]$extensions.AppendChild($manifest.ImportNode($extension,$true))
        }
    }
    [void]$manifest.DocumentElement.InsertBefore($extensions,$manifest.Package.Capabilities)
    $manifest.Save((Join-Path $stage 'AppxManifest.xml'))
    $package=Join-Path $out "AxoVisuals_$($Version)_x64.msix"
    & (Join-Path $bin 'makeappx.exe') pack /d $stage /p $package /o
    if($LASTEXITCODE -ne 0) { throw 'MSIX validation/packing failed.' }
    $cert=Get-ChildItem Cert:/CurrentUser/My | Where-Object { $_.Subject -eq 'CN=Axolotl Technology' -and $_.FriendlyName -eq 'AxoVisuals local test signing' -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(14) } | Select-Object -First 1
    if(-not $cert) { $cert=New-SelfSignedCertificate -Type Custom -Subject 'CN=Axolotl Technology' -FriendlyName 'AxoVisuals local test signing' -CertStoreLocation Cert:/CurrentUser/My -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -KeyUsage DigitalSignature -NotAfter (Get-Date).AddYears(1) -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3','2.5.29.19={text}CA=false') }
    & (Join-Path $bin 'signtool.exe') sign /fd SHA256 /sha1 $cert.Thumbprint /s My $package
    if($LASTEXITCODE -ne 0) { throw 'MSIX signing failed.' }
    Export-Certificate -Cert $cert -FilePath (Join-Path $out 'AxoVisuals-Test.cer') -Force | Out-Null
    $cert.Thumbprint | Set-Content (Join-Path $out 'certificate-thumbprint.txt')
    Copy-Item -LiteralPath "$PSScriptRoot/install-msix.ps1" -Destination $out
    & "$PSScriptRoot/verify-msix.ps1" -PackagePath $package
    Write-Output "Ready: $package"
    Write-Output 'Private key is non-exportable in CurrentUser/My. No trust store was modified. Installation requires trusting the public certificate in LocalMachine/TrustedPeople.'
} finally {
    $resolved=[IO.Path]::GetFullPath($stage)
    if($resolved.StartsWith([IO.Path]::GetFullPath($out)+[IO.Path]::DirectorySeparatorChar) -and (Split-Path $resolved -Leaf) -like 'staging-*') { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
