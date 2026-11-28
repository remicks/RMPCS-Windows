RMPCS v0.13.8 - Optional sensor setup and clearer Help

- Add an unchecked optional temperature sensor-package choice to the setup EXE and standalone MSI. Review/setup opens only after successful RMPCS installation; pending restarts defer it to Settings. Skipping, cancelling or failing the optional stage leaves RMPCS installed. Silent/basic MSI execution never installs sensor components.
- Keep the same setup available inside the app, including portable copies: Settings > Realtime system overlay > Temperature sources > Review / install optional sensor components. Standard sessions request administrator approval for this setup. The shared review window requires an explicit download action, shows progress, and uses the existing pinned hashes, trusted publisher/signature checks and protected storage. No monitoring is enabled automatically and no Windows security protection is weakened.
- Expand offline Help with quick links, numbered setup instructions and troubleshooting for sensor installation/source selection, UAC, signatures/downloads, restarts, busy or unavailable providers, anti-cheat restrictions, transparent widgets, click-through recovery, monitors/hotkeys, categorized navigation and drive optimization.
- Add an embedded installed-helper integrity manifest to the EXE setup, exclusive component-setup coordination across processes, responsive background signature verification and explicit vendor outcome reporting. Include full optional sensor license texts in all flat application payloads.

To use installed sensors: restart when requested, run RMPCS as administrator, enable Display selected temperature sensors and Allow optional in-app low-level hardware reads, choose In-app hardware, discover/select sensors, choose Temperature sensors display styles, and Apply overlay settings. Basic monitoring remains driver-free. Signed kernel code can still have defects or firmware/anti-cheat conflicts; do not bypass Windows security blocks. Shared PawnIO and library files remain on RMPCS uninstall.

Validation covers EXE bootstrap defaults, restart/failure gating, embedded helper manifests, native MSI completion conditions, shared idle review UI, Help navigation/search and the existing core/UI/installer regression suites. No driver setup, elevated installation, real hardware sensor reads, maintenance or drive optimization is performed by these checks. Supervised clean-machine installation/upgrade, UAC, vendor cancellation/restart, hardware compatibility and uninstall tests remain necessary.

Includes installer EXE, MSI, portable ZIP, source bundle, icons, runtime links and SHA256SUMS.txt. Quit RMPCS before upgrading.

Unsigned development prerelease.
