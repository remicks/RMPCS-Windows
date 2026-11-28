# RMPCS roadmap

Current build: v0.13.5 adds an opt-in local system-monitor overlay and a manual, informational preview of the v0.14.0 setup guide. Guarded v0.13.0 drive optimization and previous fixes remain available.

## v0.14.0 first-run guide

The step catalogue, bounded Back/Next session and themed informational preview ship in v0.13.5. Next: versioned per-Windows-identity completion state; distinguish new installs from existing preferences; editable interface/overlay draft with a review screen and atomic save; Skip/Later and replay from Settings. Save failure must preserve previous preferences and must not mark setup complete. No automatic elevation, sign-in task registration, cleanup, driver downloads or drive optimization. Protected operations keep their existing explicit approvals. No automatic guide popup or completion marker is active in v0.13.5.

Overlay follow-up validation: physical input pass-through, global-hotkey conflicts, monitor disconnect/DPI changes, long-session overhead and accessibility. Advanced GPU/temperature/FPS sensors require a separate supported-source and dependency security review.

Existing diagnostics, verified restore-point requests, preference undo, guarded scheduling, reports, archives and update checks remain available. See README and release notes for scope and validation limits.

## Next priorities

1. Supervised clean-machine, runtime, upgrade and uninstall testing.
   Drive optimization also needs supervised real HDD/SSD, error, concurrent Windows optimizer and power-transition tests on disposable storage. Do not broaden storage support without native topology validation.
2. Native startup/task switch behavior, system-theme and High Contrast transitions, keyboard/assistive-technology use and multi-monitor/DPI validation.
3. Restore-point availability/frequency-limit testing and real workload/scheduling scenarios.
4. Broader native integration coverage for recovery, reports and scheduled notifications.
5. Drive-health/filesystem checks and supported Windows component cleanup.
6. Broader startup coverage with explicit Windows approval/startup-folder handling.
7. Optional Degenerates Inc. artwork that preserves a clean, readable UI.

Automated checks do not replace live Windows validation. No performance or repair-success guarantee is made.
