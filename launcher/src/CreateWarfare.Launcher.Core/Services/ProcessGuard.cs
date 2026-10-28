using System.Diagnostics;
using System.Management;

namespace CreateWarfare.Launcher.Core.Services;

public static class ProcessGuard
{
    public static bool CurseForgeRunning() => Process.GetProcessesByName("CurseForge").Length > 0;

    /// <summary>Minecraft lancé depuis cette instance : un java(w) dont la ligne de commande cite son dossier.</summary>
    public static bool GameRunning(string instanceDir)
    {
        var needle = Path.GetFullPath(instanceDir).TrimEnd('\\');
        try
        {
            using var q = new ManagementObjectSearcher(
                "SELECT CommandLine FROM Win32_Process WHERE Name = 'javaw.exe' OR Name = 'java.exe'");
            foreach (var p in q.Get())
                if ((p["CommandLine"] as string)?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true)
                    return true;
        }
        catch (ManagementException e) { Log.Info($"WMI indisponible : {e.Message}"); }
        return false;
    }
}
