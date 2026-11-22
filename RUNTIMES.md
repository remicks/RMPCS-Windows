# RMPCS v0.8 runtime setup

## Easiest path

- **Installed edition:** open `RMPCS.Setup.exe`. If the required runtime is missing, leave the clearly labelled prerequisite option enabled and continue. Follow Microsoft's installer/license/UAC prompts, then the RMPCS MSI setup.
- **Portable edition:** extract every file, then open `Launch-RMPCS.exe`. It launches the app directly when a compatible runtime exists; otherwise it presents first-run prerequisite setup. `--setup` opens setup explicitly.
- **Standalone MSI:** install the runtime manually first if missing. Direct MSI use does not provide the bootstrap's runtime download screen.

The launchers run on Windows 11's included .NET Framework. The main app needs **Microsoft .NET 10 Desktop Runtime (Windows x64)**, which includes the base .NET Runtime. Running the app does not require an SDK or ASP.NET Core runtime. Installing a shared runtime changes the system even for portable RMPCS.

## Official Microsoft links

- [Pinned 10.0.15 Windows x64 Desktop Runtime installer](https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.15/windowsdesktop-runtime-10.0.15-win-x64.exe)
- [Microsoft .NET 10 download page - latest Desktop Runtime patches](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Official 10.0.15 release metadata and hashes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.15/release.json)
- [Windows runtime installation guidance](https://learn.microsoft.com/en-us/dotnet/core/install/windows)

The launchers only download/use this exact pinned 10.0.15 installer. Its official SHA-512 is:

```
f838960962172f1a769c49fa600e6f60f56f8d08a28e3cb878890ffc2e3a7a2e1b65697588e23efa767b8b8e8523ec01f55e875120507dde16d9a0869102dab3
```

They also require valid Windows Authenticode trust and a Microsoft Corporation signer before execution. Unknown redirects, unexpected sizes, checksum mismatches, rejected elevation and installer failures stop setup. Files are locked against modification during verification and execution. The exact offline-file option applies identical checks; certificate/revocation validation can require internet access or previously cached trust data.

You can cancel and install a compatible newer .NET 10 Desktop Runtime manually using Microsoft's download page, then reopen the launcher. A compatible installed runtime is reused rather than unnecessarily reinstalling the pinned version. Reported restart requirements are displayed; RMPCS's prerequisite flow does not schedule an automatic restart.

Native Microsoft runtime installation and full elevated install/uninstall have not been exercised in this managed development environment. Treat v0.8 as a development prerelease pending supervised validation on a suitable PC.
