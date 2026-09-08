<#
.SYNOPSIS
    Builds and installs the CISC Metric Sections AutoCAD plugin.

.DESCRIPTION
    This is the advanced/source-code install method. Normal users should use
    the prebuilt AutoCAD 2027 EXE in prebuilt\AutoCAD-2027 instead.

    The script:
      1. Finds AutoCAD.
      2. Builds CISCSections.dll.
      3. Copies PackageContents.xml, CISCSections.dll, and LoadCISC.lsp into
         %APPDATA%\Autodesk\ApplicationPlugins\CISCSections.bundle.
      4. Adds LoadCISC.lsp to APPLOAD Startup Suite.
      5. Registers CISCINSERT for AutoCAD command demand-loading.

.PREREQUISITES
    - Full AutoCAD 2027, 64-bit.
    - Visual Studio 2022 or the .NET 10 SDK.

.NOTES
    Run from this folder:
        powershell -ExecutionPolicy Bypass -File .\Build-And-Install.ps1
#>

param(
    [string]$AcadYear = "2027"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Find-AutoCAD {
    param([string]$Year)

    $roots = @(
        "C:\Program Files\Autodesk",
        "C:\Program Files (x86)\Autodesk"
    )

    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }

        $dirs = Get-ChildItem -LiteralPath $root -Directory -Filter "AutoCAD*" |
            Sort-Object Name -Descending

        foreach ($dir in $dirs) {
            if ($Year -and $dir.Name -notlike "*$Year*") { continue }
            $acad = Join-Path $dir.FullName "acad.exe"
            if (Test-Path -LiteralPath $acad) { return $dir.FullName }
        }
    }

    return $null
}

function Add-LoadCiscToStartupSuites {
    param([string]$LspPath)

    $autoCadKey = "HKCU:\Software\Autodesk\AutoCAD"
    if (-not (Test-Path -LiteralPath $autoCadKey)) { return 0 }

    $updatedProfiles = 0
    foreach ($releaseKey in Get-ChildItem -LiteralPath $autoCadKey) {
        foreach ($productKey in Get-ChildItem -LiteralPath $releaseKey.PSPath) {
            $profilesPath = Join-Path $productKey.PSPath "Profiles"
            if (-not (Test-Path -LiteralPath $profilesPath)) { continue }

            foreach ($profileKey in Get-ChildItem -LiteralPath $profilesPath) {
                $startupPath = Join-Path $profileKey.PSPath "Dialogs\Appload\Startup"
                New-Item -Path $startupPath -Force | Out-Null

                $item = Get-Item -LiteralPath $startupPath
                $values = $item.GetValueNames()
                $alreadyRegistered = $false

                foreach ($valueName in $values) {
                    if ($valueName -notlike "*Startup") { continue }
                    $existing = [string]$item.GetValue($valueName)
                    if ($existing -and ([Environment]::ExpandEnvironmentVariables($existing) -ieq $LspPath -or $existing -ieq $LspPath)) {
                        $alreadyRegistered = $true
                        break
                    }
                }

                if ($alreadyRegistered) { continue }

                $maxSlot = 0
                foreach ($valueName in $values) {
                    if ($valueName -notlike "*Startup") { continue }
                    $prefix = $valueName.Substring(0, $valueName.Length - "Startup".Length)
                    $slot = 0
                    if ([int]::TryParse($prefix, [ref]$slot) -and $slot -gt $maxSlot) {
                        $maxSlot = $slot
                    }
                }

                $nextSlot = $maxSlot + 1
                New-ItemProperty -LiteralPath $startupPath -Name "$($nextSlot)Startup" -Value $LspPath -PropertyType ExpandString -Force | Out-Null
                New-ItemProperty -LiteralPath $startupPath -Name "NumStartup" -Value ([string]$nextSlot) -PropertyType String -Force | Out-Null
                $updatedProfiles++
            }
        }
    }

    return $updatedProfiles
}

function Add-DemandLoadRegistration {
    param([string]$DllPath)

    $autoCadKey = "HKCU:\Software\Autodesk\AutoCAD"
    if (-not (Test-Path -LiteralPath $autoCadKey)) { return 0 }

    $updatedProducts = 0
    foreach ($releaseKey in Get-ChildItem -LiteralPath $autoCadKey) {
        foreach ($productKey in Get-ChildItem -LiteralPath $releaseKey.PSPath) {
            $appPath = Join-Path $productKey.PSPath "Applications\CISCSections"
            $commandsPath = Join-Path $appPath "Commands"

            New-Item -Path $commandsPath -Force | Out-Null
            New-ItemProperty -LiteralPath $appPath -Name "DESCRIPTION" -Value "CISC Metric Sections" -PropertyType String -Force | Out-Null
            New-ItemProperty -LiteralPath $appPath -Name "LOADCTRLS" -Value 12 -PropertyType DWord -Force | Out-Null
            New-ItemProperty -LiteralPath $appPath -Name "LOADER" -Value $DllPath -PropertyType String -Force | Out-Null
            New-ItemProperty -LiteralPath $appPath -Name "MANAGED" -Value 1 -PropertyType DWord -Force | Out-Null
            New-ItemProperty -LiteralPath $commandsPath -Name "CISCINSERT" -Value "CISCINSERT" -PropertyType String -Force | Out-Null
            $updatedProducts++
        }
    }

    return $updatedProducts
}

Write-Host "CISC Metric Sections - Advanced Build and Install" -ForegroundColor Cyan

$acadDir = Find-AutoCAD -Year $AcadYear
if (-not $acadDir) {
    throw "AutoCAD $AcadYear was not found under C:\Program Files\Autodesk. Install full AutoCAD $AcadYear or pass -AcadYear with the installed year."
}

Write-Host "AutoCAD found: $acadDir" -ForegroundColor Green

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) {
    throw "dotnet was not found. Install Visual Studio 2022 with .NET desktop development or install the .NET 10 SDK."
}

$project = Join-Path $PSScriptRoot "CISCSections.csproj"
$outDir = Join-Path $PSScriptRoot "bin\Release\net10.0-windows"

Write-Host "Building plugin..." -ForegroundColor Yellow
& $dotnet build $project -c Release /p:AcadDir="$acadDir"
if ($LASTEXITCODE -ne 0) {
    throw "Build failed. Fix the errors above and retry."
}

$dll = Join-Path $outDir "CISCSections.dll"
if (-not (Test-Path -LiteralPath $dll)) {
    throw "Cannot find built DLL: $dll"
}

$bundleRoot = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\CISCSections.bundle"
$bundleContents = Join-Path $bundleRoot "Contents"

Write-Host "Installing bundle to $bundleRoot" -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $bundleContents | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "PackageContents.xml") -Destination $bundleRoot -Force
Copy-Item -LiteralPath $dll -Destination $bundleContents -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "LoadCISC.lsp") -Destination $bundleContents -Force

$installedDll = Join-Path $bundleContents "CISCSections.dll"
$installedLsp = Join-Path $bundleContents "LoadCISC.lsp"

Write-Host "Registering APPLOAD Startup Suite..." -ForegroundColor Yellow
$startupCount = Add-LoadCiscToStartupSuites -LspPath $installedLsp

Write-Host "Registering CISCINSERT demand-load command..." -ForegroundColor Yellow
$commandCount = Add-DemandLoadRegistration -DllPath $installedDll

Write-Host ""
Write-Host "Installation complete." -ForegroundColor Green
Write-Host "Startup Suite profiles updated: $startupCount"
Write-Host "AutoCAD command registrations updated: $commandCount"
Write-Host ""
Write-Host "Open AutoCAD $AcadYear and type: CISCINSERT"
