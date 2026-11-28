RMPCS v0.13.0 - Guarded drive optimization

Adds an in-app Drives section for optional, on-demand HDD analysis/defragmentation and SSD ReTRIM through Windows' built-in optimizer.

- Analyze hard drives and request defragmentation only after a recent successful analysis. HDD defragmentation also requires at least 15% free space. Review Windows' report before proceeding.
- Verified SSDs offer ReTRIM only, never traditional defragmentation. Unknown or contradictory storage evidence and unsupported controllers/layouts stay blocked rather than guessed.
- Drive identity, filesystem, layout, media type and free space are checked again immediately before starting. Operations require administrator rights, optimization requires confirmed AC power, and the RMPCS maintenance lock prevents overlapping RMPCS sessions.
- Progress output and exit status stay inside the app and are saved in History. Work runs on one stable volume GUID at Windows' default low priority, without a shell, downloaded tools, forced termination or fallback operation after failure.
- The new page supports existing themes, fonts, sidebar navigation, Back and saved start-page preferences. Windows' optimization schedule, automatic maintenance profiles and feature defaults are unchanged.

Supported initially: accessible local NTFS volumes on a single basic ATA/SATA/NVMe data partition with consistent native descriptors. RAID, USB, virtual/dynamic/recovery/hidden layouts, dirty/read-only volumes and unknown media are excluded. Some valid hardware may be unavailable under this conservative policy. Windows/controller reporting is trusted; this is not a drive-health diagnosis or hardware-repair tool.

Validation includes storage policy and malformed-buffer fixtures, command and authorization guards, synthetic UI checks, read-only native discovery and existing core/UI/installer/bootstrap suites. These checks never perform real defragmentation, ReTRIM, live elevated install/upgrade/removal or runtime installation. Supervised lifecycle tests on disposable HDD/SSD storage, competing Windows optimizer work, errors and power transitions remain necessary. Keep backups and keep the PC powered during work. Avoid repeated manual optimization; Windows normally maintains drives. No performance improvement is guaranteed.

Installer EXE, standalone MSI, portable ZIP, source bundle, icons, official runtime links and SHA256SUMS.txt are included. Quit RMPCS before upgrading. Previous schedule, versioning, installer, theme/font and sidebar fixes are retained.

Unsigned development prerelease.
