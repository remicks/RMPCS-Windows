# RMPCS 0.12.1 - Rusty's PC Toolkit

A Windows 11 x64 desktop maintenance app based on WMPCS 0.3b, with optional prerequisite setup, read-only health checks, cleanup previews, reversible Run-entry management, and scheduled care.

**This build retains the v0.3.1 permissions fix.** The initial v0.3.0 prerelease is superseded. RMPCS protects the startup-backup registry store with an explicit non-inheriting DACL granting access only to Administrators and SYSTEM. Protection is established before backup writes/restores; failure stops the change. Opening the corrected app elevated also protects an existing store. Administrator rights are needed to view disabled-entry backups as well as change them.

## Installer hotfix v0.12.1

The earlier v0.12.0 distribution incorrectly authored MSI ProductVersion and upgrade boundaries as **0.9.2**, so Windows could block an upgrade as a same/newer version. Its welcome text and launcher metadata were stale too. **Use the v0.12.1 MSI or setup EXE, exit RMPCS using Quit RMPCS first, and upgrade normally. An uninstall-first workaround is not required for this versioning bug.** Logs, interface preferences and disabled-startup backups are preserved; existing automatic tasks are retained during a major upgrade.

The MSI now derives its version from RMPCS.dll; independent release-version verification checks the app, MSI, upgrade boundaries, UI title, setup EXE and portable launcher. Build validation rejects deliberately corrupted copies reproducing the original mismatch. Double-clicking the same installed MSI now offers Repair or Remove; explicit removal confirms separately with Cancel as the default. Repair uses Windows Installer's standard omus mode, retaining newer files and restoring missing/older files, registered preferences and shortcuts. It is not a guaranteed replacement of every damaged equal-version file.

Post-install launch is suppressed when MSI reports pending replacement/restart flags. The setup EXE disables automatic launch after a restart-required result, including an earlier prerequisite restart requirement. Portable prerequisite setup defers opening the app if its runtime requires restart. No restart is forced.

See [Installer/AUDIT.md](Installer/AUDIT.md) for the recheck matrix and limits. Native condition tests use a restricted Windows Installer session that cannot change machine state. Actual elevated install/upgrade/uninstall, files-in-use, cancellation/rollback and restart behavior still need supervised Windows validation. MSI rollback covers its own resources; earlier prerequisite installation and uninstall task cleanup are not guaranteed to roll back. Do not uninstall just to test an upgrade when automatic tasks matter.

## New in v0.12.0 - more themes and fonts

Six new coordinated palettes are available under **Settings → Fonts & appearance → Theme palette**: **Ocean Slate**, **Nordic Frost**, **Forest Mist**, **Warm Sand**, **Rose Quartz**, and **Graphite**. Each provides distinct Light/Dark window, card, text, hover, selection and control colors. System mode follows the Windows appearance. The original eight accent presets and custom HEX/RGB option retain their existing base colors. Windows High Contrast overrides custom colors.

Five new embedded font families—**Lato, Poppins, Hind, PT Sans, and Oxygen**—bring the total to eight bundled families plus the Windows system option. Normal and emphasized faces are included; no font installation or runtime font download is required. Preview, save, discard, reset and preference undo work with the new choices. Original font files, licenses and pinned source hashes are included with the source bundle; installed/portable packages retain all copyright/license notices.

Validation covers all 14 presets in Light/Dark, body and secondary-text contrast across the new surfaces, input-border/switch visibility, accent contrast, preference round-trips, embedded font resolution and licenses, existing theme/navigation/sidebar checks, and distribution integrity. This adds appearance choices only; no maintenance or scheduling behavior changes.

## New in v0.11.0 - typography and interface refinement

The app includes **Barlow**, **Rajdhani** and **Atkinson Hyperlegible** as embedded fonts, with a **Windows system** option. Choose one in **Settings → Fonts & appearance**. Font/color changes preview immediately; **Save appearance** keeps them and **Discard preview** restores the saved look. Fonts work offline and are never installed into Windows. Input, help, result and confirmation text follow the selected font. Full SIL Open Font License texts and copyright notices are included with both distributions; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). The source bundle includes pinned upstream paths and integrity hashes under Assets/Fonts.

