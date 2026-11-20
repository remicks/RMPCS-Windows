# RMPCS 0.2 - Wade's PC Toolkit

## Downloads

- [Windows x64 installer (MSI)](https://github.com/remicks/RMPCS-Windows/releases/download/v0.2.0/RMPCS-Setup-0.2.0-x64.msi)
- [Windows x64 portable ZIP](https://github.com/remicks/RMPCS-Windows/releases/download/v0.2.0/RMPCS-v0.2-win-x64-portable.zip)
- [Complete v0.2 package with source](https://github.com/remicks/RMPCS-Windows/releases/download/v0.2.0/RMPCS-v0.2.zip)
- [Required runtime and setup instructions](RUNTIMES.md)
- [Planned v0.3 prerequisite installer / portable first-run setup](ROADMAP.md)


A Windows desktop app based on Wade's Multi-Purpose Cleanup Script (WMPCS) 0.3b. All of the batch file's maintenance categories are now available inside the app, with an MSI installer, optional sign-in startup, daily/weekly scheduling, saved run history, and clear completion results.

## Install and start

1. Extract the release ZIP.
2. Run `RMPCS-Setup-0.2.0-x64.msi`. Accept Windows' administrator prompt to install.
3. Open **RMPCS** from the Start Menu. Use its **Run as administrator** button if you see the startup warning. Maintenance and automation changes require administrator rights.

Requires 64-bit Windows 11 and the **Microsoft .NET 10 Desktop Runtime (Windows x64)**. The installer checks that runtime prerequisite and tells you if it is missing. It does not download or install a runtime. Official runtime download: https://dotnet.microsoft.com/download/dotnet/10.0

The installer places the app under `C:\Program Files\Degenerates Inc\RMPCS`, adds a Start Menu shortcut, and registers with Windows Installed apps. It does not enable automation or run cleanup during installation. The release includes a portable `App` folder and complete `Source`. Elevated startup and scheduled care require the installed copy so a writable portable folder cannot become a persistent elevated task target.

This development release is unsigned. The app uses WPF/.NET 10 with no third-party runtime packages, account integrations, or telemetry. Artwork and Avalonia components from LootDrop have not yet been imported.

## Every batch-file feature

| Original WMPCS action | RMPCS 0.2 implementation |
| --- | --- |
| User temp deletion | Clean the current user's Local AppData Temp directory; configurable age |
| Windows temp deletion | Clean Windows Temp; configurable age |
| Prefetch deletion | Clear `.pf` application-prefetch cache files; preserve layout/other metadata |
| Windows Logs deletion | Clean eligible files beneath Windows Logs; this does not clear Event Viewer channels |
| Search-index deletion | Reset the SystemIndex catalog through `ISearchCatalogManager.Reset`; Windows rebuilds its databases in the background |
| `ipconfig /flushdns` | Flush DNS cache, with output and exit code |
| `ipconfig /release` and `/renew` | Optional DHCP release followed by renewal, after repair commands |
| `DISM /Online /Cleanup-Image /RestoreHealth` | Repair Windows image; runs before SFC |
| `sfc /scannow` | Check system files; skipped if selected DISM repair fails |
| Reboot | Optional restart after successful completion, with 10-, 60-, or 300-second countdown |
| Pauses, status text, author/support banner | In-app selections, confirmation, live activity, results, About information, and saved logs |

**Select full batch** selects every maintenance category, all eligible file ages, and a restart after a 60-second countdown. Choose the 10-second option to match the original countdown. Selecting a preset does not execute it; review the options and click **Run selected tasks**. One confirmation lists the selected actions and their consequences.

**Recommended care** selects seven-day temp cleanup, DNS flush, DISM, and SFC. **Run recommended care** selects that profile and presents the execution confirmation. Advanced cache/log/index cleanup, DHCP release, and reboot are excluded from recommended care.

## Cleanup and task behavior

- Age choices: all eligible ages, older than one day, seven days, or thirty days. Both creation and last-write times must precede the cutoff.
- **Preview selected file cleanup** scans without deleting. It reports totals and the first 60 paths per selected area. Running rescans; previews are estimates, not a guarantee every file can be deleted.
- Deletion bypasses the Recycle Bin. Files outside the fixed selected roots, read-only/protected files, inaccessible/locked files, reparse points/junctions, and hard-linked files are skipped. There is no directory deletion, permission change, force-unlock, or service disabling.
- The cleanup engine validates the final resolved location, timestamps, attributes, and hard-link count through the same Windows handle used for deletion; concurrent writers and rename/deletion are denied while that handle is held. Age alone does not establish that a temp file is unused. Close other applications before broad cleanup.
- Prefetch clearing can temporarily slow launches. Windows Logs cleanup removes troubleshooting data. Index reset removes/rebuilds catalog data through the Windows API rather than deleting a live database. Search results may be incomplete and indexing may consume resources until the background rebuild finishes. An accepted reset is not reported as a completed rebuild.
- Commands run sequentially, using fixed Windows executable paths and fixed argument lists. The app does not build maintenance commands through a shell. DISM may use Windows Update to obtain repair components.
- DHCP reset runs last among maintenance operations to avoid interrupting DISM downloads. Renewal is always attempted after release, even when release fails or Stop is requested. It interrupts network/VPN/remote connections and reconnection is not guaranteed.
- **Stop after current task** finishes the active operation before stopping the remaining queue. The app refuses to close while its maintenance or preview is active. A machine-wide mutex prevents overlapping manual/scheduled maintenance sessions; a scheduled run skips if another session owns it.
- A selected restart is suppressed if a task fails or the queue is stopped. It is separately visible in the confirmation. Windows can force applications to close at the end of a nonzero countdown: save all work first. **Cancel restart** cancels any pending Windows shutdown/restart, including one scheduled outside RMPCS.
- Exit codes and command output are shown. DISM exit 3010 means Windows requests a restart. Read SFC/DISM findings: a zero exit code is not proof that every issue was repaired. Cleanup skips are counted separately from failed operations. There is no blanket promise of success or higher FPS.

## Startup and schedules

Open the **Automation** tab in the installed, elevated app.

**Enable startup** registers a Task Scheduler logon task for the current Windows user, with a 30-second delay and highest available rights. It opens the GUI only. **Disable startup** removes that task. Startup is not maintenance scheduling.

For automatic maintenance, choose daily/weekly frequency, weekly day, local 24-hour time, profile, and optional conditions, then click **Save schedule** and confirm the authorization. Saving replaces this user's previous RMPCS maintenance schedule; it does not add duplicate schedules.

| Profile | Authorized unattended work |
| --- | --- |
| Light | User + Windows temp files older than seven days, then DNS flush |
| Repair | Light profile followed by DISM and SFC |

Scheduled profiles do not clear Prefetch or Windows logs, reset search, release DHCP, or reboot. These tasks remain available in manual full-batch mode. Scheduling does not depend on the currently selected manual checkboxes.

Schedules use Windows Task Scheduler with the consenting user's interactive elevated token. No passwords are collected or stored. This user must be signed in and have administrative rights. Logging out, shutting down, sleeping, or forcing processes closed can interrupt a running repair; RMPCS does not keep the PC awake. Missed runs may start later when conditions allow. Local clock/timezone and daylight-saving changes follow Windows Task Scheduler behavior.

Optional conditions require ten minutes idle (with up to one hour wait) and/or AC power **before starting**. An active repair is not terminated when you resume work or unplug the PC. Tasks do not wake the PC, impose no forced execution timeout, disallow hard termination through their task setting, and ignore overlapping triggers. A hung Windows tool may therefore require investigation rather than automatic killing.

**Refresh status** shows enabled/off status and the next eligible maintenance trigger; it also loads saved schedule options. **Remove schedule** unregisters automatic maintenance. Removing a schedule does not stop an already-running Windows repair; wait for it to finish before uninstalling.

Both startup and maintenance are OFF by default. They are only registered when explicitly enabled in the app. This build process did not register either task on the development PC.

## History and uninstall

Manual and scheduled runs save logs under `%LOCALAPPDATA%\RMPCS\Logs` for the Windows identity running the app. History displays the latest 30 files. On a new run, eligible RMPCS log files older than thirty days are cleaned up using the handle-validated cleanup engine. Each log is bounded to roughly one million characters; the live view is also bounded. Log-write failures are reported and do not deliberately interrupt Windows command output. Logs can include local paths, usernames, and network details. Nothing is uploaded by RMPCS.

Use **Load selected log** to view a previous run inside the app, or **Export log** to save the displayed activity elsewhere. A scheduled run's process result indicates task failure; review its saved log for findings and skipped files.

Uninstall from Windows Installed apps or reopen the MSI. Finish all repairs and close RMPCS first. The MSI's uninstall action removes only tasks whose RMPCS name and installed executable path match this application, before removing installed files and shortcuts. It refuses to proceed if it detects active RMPCS maintenance/tasks. Task cleanup uses built-in Windows PowerShell and does not require the .NET runtime. User logs are retained. Upgrade handling preserves task registrations for the same installed executable path.

## Validation and remaining limits

- App build and framework-dependent publish completed with no compiler warnings or errors.
- 31 isolated checks passed for cleanup boundaries, all-age extension filtering, read-only/locked/changed files, complete batch ordering, failure reporting, restart suppression, reconnection attempts, routine profile boundaries, daily/weekly task XML, principal/argument escaping, startup behavior, idle/AC conditions, and input validation.
- Junction fixture creation was denied by the managed environment; two junction-specific checks remain skipped.
- The published GUI loaded and rendered, then exited with code 0 in smoke-test mode without executing maintenance.
- The MSI is checked offline for installation metadata, component/file relationships, task-cleanup sequencing, runtime prerequisites, UI/shortcut data, embedded-cabinet extraction, and exact SHA-256 matches against the published payload.
- A live elevated install/uninstall, Task Scheduler registration/execution, real Windows repairs, search-index reset, DHCP release, and reboot have **not** been performed here. These need supervised testing on a suitable PC before wider distribution. Offline verification does not establish that the entire installed workflow succeeds.

## Build from source

From the `Source` directory, on Windows with the .NET 10 SDK:

```powershell
dotnet run --project RMPCS.Tests/RMPCS.Tests.csproj -c Release
dotnet publish RMPCS.App/RMPCS.App.csproj -c Release --self-contained false -o artifacts/App
dotnet artifacts/App/RMPCS.dll --smoke-test
dotnet run --project RMPCS.Packaging/RMPCS.Packaging.csproj -c Release -- artifacts/App Installer/TaskCleanup.ps1 artifacts/RMPCS-Setup-0.2.0-x64.msi
dotnet run --project RMPCS.Packaging/RMPCS.Packaging.csproj -c Release -- --verify artifacts/RMPCS-Setup-0.2.0-x64.msi artifacts/App Installer/TaskCleanup.ps1
```

No external MSI compiler is required. The packaging utility uses Windows Installer's native database API and the built-in MakeCab compressor; verification reads the database and extracts its cabinet without installing anything. Building the MSI does not alter installed applications, startup settings, or Task Scheduler. `NuGet.Config` clears package feeds because these projects only need installed SDK/framework reference packs.

## Suggested next features

1. **Before/after dashboard:** disk space reclaimed, repair findings, last-run status, and upcoming maintenance in one view.
2. **Restore-point integration:** check System Protection availability, offer a restore point before repair, and report actual creation results. A restore point would not back up deleted temp/log files.
3. **Read-only health report:** disk health where Windows exposes it, storage space, recent crashes, and pending-reboot checks, with an exportable report.
4. **Completion notifications:** Windows notifications for scheduled run completion or failure, with a direct link to its saved result.
5. **Troubleshooting log archive:** export selected diagnostics before a Windows Logs cleanup, with retention and clear space-use reporting.
6. **Degenerates Inc. interface pass:** crown/DI branding, Kei/Rusty artwork, and LootDrop-inspired cards after the maintenance/install/scheduling flow has been tested.

Technical references: Microsoft Windows Search catalog management (https://learn.microsoft.com/windows/win32/search/-search-3x-wds-mngidx-catalog-manager), Task Scheduler XML schema (https://learn.microsoft.com/windows/win32/taskschd/task-scheduler-schema), and Windows Installer database documentation (https://learn.microsoft.com/windows/win32/msi/about-the-installer-database).
