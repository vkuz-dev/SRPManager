<#
  Builds SRPManager and lays it out as an MSIX package.

  .\packaging\build-msix.ps1
      Builds bin\msix\layout and, when the Windows SDK is installed, packs
      bin\msix\SRPManager_<version>_x64.msix. The defaults give a test identity.

  For the Store, pass the values from Partner Center > Product identity:
      .\packaging\build-msix.ps1 -Name <Package/Identity/Name> `
          -Publisher "CN=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" -PublisherDisplay "Vladimir Kuznetsov"
  The Store signs the package itself, so upload the .msix unsigned.

  To try it locally without packing or signing, turn on Developer Mode and run:
      Add-AppxPackage -Register .\bin\msix\layout\AppxManifest.xml

  To install the .msix itself, sign it with a self-signed certificate:
      .\packaging\build-msix.ps1 -Sign
  and trust that certificate once, from an elevated PowerShell:
      Import-Certificate -FilePath .\bin\msix\SRPManager-test.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
#>
param(
    [string]$Name = "SRPManager",
    [string]$Publisher = "CN=SRPManager",
    [string]$PublisherDisplay = "Vladimir Kuznetsov",
    [switch]$Sign
)

$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$out = Join-Path $repo "bin\msix"
$layout = Join-Path $out "layout"

dotnet build (Join-Path $repo "AWLM.csproj") -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "build failed" }

# The Store wants the last version part to be 0, which the csproj already keeps.
[xml]$proj = Get-Content (Join-Path $repo "AWLM.csproj")
$version = ($proj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version

# The whole build output, minus debug symbols, so a future dependency is not left behind.
if (Test-Path $layout) { Remove-Item -Recurse -Force $layout }
New-Item -ItemType Directory -Force $layout | Out-Null
$bin = Join-Path $repo "bin\Release\net462-windows"
Copy-Item -Recurse (Join-Path $bin "*") $layout -Exclude "*.pdb"
Copy-Item -Recurse (Join-Path $PSScriptRoot "Assets") $layout

(Get-Content -Raw (Join-Path $PSScriptRoot "AppxManifest.xml")).
    Replace('$NAME$', $Name).
    Replace('$PUBLISHER_DISPLAY$', $PublisherDisplay).
    Replace('$PUBLISHER$', $Publisher).
    Replace('$VERSION$', $version) |
    Set-Content -Encoding utf8 (Join-Path $layout "AppxManifest.xml")

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) {
    Write-Host "Layout ready at $layout"
    Write-Host "No Windows SDK found, so no .msix was packed (winget install Microsoft.WindowsSDK.10.0.26100)."
    return
}

$msix = Join-Path $out "SRPManager_${version}_x64.msix"
& $makeappx.FullName pack /d $layout /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed" }
Write-Host "Packed $msix"

if (-not $Sign) { return }

# The certificate subject must equal the manifest's Publisher. It is made once
# and reused, so it only has to be trusted once.
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Publisher } | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature `
        -FriendlyName "SRPManager MSIX test" -CertStoreLocation Cert:\CurrentUser\My `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
}
$cer = Join-Path $out "SRPManager-test.cer"
Export-Certificate -Cert $cert -FilePath $cer | Out-Null

$signtool = Join-Path $makeappx.DirectoryName "signtool.exe"
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $msix
if ($LASTEXITCODE -ne 0) { throw "signtool failed" }

$trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object { $_.Thumbprint -eq $cert.Thumbprint }
if (-not $trusted) {
    Write-Host "Signed. Trust the certificate once, from an elevated PowerShell:"
    Write-Host "    Import-Certificate -FilePath `"$cer`" -CertStoreLocation Cert:\LocalMachine\TrustedPeople"
}
