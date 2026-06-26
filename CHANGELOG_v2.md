# 无限暖暖 Steam 壳管理工具 - 更新日志

## v5.1.2 — WinUI 3 稳定性修复 + 构建脚本优化

### 修复
- **WinUI 3 启动崩溃（0xC000027B）**：排查并修复多项导致 XAML 内部异常的问题
  - `BitmapImage` 不支持 `.ico` 格式 → `LoadAppIcon()` 改用 `AppWindow.SetIcon()` + `ico.png`
  - XAML 中 `LayerOnMicaBaseAltFillColorDefaultBrush` 资源初始化冲突 → 改用标准资源
  - 扩展按钮从 XAML 静态声明改为 C# 代码动态创建，避免 XAML 解析异常
  - `OnActivated` 中遗失 `if (_firstActivation)` 的闭合括号 → 补回
  - 全局异常捕获：`Application.UnhandledException` + `AppDomain.CurrentDomain.UnhandledException`
- **构建脚本 `build_all.bat`**：
  - 选项 1/2/3 使用 `goto` 导致 `exit /b 0` 终止整个脚本 → 改为 `call :build`
  - `chcp 65001` 编码问题导致中文乱码 → 输出全部改为英文
  - `echo(` 语法在某些环境不支持 → 恢复为普通 `echo`
  - `SetBusy()` 中引用已删除的 XAML 按钮名称 → 移除

### 优化
- **WinUI 3 扩展按钮**：版权声明、残留检查、骨架化、还原、网络诊断、报告等 6 个按钮由 XAML 转为 C# 动态生成，运行稳定
- **WinUI 3 关于对话框**：图标从 FontIcon 换为 `ico.png`（BitmapImage 原生支持 PNG）
- **构建脚本**：移除 `setlocal enabledelayedexpansion` 避免特殊字符冲突

### 文件
- `C#/InfiSteam.WinUI/ico.png` — 新增（窗口内图标显示用）
- `C#/InfiSteam.WinUI/MainWindow.xaml` — 扩展功能区简化为空容器
- `C#/InfiSteam.WinUI/MainWindow.xaml.cs` — `CreateExtensionButtons()` 动态生成按钮
- `C#/InfiSteam.WinUI/App.xaml.cs` — 全局异常捕获

---

## v5.1.1 — 功能对齐 + 四版本同步 + Mica 背景

### 新增
- **Python GUI Pro（全新轻量化方案）**：移植自 C# WPF 的完整 Steam 检测、ACF 读取、版本类型判断、启动器检测功能，使用 customtkinter 现代界面
- **新手引导系统**：标题栏 ❓ 按钮弹出功能说明对话框 + 按钮悬停 0.5s 自动显示 tooltip
- **骨架化模拟（DryRun）**：预览骨架化操作结果但不实际执行
- **版权声明**：按钮可手动查看，中/英根据系统语言独立显示
- **残留文件检查**：检查 ACF 临时文件、残留备份、downloading/temp 目录中的游戏残留
- **输出完整报告**：以独立窗口弹出，含路径/版本/ACF 状态/X6Game 位置/启动器检测
- **网络诊断**：Ping 检测 steamdb.info / cloudflare.com / google.com
- **统一构建脚本** `build_all.bat`：一键编译 Python GUI + C# WPF + C# WinUI 3
- **Python GUI Mica 背景**：使用 DwmSetWindowAttribute + DwmExtendFrameIntoClientArea 实现 100% 云母效果
- **Python GUI 响应式布局**：窗口 <720px 自动切换到窄屏模式，右栏可滚动

### 功能对齐（四版本同步）
| 功能 | Prompt | Python GUI | C# WPF | C# WinUI3 |
|------|:------:|:----------:|:------:|:---------:|
| Steam 路径检测 | ✅ | ✅ | ✅ | ✅ |
| 多库支持 | ✅ | ✅ | ✅ | ✅ |
| 版本类型检测(3路) | ✅ | ✅ | ✅ | ✅ |
| 残留文件检查 | ✅ | ✅ 新 | ✅ 新 | ✅ 新 |
| ACF 更新(含字段清零) | ✅ | ✅ | ✅ | ✅ |
| BytesToStage/BytesStaged 清零 | ✅ | ✅ 新 | ✅ 新 | ✅ 新 |
| 骨架化(移动X6Game) | ✅ | ✅ | ✅ 新 | ✅ 新 |
| 还原(恢复X6Game) | ✅ | ✅ | ✅ 新 | ✅ 新 |
| 骨架化模拟(DryRun) | — | ✅ | — | — |
| 网络诊断 | ✅ | ✅ | ✅ 新 | ✅ 新 |
| 输出完整报告 | ✅ | ✅ 新 | ✅ 新 | ✅ 新 |
| 版权声明 | ✅ | ✅ 新 | ✅ 新 | ✅ 新 |