**Back**, **Alt+Left** and the mouse Back button return through visited sections in both layouts, including sidebar Results & activity. They do not replay tasks, undo settings, cancel active work or exit the app. **Quit RMPCS** is a separate, clearly labeled action away from maintenance buttons. Closing/minimizing retains the configured tray behavior.

The interface now uses a consistent navigation rail, three Overview cards, grouped Settings, rounded task checkboxes/dropdowns, clearer focus states, and themed read-only text surfaces. Text selection/copy, report scrolling, keyboard controls and High Contrast color overrides remain available. Navigation selection colors are confined to the navigation headers and cannot override page content colors.

Validation includes bundled-font resolution from application resources, license-resource presence, live font preview/discard, Back history and branching, sidebar page/results routing, selectable and scrollable output, existing maintenance guards and packaging/integrity checks. Visual previews cover Light/Dark, all three bundled font families, sidebar and compact desktop layouts. Supervised Windows installation, assistive-technology/High Contrast transitions and broad beta testing remain necessary; this is an unsigned development prerelease.

## Compact sidebar

Choose **Sidebar** in the main window header for a phone-like, single-pane layout. Use the page picker for all existing tools and **Results & activity**; checks and maintenance show their output there automatically. **Full window** restores the standard layout and preserves the current session.

In **Settings → Sidebar**, choose the left/right edge, current or selected monitor, and a width of 360/400/440/480 scaled units. **Apply sidebar settings** saves only sidebar choices; **Save preferences** includes them too. Sidebar and reservation are off by default. The current monitor is captured when the sidebar opens; a missing selected display falls back to an available one with a status message. Display identifiers can change after hardware/driver changes; use **Refresh displays** to update the list.

**Reserve desktop space while visible** registers this window with the Windows appbar service, allowing maximized ordinary apps to leave room. It does not edit Windows' global work-area or taskbar settings. The sidebar stays above ordinary windows. Full-screen/exclusive apps, other always-on-top windows and Windows security UI can overlap it; reservation is not an exclusion/security boundary. Hiding to tray, minimizing, leaving sidebar mode and exiting release the reservation. Showing it again reacquires space; hidden sign-in startup reserves none. Registration failure is reported as overlay mode. Width follows Windows DPI, is capped at half the monitor, and may require scrolling on small/high-scale displays.

Existing task confirmations, administrator checks, tray preferences, themes and scheduling guards apply in both layouts. Layout changes never start maintenance. Monitor removal, DPI/work-area changes and Explorer restart trigger layout updates. Standard window bounds are restored or fitted to an available display when leaving sidebar mode.

Validation includes geometry/preference tests, WPF page routing, real positioning on three display layouts, hide/minimize/restore/disposal checks, and isolated appbar success/failure lifecycle tests. This development session has no Explorer appbar service: overlay fallback was verified, while live desktop reservation, Explorer restart, full-screen handoff and physical mixed-DPI hot-plug behavior still require supervised Windows validation.

## Downloads and first launch

