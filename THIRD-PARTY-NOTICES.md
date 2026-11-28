# Bundled font notices

RMPCS includes unmodified Barlow, Rajdhani, Atkinson Hyperlegible, Lato, Poppins, Hind, PT Sans and Oxygen font files, with Regular and SemiBold or Bold faces. These are application resources; RMPCS does not install them into Windows or download fonts at runtime.

All eight families are distributed under the SIL Open Font License 1.1. Copyright notices and full license terms are preserved in OFL-barlow.txt, OFL-rajdhani.txt, OFL-atkinsonhyperlegible.txt, OFL-lato.txt, OFL-poppins.txt, OFL-hind.txt, OFL-ptsans.txt and OFL-oxygen.txt, included with the installed/portable application and embedded in its resources.

Source: https://github.com/google/fonts/tree/06bfe0d62a98375cb09c942fa18e68bd32cff057/ofl

The source bundle includes the original font files and licenses under Assets/Fonts. SOURCES.json records the pinned upstream paths and integrity hashes. The fonts are not sold separately; their designers do not endorse RMPCS. The Windows system font option uses fonts provided by Windows, which are not redistributed by RMPCS.

# Optional sensor components

No sensor binaries or driver are bundled with RMPCS. Users may explicitly download unmodified official packages through the reviewed Settings setup. Versions and hashes are fixed in SensorComponents.cs; SENSOR-SECURITY.md describes verification and residual risks. No upstream endorsement is implied.

- LibreHardwareMonitorLib 0.9.6: MPL-2.0. Copyright LibreHardwareMonitor/OpenHardwareMonitor contributors. Source and license: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6 . Embedded PawnIO.Modules use LGPL-2.1; module source: https://github.com/namazso/PawnIO.Modules . Upstream full notices and MPL text are supplied under SensorLicenses.
- PawnIO 2.2.0 official setup: https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0 . PawnIO driver copyright namazso; GPL-2.0-or-later with the author's device-IO-control linking exception. Source, COPYING and full exception: https://github.com/namazso/PawnIO . GPL text and the author's license/exception statement are supplied under SensorLicenses. The exception does not extend to arbitrary Pawn modules; RMPCS uses the library's existing reviewed modules.
- Optional managed DLLs taken unchanged from the official LHM .NET 10 archive: DiskInfoToolkit 1.1.2, BlackSharp.Core 1.0.7, RAMSPDToolkit-NDD 1.4.2, HidSharp 2.6.4, Mono.Posix.NETStandard 1.0.0, System.Management 10.0.3 and System.IO.Ports 10.0.3. Their respective upstream package metadata/licenses are available at https://www.nuget.org/packages/DiskInfoToolkit/1.1.2 , https://www.nuget.org/packages/BlackSharp.Core/1.0.7 , https://www.nuget.org/packages/RAMSPDToolkit-NDD/1.4.2 , https://www.nuget.org/packages/HidSharp/2.6.4 , https://www.nuget.org/packages/Mono.Posix.NETStandard/1.0.0 , https://www.nuget.org/packages/System.Management/10.0.3 and https://www.nuget.org/packages/System.IO.Ports/10.0.3 . Full upstream LHM third-party notices are supplied for reference, including notices for GUI dependencies which RMPCS does not load.

These optional packages retain their upstream terms and are not relicensed by RMPCS. RMPCS's security pins intentionally reject modified binaries in the normal elevated app; users can review/build this repository and the linked upstream source independently.
