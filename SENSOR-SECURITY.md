# Optional temperature components — v0.13.7

## Scope and consent

No driver or hardware library ships in RMPCS or runs automatically. Basic counters and Windows/existing local WMI temperature sources remain separate. The Settings action reviews risks, requires an administrator session on Windows 11 x64, downloads fixed official packages, validates them, and opens visible vendor setup. A second saved option enables in-app CPU/GPU/motherboard reads. Selected temperature sources must also be enabled for display. Discovery does not install anything. Keep the vendor's administrator-only access defaults.

| Component | Reviewed version | Official package SHA-256 |
|---|---|---|
| PawnIO setup | 2.2.0 | `1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032` |
| LibreHardwareMonitor .NET 10 ZIP | 0.9.6 | `29739C4959B01B348FDDAD87664066634BCFD4F46E9BF41E4E916C318BCFDB99` |

Official URLs are fixed in SensorComponents.cs. HTTPS redirect hosts are restricted to github.com and its release asset hosts; cookies/default credentials are disabled. Downloads have time and size limits. Hashes fail closed if upstream replaces an asset. PawnIO setup additionally requires Windows WinVerifyTrust with chain revocation checking and an exact verified signer leaf certificate: namazso.eu, thumbprint `F380DCC9F706E2756A5047B832FFE719E1BC35F5`. Unavailable trust/revocation data can block setup. The file remains open without write/delete sharing through verification and execution.

## Protected loading

Only eight named, separately hash-pinned managed DLLs are extracted from the verified ZIP. The GUI, embedded setup, native payloads outside managed resources, and unrelated tools are not extracted or run. The official library GUI contains an older embedded installer; RMPCS never launches it. Each DLL is rechecked on load and read directly from its held verified stream into a private AssemblyLoadContext. Unapproved managed dependencies are rejected. Native library resolution uses Windows System32; the app/current directory is excluded, including vendor direct LoadLibrary calls.

`%ProgramFiles%\RMPCS-SensorComponents` uses administrator-owned protected ACLs: Administrators/SYSTEM write; authenticated users read/execute. ACLs are supplied at creation. Existing locations with untrusted owners, effective non-administrator write permissions or reparse points are rejected rather than repaired or overwritten. Setup uses a protected unique staging folder and cleans only its known file and empty directory. These controls do not attempt to defend against another administrator or a compromised Windows installation.

The reviewed reader enables only CPU/GPU/motherboard groups. Storage uses Windows counters. RMPCS calls enumeration, Update and temperature Value only; it does not call fan, voltage, clock or tuning controls. Hardware probing still performs privileged hardware access inside the reviewed third-party library and kernel modules, so “read-only” describes RMPCS's requested operations rather than a claim that every internal bus operation is side-effect-free. No Secure Boot, Memory Integrity, driver-blocklist or driver-device permission changes are requested. Missing/blocked components fail unavailable without downloading a fallback.

## Lifecycle and limitations

There is one serialized temperature worker gate shared by discovery and overlay polling. A ten-second UI timeout does not release the gate while underlying provider work remains blocked, preventing accumulation of workers. Polling runs at most every five seconds; cached results expire fifteen seconds after the query starts, not after a slow multi-provider batch finishes. Reconfiguration/hiding invalidates late results. Closing low-level handles is serialized after active reads. Provider-reported data can still be inaccurate or internally stale. Firmware zones are never labeled CPU cores. Zero sentinels, nonfinite and implausible values stay unavailable, and the gauge reference is not a thermal safety threshold. No sensor readings are persisted or uploaded.

Signed drivers can contain security defects or cause firmware/anti-cheat compatibility failures. Automated validation verifies downloaded bytes and their signature without executing setup, opening hardware sensors or installing a driver. Elevated installation, reboot, uninstall, representative CPU/GPU/board hardware, anti-cheat compatibility and long-session behavior still require supervised disposable-machine validation. Windows may block otherwise authentic components. Do not bypass that protection; use available Windows or existing WMI providers instead.

PawnIO is shared software. RMPCS preserves an already installed 2.2.0+ version instead of downgrading it, and does not uninstall it or the protected library directory when RMPCS is removed. To remove PawnIO, stop in-app reads and use Windows Installed apps after checking other software does not depend on it. Component version updates require a reviewed RMPCS release; there is no automatic latest-driver installation.

Official sources and licenses are listed in THIRD-PARTY-NOTICES.md.
