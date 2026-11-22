# RMPCS 0.7.0 - Rusty's PC Toolkit

A Windows 11 x64 desktop maintenance app based on WMPCS 0.3b, with optional prerequisite setup, read-only health checks, cleanup previews, reversible Run-entry management, and scheduled care.

**v0.6 retains the v0.3.1 permissions fix.** The initial v0.3.0 prerelease is superseded. RMPCS protects the startup-backup registry store with an explicit non-inheriting DACL granting access only to Administrators and SYSTEM. Protection is established before backup writes/restores; failure stops the change. Opening the corrected app elevated also protects an existing store. Administrator rights are needed to view disabled-entry backups as well as change them.

## Downloads and first launch

- [Setup EXE with optional prerequisite installation](https://github.com/remicks/RMPCS-Windows/releases/download/v0.7.0/RMPCS.Setup.exe)
- [Standalone MSI for PCs that already have the runtime](https://github.com/remicks/RMPCS-Windows/releases/download/v0.7.0/RMPCS-Setup-0.7.0-x64.msi)
- [Portable ZIP with first-run launcher](https://github.com/remicks/RMPCS-Windows/releases/download/v0.7.0/RMPCS-v0.7.0-win-x64-portable.zip)
- [Runtime requirements and official Microsoft links](RUNTIMES.md)

**Installed edition:** run `RMPCS.Setup.exe`. It detects the runtime and offers to install the missing Microsoft .NET 10 Windows x64 Desktop Runtime before opening the embedded MSI. The MSI installs under Program Files, offers branded desktop and Start Menu shortcuts and an uninstall entry, and leaves startup/schedules off. You can instead run the MSI directly if the runtime is already installed.

**Portable edition:** extract every file and open `Launch-RMPCS.exe`. When a compatible runtime is present, it verifies its companion app files and launches RMPCS. Otherwise it offers the prerequisite setup screen. Use `Launch-RMPCS.exe --setup` to open that screen explicitly. Keep the EXE, DLL and JSON files together. A portable app can still require installing a shared system runtime.

The bootstrap launchers use the .NET Framework supplied with Windows 11 and do not require .NET 10 to show setup. The main application requires .NET 10 Desktop Runtime. See [Bootstrap/Build.ps1](Bootstrap/Build.ps1) for the launcher build process.

Microsoft runtime downloads use HTTPS, an exact pinned SHA-512 from official release metadata, and Windows Authenticode trust verification with a Microsoft Corporation publisher check. A failed integrity/trust check stops execution. No certificate-validation bypass or unknown-host redirect is accepted. Downloads are size-limited, cancellable, and have a deadline. An offline-file option accepts the exact pinned installer and applies the same checks; offline signature validation depends on available certificate/revocation information. A compatible already-installed runtime is reused.

Microsoft's setup remains visible for its Windows permissions and license prompts. Cancelled or failed setup stops the chain. Restart-required results are reported; RMPCS's prerequisite launcher does not request an automatic restart. Runtime and MSI files are held against modification during verification/execution. The portable launcher similarly verifies and holds its app payload while the session runs.

## New in v0.7

1. **Run summaries:** every completed manual/scheduled queue writes duration and failure/skip state, confirmed logical deletion totals, separately labelled Recycle Bin estimates, and a before/after available-space observation for the Windows OS drive. The last manual summary appears below Activity. These measurements do not prove repair success or attribute all space changes to RMPCS. Other work, compression, allocation, quotas and open handles can affect observations; failed cleanup may leave partial counters.
2. **Diagnostic archives:** History can export a selected saved run or the latest 30 RMPCS logs to a new local ZIP. The archive keeps original logs, writes a review manifest, and caps source data at 8 MiB per log / 32 MiB total. Active, linked, inaccessible and oversized logs are skipped. File handles verify final paths and single-link identity before reading; names cannot traverse ZIP paths. The chosen destination is created without overwriting and verified before log content is written. An unsuccessful export may leave an incomplete ZIP. Logs may contain private paths and command output. Review before sharing; archives are unencrypted and never uploaded by the app. No event-channel, browser, crash-dump or account data is automatically gathered.
3. **Completion notifications:** opt in under Settings and Save. A tray instance must be running in the same Windows sign-in session. Scheduled processes post only one bounded informational status code to that user's resident app; default Windows integrity isolation is preserved, and no incoming message can run maintenance or change settings. No paths, arbitrary text or credentials are sent. Notices are not authenticated evidence: check History/Activity. Delivery is best effort, rate-limited, not queued, and can be suppressed by Windows or an unavailable/closed tray. Manual runs use the same notification preference. No notification polling, service or extra task is installed.

The maintenance command set and existing deletion/repair safety rules remain unchanged. Restore points, broader cleanup targets and workload-based unattended deferral remain future work requiring more Windows validation.

## New in v0.6 — Rusty's PC Toolkit

**Settings** now manages window/tray behavior, first-hide reminders, startup page, activity text size, default manual cleanup age, GitHub update preferences and the installed administrator-launch option. Save general preferences explicitly. The administrator option has its own Apply button, requires an elevated installed session and applies across Windows users. Defaults never save task selections or run maintenance. Interface reset leaves installation choices, administrator prompting, startup/schedules, disabled-entry backups and history intact.

**Updates:** Check manually in Settings, or opt into a launch check at most once per 24 hours. Automatic checks are off by default and do not run in scheduled maintenance processes. Preview releases are included by default because current RMPCS builds are previews. The checker contacts the official public GitHub API without credentials, sends no diagnostic/user data, compares semantic versions from the newest 100 releases, shows plain-text notes and opens a validated official release page on request. It never downloads or runs an installer. Responses are bounded to 2 MiB with a 20-second deadline and no redirects. Rate limits, offline conditions and missing channels are explained separately from a successful up-to-date check. Release browsing/installation remains manual.

**Help** is searchable offline and covers task risks, previews, repairs, networking, tray, automation, startup restore, updates, installation and privacy. It includes GitHub/Discord support links and a reviewable support summary containing only app/runtime/Windows version, architecture, elevation state and edition. This summary excludes usernames, paths, health data, startup commands and logs. Saved activity logs can contain private details and must be reviewed before sharing. Clear activity view preserves saved run logs.

**LootDrop:** [LootDrop for Windows](https://github.com/remicks/LootDrop-Windows) is a separate Degenerates Inc. app for discovering free-game offers and tracking a local wishlist and claimed collection. Official store links open in the browser; it does not collect store passwords or claim games automatically. Its GitHub link and description are available in Help.

Preferences are local to the running Windows identity and shared between portable/installed sessions; general preferences are stored under the current user's RMPCS registry key. Automatic-check attempt timestamps are separate from explicit preference saves. Elevating with different credentials uses that administrator's preferences/history. No GitHub authentication information is collected or saved.

## New in v0.5

The app uses a light Windows-style layout with vertical navigation, neutral surfaces, consistent fields and checkboxes, and a restrained rose accent. Additional/advanced tasks remain expandable; full-batch selection opens the advanced group for review.

The graphical setup wizard walks through requirements, a final preferences step, and successful completion. Choose whether to create desktop/Start Menu entries and whether the installed app should always request administrator access. These choices default on for a new install; saved choices are reused where available. Windows UAC still requires approval. Cancelling elevation leaves a standard session with protected actions disabled. The setting applies to the protected installed copy, across Windows users, and is stored under the machine's installation settings. Portable copies keep manual elevation.

On completion, **Run RMPCS after installation** defaults on. Click **Finish** to open the app, or uncheck it to finish without launching. Closing the completion window also finishes without launching. Failed/cancelled installation does not open the app. Opening RMPCS starts no maintenance. Startup to tray and schedules remain independently opt-in.

The standalone full-UI MSI also provides shortcut/admin choices and a completion launch checkbox. Basic/unattended MSI installs accept `CREATE_DESKTOP=0/1 CREATE_STARTMENU=0/1 ALWAYS_ADMIN=0/1`; they never launch RMPCS themselves. The EXE wizard passes these fixed properties and handles its own completion-page launch. Installer-managed shortcut components and preferences are removed on uninstall; protected disabled-startup backups remain preserved.

## New in v0.4

The app, taskbar, notification area, installer, shortcuts and Installed apps entry share a new shield/crown/pulse identity. A standalone icon pack includes a transparent PNG and an ICO with nine sizes from 16 to 256 pixels. The app has a compact header and expandable optional/advanced cleanup groups; confirmation prompts and original maintenance functions remain available.

### Tray and startup

Close or minimize the window to keep it in the system tray. Click the icon to reopen; right-click for **Open RMPCS** and **Exit RMPCS**. The app also has explicit **Hide to tray** and **Exit** buttons. Hiding does not stop an active task. Exit is blocked until that operation finishes; use **Stop after current task** to stop the remaining queue safely. A first-hide notification explains how to reopen/exit. If initial tray registration fails, the app stays visible and closing exits normally. It attempts to recreate its icon after Explorer restarts and restores the window if re-registration fails.

In **Automation**, choose **Enable startup to tray** to register the protected installed app for this Windows user's sign-in. This is opt-in and starts only the desktop app, with no maintenance. Existing RMPCS startup tasks already use `--startup` and adopt tray startup after upgrading. Portable sessions support the tray but cannot register privileged startup from a writable folder. Maintenance schedules remain independently opt-in.

Reopening RMPCS signals the existing desktop session for the same Windows user, logon session and elevation level. Standard and administrator sessions are separate so an administrator relaunch can replace a standard window. No arbitrary commands or maintenance requests are accepted through the activation signal. Scheduled maintenance still uses the machine-wide operation lock.

Use **Exit** before upgrading or uninstalling: the title-bar close button now hides to tray. Tray residency runs no periodic cleanup, health scan or polling. Windows may put the icon in the notification overflow menu.

Build packages with `powershell -NoProfile -File Build-Release.ps1`. Use `-DesktopChecks` only in an interactive Windows session with Explorer. The build never installs a runtime or registers tasks. See RELEASE-NOTES.md for validation limits.

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
dotnet run --project RMPCS.Packaging/RMPCS.Packaging.csproj -c Release -- artifacts/App Installer/TaskCleanup.ps1 artifacts/RMPCS-Setup-0.7.0-x64.msi
dotnet run --project RMPCS.Packaging/RMPCS.Packaging.csproj -c Release -- --verify artifacts/RMPCS-Setup-0.7.0-x64.msi artifacts/App Installer/TaskCleanup.ps1
./Bootstrap/Build.ps1 -AppPath artifacts/App -MsiPath artifacts/RMPCS-Setup-0.7.0-x64.msi -OutputPath "$PWD/artifacts/Bootstrap"
```

The bootstrap compiler is the Windows .NET Framework x64 C# compiler. No external MSI compiler or third-party runtime package is required. Build the MSI after publishing the app, then build the launchers so their embedded payload/manifests match that exact release. Never change app files inside a portable package without rebuilding its launcher manifest.

Author: Rusty. Support: discord.beer/thelocker. RMPCS does not guarantee higher FPS or repair hardware/driver faults.
