# RMPCS 0.3 - Wade's PC Toolkit

A Windows 11 x64 desktop maintenance app based on WMPCS 0.3b, with optional prerequisite setup, read-only health checks, cleanup previews, reversible Run-entry management, and scheduled care.

## Downloads and first launch

- [Setup EXE with optional prerequisite installation](https://github.com/remicks/RMPCS-Windows/releases/download/v0.3.0/RMPCS.Setup.exe)
- [Standalone MSI for PCs that already have the runtime](https://github.com/remicks/RMPCS-Windows/releases/download/v0.3.0/RMPCS-Setup-0.3.0-x64.msi)
- [Portable ZIP with first-run launcher](https://github.com/remicks/RMPCS-Windows/releases/download/v0.3.0/RMPCS-v0.3-win-x64-portable.zip)
- [Runtime requirements and official Microsoft links](RUNTIMES.md)

**Installed edition:** run `RMPCS.Setup.exe`. It detects the runtime and offers to install the missing Microsoft .NET 10 Windows x64 Desktop Runtime before opening the embedded MSI. The MSI installs under Program Files, adds a Start Menu shortcut and uninstall entry, and leaves startup/schedules off. You can instead run the MSI directly if the runtime is already installed.

**Portable edition:** extract every file and open `Launch-RMPCS.exe`. When a compatible runtime is present, it verifies its companion app files and launches RMPCS. Otherwise it offers the prerequisite setup screen. Use `Launch-RMPCS.exe --setup` to open that screen explicitly. Keep the EXE, DLL and JSON files together. A portable app can still require installing a shared system runtime.

The bootstrap launchers use the .NET Framework supplied with Windows 11 and do not require .NET 10 to show setup. The main application requires .NET 10 Desktop Runtime. See [Bootstrap/Build.ps1](Bootstrap/Build.ps1) for the launcher build process.

Microsoft runtime downloads use HTTPS, an exact pinned SHA-512 from official release metadata, and Windows Authenticode trust verification with a Microsoft Corporation publisher check. A failed integrity/trust check stops execution. No certificate-validation bypass or unknown-host redirect is accepted. Downloads are size-limited, cancellable, and have a deadline. An offline-file option accepts the exact pinned installer and applies the same checks; offline signature validation depends on available certificate/revocation information. A compatible already-installed runtime is reused.

Microsoft's setup remains visible for its Windows permissions and license prompts. Cancelled or failed setup stops the chain. Restart-required results are reported; RMPCS's prerequisite launcher does not request an automatic restart. Runtime and MSI files are held against modification during verification/execution. The portable launcher similarly verifies and holds its app payload while the session runs.

## New in v0.3

### Health snapshot

The Health tab reads a short CPU sample, RAM usage, local fixed-drive space, uptime, common pending-restart indicators, and up to ten recent matching failure events per System/Application log from the last seven days. It offers conditional advice for high resource use or low free space. It changes no settings and runs no repairs.

Unavailable checks are identified. A short high-CPU sample is not a sustained-load diagnosis, event records do not prove a cause, and this release does not assess physical drive health. Results stay in the live activity view unless you export them. A live health scan/startup-inventory test stores only a validation summary, not the underlying personal report.

### Expanded cleanup previews

Select categories and click **Preview selected file cleanup**. The table shows per-category counts, estimated MB and skipped entries; Activity shows notes and the first sixty paths per file category. Running rescans, so preview numbers can change.

New, opt-in categories:

- **Thumbnail caches:** only `thumbcache_*.db` under the current identity's Windows Explorer cache. Open/protected files are skipped; Explorer is not terminated.
- **Archived error reports:** user and machine Windows Error Reporting `ReportArchive` folders. Active `ReportQueue` folders are preserved.
- **Application crash dumps:** `.dmp` files in the current identity's CrashDumps folder. They can be valuable troubleshooting evidence.
- **Recycle Bins:** a Windows Shell query previews item count/size for the current identity across drives. Emptying permanently removes all current items regardless of the age filter. Before/after reclamation is an estimate and may be affected by concurrent changes.

New categories are excluded from Recommended care, the original full-batch preset, and scheduled profiles. Deletion is separately listed in the execution confirmation; Recycle Bin and diagnostic-data consequences are called out explicitly.

### Reversible startup-app management

The Startup tab inventories common Windows `Run` values: current-user 64-bit view and machine 32-/64-bit views. Commands are displayed as data and never executed by this manager. Administrator rights are required for changes.

**Disable selected** stores the exact string, registry type, scope and identity in a protected HKLM backup before removing the Run value. **Restore selected** restores that value and original type; it refuses a conflicting live value. Interrupted operations leave the backup available for recovery. A live value already matching its backup can be reconciled without overwriting it.

Scope is deliberately explicit: this manager does not edit Task Manager's StartupApproved state, startup-folder links, RunOnce, services, scheduled tasks or packaged startup tasks. A Run value's presence does not guarantee Windows will launch it. Use Automation for RMPCS's own sign-in task. Current programs are not stopped when a Run entry is disabled.

Disabled entries and protected backups are retained when RMPCS is uninstalled; restore entries in the app first if you want to reverse those choices. Current-user operations always apply to the Windows identity running RMPCS, including when different administrator credentials are used.

## Original maintenance functions

All WMPCS categories remain available: user/Windows temp files, `.pf` Prefetch cache files, Windows Logs, supported Windows Search catalog reset, DNS flush, DHCP release/renew, DISM image repair, SFC, and optional reboot. Prefetch layout metadata is preserved. Search reset uses the documented Windows Search API; database rebuilding continues in the background after the request succeeds.

File cleanup offers all eligible ages or older-than-one/seven/thirty-day cutoffs. Creation and modification times must both precede the cutoff. The engine rejects paths outside fixed roots, junctions/reparse points, hard-linked files, protected/read-only entries and files locked against its required access. It preserves directories and does not change ACLs, force unlocks, or disable services. Final resolved location, age and attributes are checked through the same handle used for deletion, while concurrent writing/deletion is denied. Deleted files bypass the Recycle Bin; age alone does not prove they are unused.

**Recommended care** uses seven-day temp cleanup, DNS, DISM and SFC. **Select full batch** selects the original advanced categories, all eligible ages and a restart; it does not run until you confirm the listed tasks. Prefetch clearing can slow launches temporarily, log removal deletes diagnostic history, and search rebuilding can consume resources and leave incomplete results.

Commands run sequentially using fixed Windows executable paths/arguments, without a maintenance command shell. DISM runs before SFC and may use Windows Update; a failed selected DISM repair skips SFC. Read repair output: an exit code alone does not prove every issue was fixed. DHCP release is optional and runs after repairs; renewal is attempted even if release fails or Stop is requested. It can interrupt internet, VPN and remote sessions, and reconnection is not guaranteed.

**Stop after current task** stops the remaining queue after the current operation finishes; it does not kill a repair. The window cannot close during its active operation. A machine-wide mutex blocks overlapping maintenance sessions. A selected reboot is suppressed on task failure or queue stop. Countdown choices are 10, 60 or 300 seconds. Save work before selecting reboot; Windows may force applications to close at countdown end. **Cancel restart** can also cancel a Windows shutdown scheduled outside RMPCS.

## Automation and history

Both features are opt-in and require the protected MSI-installed copy. **Enable startup** opens the GUI thirty seconds after this user signs in, with highest available rights, without running maintenance.

Daily/weekly care supports local 24-hour time, optional ten-minute idle detection, and optional AC power. The user must be signed in. Light care uses seven-day user/Windows temp cleanup plus DNS; Repair adds DISM and SFC. New cleanup categories, Prefetch/log cleanup, search reset, DHCP release and reboot are excluded. No passwords are stored.

Missed runs may start later when conditions allow. Tasks do not wake the PC or force a running repair to end on resumed activity, battery changes or a timeout. Logging out, shutting down or forcibly closing a process can still interrupt it. An overlapping scheduled run skips. Removing a task does not terminate an already-running Windows tool.

History displays the newest thirty local run logs under `%LOCALAPPDATA%\RMPCS\Logs`. Eligible RMPCS log files older than thirty days are cleaned on a new run with the same validated engine. Each log and the live view are bounded. Logs may contain local paths and network details; nothing is uploaded by the app. Export is user-directed.

## Uninstall and release status

Close RMPCS and finish active maintenance, then uninstall through Windows Installed apps or the MSI. The uninstall action removes only matching RMPCS automatic tasks before files/shortcuts are removed. It rejects detected active RMPCS maintenance/tasks. User logs and startup-manager backups/choices are retained. Upgrades preserve task registrations targeting the installed executable.

This is an unsigned development prerelease. Build/GUI smoke tests, isolated workflow checks, launcher self-checks and offline MSI verification are performed. Live elevated install/uninstall, missing-runtime installation, real startup-entry mutation, task execution, repairs, index reset, Recycle Bin emptying and reboot require supervised validation on a suitable test PC. No live cleanup or configuration changes are made as build tests. Junction fixture creation remains blocked by this execution environment.

## Build from source

On Windows 11 with the .NET 10 SDK, from the source root:

```powershell
dotnet run --project RMPCS.Tests/RMPCS.Tests.csproj -c Release
dotnet publish RMPCS.App/RMPCS.App.csproj -c Release --self-contained false -o artifacts/App
dotnet artifacts/App/RMPCS.dll --smoke-test
dotnet run --project RMPCS.Packaging/RMPCS.Packaging.csproj -c Release -- artifacts/App Installer/TaskCleanup.ps1 artifacts/RMPCS-Setup-0.3.0-x64.msi
dotnet run --project RMPCS.Packaging/RMPCS.Packaging.csproj -c Release -- --verify artifacts/RMPCS-Setup-0.3.0-x64.msi artifacts/App Installer/TaskCleanup.ps1
./Bootstrap/Build.ps1 -AppPath artifacts/App -MsiPath artifacts/RMPCS-Setup-0.3.0-x64.msi -OutputPath "$PWD/artifacts/Bootstrap"
```

The bootstrap compiler is the Windows .NET Framework x64 C# compiler. No external MSI compiler or third-party runtime package is required. Build the MSI after publishing the app, then build the launchers so their embedded payload/manifests match that exact release. Never change app files inside a portable package without rebuilding its launcher manifest.

Author: Wade. Support: discord.beer/thelocker. RMPCS does not guarantee higher FPS or repair hardware/driver faults.
