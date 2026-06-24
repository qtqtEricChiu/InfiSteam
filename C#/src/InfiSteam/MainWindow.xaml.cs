using InfiSteam.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InfiSteam;

public partial class MainWindow : Window
{
    private readonly SteamDetector _detector = new();
    private readonly AcfManager _acfManager = new();
    private readonly SteamDBScraper _scraper = new();
    private readonly StandaloneLauncherDetector _launcherDetector = new();

    private string _steamPath = "";
    private string _acfPath = "";
    private string _gamePath = "";
    private string _localBuildId = "";
    private string _localManifestGid = "";
    private string _remoteBuildId = "";
    private string _remoteManifestGid = "";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Auto-detect standalone launcher on startup
        Dispatcher.InvokeAsync(() => CheckStandaloneLauncher());
    }

    // ─── Helpers ────────────────────────────────────────

    private void SetStatus(string text, bool isError = false, bool isWarning = false, bool isSuccess = false)
    {
        Dispatcher.Invoke(() =>
        {
            txtStatus.Text = text;
            txtBottomStatus.Text = text;
            borderStatus.Background = isError ? new SolidColorBrush(Color.FromRgb(255, 220, 220))
                : isWarning ? new SolidColorBrush(Color.FromRgb(255, 245, 210))
                : isSuccess ? new SolidColorBrush(Color.FromRgb(210, 245, 210))
                : new SolidColorBrush(Color.FromRgb(227, 243, 253));
        });
    }

    private void AddLog(string msg)
    {
        Dispatcher.Invoke(() =>
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            lvLog.Items.Add(line);
            if (lvLog.Items.Count > 0)
                lvLog.ScrollIntoView(lvLog.Items[lvLog.Items.Count - 1]);
        });
    }

    private void SetBusy(bool busy)
    {
        Dispatcher.Invoke(() =>
        {
            progressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            btnDetect.IsEnabled = !busy;
            btnSteamDB.IsEnabled = !busy && !string.IsNullOrEmpty(_acfPath);
            btnUpdate.IsEnabled = !busy && !string.IsNullOrEmpty(_acfPath);
            btnVerify.IsEnabled = !busy && !string.IsNullOrEmpty(_acfPath);
        });
    }

    private static void SetIcon(TextBlock tb, string icon)
    {
        tb.Text = icon;
        tb.Foreground = icon == "✓"
            ? new SolidColorBrush(Color.FromRgb(0, 150, 0))
            : icon == "✗"
                ? new SolidColorBrush(Color.FromRgb(200, 0, 0))
                : new SolidColorBrush(Color.FromRgb(200, 160, 0));
    }

    // ─── Button Handlers ────────────────────────────────

    private void BtnDetect_Click(object sender, RoutedEventArgs e)
    {
        _steamPath = _acfPath = _gamePath = "";
        _localBuildId = _localManifestGid = "";
        SetBusy(true);
        SetStatus("正在检测 Steam 安装...", isWarning: false);
        AddLog("[i] 开始检测 Steam 安装...");

        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var result = _detector.Detect();
                if (!result.Found)
                {
                    SetStatus("未找到 Steam 安装", isWarning: true);
                    AddLog("[WARN] 未找到 Steam 安装");
                    return;
                }

                _steamPath = result.SteamPath;
                _acfPath = result.AcfPath;
                _gamePath = result.GamePath;

                txtSteamPath.Text = _steamPath;
                txtGamePath.Text = _gamePath;
                txtAcfPath.Text = _acfPath;
                SetIcon(icoSteam, "✓");
                SetIcon(icoGame, Directory.Exists(_gamePath) ? "✓" : "✗");
                SetIcon(icoAcf, File.Exists(_acfPath) ? "✓" : "✗");

                AddLog($"[OK] Steam: {_steamPath}");
                AddLog($"[OK] 游戏: {_gamePath}");

                if (File.Exists(_acfPath))
                {
                    var acf = _acfManager.Read(_acfPath);
                    _localBuildId = acf.BuildId;
                    _localManifestGid = acf.ManifestGid;

                    txtLocalBuildId.Text = _localBuildId;
                    txtLocalManifest.Text = _localManifestGid;
                    txtStateFlags.Text = acf.StateFlags;
                    txtTargetBuildId.Text = acf.TargetBuildId;
                    txtAutoUpdate.Text = acf.AutoUpdateBehavior;
                    txtBytesToDownload.Text = acf.BytesToDownload;

                    bool isRo = _acfManager.IsReadOnly(_acfPath);
                    txtReadOnly.Text = isRo ? "是" : "否";
                    SetIcon(icoReadOnly, isRo ? "✓" : "✗");

                    SetIcon(icoStateFlags, acf.StateFlags == "4" ? "✓" : "✗");
                    SetIcon(icoTargetBuildId, acf.TargetBuildId == "0" ? "✓" : "✗");
                    SetIcon(icoAutoUpdate, acf.AutoUpdateBehavior == "1" ? "✓" : "✗");
                    SetIcon(icoBytesToDownload, acf.BytesToDownload == "0" ? "✓" : "✗");

                    bool isChina = _acfManager.IsChinaVersion(acf.RawContent, _gamePath);
                    txtVersionType.Text = isChina ? "中国市场版" : "国际版";
                    SetIcon(icoVersion, "✓");

                    AddLog($"[OK] BuildID={_localBuildId}, Manifest={_localManifestGid}");
                    AddLog($"[OK] 版本类型: {txtVersionType.Text}");

                    btnSteamDB.IsEnabled = true;
                    btnUpdate.IsEnabled = false;
                    btnVerify.IsEnabled = true;
                }
                else
                {
                    AddLog("[!] ACF 文件不存在");
                    SetIcon(icoAcf, "✗");
                    btnSteamDB.IsEnabled = false;
                    btnVerify.IsEnabled = false;
                }

                if (_detector.IsSteamRunning())
                {
                    SetIcon(icoSteam, "⚠");
                    AddLog("[WARN] Steam 正在运行，更新 ACF 前请先退出 Steam");
                    SetStatus("Steam 正在运行，请先退出 Steam 再更新", isWarning: true);
                }
                else
                {
                    SetStatus("检测完成", isSuccess: true);
                }

                // 检测独立启动器
                CheckStandaloneLauncher();
            }
            catch (Exception ex)
            {
                SetStatus($"检测失败: {ex.Message}", isError: true);
                AddLog($"[ERROR] {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        });
    }

    private async void BtnSteamDB_Click(object sender, RoutedEventArgs e)
    {
        if (_detector.IsSteamRunning())
        {
            SetStatus("请先退出 Steam 再继续", isWarning: true);
            return;
        }

        SetBusy(true);
        HideRetryButton();
        SetStatus("正在连接 SteamDB...");
        AddLog("[i] 正在启动 Chrome 获取 SteamDB 数据...");

        try
        {
            var progress = new Progress<string>(msg =>
            {
                Dispatcher.Invoke(() =>
                {
                    SetStatus(msg);
                    AddLog($"[i] {msg}");
                });
            });

            var scriptDir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
            var result = await _scraper.FetchLatestAsync(scriptDir, progress);

            ProcessSteamDBResult(result);
        }
        catch (Exception ex)
        {
            SetStatus($"SteamDB 查询失败: {ex.Message}", isError: true);
            AddLog($"[ERROR] {ex.Message}");
            // 如果 Chrome 还在运行，允许重试
            if (_scraper.IsChromeAlive())
                ShowRetryButton();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void BtnRetry_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        HideRetryButton();
        SetStatus("正在重试获取数据...");
        AddLog("[i] 正在从当前 Chrome 页面重新获取数据...");

        try
        {
            var progress = new Progress<string>(msg =>
            {
                Dispatcher.Invoke(() =>
                {
                    SetStatus(msg);
                    AddLog($"[i] {msg}");
                });
            });

            var result = await _scraper.RetryFetchAsync(progress);
            ProcessSteamDBResult(result);
        }
        catch (Exception ex)
        {
            SetStatus($"重试失败: {ex.Message}", isError: true);
            AddLog($"[ERROR] 重试失败: {ex.Message}");
            if (_scraper.IsChromeAlive())
                ShowRetryButton();
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// 处理 SteamDB 查询结果（更新 UI、对比版本、启用/禁用按钮）
    /// 如果数据为空，显示警告并允许重试
    /// </summary>
    private void ProcessSteamDBResult(SteamDBScraper.SteamDBResult result)
    {
        _remoteBuildId = result.BuildId;
        _remoteManifestGid = result.ManifestGid;

        txtRemoteBuildId.Text = _remoteBuildId;
        txtRemoteManifest.Text = _remoteManifestGid;

        if (string.IsNullOrEmpty(_remoteBuildId) || string.IsNullOrEmpty(_remoteManifestGid))
        {
            SetStatus("未能解析到完整数据，可点击「重试」重新获取（无需重启 Chrome）", isWarning: true);
            SetIcon(icoBuild, string.IsNullOrEmpty(_remoteBuildId) ? "✗" : "✓");
            SetIcon(icoManifest, string.IsNullOrEmpty(_remoteManifestGid) ? "✗" : "✓");
            AddLog("[WARN] SteamDB 数据解析不完整");
            AddLog("[TIP] 如果 Chrome 页面还在 Cloudflare 验证中，请等待验证完成后再点击「重试」");
            if (_scraper.IsChromeAlive())
                ShowRetryButton();
            return;
        }

        SetIcon(icoBuild, _localBuildId == _remoteBuildId ? "✓" : "✗");
        SetIcon(icoManifest, _localManifestGid == _remoteManifestGid ? "✓" : "✗");

        AddLog($"[OK] SteamDB BuildID={_remoteBuildId}, Manifest={_remoteManifestGid}");

        if (_localBuildId == _remoteBuildId && _localManifestGid == _remoteManifestGid)
        {
            SetStatus("版本已是最新，无需更新", isSuccess: true);
            AddLog("[OK] 版本已是最新");
        }
        else
        {
            SetStatus("发现新版本，可以点击「更新 ACF」", isWarning: true);
            AddLog("[!] 发现新版本，需要更新 ACF");
            btnUpdate.IsEnabled = true;
        }
    }

    private void ShowRetryButton()
    {
        Dispatcher.Invoke(() =>
        {
            btnRetry.Visibility = Visibility.Visible;
            btnRetry.IsEnabled = true;
        });
    }

    private void HideRetryButton()
    {
        Dispatcher.Invoke(() =>
        {
            btnRetry.Visibility = Visibility.Collapsed;
            btnRetry.IsEnabled = false;
        });
    }

    private void PromptChromeProfileCleanup()
    {
        try
        {
            var scriptDir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
            var profileDir = Path.Combine(scriptDir, "chrome-profile-steamdb");
            if (Directory.Exists(profileDir))
            {
                AddLog("[i] 本次检测生成的 Chrome 临时用户文件夹可以清理");
                var result = MessageBox.Show(
                    "本次检测生成的 Chrome 临时用户文件夹 (chrome-profile-steamdb) 需要清理吗？\n\n点击「是」将删除该文件夹，点击「否」保留。",
                    "清理 Chrome 临时文件",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        Directory.Delete(profileDir, true);
                        AddLog("[OK] chrome-profile-steamdb 已清理");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[WARN] 清理失败: {ex.Message}");
                    }
                }
                else
                {
                    AddLog("[i] 已保留 chrome-profile-steamdb 文件夹");
                }
            }
        }
        catch { }
    }

    private void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_remoteBuildId) || string.IsNullOrEmpty(_remoteManifestGid))
        {
            SetStatus("请先执行 SteamDB 检测", isWarning: true);
            return;
        }
        if (_detector.IsSteamRunning())
        {
            SetStatus("请先退出 Steam 再继续", isWarning: true);
            return;
        }
        if (!File.Exists(_acfPath))
        {
            SetStatus("ACF 文件不存在", isError: true);
            return;
        }

        SetBusy(true);
        AddLog($"[i] 正在更新 ACF: BuildID={_remoteBuildId}, Manifest={_remoteManifestGid}");

        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await Task.Run(() => _acfManager.Update(_acfPath, _remoteBuildId, _remoteManifestGid));

                var acf = _acfManager.Read(_acfPath);
                _localBuildId = acf.BuildId;
                _localManifestGid = acf.ManifestGid;
                txtLocalBuildId.Text = _localBuildId;
                txtLocalManifest.Text = _localManifestGid;
                txtStateFlags.Text = "4";
                txtTargetBuildId.Text = "0";
                txtAutoUpdate.Text = "1";
                txtBytesToDownload.Text = "0";
                txtReadOnly.Text = "是";

                SetIcon(icoStateFlags, "✓");
                SetIcon(icoTargetBuildId, "✓");
                SetIcon(icoAutoUpdate, "✓");
                SetIcon(icoBytesToDownload, "✓");
                SetIcon(icoReadOnly, "✓");
                SetIcon(icoBuild, "✓");
                SetIcon(icoManifest, "✓");

                SetStatus("ACF 已更新并锁定只读", isSuccess: true);
                AddLog("[OK] ACF 已更新并锁定只读");
            }
            catch (Exception ex)
            {
                SetStatus($"更新失败: {ex.Message}", isError: true);
                AddLog($"[ERROR] {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        });
    }

    private void BtnVerify_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        AddLog("======== 开始验证 ========");

        Dispatcher.InvokeAsync(async () =>
        {
            bool allOk = true;

            await Task.Run(async () =>
            {
                await Task.Delay(50); // allow UI to update

                Dispatcher.Invoke(() =>
                {
                    // ACF exists
                    if (File.Exists(_acfPath))
                        AddLog("[OK] ACF 文件存在");
                    else { AddLog("[X] ACF 文件不存在"); allOk = false; }

                    // ACF fields
                    if (File.Exists(_acfPath))
                    {
                        var acf = _acfManager.Read(_acfPath);
                        CheckField("StateFlags", acf.StateFlags, "4", ref allOk);
                        CheckField("TargetBuildID", acf.TargetBuildId, "0", ref allOk);
                        CheckField("AutoUpdateBehavior", acf.AutoUpdateBehavior, "1", ref allOk);
                        CheckField("BytesToDownload", acf.BytesToDownload, "0", ref allOk);

                        bool isRo = _acfManager.IsReadOnly(_acfPath);
                        if (isRo) AddLog("[OK] ACF 只读锁定");
                        else { AddLog("[X] ACF 未锁定只读"); allOk = false; }
                    }

                    if (Directory.Exists(_gamePath))
                        AddLog("[OK] 游戏目录存在");
                    else { AddLog("[X] 游戏目录不存在"); allOk = false; }

                    var launcherPath = Path.Combine(_gamePath, "launcher.exe");
                    if (File.Exists(launcherPath))
                        AddLog("[OK] 启动器存在");
                    else
                        AddLog("[WARN] launcher.exe 不存在");

                    if (allOk)
                    {
                        SetStatus("验证通过，一切正常", isSuccess: true);
                        AddLog("======== 验证通过 ========");
                    }
                    else
                    {
                        SetStatus("验证发现问题，请检查日志", isWarning: true);
                        AddLog("======== 验证未通过 ========");
                    }
                });
            });
        });
    }

    private void CheckField(string name, string actual, string expected, ref bool allOk)
    {
        if (actual == expected)
            AddLog($"[OK] {name}={actual}");
        else
        {
            AddLog($"[X] {name}={actual} (应为 {expected})");
            allOk = false;
        }
    }

    // ─── Standalone Launcher ───────────────────────────

    private void CheckStandaloneLauncher(bool silent = false)
    {
        try
        {
            var launchers = _launcherDetector.Detect();
            if (launchers.Count == 0)
            {
                borderLauncher.Visibility = Visibility.Collapsed;
                return;
            }

            var first = launchers[0];
            txtLaunchOption.Text = first.LaunchOption;
            borderLauncher.Visibility = Visibility.Visible;

            AddLog($"[i] 检测到独立启动器: {first.ExePath}");
            if (!string.IsNullOrEmpty(first.GamePath))
                AddLog($"[i]   独立启动器游戏路径: {first.GamePath}");
            AddLog($"[i] 启动选项: {first.LaunchOption}");
            AddLog($"[TIP] 在 Steam 游戏属性 → 启动选项中填入以上命令");

            if (!silent)
            {
                SetStatus($"检测到独立启动器，建议配置 Steam 启动选项", isWarning: true);
            }
        }
        catch (Exception ex)
        {
            if (!silent)
                AddLog($"[WARN] 独立启动器检测失败: {ex.Message}");
        }
    }

    private void BtnCopyLaunchOption_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = txtLaunchOption.Text;
            Clipboard.SetText(text);
            txtCopyConfirm.Text = "✓ 已复制到剪贴板";
            AddLog($"[OK] 启动选项已复制到剪贴板");
        }
        catch (Exception ex)
        {
            txtCopyConfirm.Text = $"✗ 复制失败: {ex.Message}";
        }
    }

    private void BtnCloseChrome_Click(object sender, RoutedEventArgs e)
    {
        _scraper.CloseChrome();
        AddLog("[i] Chrome 已关闭");
        SetStatus("Chrome 已关闭", isSuccess: true);

        // Prompt user about chrome-profile-steamdb cleanup after closing Chrome
        PromptChromeProfileCleanup();
    }
}
