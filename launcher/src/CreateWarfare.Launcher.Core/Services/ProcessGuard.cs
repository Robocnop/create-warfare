using System.Diagnostics;
using System.Management;

namespace CreateWarfare.Launcher.Core.Services;

/// <summary>Détection de CurseForge et du jeu en cours (Windows seulement).</summary>
public static class ProcessGuard
{
    /// <summary>Vrai si un processus « CurseForge » tourne (il réécrit l'instance).</summary>
    public static bool CurseForgeRunning() => Process.GetProcessesByName("CurseForge").Length > 0;

    /// <summary>
    /// Minecraft lancé depuis cette instance. Trois indices, du plus rapide au plus lent : jeu lancé par ce launcher
    /// (<see cref="GameMarker"/>), logs/latest.log ouvert par le jeu, ou un java(w) dont la ligne de commande cite l'instance.
    /// La ligne de commande seule ne suffit pas : Sodium la réécrit (« net.caffeinemc.sodium / net.minecraft.client.main.Main / »,
    /// contournement des pilotes NVIDIA) dès le démarrage du rendu, CurseForge ou lancement natif.
    /// </summary>
    public static bool GameRunning(string instanceDir) =>
        GameMarker.Alive(instanceDir) || LogInUse(instanceDir) || CommandLineMentions(instanceDir);

    /// <summary>
    /// logs/latest.log tenu ouvert par un java (log4j garde le fichier ouvert toute la partie). Demandé au gestionnaire
    /// de redémarrage de Windows, sans ouvrir le fichier : aucun risque de gêner le jeu quand il crée son journal.
    /// </summary>
    public static bool LogInUse(string instanceDir)
    {
        var log = Path.Combine(instanceDir, "logs", "latest.log");
        if (!File.Exists(log)) return false;
        foreach (var pid in FileUsers(log))
            try
            {
                using var p = Process.GetProcessById(pid);
                if (p.ProcessName.StartsWith("java", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException) { }
        return false;
    }

    /// <summary>Processus qui ont ce fichier ouvert (Restart Manager, rstrtmgr.dll) ; liste vide en cas d'erreur.</summary>
    public static List<int> FileUsers(string path)
    {
        var pids = new List<int>();
        var key = new System.Text.StringBuilder(33);
        if (RmStartSession(out var session, 0, key) != 0) return pids;
        try
        {
            if (RmRegisterResources(session, 1, new[] { path }, 0, IntPtr.Zero, 0, IntPtr.Zero) != 0) return pids;
            uint needed = 0, count = 0, reasons = 0;
            var r = RmGetList(session, out needed, ref count, null, ref reasons);
            if (r == 234 && needed > 0) // ERROR_MORE_DATA
            {
                var infos = new RM_PROCESS_INFO[needed];
                count = needed;
                r = RmGetList(session, out needed, ref count, infos, ref reasons);
                if (r == 0) pids.AddRange(infos.Take((int)count).Select(i => i.Process.dwProcessId));
            }
        }
        finally { RmEndSession(session); }
        return pids;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 256)] public string strAppName;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 64)] public string strServiceShortName;
        public int ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] public bool bRestartable;
    }

    [System.Runtime.InteropServices.DllImport("rstrtmgr.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, System.Text.StringBuilder strSessionKey);

    [System.Runtime.InteropServices.DllImport("rstrtmgr.dll")]
    static extern int RmEndSession(uint pSessionHandle);

    [System.Runtime.InteropServices.DllImport("rstrtmgr.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames, uint nApplications, IntPtr rgApplications,
        uint nServices, IntPtr rgsServiceNames);

    [System.Runtime.InteropServices.DllImport("rstrtmgr.dll")]
    static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo,
        [System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] RM_PROCESS_INFO[]? rgAffectedApps, ref uint lpdwRebootReasons);

    static bool CommandLineMentions(string instanceDir)
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
        catch (Exception e) when (e is ManagementException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            Log.Info($"WMI indisponible : {e.Message}");
        }
        return false;
    }
}

/// <summary>
/// Jeu lancé par le launcher (lancement natif) : PID, heure de démarrage et instance, dans
/// %LOCALAPPDATA%\CreateWarfareLauncher\game.pid. Survit à une relance du launcher ; un PID réutilisé par un autre
/// programme est écarté par l'heure de démarrage.
/// </summary>
public static class GameMarker
{
    static string FilePath => Path.Combine(Log.AppDataDir, "game.pid");

    /// <summary>Mémorise le jeu lancé par le launcher (PID, heure de début, instance) dans game.pid.</summary>
    public static void Write(string instanceDir, Process p)
    {
        try
        {
            Directory.CreateDirectory(Log.AppDataDir);
            File.WriteAllText(FilePath, $"{p.Id}|{p.StartTime.ToUniversalTime().Ticks}|{Path.GetFullPath(instanceDir)}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { Log.Info($"game.pid non écrit : {e.Message}"); }
    }

    /// <summary>Efface le marqueur si c'est celui de ce PID.</summary>
    public static void Clear(string instanceDir, int pid)
    {
        try
        {
            if (Read() is { } m && m.Pid == pid) File.Delete(FilePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Vrai si le marqueur désigne cette instance et un processus vivant dont l'heure de début correspond (à 1 s près).</summary>
    public static bool Alive(string instanceDir)
    {
        if (Read() is not { } m || !m.Instance.Equals(Path.GetFullPath(instanceDir), StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            using var p = Process.GetProcessById(m.Pid);
            return !p.HasExited && Math.Abs(p.StartTime.ToUniversalTime().Ticks - m.StartTicks) < TimeSpan.TicksPerSecond;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }

    static (int Pid, long StartTicks, string Instance)? Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var parts = File.ReadAllText(FilePath).Split('|', 3);
            return parts.Length == 3 && int.TryParse(parts[0], out var pid) && long.TryParse(parts[1], out var t) ? (pid, t, parts[2]) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
}
