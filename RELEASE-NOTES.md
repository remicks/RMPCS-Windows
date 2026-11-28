RMPCS v0.13.10 - Reliability and hardening

- Log DISM, SFC and ipconfig output as whole lines. Long lines were split wherever an internal 1 KiB read buffer ended; output is now reassembled, UTF-16 padding is removed, the final partial line is kept and very long lines stay bounded.
- Report "another maintenance session is running" when a different Windows account or session holds the maintenance lock with permissions this session cannot open, instead of failing with a generic error.
- Allow TLS 1.3 for the setup EXE's Microsoft runtime download, keeping TLS 1.2 as the minimum. The pinned SHA-512, Authenticode and Microsoft publisher checks are unchanged.
- Reject optional sensor-component folders where a non-administrator could delete files, in addition to the existing write, permission and ownership checks.
- Speed up cleanup previews of large folders by checking each folder once instead of re-checking every parent folder for each file (about 7.7x on a 20,000-file test). Deletion still re-verifies every parent folder, links, hard links and the final file path before removing anything.

Validation: core checks (including new output-line regression checks), installer version/upgrade/regression, bootstrap, drive, monitoring, sidebar, theme and presentation suites passed. The runtime download host was confirmed reachable with the new TLS setting. No live elevated installation, upgrade, maintenance, drive optimization or sensor setup was performed. Supervised physical drive/hot-plug, accessibility and complete installation/upgrade testing remain necessary.

Includes RMPCS-Setup-0.13.10-x64.exe, standalone MSI, portable ZIP, source bundle, icons, runtime links and SHA256SUMS.txt. Quit RMPCS before upgrading. App features from v0.13.9 are retained.

Unsigned development prerelease.
