using InfiSteam.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using System.Diagnostics;
using System.Text;

namespace InfiSteam.WinUI;

public sealed partial class MainWindow : Window
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

    private bool _firstActivation = true;
    private bool _hasShownGuide = false;
    private bool _isMaximized = false;
    private DateTime _lastToastTime = DateTime.MinValue;
    private bool _toastVisible = false;

    [DllImport("user32.dll")]
    private static extern nint GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "InfiSteam";
        this.SystemBackdrop = new MicaBackdrop();
        LoadAppIcon();
        this.Activated += OnActivated;
    }

    private void LoadAppIcon()
    {
        try
        {
            var iconPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
                "ico.ico");
            if (!File.Exists(iconPath))
                iconPath = Path.Combine(AppContext.BaseDirectory, "ico.ico");
            if (File.Exists(iconPath))
            {
                _appIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                    new Uri(iconPath, UriKind.Absolute));
            }
        }
        catch
        {
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_firstActivation)
        {
            _firstActivation = false;
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                
                appWindow.Resize(new SizeInt32(1100, 780));
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;
                var iconFile = Path.Combine(AppContext.BaseDirectory, "ico.ico");
                if (File.Exists(iconFile))
                    appWindow.SetIcon(iconFile);

                appWindow.Changed += (aw, _) =>
                {
                    var dragRect = new RectInt32(0, 0, aw.Size.Width - 160, 48);
                    aw.TitleBar.SetDragRectangles([dragRect]);
                };
            }
            catch { }
        }

        // 延迟显示引导（等待 XamlRoot 就绪）
        _ = DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(500);
            ShowFirstRunGuide();
        });

        if (_detector != null)
        {
            CheckStandaloneLauncher(silent: true);
        }
    }

    private async void ShowFirstRunGuide(bool force = false)
    {
        if (_hasShownGuide && !force) return;
        _hasShownGuide = true;

        var stack = new StackPanel { Spacing = 16 };
        stack.Children.Add(new TextBlock
        {
            Text = "欢迎使用 InfiSteam",
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = "使用步骤：\r\n"
                  + "1️⃣ 点击「检测 Steam」→ 自动定位 Steam 安装路径\r\n"
                  + "2️⃣ 点击「查询 SteamDB」→ 打开 Chrome 获取最新版本\r\n"
                  + "   (如遇 Cloudflare 验证，完成后点击「重试」)\r\n"
                  + "3️⃣ 对比本地与 SteamDB 最新版本\r\n"
                  + "4️⃣ 如需更新，点击「更新 ACF」\r\n"
                  + "5️⃣ 点击「验证」确认配置\r\n\r\n"
                  + "💡 如有独立启动器，会自动提示",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            LineHeight = 22
        });

        // ContentDialog 默认自带淡入动画，无需额外处理
        var dialog = new ContentDialog
        {
            Title = "快速上手指南",
            Content = stack,
            PrimaryButtonText = "开始使用",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    /// <summary>允许用户反复查看快速上手指南</summary>
    private void BtnGuide_Click(object sender, RoutedEventArgs e)
    {
        ShowFirstRunGuide(force: true);
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
        ShowWindow(WinRT.Interop.WindowNative.GetWindowHandle(this), 6); // SW_MINIMIZE

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (_isMaximized)
            ShowWindow(hwnd, 9); // SW_RESTORE
        else
            ShowWindow(hwnd, 3); // SW_MAXIMIZE
        _isMaximized = !_isMaximized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private async void BtnAbout_Click(object sender, RoutedEventArgs e)
    {
        var stack = new StackPanel { Spacing = 16 };

        stack.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children =
            {
                new Image
                {
                    Width = 48,
                    Height = 48,
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                        new Uri(Path.Combine(AppContext.BaseDirectory, "ico.ico"), UriKind.Absolute))
                },
                new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "InfiSteam",
                            FontSize = 22,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                        },
                        new TextBlock
                        {
                            Text = "无限暖暖 Steam 壳管理工具",
                            FontSize = 14,
                            Opacity = 0.7
                        }
                    }
                }
            }
        });

        var separator = new Microsoft.UI.Xaml.Shapes.Line
        {
            X1 = 0, X2 = 400, Y1 = 0, Y2 = 0,
            Stroke = new SolidColorBrush(Colors.Gray),
            StrokeThickness = 0.5,
            Opacity = 0.3
        };
        stack.Children.Add(separator);

        stack.Children.Add(new TextBlock
        {
            Text = "版本: v5.0 (WinUI 3)\r\n"
                 + "框架: .NET 8.0 + Windows App SDK 1.6\r\n"
                 + "运行时: Windows Runtime (WinRT)",
            FontSize = 13,
            Opacity = 0.8
        });

        var githubLink = new HyperlinkButton
        {
            Content = "🌐 https://github.com/qtqtEricChiu/infisteam/",
            NavigateUri = new Uri("https://github.com/qtqtEricChiu/infisteam/"),
            FontSize = 13
        };
        stack.Children.Add(githubLink);

        stack.Children.Add(new TextBlock
        {
            Text = "©mocabolka with CodeBuddy",
            FontSize = 12,
            Opacity = 0.5,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var dialog = new ContentDialog
        {
            Title = "关于 InfiSteam",
            Content = stack,
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    // ─── Helpers ────────────────────────────────────────

    private void SetStatus(string text, bool isError = false, bool isWarning = false, bool isSuccess = false)
    {
        this.DispatcherQueue?.TryEnqueue(() =>
        {
            _infoBarStatus.Message = text;
            _infoBarStatus.Title = isError ? "错误" : isWarning ? "警告" : isSuccess ? "成功" : "信息";
            _infoBarStatus.Severity = isError ? InfoBarSeverity.Error
                : isWarning ? InfoBarSeverity.Warning
                : isSuccess ? InfoBarSeverity.Success
                : InfoBarSeverity.Informational;
            _infoBarStatus.IsOpen = true;
        });
    }

    private void AddLog(string msg)
    {
        this.DispatcherQueue?.TryEnqueue(() =>
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            _lvLog.Items.Add(line);
            if (_lvLog.Items.Count > 0)
            {
                _lvLog.ScrollIntoView(_lvLog.Items[^1]);
            }
            _logCount.Text = $"({_lvLog.Items.Count})";

            // Toast 通知显示
            ShowToast(msg);
        });
    }

    private void ShowToast(string msg)
    {
        // 节流：只弹关键信息，3秒内不重复弹出
        var now = DateTime.Now;
        if ((now - _lastToastTime).TotalMilliseconds < 3000 || _toastVisible)
            return;
        _lastToastTime = now;
        _toastVisible = true;

        this.DispatcherQueue?.TryEnqueue(async () =>
        {
            var icon = msg.StartsWith("[ERROR]") ? "\uE9E9"
                     : msg.StartsWith("[WARN]") || msg.StartsWith("[!]") ? "\uE7BA"
                     : msg.StartsWith("[OK]") ? "\uE73E"
                     : "\uE946";
            _toastIcon.Glyph = icon;
            _toastText.Text = msg;
            _toastBorder.Visibility = Visibility.Visible;
            _toastBorder.Opacity = 0;

            // 弹入动画
            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var fadeIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0, To = 1,
                Duration = TimeSpan.FromMilliseconds(200),
                EnableDependentAnimation = true
            };
            var slideUp = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 50, To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EnableDependentAnimation = true,
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeIn, _toastBorder);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeIn, "Opacity");
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideUp, _toastTransform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideUp, "Y");
            sb.Children.Add(fadeIn);
            sb.Children.Add(slideUp);
            sb.Begin();

            await Task.Delay(3000);

            // 淡出动画
            var fadeOut = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 1, To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EnableDependentAnimation = true
            };
            var sb2 = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeOut, _toastBorder);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeOut, "Opacity");
            sb2.Children.Add(fadeOut);
            sb2.Begin();

            await Task.Delay(300);
            _toastBorder.Visibility = Visibility.Collapsed;
            _toastVisible = false;
        });
    }

    private void SetBusy(bool busy)
    {
        this.DispatcherQueue?.TryEnqueue(() =>
        {
            _progressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            _btnDetect.IsEnabled = !busy;
            _btnSteamDB.IsEnabled = !busy && !string.IsNullOrEmpty(_acfPath);
            _btnUpdate.IsEnabled = !busy && !string.IsNullOrEmpty(_acfPath);
            _btnVerify.IsEnabled = !busy && !string.IsNullOrEmpty(_acfPath);
            _btnSkeletonize.IsEnabled = !busy && !string.IsNullOrEmpty(_gamePath);
            _btnRestore.IsEnabled = !busy && !string.IsNullOrEmpty(_gamePath);
            _btnResidual.IsEnabled = !busy && !string.IsNullOrEmpty(_acfPath);
            _btnNetDiag.IsEnabled = !busy;
            _btnReport.IsEnabled = !busy && !string.IsNullOrEmpty(_steamPath);
        });
    }

    private static void SetIcon(FontIcon icon, string state)
    {
        icon.Glyph = state switch
        {
            "✓" => "\uE001",   // Accept
            "✗" => "\uE106",   // Cancel
            "⚠" => "\uE7BA",   // Warning
            _ => "\uE001"
        };
        icon.Foreground = state == "✓" ? new SolidColorBrush(Colors.LimeGreen)
            : state == "✗" ? new SolidColorBrush(Colors.Tomato)
            : new SolidColorBrush(Colors.Orange);
    }

    // ─── Button Handlers ────────────────────────────────

    private void BtnDetect_Click(object sender, RoutedEventArgs e)
    {
        _steamPath = _acfPath = _gamePath = "";
        _localBuildId = _localManifestGid = "";
        SetBusy(true);
        SetStatus("正在检测 Steam 安装...", isWarning: false);
        AddLog("[i] 开始检测 Steam 安装...");

        this.DispatcherQueue?.TryEnqueue(async () =>
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

                _txtSteamPath.Text = _steamPath;
                _txtGamePath.Text = _gamePath;
                _txtAcfPath.Text = _acfPath;
                SetIcon(_icoStatusSteam, "✓");
                SetIcon(_icoStatusGame, Directory.Exists(_gamePath) ? "✓" : "✗");
                SetIcon(_icoStatusAcf, File.Exists(_acfPath) ? "✓" : "✗");

                AddLog($"[OK] Steam: {_steamPath}");
                AddLog($"[OK] 游戏: {_gamePath}");

                if (File.Exists(_acfPath))
                {
                    var acf = _acfManager.Read(_acfPath);
                    _localBuildId = acf.BuildId;
                    _localManifestGid = acf.ManifestGid;

                    _txtLocalBuildId.Text = _localBuildId;
                    _txtLocalManifest.Text = _localManifestGid;
                    _txtStateFlags.Text = acf.StateFlags;
                    _txtTargetBuildId.Text = acf.TargetBuildId;
                    _txtAutoUpdate.Text = acf.AutoUpdateBehavior;
                    _txtBytesToDownload.Text = acf.BytesToDownload;

                    bool isRo = _acfManager.IsReadOnly(_acfPath);
                    _txtReadOnly.Text = isRo ? "是" : "否";
                    SetIcon(_icoReadOnly, isRo ? "✓" : "✗");

                    SetIcon(_icoStateFlags, acf.StateFlags == "4" ? "✓" : "✗");
                    SetIcon(_icoTargetBuildId, acf.TargetBuildId == "0" ? "✓" : "✗");
                    SetIcon(_icoAutoUpdate, acf.AutoUpdateBehavior == "1" ? "✓" : "✗");
                    SetIcon(_icoBytesToDownload, acf.BytesToDownload == "0" ? "✓" : "✗");

                    bool isChina = _acfManager.IsChinaVersion(acf.RawContent, _gamePath);
                    _txtVersionType.Text = isChina ? "中国市场版" : "国际版";
                    // 版本类型正常

                    AddLog($"[OK] BuildID={_localBuildId}, Manifest={_localManifestGid}");
                    AddLog($"[OK] 版本类型: {_txtVersionType.Text}");

                    _btnSteamDB.IsEnabled = true;
                    _btnUpdate.IsEnabled = false;
                    _btnVerify.IsEnabled = true;
                }
                else
                {
                    AddLog("[!] ACF 文件不存在");
                    SetIcon(_icoStatusAcf, "✗");
                    _btnSteamDB.IsEnabled = false;
                    _btnVerify.IsEnabled = false;
                }

                if (_detector.IsSteamRunning())
                {
                    SetIcon(_icoStatusSteam, "⚠");
                    AddLog("[WARN] Steam 正在运行，更新 ACF 前请先退出 Steam");
                    SetStatus("Steam 正在运行，请先退出 Steam 再更新", isWarning: true);
                }
                else
                {
                    SetStatus("检测完成", isSuccess: true);
                }

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
                this.DispatcherQueue?.TryEnqueue(() =>
                {
                    SetStatus(msg);
                    AddLog($"[i] {msg}");
                });
            });

            var scriptDir = AppContext.BaseDirectory;
            var result = await _scraper.FetchLatestAsync(scriptDir, progress);

            ProcessSteamDBResult(result);
        }
        catch (Exception ex)
        {
            SetStatus($"SteamDB 查询失败: {ex.Message}", isError: true);
            AddLog($"[ERROR] {ex.Message}");
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
                this.DispatcherQueue?.TryEnqueue(() =>
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

    private void ProcessSteamDBResult(SteamDBScraper.SteamDBResult result)
    {
        _remoteBuildId = result.BuildId;
        _remoteManifestGid = result.ManifestGid;

        this.DispatcherQueue?.TryEnqueue(() =>
        {
            _txtRemoteBuildId.Text = _remoteBuildId;
            _txtRemoteManifest.Text = _remoteManifestGid;

            if (string.IsNullOrEmpty(_remoteBuildId) || string.IsNullOrEmpty(_remoteManifestGid))
            {
                SetStatus("未能解析到完整数据，可点击「重试」重新获取（无需重启 Chrome）", isWarning: true);
                SetIcon(_icoBuild, string.IsNullOrEmpty(_remoteBuildId) ? "✗" : "✓");
                SetIcon(_icoManifest, string.IsNullOrEmpty(_remoteManifestGid) ? "✗" : "✓");
                AddLog("[WARN] SteamDB 数据解析不完整");
                AddLog("[TIP] 如果 Chrome 页面还在 Cloudflare 验证中，请等待验证完成后再点击「重试」");
                if (_scraper.IsChromeAlive())
                    ShowRetryButton();
                return;
            }

            SetIcon(_icoBuild, _localBuildId == _remoteBuildId ? "✓" : "✗");
            SetIcon(_icoManifest, _localManifestGid == _remoteManifestGid ? "✓" : "✗");

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
                _btnUpdate.IsEnabled = true;
            }
        });
    }

    private void ShowRetryButton()
    {
        this.DispatcherQueue?.TryEnqueue(() =>
        {
            _btnRetry.Visibility = Visibility.Visible;
            _btnRetry.IsEnabled = true;
        });
    }

    private void HideRetryButton()
    {
        this.DispatcherQueue?.TryEnqueue(() =>
        {
            _btnRetry.Visibility = Visibility.Collapsed;
            _btnRetry.IsEnabled = false;
        });
    }

    private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
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

        try
        {
            await Task.Run(() => _acfManager.Update(_acfPath, _remoteBuildId, _remoteManifestGid));

            var acf = _acfManager.Read(_acfPath);
            _localBuildId = acf.BuildId;
            _localManifestGid = acf.ManifestGid;
            _txtLocalBuildId.Text = _localBuildId;
            _txtLocalManifest.Text = _localManifestGid;
            _txtStateFlags.Text = "4";
            _txtTargetBuildId.Text = "0";
            _txtAutoUpdate.Text = "1";
            _txtBytesToDownload.Text = "0";
            _txtReadOnly.Text = "是";

            SetIcon(_icoStateFlags, "✓");
            SetIcon(_icoTargetBuildId, "✓");
            SetIcon(_icoAutoUpdate, "✓");
            SetIcon(_icoBytesToDownload, "✓");
            SetIcon(_icoReadOnly, "✓");
            SetIcon(_icoBuild, "✓");
            SetIcon(_icoManifest, "✓");

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
    }

    private void BtnVerify_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        AddLog("======== 开始验证 ========");

        Task.Run(async () =>
        {
            await Task.Delay(50);

            this.DispatcherQueue?.TryEnqueue(() =>
            {
                bool allOk = true;

                if (File.Exists(_acfPath))
                    AddLog("[OK] ACF 文件存在");
                else { AddLog("[X] ACF 文件不存在"); allOk = false; }

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

                SetBusy(false);
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
                _borderLauncher.Visibility = Visibility.Collapsed;
                return;
            }

            var first = launchers[0];
            _txtLaunchOption.Text = first.LaunchOption;
            _borderLauncher.Visibility = Visibility.Visible;

            // 只在非静默模式下记录日志，避免重复触发 toast
            if (!silent)
            {
                AddLog($"[i] 检测到独立启动器: {first.ExePath}");
                if (!string.IsNullOrEmpty(first.GamePath))
                    AddLog($"[i]   独立启动器游戏路径: {first.GamePath}");
                AddLog($"[i] 启动选项: {first.LaunchOption}");
                AddLog($"[TIP] 在 Steam 游戏属性 → 启动选项中填入以上命令");
            }

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
            var dataPackage = new DataPackage();
            dataPackage.SetText(_txtLaunchOption.Text);
            Clipboard.SetContent(dataPackage);
            _txtCopyConfirm.Text = "✓ 已复制到剪贴板";
            AddLog($"[OK] 启动选项已复制到剪贴板");
        }
        catch (Exception ex)
        {
            _txtCopyConfirm.Text = $"✗ 复制失败: {ex.Message}";
        }
    }

    private async void BtnCloseChrome_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => _scraper.CloseChrome());
        AddLog("[i] Chrome 已关闭");
        SetStatus("Chrome 已关闭", isSuccess: true);

        await PromptChromeProfileCleanupAsync();
    }

    private async Task PromptChromeProfileCleanupAsync()
    {
        try
        {
            var scriptDir = AppContext.BaseDirectory;
            var profileDir = Path.Combine(scriptDir, "chrome-profile-steamdb");
            if (Directory.Exists(profileDir))
            {
                AddLog("[i] 本次检测生成的 Chrome 临时用户文件夹可以清理");

                var dialog = new ContentDialog
                {
                    Title = "清理 Chrome 临时文件",
                    Content = "本次检测生成的 Chrome 临时用户文件夹 (chrome-profile-steamdb) 需要清理吗？",
                    PrimaryButtonText = "是",
                    CloseButtonText = "否",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    await Task.Run(() =>
                    {
                        if (Directory.Exists(profileDir))
                            Directory.Delete(profileDir, true);
                    });
                    AddLog("[OK] chrome-profile-steamdb 已清理");
                }
                else
                {
                    AddLog("[i] 已保留 chrome-profile-steamdb 文件夹");
                }
            }
        }
        catch { }
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

        var dialog = new ContentDialog
        {
            Title = title,
            Content = text,
            CloseButtonText = "关闭 / Close",
            XamlRoot = this.Content.XamlRoot
        };
        _ = dialog.ShowAsync();
    }

    // ─── 新增：残留文件检查 ──────────────────────────

    private void BtnResidual_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        AddLog("======== 残留文件检查 ========");
        Task.Run(() =>
        {
            var result = AcfManager.CheckResidualFiles(_acfPath);
            this.DispatcherQueue?.TryEnqueue(() =>
            {
                foreach (var line in result.Split('\n'))
                    if (!string.IsNullOrWhiteSpace(line))
                        AddLog(line.TrimEnd('\r'));
                SetStatus("残留检查完成", isSuccess: true);
                SetBusy(false);
            });
        });
    }

    // ─── 新增：骨架化（调用 infi-manager.ps1）───────

    private void BtnSkeletonize_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "骨架化清理",
            Content = "即将执行骨架化清理：将 X6Game (~110GB) 移至同盘备份目录。\n\nSteam 必须已完全退出。是否继续？",
            PrimaryButtonText = "继续",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };
        _ = ShowDialogAndRun(dialog, () => RunPSCommand("skeletonize"));
    }

    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "还原 X6Game",
            Content = "即将从备份目录还原 X6Game 到 Steam 目录。\n\nSteam 必须已完全退出。是否继续？",
            PrimaryButtonText = "继续",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };
        _ = ShowDialogAndRun(dialog, () => RunPSCommand("restore"));
    }

    private async System.Threading.Tasks.Task ShowDialogAndRun(ContentDialog dialog, Action action)
    {
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            action();
    }

    private void RunPSCommand(string command)
    {
        SetBusy(true);
        Task.Run(() =>
        {
            try
            {
                // Try to locate ps1 script
                var scriptDir = AppDomain.CurrentDomain.BaseDirectory;
                var psScript = Path.Combine(scriptDir, "infi-manager.ps1");
                if (!File.Exists(psScript))
                {
                    // Also check relative to the repo
                    psScript = Path.Combine(scriptDir, "..", "..", "..", "..", "release", "AI_Prompt_with_Powershell", "infi-manager.ps1");
                }
                if (!File.Exists(psScript))
                {
                    this.DispatcherQueue?.TryEnqueue(() =>
                    {
                        AddLog("[ERROR] infi-manager.ps1 not found");
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
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    while (!proc.StandardOutput.EndOfStream)
                    {
                        var line = proc.StandardOutput.ReadLine();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            this.DispatcherQueue?.TryEnqueue(() => AddLog(line));
                        }
                    }
                    proc.WaitForExit();
                    this.DispatcherQueue?.TryEnqueue(() =>
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
                this.DispatcherQueue?.TryEnqueue(() =>
                {
                    AddLog($"[ERROR] {ex.Message}");
                    SetStatus($"错误: {ex.Message}", isError: true);
                    SetBusy(false);
                });
            }
        });
    }

    // ─── 新增：网络诊断 ──────────────────────────────

    private void BtnNetDiag_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        AddLog("======== 网络诊断 ========");
        Task.Run(() =>
        {
            var result = AcfManager.RunNetworkDiag();
            this.DispatcherQueue?.TryEnqueue(() =>
            {
                foreach (var line in result.Split('\n'))
                    if (!string.IsNullOrWhiteSpace(line))
                        AddLog(line.TrimEnd('\r'));
                SetStatus("网络诊断完成", isSuccess: true);
                SetBusy(false);
            });
        });
    }

    // ─── 新增：输出报告 ──────────────────────────────

    private async void BtnReport_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        var report = await Task.Run(() => AcfManager.GenerateReport(_steamPath, _acfPath, _gamePath));

        var scroll = new ScrollViewer { MaxHeight = 400 };
        var tb = new TextBlock
        {
            Text = report,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        };
        scroll.Content = tb;

        var dialog = new ContentDialog
        {
            Title = "📋 报告",
            Content = scroll,
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        await dialog.ShowAsync();
        SetStatus("报告已生成", isSuccess: true);
        SetBusy(false);
    }
}
