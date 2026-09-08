# CISC Metric Sections for AutoCAD 2027

AutoCAD plugin for inserting CISC metric steel sections as 2D AutoCAD geometry.

Command:

```text
CISCINSERT
```

## What Is Included

- W, C, L, HSS rectangular/square, and HSS circular sections.
- CISC SST 12.1 section data embedded in the plugin.
- Corrected HSS inside and outside corner radii from the CISC spreadsheet.
- Cross-section, side/elevation, and top/plan insertion views.
- Ribbon tab named `CISC Sections`.
- One-click installer for layman users.
- Manual prebuilt ZIP for advanced users who do not want the EXE.

No separate shape database is required.

## Easy Install

Use this for normal users.

1. Go to the latest release:
   <https://github.com/sal96422-lab/CISC-Steel-Shapes/releases/latest>
2. Download only:
   `CISC-Steel-Shapes-Installer-AutoCAD-2027.exe`
3. Close AutoCAD.
4. Run the EXE normally as the same Windows user who opens AutoCAD.
5. Open AutoCAD 2027.
6. Type `CISCINSERT`.

Do not right-click `Run as administrator` unless AutoCAD is also opened as Administrator.

## Manual Install Without EXE

Use this if a company computer blocks EXE installers.

1. Download:
   `prebuilt/AutoCAD-2027/CISCSections.bundle.zip`
2. Extract the ZIP.
3. Copy the extracted `CISCSections.bundle` folder to:

```text
%APPDATA%\Autodesk\ApplicationPlugins
```

Final structure:

```text
%APPDATA%\Autodesk\ApplicationPlugins\CISCSections.bundle\PackageContents.xml
%APPDATA%\Autodesk\ApplicationPlugins\CISCSections.bundle\Contents\CISCSections.dll
%APPDATA%\Autodesk\ApplicationPlugins\CISCSections.bundle\Contents\LoadCISC.lsp
```

4. Open AutoCAD 2027.
5. If AutoCAD asks whether to load the plugin or LISP file, choose `Always Load`.
6. Type `CISCINSERT`.

If the command is still unknown, run `NETLOAD` and select:

```text
%APPDATA%\Autodesk\ApplicationPlugins\CISCSections.bundle\Contents\CISCSections.dll
```

## Advanced Build From Source

Use this only if you want to build the plugin yourself.

Requirements:

- Full AutoCAD 2027, 64-bit.
- Visual Studio 2022 with .NET desktop development, or the .NET 10 SDK.

Build and install:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build-And-Install.ps1
```

The script builds the DLL, copies the bundle files, adds `LoadCISC.lsp` to APPLOAD Startup Suite, and registers `CISCINSERT` for AutoCAD demand-loading.

## Layers

Inserted geometry uses existing drawing layers:

- Solid/visible lines: layer `6`
- Hidden lines: layer `5`
- Centre lines: layer `3`

## Compatibility

This prebuilt installer and bundle are for full AutoCAD 2027.

They are not intended for AutoCAD LT. Older AutoCAD versions may require a separate build against that version's AutoCAD API files.
