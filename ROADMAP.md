# RMPCS roadmap

The current build is v0.7.0: Rusty's PC Toolkit, adding before/after run summaries, explicit local diagnostic log archives and opt-in completion/failure notifications. Existing Settings, Help, updates, graphical setup and maintenance safeguards remain available.

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
2. Workload-aware scheduled start deferral with bounded retries and transparent reporting.
3. Optional restore-point integration after supervised availability/failure testing.
4. Broader native Windows integration coverage for summaries, archive destinations and scheduled-to-tray delivery.
5. Drive-health/filesystem checks and supported Windows component cleanup.
6. Broader startup coverage with explicit handling of Windows approvals and startup folders.
7. Degenerates Inc. artwork and LootDrop-inspired visual styling.
