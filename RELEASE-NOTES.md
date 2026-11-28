RMPCS v0.13.7 - Temperatures, transparent widgets and cleaner navigation

- Add temperature monitoring with up to eight selectable sources across CPU, GPU, Storage, Motherboard, Firmware and Other categories. Choose Windows firmware/storage, existing LibreHardwareMonitor/OpenHardwareMonitor WMI, or explicitly enabled in-app hardware reads. Firmware zones are labeled accurately; missing or stale data stays unavailable. Temperature views support combinable numbers, circles, value circles, bars and graphs.
- Add fully transparent overlay backgrounds and separate metric widgets. Drag and lock widgets independently, remember their monitors/positions/locks, and recover from click-through through Settings. All widgets share one sampler; hiding every widget stops polling. High Contrast retains solid surfaces.
- Organize all main-screen functions into categorized navigation trees, keeping Back navigation and sidebar mode.
- Add an optional reviewed sensor-component setup under Settings > Realtime system overlay > Temperature sources. On Windows 11 x64, an administrator can review risks and open visible official PawnIO 2.2.0 setup after pinned SHA-256, Windows signature trust/revocation, expected publisher and protected-path checks. Pinned LibreHardwareMonitor 0.9.6 libraries are verified separately on download and load. Nothing installs silently or enables low-level reads automatically. Secure Boot, Memory Integrity and the vulnerable-driver blocklist are never weakened.

Driver setup and low-level reads are separate opt-ins, both off by default. Keep the vendor's administrator-only access defaults. Signed kernel code can still have bugs or firmware/anti-cheat conflicts; availability varies by hardware. No fan/voltage/tuning controls are exposed. Basic monitoring remains driver-free. Shared PawnIO and optional library files are retained on RMPCS uninstall; remove PawnIO separately only after checking other programs do not depend on it. See SENSOR-SECURITY.md and third-party notices for hashes, source/licenses and limitations.

Temperature queries are local, serialized, bounded and at most every five seconds. Stale/late readings are discarded; no monitoring values are saved or uploaded. Desktop overlays may be covered by exclusive full-screen applications. The v0.14.0 first-run setup-guide groundwork remains informational.

Validation covers source/category/sentinel parsing, preference bounds and migration, independent widget lifecycle/shared sampling, transparency, categorized navigation, synthetic native UI and existing core/installer suites. A downloaded official PawnIO package passed hash and native Windows publisher/signature verification without being launched. No driver installation, low-level sensor probing, real drive optimization or maintenance is performed by automated checks. Elevated installation/reboot/uninstall, hardware compatibility, physical input, display/DPI transitions, accessibility and extended-session overhead still need supervised testing.

Installer EXE, standalone MSI, portable ZIP, source bundle, icons, runtime links and SHA256SUMS.txt are included. Quit RMPCS before upgrading.

Unsigned development prerelease.
