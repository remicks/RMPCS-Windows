# Installer recheck: v0.12.1

## Confirmed release defect

The actual published `RMPCS-Setup-0.12.0-x64.msi` was downloaded without executing it. Its SHA-256 matched GitHub's release asset digest:

`2e58fa0ab0fb728b8c0de233ee84668d56ea2e17c95c039311b68023864524f3`

Read-only database inspection found `ProductVersion=0.9.2`, an OLDPRODUCTS upper boundary of `0.9.2`, a NEWERFOUND lower boundary of `0.9.2`, and a literal `RMPCS 0.9.2 Setup` welcome title. The prior verifier also expected that hardcoded version, allowing the defect to pass validation.

v0.12.1 derives MSI version/boundaries from the published RMPCS.dll metadata. Verification separately takes the release version from the build, validates the app's assembly/file/informational versions, and compares MSI and launcher metadata. Welcome titles use `[ProductVersion]`; launcher assembly metadata and visible version labels are generated at build time. The original UpgradeCode is preserved. Older mis-versioned packages remain eligible for a normal major upgrade; uninstall-first is not the fix.

## Review and automated checks

| Area | Recheck and behavior |
| --- | --- |
| Release identity | Three canonical MSI version fields and documented limits; app/release mismatch rejected. MSI, setup EXE and portable launcher versions must agree. |
| Upgrades | Actual Upgrade table boundaries checked, including legacy 0.9.2, 0.10.1, 0.11.0, 0.12.0, equal and future versions. Classification uses numeric version comparisons and MSI boundary flags, not string ordering. |
| Downgrades/same version | Related equal/newer products are detected before launch conditions. The exact installed ProductCode remains eligible for repair/removal; a different same-version ProductCode is blocked. |
| MSI version display | Windows Installer's own formatter must resolve the welcome title to the expected version. Setup/portable version labels and file metadata are also checked. |
| Fresh installation | Restricted native MSI condition tests select Welcome and the success-only Finish path, not maintenance/removal. |
| Repair | The full MSI offers Repair/Remove for its already-installed product before costing. Repair sets `REINSTALL=ALL REINSTALLMODE=omus`. Setup EXE queries its exact MSI ProductCode and passes repair options only for INSTALLSTATE_DEFAULT; it never decides repair merely because an EXE exists. |
| Repair limits | Standard `omus` repair preserves newer versioned files and restores missing/older files, registry resources and shortcuts. It does not promise to replace every damaged equal-version file. Use the same official MSI/setup EXE when source is needed. |
| Removal | Explicit REMOVE=ALL reaches a confirmation with Cancel as the default. Repair never defaults to removal. |
| Task cleanup | Authored only for REMOVE=ALL outside UPGRADINGPRODUCTCODE and before file removal. Source review checks the fixed protected install root, RMPCS task namespace/executable, active task instances and maintenance mutex. Logs and disabled-startup backups are not removed. |
| Upgrade sequencing | RemoveExistingProducts remains after InstallInitialize and before ProcessComponents, inside the MSI rollback transaction. No DisableRollback, ScheduleReboot or ForceReboot action is authored. |
| Prerequisites | MSI and launchers require .NET 10.0 Desktop/Core/host components. Missing or partial fresh-install prerequisites fail closed; removal can proceed without the runtime. Registry views and version bounds are checked. |
| Runtime source | The pinned 10.0.15 x64 Desktop Runtime URL/hash matched current official Microsoft release metadata. The downloaded installer matched SHA-512 and had a valid Microsoft Authenticode signature. It was not executed. |
| Desktop/Start Menu/admin choices | All eight explicit 0/1 combinations are checked through native MSI condition evaluation and bootstrap argument tests. Existing protected install preferences are searched/restored before defaults. Full-UI unchecked values normalize to 0 before costing. Windows UAC remains in control. |
| Post-install launch | Only the checked successful-install Finish action can launch. Repair/removal/cancel/error and unattended MSI execution do not. Pending MSI replacement/restart flags suppress launch; setup EXE disables automatic launch after a restart-required result, including a prerequisite result. Portable runtime setup defers opening the app when restart is required. |
| Progress/cancellation | Existing modeless MSI progress/event subscriptions and distinct success/remove/cancel/error outcomes are checked. Bootstrap fixture tests keep the UI visible during work and block forced close; failed installer codes cannot show completion. Cancellation before launch still prevents execution. |
| Payloads | One embedded cabinet is extracted and every file's size and SHA-256 compared to the published app/cleanup source. Setup embeds and verifies the MSI; portable launcher manifests cover all companion files, including licenses. Source, portable, MSI and setup distributions are rebuilt. |
| Native dependency loading | Bootstrap and package-tool native imports are restricted to Windows System32. Runtime hash and signature rejection checks remain enabled. |
| Regression detection | Isolated package copies with stale ProductVersion, either stale upgrade boundary, stale title, missing downgrade guard, partial-runtime bypass, unsafe upgrade sequencing and repair-to-removal routing must all be rejected. |

## What these checks do not prove

Native condition/title tests use `MsiOpenPackageEx` with `MSIOPENPACKAGEFLAGS_IGNOREMACHINESTATE`. Microsoft documents this as a restricted handle incapable of changing machine state. Properties are fixture states; no installation sequence, custom action, registry installation, app repair, removal, runtime installer or maintenance task is executed. Numeric boundary tests inspect the authored Upgrade table; they do not register simulated old products on the machine.

Actual elevated clean installation, upgrade from a real older registered product, repair with source resolution, Windows Installed apps removal, UAC rejection, files in use, task races, reboot delivery and cancellation/rollback require supervised testing on a disposable Windows VM. MSI rollback sequencing is checked, but live restoration has not been exercised. Rollback does not guarantee reversal of an earlier prerequisite installation or uninstall task cleanup, which changes resources outside the MSI tables. Task cleanup is deliberately skipped on major upgrade; avoid an uninstall-first workaround if automatic tasks matter.

## References

- [Windows Installer ProductVersion](https://learn.microsoft.com/en-us/windows/win32/msi/productversion)
- [Upgrade table and version boundaries](https://learn.microsoft.com/en-us/windows/win32/msi/upgrade-table)
- [RemoveExistingProducts sequencing](https://learn.microsoft.com/en-us/windows/win32/msi/removeexistingproducts-action)
- [Restricted MsiOpenPackageEx session](https://learn.microsoft.com/en-us/windows/win32/api/msi/nf-msi-msiopenpackageexw)
- [Standard REINSTALLMODE behavior](https://learn.microsoft.com/en-us/windows/win32/msi/reinstallmode)
- [Microsoft's EndDialog/DoAction launch example](https://learn.microsoft.com/en-us/windows/win32/msi/adding-a-control-event-at-the-end-of-the-installation-to-run-launch)
