# Runtime and installation requirements

RMPCS v0.1 and v0.2 require **Microsoft .NET 10 Desktop Runtime for Windows x64**. Both the MSI and portable edition need this runtime. The Desktop Runtime includes the base .NET Runtime; a separate base runtime, ASP.NET Core runtime, or SDK is not required to run RMPCS.

## Official Microsoft downloads

- [Download .NET Desktop Runtime 10.0.15 - Windows x64 installer](https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.15/windowsdesktop-runtime-10.0.15-win-x64.exe)
- [Microsoft .NET 10 downloads - choose the latest .NET Desktop Runtime Windows x64 installer](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Microsoft Windows installation instructions](https://learn.microsoft.com/en-us/dotnet/core/install/windows)

Download links were checked against Microsoft's .NET release metadata on November 19, 2026. Runtime installers remain hosted by Microsoft; they are not repackaged or silently downloaded by RMPCS v0.2. The current x64 installer published by Microsoft has SHA-512:

```
f838960962172f1a769c49fa600e6f60f56f8d08a28e3cb878890ffc2e3a7a2e1b65697588e23efa767b8b8e8523ec01f55e875120507dde16d9a0869102dab3
```

Source: [official dotnet/core 10.0.15 release metadata](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.15/release.json).

## Install RMPCS

1. Install the Microsoft Desktop Runtime above if it is missing.
2. Run `RMPCS-Setup-0.2.0-x64.msi`, then open RMPCS from the Start Menu.
3. If requested, use the app's **Run as administrator** button.

The MSI creates the protected Program Files installation and uninstall entry. Startup and scheduled maintenance are off until enabled inside the app.

## Run portable RMPCS

1. Install the same Desktop Runtime if it is missing.
2. Extract **all** files from `RMPCS-v0.2-win-x64-portable.zip` into one folder.
3. Open `RMPCS.exe`, then use **Run as administrator** if required.

The portable edition runs maintenance without installing RMPCS, but still depends on the system runtime. Automatic startup and maintenance scheduling require the MSI-installed copy in v0.2. Do not move only the EXE: its companion DLL and JSON files must stay alongside it.

RMPCS v0.1 is available as the original portable package; it predates the installer. Use v0.2 for the installer and automation features.

## Planned v0.3 improvement

The next version will add an opt-in prerequisite setup stage to installation and a bootstrap launcher for portable first run. This is planned work, not a capability of the current v0.2 downloads. See [ROADMAP.md](ROADMAP.md).
