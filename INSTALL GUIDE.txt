================================================================
  CISC METRIC SECTIONS — AutoCAD Plugin Installation Guide
================================================================

WHAT THIS PLUGIN DOES
---------------------
Inserts CISC metric steel sections (W, C, L, HSS Rectangular,
HSS Circular) into AutoCAD as 2D drawings with three view options:
  - Cross Section (end-on profile with fillets)
  - Side / Elevation View
  - Top / Plan View
Hidden lines for flanges and inner walls are optional.
Command to use: CISCINSERT


================================================================
  PREREQUISITES  (install these before anything else)
================================================================

1. AutoCAD 2025 or later (64-bit)
   - This plugin requires AutoCAD that runs on .NET 10

2. .NET 10 SDK
   - Download from: https://dotnet.microsoft.com/download
   - Choose: .NET 10.0 — Windows — SDK installer (x64)
   - Run the installer and restart your PC

3. Visual Studio 2022 (Community edition is free)
   - Download from: https://visualstudio.microsoft.com/vs/community/
   - During installation, check: ".NET desktop development" workload
   - Click Install


================================================================
  STEP 1 — GET THE PLUGIN FILES
================================================================

Get the CISCSections folder from the person who shared it with you.
It should contain these files:

  CISCSections\
  ├── CISCSections.csproj
  ├── CISCPlugin.cs
  ├── SectionData.cs
  ├── SectionDialog.cs
  ├── SectionDrawer.cs
  ├── PackageContents.xml
  └── LoadCISC.lsp

Save this folder somewhere permanent on your PC, for example:
  C:\CISCSections\

IMPORTANT: Do not move this folder after installation.


================================================================
  STEP 2 — UPDATE THE AUTOCAD PATH IN THE PROJECT FILE
================================================================

1. Open the file: CISCSections.csproj  (right-click → Open with Notepad)

2. Find this line near the top:
     <AcadDir ...>C:\Program Files\Autodesk\AutoCAD 2027</AcadDir>

3. Change it to match YOUR AutoCAD version, for example:
     <AcadDir ...>C:\Program Files\Autodesk\AutoCAD 2025</AcadDir>
     <AcadDir ...>C:\Program Files\Autodesk\AutoCAD 2026</AcadDir>

   To find your AutoCAD folder:
   - Open File Explorer
   - Go to: C:\Program Files\Autodesk\
   - Look for a folder starting with "AutoCAD"

4. Save and close Notepad.


================================================================
  STEP 3 — BUILD THE PLUGIN
================================================================

1. Open Visual Studio 2022

2. Click: File → Open → Project/Solution

3. Browse to your CISCSections folder and open:
     CISCSections.csproj

4. In the top menu click: Build → Rebuild Solution

5. Wait for it to finish. At the bottom it should say:
     "Rebuild All succeeded"

   If you see errors, check Step 2 (AutoCAD path) is correct.

6. The compiled file is now at:
     CISCSections\bin\Release\net10.0-windows\CISCSections.dll


================================================================
  STEP 4 — COPY FILES TO AUTOCAD PLUGINS FOLDER
================================================================

1. Press  Win + R  on your keyboard
   Type:  %APPDATA%\Autodesk\ApplicationPlugins
   Press Enter — File Explorer opens

2. Inside that folder, create a new folder called:
     CISCSections.bundle
   (the .bundle part is required)

3. Inside CISCSections.bundle, create another folder called:
     Contents

4. Copy PackageContents.xml  into  CISCSections.bundle\
   (not into Contents, into the bundle root)

5. Copy CISCSections.dll  into  CISCSections.bundle\Contents\

   Final structure should look like:
   ApplicationPlugins\
   └── CISCSections.bundle\
       ├── PackageContents.xml
       └── Contents\
           └── CISCSections.dll


================================================================
  STEP 5 — SET UP AUTOCAD TO LOAD AUTOMATICALLY
================================================================

1. Open AutoCAD

2. In the command line, type:  APPLOAD  and press Enter

3. In the dialog that opens, look at the bottom for "Startup Suite"
   Click the button:  Contents...

4. Click:  Add

5. In the file browser, change the file type dropdown to:
     All Files (*.*)

6. Browse to your CISCSections folder and select:
     LoadCISC.lsp

7. Click Open, then Close, then Close again

8. A security dialog may appear — click:  Always Load

9. Close AutoCAD completely


================================================================
  STEP 6 — TEST THE INSTALLATION
================================================================

1. Open AutoCAD

2. Look at the command line at startup — you should see:
     "CISC Metric Sections v1.0 loaded. Type CISCINSERT..."

3. Type  CISCINSERT  and press Enter

4. The section insert dialog should appear


================================================================
  TROUBLESHOOTING
================================================================

Problem: "Unknown command: CISCINSERT"
Fix: The plugin did not load automatically. Type NETLOAD in
     AutoCAD, change filter to All Files, select CISCSections.dll,
     click Open. Then type CISCINSERT again.
     Also re-check that LoadCISC.lsp is in the Startup Suite (Step 5).

Problem: Build fails with "file not found" errors
Fix: The AutoCAD path in the .csproj file is wrong. Re-do Step 2
     and make sure the folder name matches exactly.

Problem: "File cannot be loaded — running scripts is disabled"
Fix: Open PowerShell and run:
       Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
     Then try again.

Problem: Cannot overwrite CISCSections.dll (file in use)
Fix: AutoCAD is open and has the file locked. Close AutoCAD
     completely, copy the file, then reopen AutoCAD.


================================================================
  UPDATING THE PLUGIN
================================================================

If you receive an updated version of the plugin files:

1. Close AutoCAD
2. Rebuild in Visual Studio (Step 3)
3. Copy the new CISCSections.dll to:
     %APPDATA%\Autodesk\ApplicationPlugins\CISCSections.bundle\Contents\
   Overwrite the old file.
4. Reopen AutoCAD — it loads the new version automatically.


================================================================
  LAYERS CREATED IN YOUR DRAWING
================================================================

The plugin automatically creates two layers:
  CISC-VISIBLE  — White, continuous lines (solid outlines)
  CISC-HIDDEN   — Green, dashed lines (hidden features)

These layers follow standard AutoCAD layer conventions and
can be controlled (on/off/freeze) like any other layer.

================================================================
