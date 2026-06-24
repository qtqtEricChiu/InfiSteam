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

    // 超时时间大幅延长，以应对 Cloudflare 验证
    private const int CdpReadyTimeoutSec = 90;      // 等待 CDP 就绪最长 90 秒
    private const int PageLoadWaitMs = 20000;       // 页面初次加载等待 20 秒
    private const int CloudflarePollIntervalMs = 2000;  // Cloudflare 检测轮询间隔
    private const int CloudflareMaxWaitSec = 120;   // Cloudflare 最多等待 120 秒
    private const int ParseRetryDelayMs = 10000;     // 解析失败后重试间隔 10 秒
    private const int ParseMaxRetries = 3;           // 解析失败最多重试 3 次

    private string? _chromeProfilePath;
    private Process? _chromeProcess;

    public record SteamDBResult(string BuildId, string ManifestGid);

    /// <summary>
    /// 启动 Chrome 并获取 SteamDB 最新数据（含 Cloudflare 处理和自动重试）
    /// </summary>
    public async Task<SteamDBResult> FetchLatestAsync(string? scriptDir, IProgress<string>? progress = null)
    {
        // 确保 Chrome 用户目录存在
        _chromeProfilePath = Path.Combine(scriptDir ?? Path.GetTempPath(), "chrome-profile-steamdb");
        Directory.CreateDirectory(_chromeProfilePath);

        // 关闭同端口的已有 Chrome 实例
        await KillDebugChromeAsync();

        // 启动 Chrome
        var chromePath = FindChrome();
        if (chromePath == null)
            throw new InvalidOperationException("未找到 Chrome 浏览器，请确保已安装 Google Chrome");

        progress?.Report("正在启动 Chrome（如遇到 Cloudflare 验证请耐心等待）...");
        _chromeProcess = Process.Start(new ProcessStartInfo
        {
            FileName = chromePath,
            Arguments = $"--remote-debugging-port={DebugPort} " +
                        $"--user-data-dir=\"{_chromeProfilePath}\" " +
                        $"--no-first-run --no-default-browser-check " +
                        $"--window-size=1280,800 \"{DepotsUrl}\"",
            WindowStyle = ProcessWindowStyle.Normal,
            UseShellExecute = true
        });

        if (_chromeProcess == null)
            throw new InvalidOperationException("无法启动 Chrome");

        progress?.Report("等待 Chrome 远程调试就绪（最长 90 秒）...");
        await WaitForCdpReadyAsync(progress);

        progress?.Report("正在加载 SteamDB 页面（可能需要通过 Cloudflare 人机验证）...");
        await Task.Delay(PageLoadWaitMs);

        // 获取 Depots 页面内容（含 Cloudflare 处理）
        var depotsText = await FetchPageTextWithCloudflareWait(progress);

        // 检查是否被封禁
        if (depotsText.Contains("banned", StringComparison.OrdinalIgnoreCase) ||
            depotsText.Contains("You have been banned", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("IP 被 SteamDB 封禁，请等待 1 小时后重试，或更换网络环境");

        // 从 Depots 页面提取 BuildId（失败时自动重试）
        var buildId = "";
        for (int retry = 0; retry <= ParseMaxRetries; retry++)
        {
            buildId = ExtractBuildId(depotsText);
            if (!string.IsNullOrEmpty(buildId))
                break;

            if (retry < ParseMaxRetries)
            {
                progress?.Report($"BuildID 未解析到，等待 10 秒后重试（{retry + 1}/{ParseMaxRetries}）...");
                await Task.Delay(ParseRetryDelayMs);
                depotsText = await FetchPageTextWithCloudflareWait(progress);
            }
        }

        // 导航到 Manifests 页面
        progress?.Report("正在获取 Manifests 数据...");
        await NavigateToAsync(ManifestsUrl);
        await Task.Delay(PageLoadWaitMs);

        var manifestsText = await FetchPageTextWithCloudflareWait(progress);

        // 从 Manifests 页面提取 ManifestGid（失败时自动重试）
        var manifestGid = "";
        for (int retry = 0; retry <= ParseMaxRetries; retry++)
        {
            manifestGid = ExtractManifestGid(manifestsText);
            if (!string.IsNullOrEmpty(manifestGid))
                break;

            if (retry < ParseMaxRetries)
            {
                progress?.Report($"Manifest GID 未解析到，等待 10 秒后重试（{retry + 1}/{ParseMaxRetries}）...");
                await Task.Delay(ParseRetryDelayMs);
                manifestsText = await FetchPageTextWithCloudflareWait(progress);
            }
        }

        progress?.Report("数据获取完成");
        return new SteamDBResult(buildId, manifestGid);
    }

    /// <summary>
    /// 在当前已打开的 Chrome 页面上重新获取数据（不重启 Chrome）
    /// 用于解析失败后让用户手动重试，避免重新触发 Cloudflare 验证
    /// </summary>
    public async Task<SteamDBResult> RetryFetchAsync(IProgress<string>? progress = null)
    {
        if (_chromeProcess == null || _chromeProcess.HasExited)
            throw new InvalidOperationException("Chrome 已关闭，无法重试。请重新点击「查询 SteamDB」。");

        progress?.Report("正在从当前 Chrome 页面重新获取数据...");

        // 重新获取 Depots 页面内容
        await NavigateToAsync(DepotsUrl);
        await Task.Delay(PageLoadWaitMs);
        var depotsText = await FetchPageTextWithCloudflareWait(progress);
        var buildId = ExtractBuildId(depotsText);

        // 如果还是获取不到，等待后重试几次
        for (int retry = 0; string.IsNullOrEmpty(buildId) && retry < ParseMaxRetries; retry++)
        {
            progress?.Report($"BuildID 未解析到，重试（{retry + 1}/{ParseMaxRetries}）...");
            await Task.Delay(ParseRetryDelayMs);
            depotsText = await FetchPageTextWithCloudflareWait(progress);
            buildId = ExtractBuildId(depotsText);
        }

        // 获取 Manifests 页面
        progress?.Report("正在获取 Manifests 数据...");
        await NavigateToAsync(ManifestsUrl);
        await Task.Delay(PageLoadWaitMs);
        var manifestsText = await FetchPageTextWithCloudflareWait(progress);
        var manifestGid = ExtractManifestGid(manifestsText);

        for (int retry = 0; string.IsNullOrEmpty(manifestGid) && retry < ParseMaxRetries; retry++)
        {
            progress?.Report($"Manifest GID 未解析到，重试（{retry + 1}/{ParseMaxRetries}）...");
            await Task.Delay(ParseRetryDelayMs);
            manifestsText = await FetchPageTextWithCloudflareWait(progress);
            manifestGid = ExtractManifestGid(manifestsText);
        }

        progress?.Report("重试数据获取完成");
        return new SteamDBResult(buildId, manifestGid);
    }

    /// <summary>
    /// 检查 Chrome 是否仍在运行
    /// </summary>
    public bool IsChromeAlive()
    {
        return _chromeProcess != null && !_chromeProcess.HasExited;
    }

    /// <summary>
    /// 关闭 Chrome（仅关闭本流程启动的实例）
    /// </summary>
    public void CloseChrome()
    {
        try
        {
            if (_chromeProcess != null && !_chromeProcess.HasExited)
            {
                _chromeProcess.Kill();
                _chromeProcess.WaitForExit(3000);
            }

            // 扫描同端口的残留 Chrome 进程
            var procs = Process.GetProcessesByName("chrome");
            foreach (var proc in procs)
            {
                try
                {
                    var cmdLine = GetCommandLine(proc);
                    if (cmdLine.Contains($"remote-debugging-port={DebugPort}") &&
                        cmdLine.Contains("chrome-profile-steamdb"))
                    {
                        proc.Kill();
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    // ─── 私有方法 ─────────────────────────────────────────────

    /// <summary>
    /// 等待 CDP 就绪（轮询 /json/list 直到有响应）
    /// </summary>
    private async Task WaitForCdpReadyAsync(IProgress<string>? progress)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        for (int i = 0; i < CdpReadyTimeoutSec; i++)
        {
            try
            {
                var resp = await http.GetStringAsync($"{ChromeDebugUrl}/json/list");
                if (!string.IsNullOrWhiteSpace(resp))
                    return;
            }
            catch
            {
                // CDP 尚未就绪，继续等待
            }

            if ((i + 1) % 5 == 0)
                progress?.Report($"等待 Chrome 远程调试就绪...（{i + 1}秒 / {CdpReadyTimeoutSec}秒）");

            await Task.Delay(1000);
        }

        throw new InvalidOperationException(
            "Chrome 远程调试启动超时。请检查 Chrome 是否正常启动，或是否有其他程序占用了 9222 端口。");
    }

    /// <summary>
    /// 获取当前页面文本，如遇 Cloudflare 验证则自动等待完成
    /// </summary>
    private async Task<string> FetchPageTextWithCloudflareWait(IProgress<string>? progress)
    {
        var text = await FetchPageTextViaCdpAsync();

        // 检测 Cloudflare "请稍候" / "Checking your browser" 页面
        if (IsCloudflareChallenge(text))
        {
            progress?.Report("检测到 Cloudflare 验证页面，正在等待验证完成（最多等待 2 分钟）...");

            for (int i = 0; i < CloudflareMaxWaitSec * 1000 / CloudflarePollIntervalMs; i++)
            {
                await Task.Delay(CloudflarePollIntervalMs);
                text = await FetchPageTextViaCdpAsync();

                if (!IsCloudflareChallenge(text))
                {
                    progress?.Report("Cloudflare 验证已通过，继续获取数据...");
                    // 额外等待页面完全渲染
                    await Task.Delay(3000);
                    text = await FetchPageTextViaCdpAsync();
                    break;
                }

                var elapsed = (i + 1) * CloudflarePollIntervalMs / 1000;
                if (i % 5 == 0)
                    progress?.Report($"仍在 Cloudflare 验证中...（{elapsed}秒 / {CloudflareMaxWaitSec}秒）");
            }

            // 超时后返回当前文本（可能是验证失败或超时）
            if (IsCloudflareChallenge(text))
            {
                progress?.Report("Cloudflare 验证等待超时，将尝试直接解析页面内容...");
            }
        }

        return text;
    }

    /// <summary>
    /// 判断页面是否为 Cloudflare 人机验证页面
    /// </summary>
    private static bool IsCloudflareChallenge(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var lower = text.ToLowerInvariant();
        return lower.Contains("checking your browser") ||
               lower.Contains("please wait") ||
               lower.Contains("cloudflare") ||
               lower.Contains("just a moment") ||
               lower.Contains("请稍候") ||
               lower.Contains("正在检查您的浏览器") ||
               lower.Contains("cf-challenge") ||
               lower.Contains("cf_chl_");
    }

    /// <summary>
    /// 通过 CDP WebSocket 获取当前页面 document.body.innerText
    /// </summary>
    private async Task<string> FetchPageTextViaCdpAsync()
    {
        var wsUrl = await GetActivePageWsUrlAsync();
        if (wsUrl == null)
            return "";

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(wsUrl), CancellationToken.None);

        var cmd = JsonSerializer.Serialize(new
        {
            id = 1,
            method = "Runtime.evaluate",
            @params = new { expression = "document.body.innerText" }
        });

        var sendBuf = Encoding.UTF8.GetBytes(cmd);
        await ws.SendAsync(new ArraySegment<byte>(sendBuf), WebSocketMessageType.Text, true, CancellationToken.None);

        var recvBuf = new byte[131072];  // 128KB 缓冲区，应对较大页面
        var result = await ws.ReceiveAsync(recvBuf, CancellationToken.None);
        var responseText = Encoding.UTF8.GetString(recvBuf, 0, result.Count);

        var responseNode = JsonNode.Parse(responseText);
        return responseNode?["result"]?["result"]?["value"]?.GetValue<string>() ?? "";
    }

    /// <summary>
    /// 获取当前 steamdb.info 活动页面的 WebSocket URL
    /// </summary>
    private async Task<string?> GetActivePageWsUrlAsync()
    {
        using var client = new HttpClient();
        var listJson = await client.GetStringAsync($"{ChromeDebugUrl}/json/list");
        var pages = JsonSerializer.Deserialize<JsonArray>(listJson);

        if (pages == null) return null;

        foreach (var page in pages)
        {
            var url = page!["url"]?.GetValue<string>() ?? "";
            if (url.Contains("steamdb.info"))
            {
                return page["webSocketDebuggerUrl"]?.GetValue<string>();
            }
        }

        return null;
    }

    /// <summary>
    /// 通过 CDP 导航到指定 URL
    /// </summary>
    private async Task NavigateToAsync(string url)
    {
        var wsUrl = await GetActivePageWsUrlAsync();
        if (wsUrl == null) return;

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(wsUrl), CancellationToken.None);

        var cmd = JsonSerializer.Serialize(new
        {
            id = 2,
            method = "Page.navigate",
            @params = new { url }
        });

        var sendBuf = Encoding.UTF8.GetBytes(cmd);
        await ws.SendAsync(new ArraySegment<byte>(sendBuf), WebSocketMessageType.Text, true, CancellationToken.None);

        // 等待导航响应
        var recvBuf = new byte[4096];
        try { await ws.ReceiveAsync(recvBuf, CancellationToken.None); } catch { }
    }

    /// <summary>
    /// 从 Depots 页面文本中提取 BuildID
    /// </summary>
    private static string ExtractBuildId(string text)
    {
        // steamdb.info depots 页面中 public 行格式：public  XXXXXX
        var match = Regex.Match(text, @"public\s+(\d+)");
        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>
    /// 从 Manifests 页面文本中提取最新的 Manifest GID
    /// </summary>
    private static string ExtractManifestGid(string text)
    {
        // Manifest GID 是 19 位数字
        var match = Regex.Match(text, @"(\d{19})");
        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>
    /// 关闭占用调试端口的已有 Chrome 实例
    /// </summary>
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

        await Task.Delay(1500);  // 等待端口释放
    }

    /// <summary>
    /// 查找 Chrome 可执行文件路径
    /// </summary>
    private static string? FindChrome()
    {
        var candidates = new[]
        {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Google\Chrome\Application\chrome.exe")
        };

        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        return null;
    }

    /// <summary>
    /// 获取进程的命令行参数（用于精确匹配 Chrome 实例）
    /// </summary>
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
