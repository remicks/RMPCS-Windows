# RMPCS roadmap

The current build is v0.5.0, with a Windows-style light interface, graphical setup pages, optional shortcuts, administrator launch preference and a completion-page launch checkbox. Tray residency and startup to tray remain included. The v0.3.1 Administrators/SYSTEM-only startup-backup protection is retained. v0.3.0 is superseded.

## v0.3 - implemented, supervised validation pending

- Optional verified Microsoft runtime setup before the MSI, with an embedded MSI installer launcher.
- Portable first-run launcher that can show setup without the main app's .NET 10 runtime.
- Compatible-runtime detection, exact offline-file support, cancellation, integrity/trust checks and exit-code handling.
- Read-only health snapshot with resource/storage advice and available recent-failure information.
- Per-category cleanup previews and opt-in thumbnail cache, error archive, application dump and Recycle Bin cleanup.
- Reversible common Run-entry management with protected original-value backups and conflict protection.

Build and isolated/offline checks do not replace live install, task, registry-mutation and cleanup testing. See README and release validation notes.

## Next priorities

1. Supervised clean-machine, missing-runtime, upgrade and uninstall testing; broaden automated Windows integration coverage.
2. Before/after run summary with clearly measured versus estimated reclamation.
3. Optional restore-point integration and diagnostic-log archives.
4. Scheduled-run completion/failure notifications and workload-aware start deferral.
5. Drive-health/filesystem checks and supported Windows component cleanup.
6. Broader startup coverage with explicit handling of Windows approvals and startup folders.
7. Degenerates Inc. artwork and LootDrop-inspired visual styling.
