RMPCS v0.13.5 - Realtime system overlay and setup-guide groundwork

Adds an opt-in desktop system monitor and the reusable guide structure planned for v0.14.0.

- New realtime overlay shows CPU, RAM, network download/upload rates, system-drive available space and Windows uptime. Sampling is sequential and local, stops when hidden, keeps only 60 in-memory graph samples, and never runs maintenance or saves/uploads monitoring data.
- Customize metrics, update interval, fonts, text size, opacity, width, monitor, corner, topmost, position locking and click-through. Settings, a tray action and optional Ctrl+Alt+O provide show/hide and recovery controls. High Contrast takes priority and unavailable counters show a dash instead of false readings.
- First-run guide groundwork includes a six-step catalogue, bounded Back/Next navigation and a themed informational preview in Settings. Automatic onboarding and reviewed preference application are reserved for v0.14.0; this preview changes no settings or permissions and starts no tasks.

The overlay is off by default. Showing it with RMPCS does not enable Windows sign-in startup. Network totals may overlap on VPN/virtual adapters; system-drive free space refreshes at most every 30 seconds. CPU/rates need a baseline and long gaps/counter changes reset it. CPU on systems with more than 64 logical processors is unavailable. No GPU, temperature, FPS or disk-activity sensors, sensor drivers or game injection are included. Exclusive fullscreen/other topmost apps may cover it.

Validation: core preference/rate/placement/guide fixtures, synthetic overlay/guide native-window UI checks, local counter availability checks and the existing drive/theme/sidebar/installer/bootstrap suites. Physical click-through, shortcut conflicts, display hot-plug/DPI transitions, accessibility and extended-session overhead still need supervised testing. No live drive optimization, elevated install/upgrade/removal, runtime installation or maintenance is performed by these checks.

Installer EXE, standalone MSI, portable ZIP, source bundle, icons, official runtime links and SHA256SUMS.txt are included. Quit RMPCS before upgrading. Previous features and safeguards remain available.

Unsigned development prerelease.
