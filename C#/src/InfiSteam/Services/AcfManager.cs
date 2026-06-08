using System.Text.RegularExpressions;

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
            RawContent: content
        );
    }

    public void Update(string acfPath, string newBuildId, string newManifestGid)
    {
        var content = File.ReadAllText(acfPath);

        content = RegexReplace(content, "buildid", newBuildId);
        content = RegexReplace(content, "manifest", newManifestGid);
        content = RegexReplace(content, "StateFlags", "4");
        content = RegexReplace(content, "TargetBuildID", "0");
        content = RegexReplace(content, "AutoUpdateBehavior", "1");
        content = RegexReplace(content, "BytesToDownload", "0");
        content = RegexReplace(content, "BytesDownloaded", "0");

        // Remove readonly
        var attr = File.GetAttributes(acfPath);
        if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            File.SetAttributes(acfPath, attr & ~FileAttributes.ReadOnly);

        File.WriteAllText(acfPath, content);

        // Lock readonly
        File.SetAttributes(acfPath, File.GetAttributes(acfPath) | FileAttributes.ReadOnly);
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
