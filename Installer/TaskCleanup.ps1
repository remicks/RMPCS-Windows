param([Parameter(Mandatory=$true)][string]$InstallRoot)
$ErrorActionPreference = 'Stop'
# Runs only as an elevated MSI uninstall action, before installed files are removed.
# Never changes startup entries or tasks belonging to other applications.
$programFiles64 = if ($env:ProgramW6432) { $env:ProgramW6432 } else { $env:ProgramFiles }
$expectedRoot = [IO.Path]::GetFullPath((Join-Path $programFiles64 'Degenerates Inc\RMPCS')).TrimEnd('\')
$actualRoot = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\','.')
if ($actualRoot -ne $expectedRoot) { throw 'Unexpected RMPCS installation directory.' }
$expectedExe = Join-Path $expectedRoot 'RMPCS.exe'
$lease = New-Object Threading.Mutex($false, 'Global\DegeneratesInc.RMPCS.Maintenance')
$owned = $false
try {
    try { $owned = $lease.WaitOne(0) } catch [Threading.AbandonedMutexException] { $owned = $true }
    if (-not $owned) { throw 'RMPCS maintenance is running. Wait for it to finish, close RMPCS, and retry uninstall.' }
    $service = New-Object -ComObject 'Schedule.Service'
    $service.Connect()
    $folder = $service.GetFolder('\')
    $names = @()
    foreach ($task in $folder.GetTasks(1)) {
        if ($task.Name -notmatch '^DegeneratesInc\.RMPCS\.(Startup|Maintenance)\.S-1-[0-9-]+$') { continue }
        $actions = $task.Definition.Actions
        if ($actions.Count -ne 1) { continue }
        $action = $actions.Item(1)
        if ($action.Type -ne 0 -or [IO.Path]::GetFullPath($action.Path) -ne $expectedExe) { continue }
        if ($task.GetInstances(0).Count -gt 0) { throw 'A scheduled RMPCS session is active. Close RMPCS or wait for maintenance to finish, then retry.' }
        $names += $task.Name
    }
    foreach ($name in $names) { $folder.DeleteTask($name, 0) }
} finally {
    if ($owned) { $lease.ReleaseMutex() }
    $lease.Dispose()
}
