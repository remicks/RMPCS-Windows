# RMPCS roadmap

## v0.3 - optional prerequisite setup

Requested by Wade while publishing v0.2:

- Detect required extras before starting the main app. At present this is the Microsoft .NET 10 Windows x64 Desktop Runtime.
- Installer: offer an **Install missing prerequisites** option during setup, explain what will be installed, request Windows elevation when required, and continue with RMPCS only after successful prerequisite installation.
- Portable: provide a small bootstrap launcher that can run before the managed GUI, detect missing prerequisites on first launch, and offer the same setup flow. The existing WPF EXE cannot provide this screen when its own runtime is missing.
- Fetch only official Microsoft prerequisite installers over HTTPS. Validate Microsoft's Authenticode signature and the checksum from official release metadata before execution.
- Do not reinstall compatible prerequisites unnecessarily. Report download failures, rejected elevation, installer errors and pending-reboot results accurately; permit retry or cancellation. Never silently enable maintenance or reboot the PC.
- Preserve a manual/offline download path for users who decline automatic setup or have no connection.
- Explain that installing a shared runtime changes the system even when the RMPCS app itself is portable.
- Test missing-runtime, already-installed, offline, cancelled, signature/checksum mismatch and reboot-required scenarios before release.

## Other requested ideas

- Health dashboard with disk space reclaimed, repair findings and upcoming maintenance.
- Optional System Restore point creation with actual outcome reporting; no claim that it backs up deleted temporary/log files.
- Scheduled completion/failure notifications and diagnostic-log archives.
- Degenerates Inc. artwork and LootDrop-inspired visual elements after installed workflow validation.

These items describe planned work, not implemented v0.2 features.
