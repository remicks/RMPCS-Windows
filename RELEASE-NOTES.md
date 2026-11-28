RMPCS v0.13.6 - Independent overlay metric views

Adds independently selectable, combinable display styles to each overlay metric.

- Choose standalone numbers, a plain circle, a circle with a numerical value inside, a bar and a history graph. Any combination can be enabled together, including numbers plus a value circle. CPU, RAM, Network, drive space and uptime keep separate view choices.
- Display toggles are independent of metric on/off switches. Turning off every view pauses collection for that metric without deselecting it; all metrics/views off pauses the overlay timer. Existing v0.13.5 numeric and CPU/RAM graph choices migrate automatically.
- Gauges and graphs use clearly labeled scales. CPU/RAM show usage percentages, drive gauges show available-space percentage, network views use an explicit MiB/s reference instead of claiming link utilization, and uptime visuals show the current 24-hour uptime cycle. Long layouts scroll while the Hide control remains above them.

Network download/upload have separate displays sharing their view options. Above-reference network readings are labeled; numeric rates remain actual values. Missing data produces unfilled, unavailable gauges rather than invented zero values. Circle-only, value-circle and numeric displays can all coexist.

The update adds no drivers, dependencies, privileges, animations or network requests. Sampling remains sequential and local, stops when hidden, and retains only bounded in-memory graph data. Monitoring values are not saved or uploaded. The v0.14.0 setup-guide groundwork remains unchanged.

Validation includes all 32 display combinations, per-metric editing and preference migration, numeric-circle independence, missing-data and reference-scale checks, native overlay/guide UI and existing installer/core/UI suites. A synthetic preview is rendered during UI checks. Physical click-through, shortcut conflicts, monitor hot-plug/DPI transitions, accessibility and extended-session overhead still need supervised desktop validation. No maintenance, drive optimization or elevated installation is performed by these checks.

Installer EXE, standalone MSI, portable ZIP, source bundle, icons, runtime links and SHA256SUMS.txt are included. Quit RMPCS before upgrading.

Unsigned development prerelease.
