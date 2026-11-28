param(
    [Parameter(Mandatory=$true)][string]$AppPath,
    [Parameter(Mandatory=$true)][string]$MsiPath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [Parameter(Mandatory=$true)][string]$ExpectedVersion
)
$ErrorActionPreference='Stop'
$appRoot=(Resolve-Path -LiteralPath $AppPath).Path
$msiFile=(Resolve-Path -LiteralPath $MsiPath).Path
$outputRoot=[IO.Path]::GetFullPath($OutputPath)
if($ExpectedVersion -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'){throw 'Invalid release version'}
$payloadVersion=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $appRoot 'RMPCS.dll')).Version
if($payloadVersion.ToString(3) -ne $ExpectedVersion -or $payloadVersion.Revision -ne 0){throw 'Bootstrap application/release version mismatch'}
$installerCom=New-Object -ComObject WindowsInstaller.Installer
$databaseCom=$installerCom.OpenDatabase($msiFile,0)
$versionView=$databaseCom.OpenView('SELECT `Value` FROM `Property` WHERE `Property`=''ProductVersion''')
$versionView.Execute()
$versionRow=$versionView.Fetch()
if($null -eq $versionRow -or $versionRow.StringData(1) -ne $ExpectedVersion){throw 'Bootstrap MSI/release version mismatch'}
$versionView.Close()
$codeView=$databaseCom.OpenView('SELECT `Value` FROM `Property` WHERE `Property`=''ProductCode''')
$codeView.Execute()
$codeRow=$codeView.Fetch()
if($null -eq $codeRow){throw 'MSI product identity is missing'}
$productGuid=[guid]$codeRow.StringData(1)
$productCode=$productGuid.ToString('B').ToUpperInvariant()
$codeView.Close()
foreach($comObject in @($codeRow,$codeView,$versionRow,$versionView,$databaseCom,$installerCom)){[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($comObject)}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$portableRoot=Join-Path $outputRoot 'Portable'
New-Item -ItemType Directory -Path $portableRoot -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework x64 C# compiler is required to build the bootstrap launchers.' }
$sources=@((Join-Path $PSScriptRoot 'Program.cs'),(Join-Path $PSScriptRoot 'BootstrapPolicy.cs'),(Join-Path $PSScriptRoot 'SetupChoices.cs'),(Join-Path $PSScriptRoot 'SetupLayout.cs'),(Join-Path $PSScriptRoot 'SetupProgress.cs'))
$versionSource=Join-Path $outputRoot 'GeneratedVersion.cs'
@("using System.Reflection;",('[assembly: AssemblyVersion("'+$ExpectedVersion+'.0")]'),('[assembly: AssemblyFileVersion("'+$ExpectedVersion+'.0")]'),('[assembly: AssemblyInformationalVersion("'+$ExpectedVersion+'")]')) | Set-Content -LiteralPath $versionSource -Encoding ascii
('namespace RMPCS.Bootstrap { internal static class PackageIdentity { internal const string ProductCode="'+$productCode+'"; } }') | Add-Content -LiteralPath $versionSource -Encoding ascii
$sources+=$versionSource
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
$setupFile=Join-Path $outputRoot ('RMPCS-Setup-'+$ExpectedVersion+'-x64.exe')
& $compiler @common '/define:INSTALLER' ('/out:'+$setupFile) ('/resource:'+$installerManifest+',PayloadManifest') ('/resource:'+$portableManifest+',InstalledSensorManifest') ('/resource:'+$msiFile+',InstallerPayload') @sources
if ($LASTEXITCODE -ne 0) { throw 'Installer bootstrap compilation failed.' }
Write-Output ('Created '+$setupFile)
Write-Output ('Created '+(Join-Path $portableRoot 'Launch-RMPCS.exe'))
