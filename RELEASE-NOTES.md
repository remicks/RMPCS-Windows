RMPCS v0.12.1 - Installer version and upgrade hotfix

Fixes the v0.12.0 MSI incorrectly identifying itself as 0.9.2 and blocking upgrades. Use this MSI or setup EXE, close RMPCS using Quit RMPCS, and upgrade normally. Uninstalling first is not required for this bug.

- MSI ProductVersion and both upgrade boundaries now follow the published application's version. Independent release checks compare app assembly/file/informational versions, MSI, setup EXE and portable launcher metadata. The MSI welcome title, launcher version labels and EXE metadata are generated rather than hardcoded. UpgradeCode remains unchanged so legacy mis-versioned packages can be upgraded.
- The installed MSI offers explicit Repair/Remove paths before costing. Removal confirmation defaults to Cancel. Setup EXE uses the exact registered MSI ProductCode to choose same-product repair; fresh installs and major upgrades do not receive REINSTALL flags. Standard omus repair retains newer versioned files and restores missing/older files, registered preferences and shortcuts; it is not a guaranteed replacement of every damaged equal-version file.
- Prerequisite discovery now consistently checks .NET 10.0 Desktop, Core and host components. Fresh installs reject missing/partial runtimes; removal does not require the runtime. The pinned runtime was independently checked against current Microsoft metadata, downloaded SHA-512 and Microsoft Authenticode trust without executing it.
- Restart-required results disable automatic app launch. MSI pending-replacement/restart flags also suppress post-install launch, and portable prerequisite setup defers opening the app when restart is required. No restart is forced. Native imports in bootstrap/package tools are restricted to System32.
- Installer regression checks exercise real restricted Windows Installer condition/title APIs, numeric Upgrade-table boundaries and deliberately corrupted package copies. The actual published v0.12.0 MSI was downloaded, its GitHub digest matched, and the 0.9.2 defect reproduced through read-only inspection. Future stale versions/boundaries/titles fail the build.

Full review matrix and limits: Installer/AUDIT.md in the repository/source bundle. Version/payload/shortcut/preference/progress/cancel/outcome and launch guards are checked. No live elevated application installation, upgrade, repair, removal, runtime installation or maintenance was performed. Real VM lifecycle, source-resolution, files-in-use, reboot and rollback validation remains necessary. MSI rollback covers authored resources; an earlier prerequisite installation and uninstall task cleanup are not guaranteed to roll back. Major upgrades preserve RMPCS tasks by skipping uninstall cleanup.

Installer EXE, standalone MSI, portable ZIP, source bundle, icons, official runtime links and SHA256SUMS.txt are included. App features, themes and fonts are retained.

Unsigned development prerelease.
