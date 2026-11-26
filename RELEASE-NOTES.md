RMPCS v0.12.2 - Schedule recovery and version labels

Keeps automatic-maintenance controls usable when a saved schedule was changed outside RMPCS, and corrects stale version labels in logs and the core library.

- A maintenance task edited in Windows Task Scheduler (for example an added trigger or changed arguments) no longer disables both the sign-in and maintenance switches. The known on/off state stays usable, so the schedule can still be turned off or replaced, and Schedules explains that its saved settings could not be shown.
- Saved session logs now record the running app version instead of a fixed 0.9.2 label.
- RMPCS.Core takes its version from release-version.txt. Its file version continues the existing 1.0.x numbering and only increases (now 1.0.12.2).

Validation: core, installer version/upgrade/regression, bootstrap, theme and presentation checks passed. Schedule parsing was checked against the app's own task XML and edited/malformed variants. No live elevated installation, upgrade, removal or maintenance was performed. A known intermittent sidebar disposal self-check failure on multi-monitor desktops predates this release and is unchanged.

Installer EXE, standalone MSI, portable ZIP, source bundle, icons, official runtime links and SHA256SUMS.txt are included. Exit RMPCS using Quit RMPCS before upgrading. App features, themes, fonts and the v0.12.1 installer fixes are retained.

Unsigned development prerelease.