### 修复
- **WinUI 3 ContentDialog 冲突**：`ShowFirstRunGuide` + `ShowDisclaimer` 同时弹出导致 `0xC000027B` → 取消自动弹出版权声明
- **WinUI 3 XAML 编译失败**：`FontIcon` 无效 Glyph 值导致编译器崩溃 → 改用纯 `TextBlock`
- **WinUI 3 `Process` 未定义**：`AcfManager.cs` 缺少 `using System.Diagnostics;`
- **WinUI 3 多余括号**：`BtnReport_Click` 尾部多余 `}` 导致 CS1022
- **Python GUI 版本类型误判**：旧逻辑 `"3164332" in txt and "China" in txt` → 修复为三路检测（sub/1221922 / schinese / China.pak）
- **Python GUI 子进程弹出 cmd 窗口**：添加 `_SUPPRESS` + `DETACHED_PROCESS` 三重隐藏
- **构建脚本 `echo [1/3]` 错误解析**：方括号被误认为命令 → 全部替换为 `echo(`

### 优化
- **Python GUI 100% Mica 背景**：窗口完全透明 + DwmExtendFrameIntoClientArea，Mica 覆盖整个客户区
- **Python GUI 响应式**：绑定 `<Configure>` 事件，<720px 自动切换窄屏按钮尺寸
- **Python GUI 标题栏简洁化**：只显示 `InfiSteam`
- **Python GUI 新窗口统一图标**：引导/声明/报告弹窗均设置 `ico.ico`
- **Python GUI 报告弹窗**：`cmd_report()` 不再写入日志，弹出独立 680x500 窗口
- **C# 版权声明双语分离**：根据 `CultureInfo.CurrentUICulture` 只显示对应语言
- **C# 报告弹窗**：WPF 弹出独立 Window，WinUI 3 弹出 ContentDialog
- **WinUI 3 新手引导**：新增侧栏按钮，允许用户反复查看

### 文件
- `source/infi-gui-pro.py` — Python GUI Pro（轻量化方案）
- `source/C#_src/InfiSteam/` — C# WPF 完整源码（含所有新功能）
- `source/C#_src/InfiSteam.WinUI/` — C# WinUI 3 完整源码
- `source/AI_Prompt/` — AI Prompt 参考文件
- `source/README.md` — 中文简介（v5.1 更新）
- `source/readme_en.md` — 英文简介（v5.1 更新）
- `source/readme_full.md` — 完整文档
- `source/build_all.bat` — 统一构建脚本
- `release/` — 三个版本的编译输出

---

## v5.1 — Prompt 优化 + WinUI 3 修复 + 文件夹重构

### 新增
- **AI Prompt 重要通知**：在 `steamdb-check-prompt-glo.md` 中添加强制免责声明，Agent 必须在开始操作前向用户输出（支持自动翻译）
- **Agent 行为规范（严格执行）**：新增完整章节，严格限制：
  - 浏览器使用：优先系统 Chrome，禁止直接使用 `curl`/`Invoke-WebRequest` 等网络工具访问 SteamDB
  - Cloudflare 处理：必须等待验证完成，禁止跳过或忽略 Cloudflare 过渡页
  - 网络访问限制：仅通过 Chrome CDP 获取数据，禁止无头浏览器模式
- **网络检测功能**：访问 SteamDB 超时自动执行网络诊断：
  - 同时 Ping SteamDB 和 Cloudflare 检测延迟
  - DNS 解析检测（对两个域名）
  - 代理设置检测
  - 综合判断建议（区分网络断开、SteamDB 被屏蔽、延迟过高等场景）
- **ACF 场清零扩展**：在更新时确保 `BytesToStage`、`BytesStaged` 等暂存字段清零

### 修复
- **WinUI 3 Toast 重复弹出**：`CheckStandaloneLauncher` 方法在 `silent=true` 时仍调用 `AddLog` 导致 toast 频繁弹出 → 修复为 silent 模式不再记录日志
- **WinUI 3 图标加载**：修复 release 版本无法加载 `ico.ico` 的问题
- **WPF 图标加载**：修复 `IOException: 找不到资源 'ico.ico'` → 改用 `pack://` URI 方案
- **路径约束同步**：`infi-manager.ps1` 中原本写入 `$env:TEMP` 的临时文件全部改为写入脚本所在目录，与 Prompt 指令保持一致

