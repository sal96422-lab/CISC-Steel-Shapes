CISC Steel Shapes - AutoCAD 2027 Easy Install
=============================================

This folder contains a one-click installer and a manual prebuilt bundle.

Use this method when you want to install the plugin without Visual Studio,
without the .NET SDK, and without building the source code.


Files
-----

  CISC-Steel-Shapes-Installer-AutoCAD-2027.exe
  CISCSections.bundle.zip


Requirements
------------

  - AutoCAD 2027

You do not need:

  - Visual Studio
  - .NET SDK
  - GitHub Desktop
  - Source code
  - Any separate shape database


Install
-------

1. Close AutoCAD.

2. Download:

     CISC-Steel-Shapes-Installer-AutoCAD-2027.exe

3. Double-click the EXE installer.

4. Wait until it says the installation finished.

5. Open AutoCAD.

6. If AutoCAD asks whether to load LoadCISC.lsp or the plugin DLL, choose:

     Always Load

7. Type this command:

     CISCINSERT


Manual Install From Zip
-----------------------

Use this only if the EXE is blocked by your company computer.

1. Close AutoCAD.

2. Download:

     CISCSections.bundle.zip

3. Right-click the zip file and choose:

     Extract All

4. You should now have this folder:

     CISCSections.bundle

5. Press Windows key + R.

6. Paste this path and press Enter:

     %APPDATA%\Autodesk\ApplicationPlugins

7. Copy the whole CISCSections.bundle folder into ApplicationPlugins.

   Final result:

     %APPDATA%\Autodesk\ApplicationPlugins\CISCSections.bundle
     %APPDATA%\Autodesk\ApplicationPlugins\CISCSections.bundle\PackageContents.xml
     %APPDATA%\Autodesk\ApplicationPlugins\CISCSections.bundle\Contents\CISCSections.dll

8. Open AutoCAD.

9. If AutoCAD asks whether to load the plugin, choose:

     Always Load

10. Type this command:

     CISCINSERT


Layer Mapping
-------------

Inserted geometry uses the drawing's existing layers:

  - Solid / visible lines: layer 6
  - Hidden lines: layer 5
  - Centre lines: layer 3


Troubleshooting
---------------

If AutoCAD says "Unknown command: CISCINSERT":

1. Type:

     NETLOAD

2. Select this file:

     %APPDATA%\Autodesk\ApplicationPlugins\CISCSections.bundle\Contents\CISCSections.dll

3. Type:

     CISCINSERT


Uninstall
---------

Close AutoCAD and delete this folder:

  %APPDATA%\Autodesk\ApplicationPlugins\CISCSections.bundle
