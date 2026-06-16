<#
.SYNOPSIS
    Build CISCSections.dll and install it as an AutoCAD bundle.

.DESCRIPTION
    1. Locates your AutoCAD installation to resolve API references.
    2. Builds the project with MSBuild (Release | x64).
    3. Copies the DLL + PackageContents.xml into the AutoCAD ApplicationPlugins
       folder — AutoCAD loads every bundle in that folder automatically on startup.
    4. You only need to run this script ONCE after a fresh clone/download.
       If you later rebuild (e.g. after editing section data), re-run the script.

.PREREQUISITES
    - .NET Framework 4.8 Developer Pack
        https://dotnet.microsoft.com/download/dotnet-framework/net48
    - MSBuild (comes with Visual Studio or "Build Tools for Visual Studio")
    - AutoCAD 2019 or later (64-bit)

.NOTES
    Run this script from the CISCSections\ folder:
        cd "<path to CISCSections>"
        .\Build-And-Install.ps1

    To target a different AutoCAD year (e.g. 2022):
        .\Build-And-Install.ps1 -AcadYear 2022
#>

param(
    [string]$AcadYear = ""   # e.g. "2024" — leave blank to auto-detect
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─── 1. Find AutoCAD installation ───────────────────────────────────────────

function Find-AutoCAD {
    param([string]$Year)

    $roots = @(
        "C:\Program Files\Autodesk",
        "C:\Program Files (x86)\Autodesk"
    )

    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }

        $dirs = Get-ChildItem $root -Directory -Filter "AutoCAD*" |
                Sort-Object Name -Descending

        foreach ($dir in $dirs) {
            if ($Year -and $dir.Name -notlike "*$Year*") { continue }
            $acad = Join-Path $dir.FullName "acad.exe"
            if (Test-Path $acad) { return $dir.FullName }
        }
    }

    return $null
}

Write-Host "╔══════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  CISC Metric Sections — Build & Install Script  ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════╝" -ForegroundColor Cyan

$acadDir = Find-AutoCAD -Year $AcadYear

if (-not $acadDir) {
    Write-Error ("AutoCAD installation not found under C:\Program Files\Autodesk\.`n" +
                 "Set the AcadDir property manually in CISCSections.csproj, then re-run.")
}

Write-Host "AutoCAD found : $acadDir" -ForegroundColor Green

# ─── 2. Find MSBuild ────────────────────────────────────────────────────────

function Find-MSBuild {
    # VS 2022 / 2019
    $vsPaths = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2019\*\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\*\MSBuild\Current\Bin\MSBuild.exe"
    )
    foreach ($p in $vsPaths) {
        $found = Resolve-Path $p -ErrorAction SilentlyContinue
        if ($found) { return $found[0].Path }
    }

    # .NET SDK dotnet build fallback
    $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue)?.Source
    if ($dotnet) { return $dotnet }

    return $null
}

$msbuild = Find-MSBuild
if (-not $msbuild) {
    Write-Error ("MSBuild not found. Install 'Build Tools for Visual Studio 2019/2022'`n" +
                 "or the .NET SDK, then re-run.")
}

Write-Host "MSBuild      : $msbuild" -ForegroundColor Green

# ─── 3. Build ───────────────────────────────────────────────────────────────

$proj    = Join-Path $PSScriptRoot "CISCSections.csproj"
$outDir  = Join-Path $PSScriptRoot "bin\Release\net10.0-windows"

Write-Host "`nBuilding project..." -ForegroundColor Yellow

if ($msbuild -like "*dotnet*") {
    & $msbuild build $proj -c Release /p:AcadDir="$acadDir" 2>&1
} else {
    & $msbuild $proj /p:Configuration=Release /p:Platform=x64 `
               /p:AcadDir="$acadDir" /nologo /verbosity:minimal 2>&1
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed (exit code $LASTEXITCODE). Fix the errors above and retry."
}

Write-Host "Build succeeded." -ForegroundColor Green

# ─── 4. Install bundle ──────────────────────────────────────────────────────

$bundleRoot    = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\CISCSections.bundle"
$bundleContent = Join-Path $bundleRoot "Contents"

Write-Host "`nInstalling bundle to:`n  $bundleRoot" -ForegroundColor Yellow

New-Item -ItemType Directory -Force -Path $bundleRoot    | Out-Null
New-Item -ItemType Directory -Force -Path $bundleContent | Out-Null

# Package manifest
Copy-Item (Join-Path $PSScriptRoot "PackageContents.xml") -Destination $bundleRoot -Force

# Plugin DLL
$dll = Join-Path $outDir "CISCSections.dll"
if (-not (Test-Path $dll)) {
    # Fallback: look in any Release subfolder
    $dll = Get-ChildItem $PSScriptRoot -Recurse -Filter "CISCSections.dll" |
           Where-Object { $_.FullName -like "*Release*" } |
           Select-Object -First 1 -ExpandProperty FullName
}

if (-not $dll -or -not (Test-Path $dll)) {
    Write-Error "Cannot find CISCSections.dll after build. Check the build output above."
}

Copy-Item $dll -Destination $bundleContent -Force

# Optional: copy .pdb for debugging
$pdb = [System.IO.Path]::ChangeExtension($dll, ".pdb")
if (Test-Path $pdb) { Copy-Item $pdb -Destination $bundleContent -Force }

Write-Host @"

╔════════════════════════════════════════════════════════════╗
║  Installation complete!                                    ║
║                                                            ║
║  Installed to:                                             ║
║  %APPDATA%\Autodesk\ApplicationPlugins\CISCSections.bundle ║
║                                                            ║
║  NEXT STEP: Restart AutoCAD, then type:  CISCINSERT        ║
║                                                            ║
║  The plugin loads automatically every time AutoCAD starts. ║
║  You do NOT need to run this script again unless you       ║
║  update the source code.                                   ║
╚════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Green
