# RMPCS — Wade's PC Toolkit

First desktop edition, 0.1.0, based on WMPCS 0.3b. Windows 11 x64, .NET 10 Desktop Runtime. No third-party packages, account integrations, telemetry, or automatic maintenance.

## Start

Extract the download first. Open `App/RMPCS.exe`. If you see the administrator warning, click **Run as administrator**, or close the app and right-click the EXE → **Run as administrator**. Windows displays its normal UAC prompt. Maintenance stays disabled in a standard session; preview and export remain available.

The app currently uses WPF for a small Windows-only foundation. It can reuse Degenerates Inc. artwork and LootDrop's visual direction in later versions; it does not yet reuse Avalonia UI components.

## Use

- **Run recommended care:** one confirmation starts user and Windows temp cleanup, DNS flush, DISM image repair, then SFC, entirely in the app.
- **Individual tasks:** select checkboxes, preview temp files if desired, and click **Run selected tasks**. Cleanup displays its own deletion confirmation.
- **Stop after current task:** finishes the active operation, then stops the remaining queue. It does not terminate Windows repairs midway. Closing the app is blocked while tasks run.
- **DHCP renewal:** optional, separately confirmed, and runs last. Release interrupts connectivity; renewal is always attempted after release, even if you stop the queue. It can affect VPN/remote connections, and reconnection is not guaranteed.
- **Restart:** separate confirmation schedules a restart after 60 seconds. Save work first; Windows may force apps to close. **Cancel restart** aborts a pending Windows shutdown, including one scheduled outside RMPCS.
- **Export log:** saves the current activity to a text file you choose. Activity is kept in memory, bounded in size, and is not automatically saved. Exported logs can include local paths and network details.

## Cleanup boundaries

Temp cleanup only targets `%LOCALAPPDATA%\Temp` and/or `%WINDIR%\Temp`. Both creation and modification time must be older than seven days. Preview does not delete anything. Cleanup rechecks eligibility and path boundaries before deleting; it skips links/junctions, inaccessible paths, locked files, and changed files, preserves directories, and never clears read-only attributes. Deleted files bypass the Recycle Bin. Close other applications before maintenance; age alone cannot prove a file is unused.

This edition deliberately preserves Prefetch, Windows logs, and search index files rather than reproducing the batch script's indiscriminate deletion. There are no external settings shortcuts or hidden cleanup tasks. More targeted in-app maintenance can be added after review.

## Results and limits

Commands run sequentially using fixed paths to Windows' own executables, without shell-built commands. The UI shows command output and actual exit codes. DISM runs before SFC; if DISM fails, SFC is skipped. Exit 3010 requests a restart. Read the SFC/DISM output for repair findings: a zero exit code alone does not prove that every problem was repaired. DISM may download repair components through Windows Update. No network is used by RMPCS itself.

This is a basic maintenance utility, not a guarantee of higher FPS, improved performance, or resolution of hardware/driver problems. It has no registry tweaks or service disabling. The executable is unsigned and this is an initial development build.

## Build and verify

Install the .NET 10 SDK, then run from the source folder (`Source` in the download):

```powershell
dotnet build RMPCS.App/RMPCS.App.csproj -c Release
dotnet run --project RMPCS.Tests/RMPCS.Tests.csproj -c Release
dotnet RMPCS.App/bin/Release/net10.0-windows/RMPCS.dll --smoke-test
dotnet publish RMPCS.App/RMPCS.App.csproj -c Release --self-contained false -o artifacts/App
```

Tests use an isolated fixture folder only, never real cleanup roots. `--smoke-test` loads and renders the actual window, then closes without startup warnings or maintenance. Real repairs, DNS changes, DHCP resets, and restarts are intentionally not run as build checks. `NuGet.Config` clears package feeds because this version needs only the installed .NET SDK/framework reference packs.

Source layout: `RMPCS.Core` holds the temp cleaner; `RMPCS.App` holds the interface and Windows command execution; `RMPCS.Tests` verifies cleanup boundaries and behavior.

## Initial verification

Release build/publish passed with no compiler warnings or errors. The published EXE loaded and rendered its real window in smoke-test mode and exited with code 0. Nine isolated cleanup checks passed: old-file selection, preview without deletion, locked/changed-file handling, preservation of recent and outside-root files, directory preservation, forged-path rejection, filesystem-root rejection, and missing-folder handling. Junction fixture creation was denied by the execution environment, so the two junction-specific checks were skipped and remain unverified. No real maintenance, repair, network reset, elevation, or restart was performed during verification.

Cleanup validates age and resolved location through the same Windows file handle used for deletion, and skips files with multiple hard links. The full elevated Windows maintenance workflow still needs a supervised test on a suitable PC.
