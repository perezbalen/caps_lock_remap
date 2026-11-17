# Caps Lock Remap

Windows 11 tray utility planned to turn Caps Lock into configurable hold modifiers and an optional tap key. See [the design](docs/design.md) for requirements and implementation milestones.

## Current state

Development scaffold only: the generated Windows Forms window builds, but keyboard remapping, tray behavior, settings, and startup integration are not implemented. Running this project does not replace AutoHotkey yet.

## Prerequisites

Windows x64 and .NET SDK 10.0.404 (selected by `global.json`; later patches in the same feature band are allowed). Visual Studio and separate C++ build tools are not required for this project. Internet access to NuGet is needed for initial self-contained publishing downloads.

## Commands

Run from the repository root in PowerShell:

```powershell
dotnet build CapsLockRemap.slnx -c Release
dotnet run --project src/CapsLockRemap
dotnet publish src/CapsLockRemap/CapsLockRemap.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/win-x64
```

The portable output is `artifacts/win-x64/CapsLockRemap.exe`. This build bundles the runtime; native libraries may extract at launch. Build output is ignored by Git. Running requires an interactive desktop; publishing does not launch the app or register startup tasks.

## Environment verification (2026-11-16)

- Windows x64; Git and winget available.
- .NET SDK 10.0.404 and Windows Desktop runtime already installed; no additional software installation needed.
- Release solution build passed with zero warnings and errors.
- Self-contained single-file win-x64 publish passed after allowing NuGet network access. Initial restricted-network publishing failed with NU1301; this was an environment restriction.
- GUI behavior has not been manually tested. No keyboard hooks or startup tasks have been installed.

Next: replace the template window with the tray host and prototype the input engine, following `docs/design.md`.
