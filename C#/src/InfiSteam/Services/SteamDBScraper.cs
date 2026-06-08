using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace InfiSteam.Services;

public class SteamDBScraper
{
    private const string DepotsUrl = "https://steamdb.info/app/3164330/depots/";
    private const string ManifestsUrl = "https://steamdb.info/depot/3164332/manifests/";
    private const string ChromeDebugUrl = "http://127.0.0.1:9222";
    private const int DebugPort = 9222;
    private const int PageLoadWaitMs = 8000;

    private string? _chromeProfilePath;
    private Process? _chromeProcess;

    public record SteamDBResult(string BuildId, string ManifestGid);

    public async Task<SteamDBResult> FetchLatestAsync(string? scriptDir, IProgress<string>? progress = null)
    {
        // Ensure Chrome profile directory
        _chromeProfilePath = Path.Combine(scriptDir ?? Path.GetTempPath(), "chrome-profile-steamdb");
        Directory.CreateDirectory(_chromeProfilePath);

        // Kill any existing Chrome on debug port
        await KillDebugChromeAsync();

        // Launch Chrome
        var chromePath = FindChrome();
        if (chromePath == null)
            throw new InvalidOperationException("未找到 Chrome 浏览器，请确保已安装 Google Chrome");

        progress?.Report("正在启动 Chrome...");
        _chromeProcess = Process.Start(new ProcessStartInfo
        {
            FileName = chromePath,
            Arguments = $"--remote-debugging-port={DebugPort} --user-data-dir=\"{_chromeProfilePath}\" --no-first-run --no-default-browser-check --window-size=1280,800 \"{DepotsUrl}\"",
            WindowStyle = ProcessWindowStyle.Normal,
            UseShellExecute = true
        });

        if (_chromeProcess == null)
            throw new InvalidOperationException("无法启动 Chrome");

        progress?.Report("等待 Chrome 加载 SteamDB 页面...");

        // Wait for page load and CDP readiness
        var depotsText = await WaitForPageAndFetchText(DepotsUrl, progress);

        // Check ban
        if (depotsText.Contains("banned") || depotsText.Contains("You have been banned"))
            throw new InvalidOperationException("IP 被 SteamDB 封禁，请等待 1 小时后重试，或更换网络环境");

        // Extract BuildId from depots page
        var buildIdMatch = Regex.Match(depotsText, @"public\s+(\d+)");
        var buildId = buildIdMatch.Success ? buildIdMatch.Groups[1].Value : "";

        // Navigate to manifests page
        progress?.Report("正在获取 Manifests 数据...");
        await NavigateToAsync(ManifestsUrl);
        await Task.Delay(PageLoadWaitMs);

        var manifestsText = await FetchPageTextAsync();
        var manifestMatch = Regex.Match(manifestsText, @"(\d{19})");
        var manifestGid = manifestMatch.Success ? manifestMatch.Groups[1].Value : "";

        progress?.Report("数据获取完成");

        return new SteamDBResult(buildId, manifestGid);
    }

    public void CloseChrome()
    {
        try
        {
            if (_chromeProcess != null && !_chromeProcess.HasExited)
            {
                _chromeProcess.Kill();
                _chromeProcess.WaitForExit(3000);
            }
        }
        catch { }
    }