### 优化
- **文件夹重组**：`source/` 存放开发文件，`release/` 包含三个独立子文件夹（WPF、WinUI3、AI_Prompt_with_Powershell）
- **构建脚本**：`build_csharp.bat` 分开编译 WPF 和 WinUI3 到不同输出目录
- **infi-manager.ps1**：添加 `Test-NetworkConnectivity` 网络检测函数，集成到 `Invoke-SteamDBCheck`
- **网络检测代码**：从单目标 ping 升级为同时检测 SteamDB 和 Cloudflare，提供更准确的网络诊断

### 技术细节
- `Prompts/steamdb-check-prompt-glo.md` 新增：
  - 📢 重要通知区块（免责声明 + 自动翻译）
  - ⛔ Agent 行为规范（浏览器、Cloudflare、网络访问三项严格限制）
  - 🛠 超时处理与网络检测（判断条件 + PowerShell 诊断代码 + 综合建议）
- `infi-manager.ps1` 新增 `Test-NetworkConnectivity()` 函数
- `MainWindow.xaml.cs` 修复 `CheckStandaloneLauncher()` silent 模式行为

### 文件
- `release/AI_Prompt_with_Powershell/` — Prompt 文件 + infi-manager.ps1（独立可运行）
- `release/C#_WPF/` — WPF 编译输出
- `release/C#_WinUI3/` — WinUI3 编译输出
- `source/` — 所有开发源文件
- `build_csharp.bat` — 新版构建脚本

---

## v5.0 — Cloudflare 处理 + 重试机制 + ACF 备份

### 新增
- **ACF 自动备份**：更新 ACF 前自动备份到 `steamapps/backups/appmanifest_3164330.acf.bak.{timestamp}`
- **Chrome 临时文件清理提示**：关闭 Chrome 后弹出提示，询问是否删除 `chrome-profile-steamdb/` 文件夹
- **Cloudflare 验证自动处理**：检测 "请稍候" / "Checking your browser" 页面，自动等待验证完成（最多等待 2 分钟）
- **重试按钮**：SteamDB 数据解析失败时，可在同一 Chrome 页面重新获取（避免重新触发 Cloudflare 验证）
- **精确关闭 Chrome**：只关闭本次启动的 SteamDB 专用 Chrome 实例（通过命令行参数匹配）

### 修复
- **修复超时时间过短**：CDP 等待延长至 90 秒，页面加载等待 20 秒，解析失败自动重试 3 次
- **修复关闭 Chrome 后清理提示**：清理提示移至关闭 Chrome 后（原位置在 SteamDB 检测完成后，逻辑错误）
- 修复 `AcfManager.Backup()` 变量名语法错误
- 更新版本号至 v5.0

### 技术细节
- `SteamDBScraper.FetchLatestAsync()`：启动 Chrome 并获取 SteamDB 数据，含 Cloudflare 处理和自动重试
- `SteamDBScraper.RetryFetchAsync()`：在当前已打开的 Chrome 页面上重新获取数据（不重启 Chrome）
- `SteamDBScraper.IsChromeAlive()`：检查 Chrome 是否仍在运行
- `IsCloudflareChallenge()`：判断页面是否为 Cloudflare 人机验证页面
- `FetchPageTextWithCloudflareWait()`：获取页面文本，如遇 Cloudflare 验证则自动等待完成

### 文件
- `C#/src/InfiSteam/` — 更新后完整源代码
- `C#/build/InfiSteam.exe` — 自包含单文件发行版

---

## v4.0 — C# 原生桌面程序 (WPF)

### 新增
- **全新 C# WPF 桌面程序**（.NET 10 + WinUI 3 → WPF 迁移）
  - 自包含单文件 EXE，无需安装 Python 或 .NET 运行时
  - 框架依赖版仅约 1 MB（需 .NET 10 Desktop Runtime）
  - 原生 Windows 桌面程序
  - 启动更快、资源占用更低
- **核心服务类（C# 实现）**：
  - `SteamDetector.cs` — Steam 路径自动检测（注册表 + libraryfolders.vdf 解析）
  - `AcfManager.cs` — ACF 文件解析、修改、备份、锁定
  - `SteamDBScraper.cs` — SteamDB 网页数据抓取与版本对比（Chrome CDP）
  - `StandaloneLauncherDetector.cs` — 三路独立启动器检测（注册表 + config.ini + 开始菜单）
- **独立启动器检测面板**：自动检测并提示独立启动器配置

