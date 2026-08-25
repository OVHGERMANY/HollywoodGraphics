# HollywoodGraphics

HollywoodGraphics is a configurable post-processing and terrain-detail plugin for the official SPT 4.1.2 client build. It is not built against a custom or ported EFT client. The plugin disables itself when Amand's Graphics is detected to avoid competing graphics patches.

## Install

Download the current release archive and extract it into the root of an official SPT 4.1.2 installation. A complete installation contains the DLL and all ten bloom textures under:

```text
BepInEx/plugins/HollywoodGraphics/
```

## Build

Point the project at an official SPT 4.1.2 installation with either `SptRoot` or the `SPT_ROOT` environment variable:

```powershell
$env:SPT_ROOT = 'E:\Games\SPT'
dotnet build .\HollywoodGraphics\HollywoodGraphics.csproj --configuration Release --property:TreatWarningsAsErrors=true
```

A normal build never copies files into SPT. Deployment is deliberately explicit and includes the tracked bloom textures:

```powershell
dotnet build .\HollywoodGraphics\HollywoodGraphics.csproj --configuration Release --target:Deploy --property:SptRoot='E:\Games\SPT' --property:DeployRoot='E:\Games\SPT'
```

## Package

The release assets are versioned under `assets/bloom`. The packaging script verifies their SHA-256 hashes, builds the DLL, and creates a deterministic archive with the same layout as the published v2.0.0 package:

```powershell
.\scripts\New-ReleasePackage.ps1 -SptRoot 'E:\Games\SPT'
```

Generated archives and checksum files are written under `artifacts/release/`, which Git ignores.

## Bug reports

Use the GitHub bug form and attach `BepInEx/LogOutput.log`. Reports must identify the exact SPT version and whether the issue reproduces without a custom client port.
