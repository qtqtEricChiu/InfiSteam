using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace InfiSteam.Services;

public class SteamDetector
{
    public record DetectionResult(
        string SteamPath,
        string SteamAppsPath,
        string AcfPath,
        string GamePath,
        bool Found);

    private const string AppId = "3164330";

    public DetectionResult Detect()
    {
        string? steamPath = null;

        // 1. Registry detection (most accurate)
        steamPath = TryReadRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath")
                 ?? TryReadRegistry(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath");

        // 2. Process detection (fallback)
        if (steamPath == null)
        {
            var proc = Process.GetProcessesByName("steam").FirstOrDefault();
            if (proc != null)
            {
                try { steamPath = Path.GetDirectoryName(proc.MainModule?.FileName); }
                catch { }
            }
        }

        // 3. Common installation paths (last resort)
        if (steamPath == null)
        {
            var progX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            var progFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");

            foreach (var basePath in new[] { progX86, progFiles, localAppData })
            {
                if (basePath == null) continue;
                var candidate = Path.Combine(basePath, "Steam");
                if (File.Exists(Path.Combine(candidate, "steam.exe")))
                {
                    steamPath = candidate;
                    break;
                }
            }
        }

        if (steamPath == null)
            return new DetectionResult("", "", "", "", false);

        // 4. Find game library via libraryfolders.vdf
        var (gameLibrary, acfPath, gamePath) = FindGameLibrary(steamPath);
        if (gameLibrary == null)
            return new DetectionResult(steamPath, "", "", "", false);

        var steamApps = Path.Combine(gameLibrary, "steamapps");
        return new DetectionResult(steamPath, steamApps, acfPath, gamePath, true);
    }

    /// <summary>
    /// Reads libraryfolders.vdf to find which library contains AppId, then returns (libraryPath, acfPath, gamePath).
    /// </summary>
    private static (string? libraryPath, string acfPath, string gamePath) FindGameLibrary(string steamPath)
    {
        var candidates = new List<string> { steamPath };

        // Parse libraryfolders.vdf for additional libraries
        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            try
            {
                var vdfContent = File.ReadAllText(vdfPath);
                var matches = Regex.Matches(vdfContent, @"""path""\s+""([^""]+)""");
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    var libPath = m.Groups[1].Value.Replace("\\\\", "\\");
                    if (!candidates.Contains(libPath, StringComparer.OrdinalIgnoreCase))
                        candidates.Add(libPath);
                }
            }
            catch { /* best effort */ }
        }

        // Search each library for appmanifest
        foreach (var lib in candidates)
        {
            var acfPath = Path.Combine(lib, "steamapps", $"appmanifest_{AppId}.acf");
            if (File.Exists(acfPath))
            {
                var gamePath = Path.Combine(lib, "steamapps", "common", "Infinity Nikki");
                return (lib, acfPath, gamePath);
            }
        }

        return (null, "", "");
    }

    public bool IsSteamRunning()
    {
        var procs = Process.GetProcessesByName("steam");
        var helpers = Process.GetProcessesByName("steamwebhelper");
        return procs.Length > 0 || helpers.Length > 0;
    }

    private static string? TryReadRegistry(string keyPath, string valueName)
    {
        try
        {
            var parts = keyPath.Split('\\', 2);
            var hive = parts[0] switch
            {
                "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                "HKEY_CURRENT_USER" => Registry.CurrentUser,
                _ => null
            };
            if (hive == null) return null;

            using var key = hive.OpenSubKey(parts[1]);
            return key?.GetValue(valueName) as string;
        }
        catch
        {
            return null;
        }
    }
}
