using System.Text.RegularExpressions;
using System.Diagnostics;

namespace InfiSteam.Services;

public class AcfManager
{
    private const string AppId = "3164330";
    private const string DepotId = "3164332";

    public record AcfData(
        string BuildId,
        string ManifestGid,
        string StateFlags,
        string TargetBuildId,
        string AutoUpdateBehavior,
        string BytesToDownload,
        string BytesDownloaded,
        string BytesToStage,
        string BytesStaged,
        string RawContent);

    public AcfData Read(string acfPath)
    {
        var content = File.ReadAllText(acfPath);

        return new AcfData(
            BuildId: ExtractValue(content, "buildid"),
            ManifestGid: ExtractValue(content, "manifest"),
            StateFlags: ExtractValue(content, "StateFlags"),
            TargetBuildId: ExtractValue(content, "TargetBuildID"),
            AutoUpdateBehavior: ExtractValue(content, "AutoUpdateBehavior"),
            BytesToDownload: ExtractValue(content, "BytesToDownload"),
            BytesDownloaded: ExtractValue(content, "BytesDownloaded"),
            BytesToStage: ExtractValue(content, "BytesToStage"),
            BytesStaged: ExtractValue(content, "BytesStaged"),
            RawContent: content
        );
    }

    public void Update(string acfPath, string newBuildId, string newManifestGid)
    {
        // Backup ACF before updating
        Backup(acfPath);

        var content = File.ReadAllText(acfPath);

        content = RegexReplace(content, "buildid", newBuildId);
        content = RegexReplace(content, "manifest", newManifestGid);
        content = RegexReplace(content, "StateFlags", "4");
        content = RegexReplace(content, "TargetBuildID", "0");
        content = RegexReplace(content, "AutoUpdateBehavior", "1");
        content = RegexReplace(content, "BytesToDownload", "0");
        content = RegexReplace(content, "BytesDownloaded", "0");
        content = RegexReplace(content, "BytesToStage", "0");
        content = RegexReplace(content, "BytesStaged", "0");

        // Remove readonly
        var attr = File.GetAttributes(acfPath);
        if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            File.SetAttributes(acfPath, attr & ~FileAttributes.ReadOnly);

        File.WriteAllText(acfPath, content);

        // Lock readonly
        File.SetAttributes(acfPath, File.GetAttributes(acfPath) | FileAttributes.ReadOnly);
    }

    public static string GenerateReport(string steamPath, string acfPath, string gamePath)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== InfiSteam Full Report =====");
        sb.AppendLine($"Steam Root:   {steamPath}");
        sb.AppendLine($"Game Dir:     {gamePath}");
        sb.AppendLine($"ACF File:     {acfPath}");
        sb.AppendLine($"Steam running: {Process.GetProcessesByName("steam").Length > 0}");
        sb.AppendLine();

        if (File.Exists(acfPath))
        {
            var acf = new AcfManager().Read(acfPath);
            sb.AppendLine("--- ACF State ---");
            sb.AppendLine($"  BuildID:           {acf.BuildId}");
            sb.AppendLine($"  Manifest GID:      {acf.ManifestGid}");
            sb.AppendLine($"  StateFlags:        {acf.StateFlags} {(acf.StateFlags == "4" ? "OK" : "NEED FIX")}");
            sb.AppendLine($"  TargetBuildID:     {acf.TargetBuildId} {(acf.TargetBuildId == "0" ? "OK" : "NEED FIX")}");
            sb.AppendLine($"  AutoUpdateBehavior: {acf.AutoUpdateBehavior} {(acf.AutoUpdateBehavior == "1" ? "OK" : "WARN")}");
            sb.AppendLine($"  BytesToDownload:   {acf.BytesToDownload}");
            sb.AppendLine($"  BytesDownloaded:   {acf.BytesDownloaded}");
            sb.AppendLine($"  BytesToStage:      {acf.BytesToStage}");
            sb.AppendLine($"  BytesStaged:       {acf.BytesStaged}");
            sb.AppendLine();

            var isChina = IsChinaVersionStatic(acf.RawContent, gamePath);
            sb.AppendLine($"Version Type: {(isChina ? "China" : "Global")}");

            var isRo = new AcfManager().IsReadOnly(acfPath);
            sb.AppendLine($"ReadOnly:      {(isRo ? "YES (locked)" : "NO (writable)")}");
        }

        // X6Game location
        sb.AppendLine();
        sb.AppendLine("--- X6Game ---");
        var x6gInSteam = Path.Combine(gamePath, "InfinityNikki", "X6Game");
        if (Directory.Exists(x6gInSteam))
        {
            var size = Directory.GetFiles(x6gInSteam, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
            sb.AppendLine($"  Location: Steam dir ({size / 1024.0 / 1024.0 / 1024.0:F2} GB)");
        }
        else
        {
            var backupDir = Path.Combine(Path.GetPathRoot(gamePath) ?? "C:\\", "X6Game_backup");
            if (Directory.Exists(backupDir))
                sb.AppendLine($"  Location: Backup: {backupDir}");
            else
                sb.AppendLine($"  Location: Not found");
        }

        sb.AppendLine();
        sb.AppendLine("===== End of Report =====");
        return sb.ToString();
    }

