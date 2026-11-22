RMPCS v0.6.0 — Rusty's PC Toolkit

- Corrected the product name across the app, installer, Windows shortcuts, Installed apps entry, tray, logs and documentation.
- GitHub update checks with semantic version comparison, stable/preview filtering and plain-text release notes. Optional launch checks are off by default and limited to once per 24 hours per Windows identity. No credentials, PC reports or logs are sent; the app never downloads or executes updates.
- A dedicated Settings section for close/minimize-to-tray behavior, tray reminders, startup page, activity text size, default manual cleanup age, update preferences and the installed administrator-launch preference. General preferences are saved explicitly. Installation/UAC changes use their own administrator-only Apply button.
- Searchable offline Help with maintenance, repair, network, automation, privacy, installation, update and troubleshooting guidance.
- A verified LootDrop for Windows GitHub link and brief description. Existing Discord support and GitHub issue links are available in Help.
- Small usability additions: a reviewable support summary without logs/usernames/paths, clearer interface reset scope, F1/Ctrl+, navigation, a toolbar shortcut to Updates, and Clear activity view without deleting saved history.

Download RMPCS.Setup.exe for guided installation, or extract the portable ZIP and open Launch-RMPCS.exe. The installer retains optional shortcuts, administrator prompting and launch-after-install. Runtime requirements, official links and SHA256 checksums are included. Close using Exit before upgrading if close-to-tray is enabled.

Update checks read the newest 100 releases from the official public repository. Only canonical HTTPS release links for remicks/RMPCS-Windows are accepted. Redirects, oversized responses and malformed metadata are rejected. Network/rate-limit failures are shown as unavailable, never as up to date. The stable-only channel can have no matching releases while RMPCS remains in preview. Review releases in your browser and install manually.

Validation includes isolated policy/workflow tests, mocked update requests and failures, semantic-version/URL/channel checks, preference normalization and offline Help search, launcher integrity tests, offline MSI checks and read-only UI rendering. A separate non-gating CI check exercises the live public GitHub feed; API availability/rate limits are reported independently. Native preference writes, live UAC, tray/shell interaction, browser handoff, sign-in, clean-machine runtime installation and elevated install/upgrade/repair/uninstall still require supervised Windows testing. No live cleanup, repairs, startup entries or scheduled tasks were changed during this build.

Unsigned development prerelease. Interface preference reset does not change startup tasks, schedules, installed shortcuts, administrator prompting, protected startup backups or saved logs. Portable and installed sessions share interface preferences/logs for the running Windows identity.