- [Setup EXE with optional prerequisite installation](https://github.com/remicks/RMPCS-Windows/releases/download/v0.12.1/RMPCS.Setup.exe)
- [Standalone MSI for PCs that already have the runtime](https://github.com/remicks/RMPCS-Windows/releases/download/v0.12.1/RMPCS-Setup-0.12.1-x64.msi)
- [Portable ZIP with first-run launcher](https://github.com/remicks/RMPCS-Windows/releases/download/v0.12.1/RMPCS-v0.12.1-win-x64-portable.zip)
- [Runtime requirements and official Microsoft links](RUNTIMES.md)

**Installed edition:** run `RMPCS.Setup.exe`. It detects the runtime and offers to install the missing Microsoft .NET 10 Windows x64 Desktop Runtime before opening the embedded MSI. The MSI installs under Program Files, offers branded desktop and Start Menu shortcuts and an uninstall entry, and leaves startup/schedules off. You can instead run the MSI directly if the runtime is already installed.

**Portable edition:** extract every file and open `Launch-RMPCS.exe`. When a compatible runtime is present, it verifies its companion app files and launches RMPCS. Otherwise it offers the prerequisite setup screen. Use `Launch-RMPCS.exe --setup` to open that screen explicitly. Keep the EXE, DLL and JSON files together. A portable app can still require installing a shared system runtime.

The bootstrap launchers use the .NET Framework supplied with Windows 11 and do not require .NET 10 to show setup. The main application requires .NET 10 Desktop Runtime. See [Bootstrap/Build.ps1](Bootstrap/Build.ps1) for the launcher build process.

Microsoft runtime downloads use HTTPS, an exact pinned SHA-512 from official release metadata, and Windows Authenticode trust verification with a Microsoft Corporation publisher check. A failed integrity/trust check stops execution. No certificate-validation bypass or unknown-host redirect is accepted. Downloads are size-limited, cancellable, and have a deadline. An offline-file option accepts the exact pinned installer and applies the same checks; offline signature validation depends on available certificate/revocation information. A compatible already-installed runtime is reused.

Microsoft's setup remains visible for its Windows permissions and license prompts. Cancelled or failed setup stops the chain. Restart-required results are reported; RMPCS's prerequisite launcher does not request an automatic restart. Runtime and MSI files are held against modification during verification/execution. The portable launcher similarly verifies and holds its app payload while the session runs.

## New in v0.9.2 - visible setup progress

The setup EXE has a dedicated Installation page with a larger progress bar, a current-phase message and a persistent taskbar window while it prepares files and waits for Microsoft/Windows Installer. Checksum/trust verification runs off the UI thread. Known-length downloads show download percentage; installer waiting uses an indeterminate activity indicator, with real installation progress in Windows Installer's own window. No overall percentage is fabricated.

The standalone MSI's full UI includes a modeless progress dialog before execution for installation, major upgrade, repair and removal. Its progress/status controls receive native Windows Installer events, including rollback updates. Completion, removal, cancellation and error screens remain visible for acknowledgment. Cancelling asks for confirmation and lets Windows finish rollback. The launcher cannot be closed to forcibly terminate an active child installer; cancellation during preparation is checked before the next process launch.

Windows Installed apps/basic UI uses native Windows Installer progress. Explicit silent switches remain supported. Windows owns UAC and Microsoft installer/license prompts; setup is not forced above other applications. Exit RMPCS from its tray menu before updating. Full elevated lifecycle validation is still pending; see release notes.

## New in v0.9 - simpler controls and complete app themes

Current patch: v0.11.0 includes visible setup progress and retains v0.9.1 native Windows-library loading restrictions to System32.

- **Guided Overview:** Check my PC performs a read-only health scan; Review recommended care opens the maintenance selection without running it. Startup apps is a separate clear next step. The run action appears only while reviewing Maintenance. Less-used tools and raw startup-command details stay expandable. The window adapts to smaller work areas and content scrolls.
- **Startup-entry switches:** On means a common Run entry is present; Off means RMPCS has a saved disabled-entry backup. The switch does not claim Task Manager or Windows approval status. Administrator rights, confirmation, protected backups, current-value revalidation and conflicting-restore protection remain required. Loading/refreshing the list never invokes a write. Cancelled/denied changes return to the prior state; failures refresh the inventory rather than show unverified success. Stored commands are displayed only and never launched.
- **Theme-aware switches:** Window/tray, update and repair preferences, administrator prompting, sign-in startup, schedule enablement and idle/AC conditions use restrained switches. General settings still require Save preferences; administrator prompting uses its separate Apply button. Startup and schedule switches apply only after confirmation, while Save & enable schedule applies the reviewed schedule form. Unknown task status disables its switches. Maintenance task selections remain checkboxes with a separate run confirmation.
- **Appearance in Settings:** System (follow Windows), Light and Dark modes; Midnight Purple, Electric Blue, Neon Cyan, Emerald Green, Crimson Red, Sunset Orange, Hot Pink and Gold accent presets; or custom HEX/RGB. Midnight Purple uses LootDrop's #D58AFF accent and a related dark palette. Save appearance persists only appearance; Save preferences saves all general choices. Preview is immediate; Discard preview restores saved appearance. App-owned views, controls, dropdowns, grids, text, scrollbars, confirmations and tray menus share dynamic colors. Windows High Contrast overrides custom colors, and Windows theme changes are observed in System mode. Accent text and labels choose contrasting colors. Windows-owned file/UAC dialogs and installer surfaces retain their OS/installer styling.

Appearance preferences are bounded data stored alongside existing local preferences and covered by one-step preference undo. No theme downloads, external theme code, extra dependency, credential collection or maintenance category is introduced. Confirmation dialogs default to Cancel; Escape/window-close never approve a Yes/No action. Existing schedules and deletion/repair policies are preserved. See release notes for validation limits.

## New in v0.8

The original recommendations **6, 7, 10 and 12** are implemented:

- **Crash/stability reports (Diagnostics):** request 1/7/30 days of bounded System/Application event metadata, recurring source/ID counts and recent entries. No raw event messages, usernames or machine identifiers are collected. The report describes a sample, not a proven cause. Explicitly export the displayed report to a new local file; nothing is uploaded.
- **Restore points and undo (Protection):** request a restore point, then verify a new matching sequence/description before reporting success. Accepted-but-skipped requests are reported as unverified. Optional pre-repair creation in Settings applies only to manual DISM/SFC/Search resets; a failure requires a separate default-No confirmation to continue. No System Protection/service/frequency changes or automatic OS rollback. Restore points cannot recover permanently deleted personal files. One-step undo covers RMPCS interface/tray/update/repair preferences and the installed administrator-prompt preference, with expected-state checks; administrator undo also checks the Windows identity. Startup-entry restore remains separate. Undo does not reverse cleanup, repairs or schedules.
- **Network diagnostics (Diagnostics):** fixed Microsoft/GitHub DNS and HTTPS HEAD tests with normal TLS validation, no credentials/cookies/redirects and a 20-second overall deadline, plus five bounded pings to one OS-configured gateway. Multiple gateways are disclosed; ICMP filtering is not treated as proof of disconnection. No network resets are performed. Keep an in-memory baseline and compare the same target/gateway after a separately chosen manual repair; export explicitly to retain it. Gateway addresses may be private. Natural variation cannot establish that a repair helped.
- **Smarter scheduling (Automation):** opt-in 1/2/4-hour maintenance start windows and 15/30/60-minute maximum workload waits. Busy or unknown signals defer: CPU >=70%, RAM >=85%, aggregate network >=5 MiB/s, Windows full-screen/presentation/quiet state or active microphone sessions. No audio is recorded. Game/call/download detection is best effort, not guaranteed identification. Selected idle/AC conditions are rechecked after waiting. Skips and deferrals are logged, with existing opt-in tray notices. Guards never terminate an active repair. Existing schedules retain their behavior until explicitly changed.

Weekly windows crossing midnight belong to the scheduled start day. Windows use local wall-clock time, including timezone/DST changes; deferral budgets use elapsed time. Workload checks occur at most once per minute while waiting; an unavailable check defers and a preflight timeout skips. These are start safeguards, not continuous enforcement after maintenance begins. Only the protected installed edition can register privileged automatic tasks; portable users can run reports, restore-point requests and manual maintenance with the appropriate rights.

## New in v0.7

1. **Run summaries:** every completed manual/scheduled queue writes duration and failure/skip state, confirmed logical deletion totals, separately labelled Recycle Bin estimates, and a before/after available-space observation for the Windows OS drive. The last manual summary appears below Activity. These measurements do not prove repair success or attribute all space changes to RMPCS. Other work, compression, allocation, quotas and open handles can affect observations; failed cleanup may leave partial counters.
2. **Diagnostic archives:** History can export a selected saved run or the latest 30 RMPCS logs to a new local ZIP. The archive keeps original logs, writes a review manifest, and caps source data at 8 MiB per log / 32 MiB total. Active, linked, inaccessible and oversized logs are skipped. File handles verify final paths and single-link identity before reading; names cannot traverse ZIP paths. The chosen destination is created without overwriting and verified before log content is written. An unsuccessful export may leave an incomplete ZIP. Logs may contain private paths and command output. Review before sharing; archives are unencrypted and never uploaded by the app. No event-channel, browser, crash-dump or account data is automatically gathered.
3. **Completion notifications:** opt in under Settings and Save. A tray instance must be running in the same Windows sign-in session. Scheduled processes post only one bounded informational status code to that user's resident app; default Windows integrity isolation is preserved, and no incoming message can run maintenance or change settings. No paths, arbitrary text or credentials are sent. Notices are not authenticated evidence: check History/Activity. Delivery is best effort, rate-limited, not queued, and can be suppressed by Windows or an unavailable/closed tray. Manual runs use the same notification preference. No notification polling, service or extra task is installed.

The maintenance command set and existing deletion/repair safety rules remain unchanged. The v0.8 additions below keep privileged changes explicit; broader cleanup targets remain future work.

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
dotnet run --project RMPCS.Packaging/RMPCS.Packaging.csproj -c Release -- artifacts/App Installer/TaskCleanup.ps1 artifacts/RMPCS-Setup-0.12.1-x64.msi
dotnet run --project RMPCS.Packaging/RMPCS.Packaging.csproj -c Release -- --verify artifacts/RMPCS-Setup-0.12.1-x64.msi artifacts/App Installer/TaskCleanup.ps1
./Bootstrap/Build.ps1 -AppPath artifacts/App -MsiPath artifacts/RMPCS-Setup-0.12.1-x64.msi -OutputPath "$PWD/artifacts/Bootstrap"
```

The bootstrap compiler is the Windows .NET Framework x64 C# compiler. No external MSI compiler or third-party runtime package is required. Build the MSI after publishing the app, then build the launchers so their embedded payload/manifests match that exact release. Never change app files inside a portable package without rebuilding its launcher manifest.

Author: Rusty. Support: discord.beer/thelocker. RMPCS does not guarantee higher FPS or repair hardware/driver faults.

## Discord release announcements

Release notifications use the repository Actions secret `DISCORD_WEBHOOK_URL`. The URL is never stored in source, outputs or downloadable application files. The publishing workflow calls a separate read-only notification job only after release creation and asset upload succeed. A published-release trigger also supports releases published manually in GitHub. Both preview and stable releases are supported; the expected installer, portable/source/icon archives, runtime requirements and checksum file must all be fully uploaded.

The requested SethCohen/github-releases-to-discord action is pinned to reviewed v1.20.0 commit `24d166886aee4646d448c8a389ff9e1ebcab3682`, with Node 24 forced. It receives the webhook secret and sanitized public release data, no repository-write permissions or persisted checkout credentials. Announcements include download links, a preview label when applicable and bounded release notes. Message content is empty and release-note @ characters are neutralized; no everyone/role/user pings are configured. A bounded preflight checks the official webhook URL format and published release metadata without contacting Discord. Upstream delivery is limited to a one-minute step; it can retry rate limits and reports delivery failures. The upstream action logs Discord's message response, which can include channel/message/webhook IDs and the public release message, but webhook credentials are masked. A code review and commit pin reduce update risk; they do not eliminate third-party dependency risk.

Workflow changes on main run tests and preflight only and never post to Discord. In Actions, **Announce RMPCS releases** can validate an exact existing release tag with its Send option left off, or send/retry an announcement when explicitly selected. Network delivery has no exactly-once guarantee; inspect the channel before manually retrying a notification whose result is uncertain. Retry the announcement workflow rather than rerunning the immutable release publishing job. A notification failure leaves the published release available and visible as a failed announcement job.
