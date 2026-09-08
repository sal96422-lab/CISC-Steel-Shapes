using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Principal;
using Microsoft.Win32;

static void Step(string message)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("== " + message + " ==");
    Console.ResetColor();
}

static int Fail(string message)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("INSTALLER STOPPED");
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(message);
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("Press Enter to close.");
    Console.ReadLine();
    return 1;
}

static async Task ExtractResourceAsync(Assembly assembly, string resourceSuffix, string destinationPath)
{
    string? resourceName = assembly
        .GetManifestResourceNames()
        .FirstOrDefault(name => name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

    if (resourceName is null)
    {
        throw new InvalidOperationException($"The installer does not contain {resourceSuffix}. Please rebuild the installer.");
    }

    await using Stream? resource = assembly.GetManifestResourceStream(resourceName);
    if (resource is null)
    {
        throw new InvalidOperationException($"The embedded file {resourceSuffix} could not be opened.");
    }

    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
    await using FileStream output = File.Create(destinationPath);
    await resource.CopyToAsync(output);
}

static bool IsRunningAsAdministrator()
{
    using WindowsIdentity identity = WindowsIdentity.GetCurrent();
    WindowsPrincipal principal = new(identity);
    return principal.IsInRole(WindowsBuiltInRole.Administrator);
}

static void InstallBundleFromZip(string zipPath, string destRoot)
{
    string destBundle = Path.Combine(destRoot, "CISCSections.bundle");

    Directory.CreateDirectory(destRoot);

    if (Directory.Exists(destBundle))
    {
        Directory.Delete(destBundle, recursive: true);
    }

    ZipFile.ExtractToDirectory(zipPath, destRoot, overwriteFiles: true);
}

static int AddLoadCiscToStartupSuites(string lspPath)
{
    int updatedProfiles = 0;
    using RegistryKey? autoCadKey = Registry.CurrentUser.OpenSubKey(@"Software\Autodesk\AutoCAD", writable: true);
    if (autoCadKey is null) return 0;

    foreach (string releaseName in autoCadKey.GetSubKeyNames())
    {
        using RegistryKey? releaseKey = autoCadKey.OpenSubKey(releaseName, writable: true);
        if (releaseKey is null) continue;

        foreach (string productName in releaseKey.GetSubKeyNames())
        {
            using RegistryKey? productKey = releaseKey.OpenSubKey(productName, writable: true);
            using RegistryKey? profilesKey = productKey?.OpenSubKey("Profiles", writable: true);
            if (profilesKey is null) continue;

            foreach (string profileName in profilesKey.GetSubKeyNames())
            {
                using RegistryKey? profileKey = profilesKey.OpenSubKey(profileName, writable: true);
                if (profileKey is null) continue;

                using RegistryKey startupKey = profileKey.CreateSubKey(@"Dialogs\Appload\Startup", writable: true);
                string[] valueNames = startupKey.GetValueNames();

                bool alreadyRegistered = valueNames.Any(valueName =>
                {
                    if (!valueName.EndsWith("Startup", StringComparison.OrdinalIgnoreCase)) return false;
                    string? existing = startupKey.GetValue(valueName)?.ToString();
                    if (string.IsNullOrWhiteSpace(existing)) return false;

                    string expandedExisting = Environment.ExpandEnvironmentVariables(existing);
                    return string.Equals(expandedExisting, lspPath, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(existing, lspPath, StringComparison.OrdinalIgnoreCase);
                });

                if (alreadyRegistered) continue;

                int maxSlot = valueNames
                    .Select(valueName =>
                    {
                        if (!valueName.EndsWith("Startup", StringComparison.OrdinalIgnoreCase)) return 0;
                        string prefix = valueName[..^"Startup".Length];
                        return int.TryParse(prefix, out int slot) ? slot : 0;
                    })
                    .DefaultIfEmpty(0)
                    .Max();

                int nextSlot = maxSlot + 1;
                startupKey.SetValue($"{nextSlot}Startup", lspPath, RegistryValueKind.ExpandString);
                startupKey.SetValue("NumStartup", nextSlot.ToString(), RegistryValueKind.String);
                updatedProfiles++;
            }
        }
    }

    return updatedProfiles;
}

try
{
    Console.Title = "CISC Steel Shapes Installer";
    Console.WriteLine("CISC Steel Shapes AutoCAD Plugin Installer");

    Step("Checking AutoCAD is closed");
    if (Process.GetProcessesByName("acad").Length > 0)
    {
        return Fail("AutoCAD is open. Close AutoCAD completely, then run this installer again.");
    }

    Step("Preparing files");
    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    string userDestRoot = Path.Combine(appData, "Autodesk", "ApplicationPlugins");
    string userDestBundle = Path.Combine(userDestRoot, "CISCSections.bundle");
    string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    string allUsersDestRoot = Path.Combine(commonAppData, "Autodesk", "ApplicationPlugins");
    string allUsersDestBundle = Path.Combine(allUsersDestRoot, "CISCSections.bundle");
    string tempRoot = Path.Combine(Path.GetTempPath(), "CISC-Steel-Shapes-Installer");
    string tempZip = Path.Combine(tempRoot, "CISCSections.bundle.zip");

    if (Directory.Exists(tempRoot))
    {
        Directory.Delete(tempRoot, recursive: true);
    }

    Directory.CreateDirectory(tempRoot);

    Assembly assembly = Assembly.GetExecutingAssembly();
    await ExtractResourceAsync(assembly, "CISCSections.bundle.zip", tempZip);

    Step("Installing plugin for current Windows user");
    InstallBundleFromZip(tempZip, userDestRoot);

    bool installedAllUsers = false;
    string? allUsersWarning = null;
    Step("Trying all-users AutoCAD plugin folder");
    try
    {
        InstallBundleFromZip(tempZip, allUsersDestRoot);
        installedAllUsers = true;
    }
    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
    {
        allUsersWarning = "Skipped all-users folder because Windows did not allow writing there. This is okay for a normal per-user install.";
    }

    string packageFile = Path.Combine(userDestBundle, "PackageContents.xml");
    string dllFile = Path.Combine(userDestBundle, "Contents", "CISCSections.dll");
    string lspFile = Path.Combine(userDestBundle, "Contents", "LoadCISC.lsp");
    await ExtractResourceAsync(assembly, "LoadCISC.lsp", lspFile);

    if (!File.Exists(packageFile))
    {
        return Fail("PackageContents.xml was not installed. The installer may be corrupted.");
    }

    if (!File.Exists(dllFile))
    {
        return Fail("CISCSections.dll was not installed. The installer may be corrupted.");
    }

    if (!File.Exists(lspFile))
    {
        return Fail("LoadCISC.lsp was not installed. The installer may be corrupted.");
    }

    Step("Adding LoadCISC.lsp to AutoCAD Startup Suite");
    int updatedProfiles = AddLoadCiscToStartupSuites(lspFile);

    Step("Installed successfully");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Installed folder:");
    Console.WriteLine(userDestBundle);
    Console.ResetColor();
    Console.WriteLine();
    if (installedAllUsers)
    {
        Console.WriteLine("Also installed all-users folder:");
        Console.WriteLine(allUsersDestBundle);
        Console.WriteLine();
    }
    else if (!string.IsNullOrWhiteSpace(allUsersWarning))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(allUsersWarning);
        Console.ResetColor();
        Console.WriteLine();
    }

    Console.WriteLine($"Windows user: {Environment.UserDomainName}\\{Environment.UserName}");
    if (IsRunningAsAdministrator())
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Note: this installer is running as Administrator. If AutoCAD is normally opened without Administrator, run this installer normally too.");
        Console.ResetColor();
        Console.WriteLine();
    }

    Console.WriteLine($"Startup Suite profiles updated: {updatedProfiles}");
    Console.WriteLine();
    Console.WriteLine("Next steps:");
    Console.WriteLine("1. Open AutoCAD.");
    Console.WriteLine("2. If AutoCAD asks whether to load LoadCISC.lsp or the plugin DLL, choose Always Load.");
    Console.WriteLine("3. Type CISCINSERT and press Enter.");
    Console.WriteLine();
    Console.WriteLine("Press Enter to close.");
    Console.ReadLine();
    return 0;
}
catch (Exception ex)
{
    return Fail(ex.Message);
}
