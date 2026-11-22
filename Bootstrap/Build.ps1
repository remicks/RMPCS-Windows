param(
    [Parameter(Mandatory=$true)][string]$AppPath,
    [Parameter(Mandatory=$true)][string]$MsiPath,
    [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference='Stop'
$appRoot=(Resolve-Path -LiteralPath $AppPath).Path
$msiFile=(Resolve-Path -LiteralPath $MsiPath).Path
$outputRoot=[IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$portableRoot=Join-Path $outputRoot 'Portable'
New-Item -ItemType Directory -Path $portableRoot -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework x64 C# compiler is required to build the bootstrap launchers.' }
$sources=@((Join-Path $PSScriptRoot 'Program.cs'),(Join-Path $PSScriptRoot 'BootstrapPolicy.cs'),(Join-Path $PSScriptRoot 'SetupChoices.cs'),(Join-Path $PSScriptRoot 'SetupLayout.cs'))
$common=@('/nologo','/target:winexe','/platform:x64','/optimize+','/warn:4',('/win32icon:'+(Join-Path $PSScriptRoot '..\Assets\RMPCS.ico')),('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')),'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Net.Http.dll')
$common+=('/resource:'+(Join-Path $PSScriptRoot '..\Assets\RMPCS.png')+',BrandArtwork')
$payloadFiles=@(Get-ChildItem -LiteralPath $appRoot -File | Where-Object Extension -ne '.pdb')
$payloadFiles | Copy-Item -Destination $portableRoot -Force
$portableManifest=Join-Path $outputRoot 'portable-manifest.txt'
$payloadFiles | ForEach-Object { $_.Name+'|'+(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } | Set-Content -LiteralPath $portableManifest -Encoding ascii
& $compiler @common ('/out:'+(Join-Path $portableRoot 'Launch-RMPCS.exe')) ('/resource:'+$portableManifest+',PayloadManifest') @sources
if ($LASTEXITCODE -ne 0) { throw 'Portable bootstrap compilation failed.' }
$installerManifest=Join-Path $outputRoot 'installer-manifest.txt'
('RMPCS.msi|'+(Get-FileHash -LiteralPath $msiFile -Algorithm SHA256).Hash.ToLowerInvariant()) | Set-Content -LiteralPath $installerManifest -Encoding ascii
& $compiler @common '/define:INSTALLER' ('/out:'+(Join-Path $outputRoot 'RMPCS.Setup.exe')) ('/resource:'+$installerManifest+',PayloadManifest') ('/resource:'+$msiFile+',InstallerPayload') @sources
if ($LASTEXITCODE -ne 0) { throw 'Installer bootstrap compilation failed.' }
Write-Output ('Created '+(Join-Path $outputRoot 'RMPCS.Setup.exe'))
Write-Output ('Created '+(Join-Path $portableRoot 'Launch-RMPCS.exe'))