### 技术变更
- 放弃 WinUI 3（Windows App SDK）→ 迁移至 WPF，解决 XAML 编译器崩溃与运行时异常问题
- 修复老版本 Steam 路径误报问题（未读取 `libraryfolders.vdf`，只检查主库路径）
- 添加 `FindGameLibrary()` 方法遍历所有游戏库
- 发布配置：`dotnet publish -c Release -r win-x64 --self-contained true -o output`

### 文件
- `C#/src/InfiSteam/` — 完整源代码（含 11 个文件、4 个服务类）
- `C#/build/InfiSteam.exe` — 自包含单文件发行版

---

## v3.0 — 通用化改造完成

将原本硬编码路径的脚本改造为通用版本，支持任何用户的电脑自动检测路径，并集成 SteamDB 自动检测功能。

### 文件变更

#### 1. config.json
**变更内容：**
- 移除 `core_content_dir` 硬编码路径
- `steam_root`、`game_dir`、`acf_file` 改为 `"auto"`（自动检测）
- `skeleton.delete_dirs` 改为 `skeleton.move_dirs`（移动模式替代删除）

#### 2. infi-manager.ps1
**新增功能：**
- **自动检测 Steam 路径**：通过注册表 + 常见路径搜索
- **自动检测游戏库**：读取 `libraryfolders.vdf` 查找无限暖暖安装位置
- **`steamdb-check` 命令**：全自动 SteamDB 版本检测和更新
  - 自动关闭 Steam
  - 启动独立 Chrome 用户目录访问 SteamDB
  - 通过 CDP 获取页面内容
  - 自动对比版本并更新 ACF
  - 自动备份和锁定 ACF
- **`restore` 命令**：将 X6Game 从备份还原到 Steam 目录
- **骨架化改为移动模式**：X6Game 移动到 `infi/X6Game_backup` 而非删除

**改进：**
- 所有路径基于脚本目录相对定位
- 备份目录：`infi/backups/`
- Chrome 用户目录：`infi/chrome-profile-steamdb/`
- 支持 `-NoInteractive` 非交互模式（用于自动化）

#### 3. infi-gui.py
**变更内容：**
- 移除硬编码路径（`Q:\SteamLibrary` 等）
- 更新按钮功能：
  - "SteamDB 自动检测" 替代 "检查更新 (SteamDB)"
  - 新增 "还原 X6Game" 按钮
- 移除 Marvis Prompt 对话框（不再需要）
- 所有路径从 config.json 动态加载

---

## 通用化特性

### 路径自动检测
```powershell
# Steam 安装路径检测顺序：
1. 注册表 HKLM\SOFTWARE\WOW6432Node\Valve\Steam
2. 注册表 HKCU\SOFTWARE\Valve\Steam
3. 常见路径：C:\Program Files (x86)\Steam, D:\Steam 等

# 游戏库检测：
1. 读取 libraryfolders.vdf
2. 搜索所有库中的 appmanifest_3164330.acf
```

### SteamDB 反爬处理
- 使用独立 Chrome 用户目录（`chrome-profile-steamdb/`）
- 通过 Chrome DevTools Protocol (CDP) 获取页面
- 模拟真实用户浏览行为

### 骨架化新实现
- **旧方式**：删除 X6Game（数据丢失风险）
- **新方式**：移动 X6Game 到 `infi/X6Game_backup/`
- 可随时通过 `restore` 命令还原

---

## 使用方式

### 命令行
```powershell
# 查看状态
.\infi-manager.ps1 status

# 自动检测 SteamDB 并更新
.\infi-manager.ps1 steamdb-check

# 骨架化（移动 X6Game 到备份）
.\infi-manager.ps1 skeletonize

# 还原 X6Game
.\infi-manager.ps1 restore

# 验证
.\infi-manager.ps1 verify
```

### GUI
```bash
python infi-gui.py
```

---

## 测试验证
- ✅ Steam 路径自动检测（D:\Entertainment\Steam）
- ✅ 游戏库自动检测（Q:\SteamLibrary）
- ✅ SteamDB 自动检测（BuildID + Manifest 匹配）
- ✅ ACF 备份和恢复
- ✅ 只读锁定保持
- ✅ 骨架化移动模式
- ✅ 还原功能

---

## 注意事项
1. SteamDB BuildID 提取：由于 SteamDB 页面结构，App BuildID 可能无法直接从 depots 页面提取。当提取失败时，脚本会回退到使用本地值（Manifest GID 仍然正确对比）。
2. Chrome 远程调试：如果 9222 端口被占用，可能需要手动关闭现有 Chrome 实例。
3. 首次运行 SteamDB 检测时，Chrome 需要初始化用户目录，可能较慢。
