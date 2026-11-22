RMPCS v0.5.0 introduces a clean Windows-style interface and a graphical setup wizard.

- Light neutral surfaces, a restrained rose accent, vertical navigation, consistent controls, spacing and clearer focus states. Existing maintenance functions and confirmations remain available.
- Graphical setup with requirements, preferences and completion pages. The final preferences step lets you choose desktop/Start Menu shortcuts and whether the installed app always requests administrator access. Windows UAC remains active.
- A **Run RMPCS after installation** checkbox on the successful completion page. Finish opens the app only when selected. Cancellation/failure does not show completion or launch the app. Standalone full-UI MSI installation offers the same choices and launch checkbox; unattended installs never launch the app automatically.
- Windows Installer owns shortcut components and saves machine-wide preferences. Prior choices are reused during upgrades/repair unless explicitly overridden. No AppCompat compatibility-layer changes are used.
- System tray icon with Open and Exit commands. Closing or minimizing hides the window while tasks finish; Exit refuses to interrupt an active operation. No polling or automatic maintenance is added by tray residency.
- Opt-in sign-in startup opens directly in the tray. Enable it in Automation after installing. Existing RMPCS sign-in tasks use the same `--startup` argument and also open in the tray after upgrading. Maintenance schedules stay separate.
- Reopening the same copy signals its existing desktop session for the same Windows user, session and elevation level. Scheduled processes keep the independent maintenance lock.
- Verified runtime setup and portable first-run launcher remain included. Portable sessions retain manual elevation and do not inherit the installed edition's administrator preference.

Use **RMPCS.Setup.exe** for guided installation, or extract the portable ZIP and open **Launch-RMPCS.exe**. Runtime links are in RUNTIMES.md. Startup remains off for new installations. The protected installed copy is required for elevated automatic startup; portable users can use the tray during a manually opened session.

Validation includes isolated policy/workflow checks, all eight shortcut/elevation choice combinations, launcher integrity/self-tests and offline MSI checks for conditional shortcut components, saved preferences and completion-only launch. App pages and all graphical setup steps are rendered without installation. Live tray shell interactions, Explorer restart recovery, sign-in/UAC, high-DPI desktop behavior, launch-after-install and elevated install/upgrade/repair/uninstall require interactive Windows validation. This managed environment denies native tray registration. No live cleanup, repairs, startup entries or scheduled tasks were changed.

Unsigned development prerelease. Close RMPCS using **Exit** before upgrading/uninstalling; closing its window alone now keeps it in the tray. Protected startup backups retain the v0.3.1 permissions fix.