    private async Task<string> WaitForPageAndFetchText(string targetUrl, IProgress<string>? progress)
    {
        // Wait for CDP to be ready
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(1000);
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var resp = await http.GetStringAsync($"{ChromeDebugUrl}/json/list");
                if (!string.IsNullOrEmpty(resp))
                    break;
            }
            catch { }
        }

        // Wait additional time for SteamDB page to fully render
        await Task.Delay(PageLoadWaitMs);

        // Find the target page
        using var client = new HttpClient();
        var listJson = await client.GetStringAsync($"{ChromeDebugUrl}/json/list");
        var pages = JsonSerializer.Deserialize<JsonArray>(listJson);

        string? wsUrl = null;
        foreach (var page in pages!)
        {
            var url = page!["url"]?.GetValue<string>() ?? "";
            if (url.Contains("steamdb.info"))
            {
                wsUrl = page["webSocketDebuggerUrl"]?.GetValue<string>();
                break;
            }
        }

        if (wsUrl == null)
            throw new InvalidOperationException("未找到 SteamDB 页面");

        // Connect via WebSocket and evaluate JS
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(wsUrl), CancellationToken.None);

        var cmd = JsonSerializer.Serialize(new
        {
            id = 1,
            method = "Runtime.evaluate",
            @params = new { expression = "document.body.innerText" }
        });

        await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(cmd)), WebSocketMessageType.Text, true, CancellationToken.None);

        var buffer = new byte[65536];
        var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
        var responseText = Encoding.UTF8.GetString(buffer, 0, result.Count);

        var responseNode = JsonNode.Parse(responseText);
        var value = responseNode?["result"]?["result"]?["value"]?.GetValue<string>() ?? "";

        return value;
    }

    private async Task NavigateToAsync(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var listJson = await client.GetStringAsync($"{ChromeDebugUrl}/json/list");
            var pages = JsonSerializer.Deserialize<JsonArray>(listJson);

            string? pageId = null;
            foreach (var page in pages!)
            {
                if (page!["url"]?.GetValue<string>()?.Contains("steamdb.info") == true)
                {
                    pageId = page["id"]?.GetValue<string>();
                    break;
                }
            }

            if (pageId == null) return;

            // Use HTTP endpoint to navigate
            var navJson = JsonSerializer.Serialize(new { url });
            await client.PostAsync($"{ChromeDebugUrl}/json/activate/{pageId}", null);
        }
        catch { }

        // Fallback: use websocket navigation
        try
        {
            using var client = new HttpClient();
            var listJson = await client.GetStringAsync($"{ChromeDebugUrl}/json/list");
            var pages = JsonSerializer.Deserialize<JsonArray>(listJson);

            string? wsUrl = null;
            foreach (var page in pages!)
            {
                if (page!["url"]?.GetValue<string>()?.Contains("steamdb.info") == true)
                {
                    wsUrl = page["webSocketDebuggerUrl"]?.GetValue<string>();
                    break;
                }
            }

            if (wsUrl == null) return;

            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(wsUrl), CancellationToken.None);

            var cmd = JsonSerializer.Serialize(new
            {
                id = 2,
                method = "Page.navigate",
                @params = new { url }
            });

            await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(cmd)), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch { }
    }

    private async Task<string> FetchPageTextAsync()
    {
        using var client = new HttpClient();
        var listJson = await client.GetStringAsync($"{ChromeDebugUrl}/json/list");
        var pages = JsonSerializer.Deserialize<JsonArray>(listJson);

        string? wsUrl = null;
        foreach (var page in pages!)
        {
            if (page!["url"]?.GetValue<string>()?.Contains("steamdb.info") == true)
            {
                wsUrl = page["webSocketDebuggerUrl"]?.GetValue<string>();
                break;
            }
        }

        if (wsUrl == null) return "";

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(wsUrl), CancellationToken.None);

        var cmd = JsonSerializer.Serialize(new
        {
            id = 3,
            method = "Runtime.evaluate",
            @params = new { expression = "document.body.innerText" }
        });

        await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(cmd)), WebSocketMessageType.Text, true, CancellationToken.None);

        var buffer = new byte[65536];
        var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
        var responseText = Encoding.UTF8.GetString(buffer, 0, result.Count);

        var responseNode = JsonNode.Parse(responseText);
        return responseNode?["result"]?["result"]?["value"]?.GetValue<string>() ?? "";
    }

    private async Task KillDebugChromeAsync()
    {
        try
        {
            var procs = Process.GetProcessesByName("chrome");
            foreach (var proc in procs)
            {
                try
                {
                    var cmdLine = GetCommandLine(proc);
                    if (cmdLine.Contains($"remote-debugging-port={DebugPort}"))
                    {
                        proc.Kill();
                    }
                }
                catch { }
            }
        }
        catch { }
        await Task.Delay(1000);
    }

    private static string FindChrome()
    {
        var candidates = new[]
        {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
        };

        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        return "";
    }

    private static string GetCommandLine(Process process)
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}");
            foreach (var obj in searcher.Get())
            {
                return obj["CommandLine"]?.ToString() ?? "";
            }
        }
        catch { }
        return "";
    }
}
