# 无限暖暖 Steam 壳管理工具 - 更新日志

## v4.0 — C# 原生桌面程序 (WPF)

### 新增
- **全新 C# WPF 桌面程序**（.NET 10 + WinUI 3 → WPF 迁移）
  - 自包含单文件 EXE，无需安装 Python 或 .NET 运行时
  - 框架依赖版仅约 1 MB（需 .NET 10 Desktop Runtime）
  - 原生 Windows 桌面程序，启动更快、资源占用更低
- **核心服务类（C# 实现）**：
  - `SteamDetector.cs` — Steam 路径自动检测（注册表 + libraryfolders.vdf 解析）
  - `AcfManager.cs` — ACF 文件解析、修改、备份、锁定
  - `SteamDBScraper.cs` — SteamDB 网页数据抓取与版本对比（无 Chrome CDP 依赖）
  - `StandaloneLauncherDetector.cs` — 三路独立启动器检测（注册表 + config.ini + 开始菜单）
- **独立启动器检测面板**：自动检测并提示独立启动器配置

### 技术变更
- 放弃 WinUI 3（Windows App SDK）→ 迁移至 WPF，解决 XAML 编译器崩溃与运行时异常问题
- 修复老版本 Steam 路径误报问题（未读取 `libraryfolders.vdf`，只检查主库路径）
- 添加 `FindGameLibrary()` 方法遍历所有游戏库
- 发布配置：`dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true`

### 文件
- `C#/src/InfiSteam/` — 完整源代码（含 11 个文件、4 个服务类）
- `C#/build/InfiSteam.exe` — 自包含单文件发行版（126 MB）

---

## v3.0 — 通用化改造完成
将原本硬编码路径的脚本改造为通用版本，支持任何用户的电脑自动检测路径，并集成 SteamDB 自动检测功能。

## 文件变更

### 1. config.json
**变更内容：**
- 移除 `core_content_dir` 硬编码路径
- `steam_root`、`game_dir`、`acf_file` 改为 `"auto"`（自动检测）
- `skeleton.delete_dirs` 改为 `skeleton.move_dirs`（移动模式替代删除）

### 2. infi-manager.ps1
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

### 3. infi-gui.py
**变更内容：**
- 移除硬编码路径（`Q:\SteamLibrary` 等）
- 更新按钮功能：
  - "SteamDB 自动检测" 替代 "检查更新 (SteamDB)"
  - 新增 "还原 X6Game" 按钮
- 移除 Marvis Prompt 对话框（不再需要）
- 所有路径从 config.json 动态加载

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

## 测试验证
- ✅ Steam 路径自动检测（D:\Entertainment\Steam）
- ✅ 游戏库自动检测（Q:\SteamLibrary）
- ✅ SteamDB 自动检测（BuildID + Manifest 匹配）
- ✅ ACF 备份和恢复
- ✅ 只读锁定保持
- ✅ 骨架化移动模式
- ✅ 还原功能

## 注意事项
1. SteamDB BuildID 提取：由于 SteamDB 页面结构，App BuildID 可能无法直接从 depots 页面提取。当提取失败时，脚本会回退到使用本地值（Manifest GID 仍然正确对比）。
2. Chrome 远程调试：如果 9222 端口被占用，可能需要手动关闭现有 Chrome 实例。
3. 首次运行 SteamDB 检测时，Chrome 需要初始化用户目录，可能较慢。
