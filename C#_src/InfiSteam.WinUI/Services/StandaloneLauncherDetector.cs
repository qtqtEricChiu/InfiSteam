using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace InfiSteam.Services;

public class StandaloneLauncherInfo
{
    public string ExePath { get; init; } = "";
    public string? GamePath { get; init; }
    public string Source { get; init; } = "";
    public string LaunchOption => $"\"{ExePath}\" %command%";
}

public class StandaloneLauncherDetector
{
    public List<StandaloneLauncherInfo> Detect()
    {
        var found = new List<StandaloneLauncherInfo>();

        // Method 1: Registry uninstall info
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var subKey in new[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
            {
                try
                {
                    using var key = hive.OpenSubKey(subKey);
                    if (key == null) continue;

                    foreach (var name in key.GetSubKeyNames())
                    {
                        try
                        {
                            using var appKey = key.OpenSubKey(name);
                            var displayName = appKey?.GetValue("DisplayName") as string;
                            if (string.IsNullOrEmpty(displayName)) continue;
                            if (!displayName.Contains("Infinity", StringComparison.OrdinalIgnoreCase) &&
                                !displayName.Contains("Nikki", StringComparison.OrdinalIgnoreCase) &&
                                !displayName.Contains("Infold", StringComparison.OrdinalIgnoreCase))
                                continue;

                            var installLoc = appKey?.GetValue("InstallLocation") as string;
                            if (string.IsNullOrEmpty(installLoc) || !Directory.Exists(installLoc))
                                continue;

                            var launcherExe = Path.Combine(installLoc, "launcher.exe");
                            if (File.Exists(launcherExe))
                            {
                                found.Add(new StandaloneLauncherInfo
                                {
                                    ExePath = launcherExe,
                                    GamePath = null,
                                    Source = $"注册表: {displayName}"
                                });
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        // Method 2: config.ini detection in common directories
        var commonDirs = new[]
        {
            @"D:\Entertainment\InfinityNikkiLauncher",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "InfinityNikkiLauncher"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "InfinityNikkiLauncher"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InfinityNikkiLauncher"),
            @"C:\InfinityNikkiLauncher",
        };

        foreach (var dir in commonDirs)
        {
            var configPath = Path.Combine(dir, "config.ini");
            if (!File.Exists(configPath)) continue;

            try
            {
                var content = File.ReadAllText(configPath);
                string? gamePath = null;
                var match = Regex.Match(content, @"game_path\s*=\s*(.+)", RegexOptions.IgnoreCase);
                if (match.Success)
                    gamePath = match.Groups[1].Value.Trim();

                var launcherExe = Path.Combine(dir, "launcher.exe");
                if (File.Exists(launcherExe))
                {
                    found.Add(new StandaloneLauncherInfo
                    {
                        ExePath = launcherExe,
                        GamePath = gamePath,
                        Source = "配置文件 config.ini"
                    });
                }
            }
            catch { }
        }

        // Method 3: Start menu shortcuts
        var shortcutDirs = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         @"Microsoft\Windows\Start Menu\Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                         @"Microsoft\Windows\Start Menu\Programs"),
        };

        foreach (var shortcutDir in shortcutDirs)
        {
            if (!Directory.Exists(shortcutDir)) continue;

            try
            {
                foreach (var lnk in Directory.EnumerateFiles(shortcutDir, "*.lnk", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileNameWithoutExtension(lnk);
                    if (!name.Contains("Infinity", StringComparison.OrdinalIgnoreCase) &&
                        !name.Contains("Nikki", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Read shortcut target via WScript.Shell COM
                    try
                    {
                        var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                        var shortcut = shell!.GetType().InvokeMember("CreateShortcut",
                            System.Reflection.BindingFlags.InvokeMethod, null, shell, [lnk]);
                        var targetPath = shortcut!.GetType().InvokeMember("TargetPath",
                            System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;

                        if (!string.IsNullOrEmpty(targetPath) &&
                            (targetPath.Contains("launcher", StringComparison.OrdinalIgnoreCase) ||
                             targetPath.Contains("xstarter", StringComparison.OrdinalIgnoreCase)) &&
                            File.Exists(targetPath))
                        {
                            found.Add(new StandaloneLauncherInfo
                            {
                                ExePath = targetPath,
                                GamePath = null,
                                Source = $"开始菜单: {name}"
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // Deduplicate by ExePath
        return found.GroupBy(f => f.ExePath, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
    }
}