    public static string CheckResidualFiles(string acfPath)
    {
        var sb = new System.Text.StringBuilder();
        var steamappsDir = Path.GetDirectoryName(acfPath) ?? "";
        bool found = false;

        // ACF temp files
        var acfTmps = Directory.GetFiles(steamappsDir, $"appmanifest_{AppId}.acf.*.tmp");
        if (acfTmps.Length > 0)
        {
            sb.AppendLine($"[!] ACF temp files: {acfTmps.Length} found");
            foreach (var f in acfTmps)
                sb.AppendLine($"    {f} ({new FileInfo(f).Length / 1024.0:F1} KB)");
            found = true;
        }

        // ACF backup files
        var acfBaks = Directory.GetFiles(steamappsDir, $"appmanifest_{AppId}.acf.bak.*");
        if (acfBaks.Length > 0)
        {
            sb.AppendLine($"[!] ACF backup files: {acfBaks.Length} found");
            foreach (var f in acfBaks)
                sb.AppendLine($"    {f} ({new FileInfo(f).Length / 1024.0:F1} KB)");
            found = true;
        }

        // Downloading dir
        var dlDir = Path.Combine(steamappsDir, "downloading", AppId);
        if (Directory.Exists(dlDir))
        {
            var size = Directory.GetFiles(dlDir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
            sb.AppendLine($"[!] Downloading dir: {dlDir} ({size / 1024.0 / 1024.0:F1} MB)");
            found = true;
        }

        // Temp dir
        var tmpDir = Path.Combine(steamappsDir, "temp", AppId);
        if (Directory.Exists(tmpDir))
        {
            var size = Directory.GetFiles(tmpDir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
            sb.AppendLine($"[!] Temp dir: {tmpDir} ({size / 1024.0 / 1024.0:F1} MB)");
            found = true;
        }

        if (!found)
            sb.AppendLine("[OK] No residual files found");

        return sb.ToString();
    }

    public static string RunNetworkDiag()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Network Connectivity Test");
        sb.AppendLine("------------------------");

        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            foreach (var host in new[] { "steamdb.info", "cloudflare.com", "google.com" })
            {
                try
                {
                    var reply = ping.Send(host, 3000);
                    if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                        sb.AppendLine($"  {host}: {reply.RoundtripTime} ms (OK)");
                    else
                        sb.AppendLine($"  {host}: {reply.Status}");
                }
                catch
                {
                    sb.AppendLine($"  {host}: Timeout / Unreachable");
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  Ping failed: {ex.Message}");
        }

        return sb.ToString();
    }

    private static void Backup(string acfPath)
    {
        try
        {
            var acfDir = Path.GetDirectoryName(acfPath) ?? "";
            var backupDir = Path.Combine(acfDir, "..", "backups");
            backupDir = Path.GetFullPath(backupDir);
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupName = $"appmanifest_3164330.acf.bak.{timestamp}";
            var backupPath = Path.Combine(backupDir, backupName);

            File.Copy(acfPath, backupPath, true);
        }
        catch
        {
            // Best effort backup
        }
    }

    public bool IsReadOnly(string acfPath)
    {
        return (File.GetAttributes(acfPath) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
    }

    public void SetReadOnly(string acfPath, bool readOnly)
    {
        var attr = File.GetAttributes(acfPath);
        if (readOnly)
            File.SetAttributes(acfPath, attr | FileAttributes.ReadOnly);
        else
            File.SetAttributes(acfPath, attr & ~FileAttributes.ReadOnly);
    }

    public bool IsChinaVersion(string acfContent, string gamePath)
    {
        return acfContent.Contains("sub/1221922")
            || acfContent.Contains("schinese")
            || Directory.Exists(Path.Combine(gamePath, "InfinityNikki", "X6Game", "Content", "Paks"))
               && Directory.GetFiles(Path.Combine(gamePath, "InfinityNikki", "X6Game", "Content", "Paks"), "*China*").Any();
    }

    private static bool IsChinaVersionStatic(string acfContent, string gamePath)
    {
        return acfContent.Contains("sub/1221922")
            || acfContent.Contains("schinese")
            || Directory.Exists(Path.Combine(gamePath, "InfinityNikki", "X6Game", "Content", "Paks"))
               && Directory.GetFiles(Path.Combine(gamePath, "InfinityNikki", "X6Game", "Content", "Paks"), "*China*").Any();
    }

    private static string ExtractValue(string content, string key)
    {
        var match = Regex.Match(content, $@"""{key}""\s+""([^""]*)""");
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string RegexReplace(string content, string key, string newValue)
    {
        return Regex.Replace(content, $@"""{key}""\s+""[^""]*""", $@"""{key}""		""{newValue}""");
    }
}
