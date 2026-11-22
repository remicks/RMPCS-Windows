param([switch]$DesktopChecks)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$version=([xml](Get-Content RMPCS.App\RMPCS.App.csproj -Raw)).Project.PropertyGroup.Version
$release=Join-Path $PSScriptRoot ('artifacts\v'+$version)
$app=Join-Path $release 'App'
New-Item -ItemType Directory -Force -Path $release | Out-Null
function Run-Dotnet([string[]]$Arguments) { & dotnet @Arguments; if($LASTEXITCODE -ne 0){throw ('dotnet failed: '+($Arguments -join ' '))} }
function Check-Exe([string]$File,[string[]]$Arguments) {
    $process=Start-Process -FilePath $File -ArgumentList $Arguments -PassThru -WindowStyle Hidden
    if(-not $process.WaitForExit(30000)){throw ('Self-check timed out: '+$File)}
    if($process.ExitCode -ne 0){throw ('Self-check failed: '+$File+' (exit '+$process.ExitCode+')')}
}
Run-Dotnet @('run','--project','RMPCS.Tests\RMPCS.Tests.csproj','-c','Release')
Run-Dotnet @('publish','RMPCS.App\RMPCS.App.csproj','-c','Release','--self-contained','false','-o',$app)
Run-Dotnet @('build','RMPCS.Packaging\RMPCS.Packaging.csproj','-c','Release')
$builder=Join-Path $PSScriptRoot 'RMPCS.Packaging\bin\Release\net10.0\RMPCS.Packaging.dll'
$msi=Join-Path $release ('RMPCS-Setup-'+$version+'-x64.msi')
Run-Dotnet @($builder,$app,'Installer\TaskCleanup.ps1',$msi)
Run-Dotnet @($builder,'--verify',$msi,$app,'Installer\TaskCleanup.ps1')
& (Join-Path $PSScriptRoot 'Bootstrap\Build.ps1') -AppPath $app -MsiPath $msi -OutputPath (Join-Path $release 'Bootstrap')
$setup=Join-Path $release 'Bootstrap\RMPCS.Setup.exe'
$portable=Join-Path $release 'Bootstrap\Portable'
Check-Exe $setup @('--self-test',('"'+(Join-Path $release 'installer-check.txt')+'"'))
Check-Exe (Join-Path $portable 'Launch-RMPCS.exe') @('--self-test',('"'+(Join-Path $release 'portable-check.txt')+'"'))
Check-Exe (Join-Path $app 'RMPCS.exe') @('--backup-acl-check')
Check-Exe (Join-Path $app 'RMPCS.exe') @('--diagnostic-read-check',('"'+(Join-Path $release 'diagnostic-read-check.txt')+'"'))
if($DesktopChecks) {
    Check-Exe (Join-Path $app 'RMPCS.exe') @('--smoke-test')
    Check-Exe $setup @('--smoke-test')
    Check-Exe (Join-Path $app 'RMPCS.exe') @('--tray-smoke-test',('"'+(Join-Path $release 'tray-check.txt')+'"'))
}
Copy-Item README.md,RUNTIMES.md,ROADMAP.md -Destination $portable -Force
$portableZip=Join-Path $PSScriptRoot ('artifacts\RMPCS-v'+$version+'-win-x64-portable.zip')
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $portableZip -Force
$iconsZip=Join-Path $PSScriptRoot ('artifacts\RMPCS-v'+$version+'-icons.zip')
Compress-Archive -Path Assets\RMPCS.ico,Assets\RMPCS.png,Assets\README.md -DestinationPath $iconsZip -Force
$bundle=Join-Path $PSScriptRoot ('artifacts\RMPCS-v'+$version)
New-Item -ItemType Directory -Force -Path $bundle | Out-Null
Copy-Item $setup,$msi,README.md,RUNTIMES.md,ROADMAP.md,RELEASE-NOTES.md -Destination $bundle -Force
Copy-Item $portable -Destination $bundle -Recurse -Force
$source=Join-Path $bundle 'Source'
New-Item -ItemType Directory -Force -Path $source | Out-Null
foreach($folder in @('RMPCS.App','RMPCS.Core','RMPCS.Tests','RMPCS.Packaging','Installer','Bootstrap','Assets')) {
    $target=Join-Path $source $folder
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Get-ChildItem -LiteralPath $folder -File | Copy-Item -Destination $target -Force
}
Copy-Item Build-Release.ps1,NuGet.Config,.gitignore,README.md,RUNTIMES.md,ROADMAP.md,RELEASE-NOTES.md,release-version.txt -Destination $source -Force
New-Item -ItemType Directory -Force -Path (Join-Path $source '.github\workflows') | Out-Null
Copy-Item .github\workflows\release.yml -Destination (Join-Path $source '.github\workflows') -Force
$fullZip=Join-Path $PSScriptRoot ('artifacts\RMPCS-v'+$version+'.zip')
Compress-Archive -Path (Join-Path $bundle '*') -DestinationPath $fullZip -Force
$files=@($setup,$msi,$portableZip,$iconsZip,$fullZip,(Join-Path $PSScriptRoot 'RUNTIMES.md'))
$files | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_) } | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
Write-Output ('Packaged RMPCS '+$version+'. No runtime installation, startup registration or maintenance performed.')
