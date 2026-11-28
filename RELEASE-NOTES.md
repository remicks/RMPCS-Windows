RMPCS v0.13.9 - Simpler names and expandable details

- Show drive labels and letters, such as Games (D:) · SSD, with a clear fallback for unlabelled drives. Letters distinguish duplicate labels; unknown media remains unverified. Labels are display-only and never enter optimization commands. Free-space figures use accurate decimal GB, with GiB available in technical details.
- Simplify navigation and everyday controls: Clean up & repair, Troubleshooting, Drive care, Refresh website address cache, Check drive, Optimize hard drive and Optimize SSD. Cleanup previews and confirmations use matching names. SSDs still receive only ReTRIM; HDDs still require a recent successful check. Safety warnings and blocked-operation reasons remain visible.
- Add Settings > Names & information with independent options to show technical names and automatically expand related technical details. Both default off, save with general preferences and support undo. Details can also be opened manually for cleanup, schedules, startup apps, monitoring, troubleshooting and drives. Full Windows reports/logs remain available without rewriting their output.
- Update Help for the new names and settings, correct the monitoring description to include optional temperature sources, and use versioned setup EXE filenames across downloads, checksums and Discord announcements.

- Fix themed dropdowns that showed raw technical records instead of display labels, and keep the chosen manual cleanup age when only display settings change.

These changes affect display and information only. Drive identity/media checks, permissions, commands, repair order, selections and scheduling safeguards remain in place. Drive labels may be duplicated or renamed and are never trusted as proof of media type or eligibility.

Validation includes label sanitation/fallbacks, duplicate-label identification, unchanged command targets, simple/technical mode switching without changing selections or guard states, preference migration/undo, read-only label discovery, native UI checks and the existing core/installer suites. No real drive optimization, cleanup, privileged setup or driver installation is performed. Supervised physical drive/hot-plug, accessibility and complete installation/upgrade testing remain necessary.

Includes RMPCS-Setup-0.13.9-x64.exe, standalone MSI, portable ZIP, source bundle, icons, runtime links and SHA256SUMS.txt. Quit RMPCS before upgrading.

Unsigned development prerelease.
