RMPCS v0.4.0 adds a consistent desktop identity and a quieter desktop experience.

- Shield/crown/pulse icon in nine Windows sizes, embedded in the app and setup EXEs. Branded desktop/Start Menu shortcuts, Installed apps icon and standalone icon pack included.
- Cleaner header, compact navigation and expandable additional/advanced cleanup sections. Full-batch selection opens its advanced options for review; all existing functions and confirmation prompts remain available.
- System tray icon with Open and Exit commands. Closing or minimizing hides the window while tasks finish; Exit refuses to interrupt an active operation. No polling or automatic maintenance is added by tray residency.
- Opt-in sign-in startup opens directly in the tray. Enable it in Automation after installing. Existing RMPCS sign-in tasks use the same `--startup` argument and also open in the tray after upgrading. Maintenance schedules stay separate.
- Reopening the same copy signals its existing desktop session for the same Windows user, session and elevation level. Scheduled processes keep the independent maintenance lock.
- Refreshed prerequisite setup screen, branded MSI dialogs, verified runtime setup and portable first-run launcher.

Use **RMPCS.Setup.exe** for guided installation, or extract the portable ZIP and open **Launch-RMPCS.exe**. Runtime links are in RUNTIMES.md. Startup remains off for new installations. The protected installed copy is required for elevated automatic startup; portable users can use the tray during a manually opened session.

Validation: 51 isolated policy/workflow checks passed (two junction fixtures unavailable in the managed environment); app build and read-only UI rendering passed. Launcher integrity/self-tests and offline MSI verification are included in the release build. Live tray shell interactions, Explorer restart recovery, sign-in/UAC, high-DPI desktop behavior and elevated install/upgrade/uninstall require interactive Windows validation. The local validation environment has no Explorer shell; native tray registration could not be exercised there. No live cleanup, repairs, startup entries or scheduled tasks were changed.

Unsigned development prerelease. Close RMPCS using **Exit** before upgrading/uninstalling; closing its window alone now keeps it in the tray. Protected startup backups retain the v0.3.1 permissions fix.
