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
            btnSkeletonize.IsEnabled = !busy && !string.IsNullOrEmpty(_gamePath);
            btnRestore.IsEnabled = !busy && !string.IsNullOrEmpty(_gamePath);
            btnResidual.IsEnabled = !busy && !string.IsNullOrEmpty(_acfPath);
            btnNetDiag.IsEnabled = !busy;
            btnReport.IsEnabled = !busy && !string.IsNullOrEmpty(_steamPath);
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

    // ─── 新增：免责声明 ──────────────────────────────

    private void BtnDisclaimer_Click(object sender, RoutedEventArgs e)
    {
        ShowDisclaimer();
    }

    private void ShowDisclaimer()
    {
        var ci = System.Globalization.CultureInfo.CurrentUICulture;
        bool isCn = ci.Name.StartsWith("zh");

        string text, title;
        if (isCn)
        {
            title = "版权声明";
            text = "© mocabolka 2026\n\n"
                + "本工具与 Valve/Steam、SteamDB、叠纸游戏/Infold Games 无关。\n"
                + "仅供学习交流使用。";
        }
        else
        {
            title = "Copyright Notice";
            text = "© mocabolka 2026\n\n"
                + "This tool is not affiliated with Valve/Steam, SteamDB, or Papergames/Infold Games.\n"
                + "For learning and exchange purposes only.";
        }

        MessageBox.Show(text, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ─── 新增：残留文件检查 ──────────────────────────

    private void BtnResidual_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        AddLog("======== 残留文件检查 ========");

        Dispatcher.InvokeAsync(async () =>
        {
            await Task.Run(() =>
            {
                var result = AcfManager.CheckResidualFiles(_acfPath);
                Dispatcher.Invoke(() =>
                {
                    foreach (var line in result.Split('\n'))
                        if (!string.IsNullOrWhiteSpace(line))
                            AddLog(line.TrimEnd('\r'));
                    SetStatus("残留检查完成", isSuccess: true);
                    SetBusy(false);
                });
            });
        });
    }

    // ─── 新增：骨架化（调用 infi-manager.ps1）───────

    private void BtnSkeletonize_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "即将执行骨架化清理：将 X6Game (~110GB) 移至同盘备份目录。\n\nSteam 必须已完全退出。是否继续？",
            "骨架化清理",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
            RunPSCommand("skeletonize");
    }

    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "即将从备份目录还原 X6Game 到 Steam 目录。\n\nSteam 必须已完全退出。是否继续？",
            "还原 X6Game",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
            RunPSCommand("restore");
    }

    private void RunPSCommand(string command)
    {
        SetBusy(true);

        Dispatcher.InvokeAsync(async () =>
        {
            await Task.Run(() =>
            {
                try
                {
                    var scriptDir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
                    var psScript = Path.Combine(Directory.GetParent(scriptDir)?.Parent?.Parent?.FullName ?? "",
                        "release", "AI_Prompt_with_Powershell", "infi-manager.ps1");

                    // Try to locate ps1 script relative to source dir
                    if (!File.Exists(psScript))
                    {
                        // Fallback: check common locations
                        var candidates = new[]
                        {
                            Path.Combine(scriptDir, "infi-manager.ps1"),
                            Path.Combine(Directory.GetCurrentDirectory(), "infi-manager.ps1"),
                        };
                        psScript = candidates.FirstOrDefault(File.Exists) ?? psScript;
                    }

                    if (!File.Exists(psScript))
                    {
                        psScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "infi-manager.ps1");
                    }

                    if (!File.Exists(psScript))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AddLog("[ERROR] infi-manager.ps1 not found - skeletonize/restore unavailable");
                            SetStatus("脚本未找到", isError: true);
                            SetBusy(false);
                        });
                        return;
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; & '{psScript}' {command}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        while (!proc.StandardOutput.EndOfStream)
                        {
                            var line = proc.StandardOutput.ReadLine();
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                Dispatcher.Invoke(() => AddLog(line));
                            }
                        }
                        proc.WaitForExit();
                        Dispatcher.Invoke(() =>
                        {
                            AddLog($"[i] exit code: {proc.ExitCode}");
                            SetStatus(proc.ExitCode == 0 ? "完成" : "失败",
                                      isError: proc.ExitCode != 0,
                                      isSuccess: proc.ExitCode == 0);
                            SetBusy(false);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        AddLog($"[ERROR] {ex.Message}");
                        SetStatus($"错误: {ex.Message}", isError: true);
                        SetBusy(false);
                    });
                }
            });
        });
    }

    // ─── 新增：网络诊断 ──────────────────────────────

    private void BtnNetDiag_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        AddLog("======== 网络诊断 ========");

        Dispatcher.InvokeAsync(async () =>
        {
            await Task.Run(() =>
            {
                var result = AcfManager.RunNetworkDiag();
                Dispatcher.Invoke(() =>
                {
                    foreach (var line in result.Split('\n'))
                        if (!string.IsNullOrWhiteSpace(line))
                            AddLog(line.TrimEnd('\r'));
                    SetStatus("网络诊断完成", isSuccess: true);
                    SetBusy(false);
                });
            });
        });
    }

    // ─── 新增：输出报告 ──────────────────────────────

    private void BtnReport_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        AddLog("======== 生成报告 ========");

        Dispatcher.InvokeAsync(async () =>
        {
            await Task.Run(() =>
            {
                var report = AcfManager.GenerateReport(_steamPath, _acfPath, _gamePath);
                Dispatcher.Invoke(() =>
                {
                    // 弹出报告窗口
                    var w = new Window
                    {
                        Title = "报告",
                        Width = 680,
                        Height = 520,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this,
                        Content = new Grid
                        {
                            Margin = new Thickness(15),
                            RowDefinitions =
                            {
                                new RowDefinition { Height = GridLength.Auto },
                                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                                new RowDefinition { Height = GridLength.Auto }
                            }
                        }
                    };
                    var grid = (Grid)w.Content;

                    grid.Children.Add(new TextBlock
                    {
                        Text = "📋 报告",
                        FontSize = 18,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 10)
                    });

                    var tb = new System.Windows.Controls.TextBox
                    {
                        Text = report,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    Grid.SetRow(tb, 1);
                    grid.Children.Add(tb);

                    var closeBtn = new Button
                    {
                        Content = "关闭",
                        Width = 80,
                        Height = 30,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    closeBtn.Click += (s, ev) => w.Close();
                    Grid.SetRow(closeBtn, 2);
                    grid.Children.Add(closeBtn);

                    w.ShowDialog();
                    SetStatus("报告已生成", isSuccess: true);
                    SetBusy(false);
                });
            });
        });
    }
}
