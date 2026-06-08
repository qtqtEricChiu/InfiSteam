<p align="center">
  <img src="ico.png" width="128" alt="Icon" />
</p>

<h1 align="center">InfiSteam / 无限暖暖 Steam 壳管理器</h1>

<p align="center">
  <strong>Infinity Nikki Steam Shell Manager</strong><br />
  <em>防止 Steam 误判版本过期并自动重置本地文件（即“更新”），通过始终保持最新，实现无需Steam版本游戏包体，无限暖暖亦能通过Steam完成高级启动叠纸账号版本游戏。</em>
</p>

<p align="center">
  <sub>包括本文档及程序在内均包含AI辅助生成。不代表本人立场。</sub>
  <sub>Version 4.0 &middot; C# WPF Native &middot; Fluent Design GUI &middot; SteamDB Auto-Check &middot; ACF Anti-Update</sub>
</p>

---

## 目录

- [背景](#背景)
- [方案原理](#方案原理)
- [文件结构](#文件结构)
- [快速开始](#快速开始)
- [功能详解](#功能详解)
- [SteamDB 自动检测](#steamdb-自动检测)
- [配置文件](#配置文件)
- [注意事项](#注意事项)
- [故障排除](#故障排除)

---

## 背景

无限暖暖 Steam 中国版（AppID: **3164330**，Sub: **1221922**）通过 `%command%` 高级启动选项关联启动国服版本。本地存在两份数据：

| 位置 | 内容 | 典型大小 |
|------|------|----------|
| Steam 库目录 `\steamapps\common\Infinity Nikki\` | Steam 壳文件（启动器 + DLL） | ~0.7 GB |
| 独立于启动器的游戏目录 `\steamapps\common\Infinity Nikki\InfinityNikki\X6Game\` | 核心游戏数据 | ~110 GB |
| 叠纸账号版本 启动器目录 | ~0.2 GB |
| 叠纸账号版本 游戏目录 | ~120 GB |

**问题**：每次官方更新后，Steam 检测到本地版本低于云端版本，触发完整下载覆盖。实际上核心数据由独立启动器管理，Steam 壳只需保持版本号同步即可。

本工具通过 **ACF 防伪** 与 **骨架化清理** 组合方案解决此问题。

---

## 方案原理

### ACF 防伪（核心）

直接修改 Steam 的 `appmanifest_3164330.acf`，让 Steam 认为本地已是最新：

| 字段 | 目标值 | 作用 |
|------|--------|------|
| `StateFlags` | `4` | 状态：已安装就绪 |
| `TargetBuildID` | `0` | 不要求更新到特定版本 |
| `buildid` | 最新值 | 匹配 SteamDB 最新 Public BuildID |
| `InstalledDepots.manifest` | 最新 GID | 匹配 Depot 3164332 最新 Manifest |
| `AutoUpdateBehavior` | `1` | 仅启动时检查更新 |
| 只读属性 | ON | 阻止 Steam 改写 ACF |

### 骨架化清理（辅助）

将 Steam 目录中的 `InfinityNikki\X6Game` 移至同盘备份位置（`{盘符}\X6Game_backup`），释放 Steam 目录空间。需要时可一键还原。

---

## 文件结构

```
release/
├── ico.png                     # 应用图标
├── ico.ico                     # Windows 图标
├── config.json                 # 配置文件
├── infi-manager.ps1            # PowerShell 核心脚本（8 个命令）
├── infi-gui.py                 # GUI 启动器（Fluent Design）
├── infi-gui-fluent.py          # GUI 变体（需 ttkbootstrap）暂未启用
├── infi-gui-modern.py          # GUI 变体（需 customtkinter）暂未启用
├── infisteam_single.exe        # 单文件编译包（开箱即用）暂未启用
├── steamdb-check-prompt-glo.md # AI Agent 检测流程规范
├── README.md                   # 本文件
├── CHANGELOG_v2.md             # 更新日志
├── 安装说明.txt                 # 简体中文安装说明
├── C#/                         # C# WPF 桌面程序（.NET 10）
│   ├── src/InfiSteam/  #   完整源代码
│   │   ├── Services/           #     核心服务类（4 个）
│   │   ├── MainWindow.xaml     #     主界面
│   │   ├── MainWindow.xaml.cs  #     主界面逻辑
│   │   ├── App.xaml / .cs      #     应用入口
│   │   └── InfiSteam.csproj
│   └── build/                  #   自包含发行版
│       └── InfiSteam.exe # 单文件可执行（126 MB，无需运行时）
```

---

## 快速开始

### 方式一：C# 原生桌面程序（推荐）

#### 开箱即用

```powershell
# 直接运行，无需任何依赖
.\C#\build\InfiSteam.exe
```

#### 从源码构建

```powershell
# 需要 .NET 10 SDK
cd C#\src\InfiSteam

# 自包含版（无运行时依赖，~134 MB）
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

# 框架依赖版（需安装 .NET 10 Desktop Runtime，~1 MB）
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

#### 功能特色

- 原生 WPF 桌面程序，启动快，内存占用低
- 自动检测 Steam 路径（注册表 + libraryfolders.vdf 全库扫描）
- 独立启动器三路检测（注册表 + config.ini + 开始菜单快捷方式）
- SteamDB 版本对比与 ACF 更新
- 独立启动器检测提示面板

---

### 方式二：AI Agent 提示词

命令OpenClaw、WorkBuddy、Marvis、QClaw读取 `steamdb-check-prompt-glo.md`，AI Agent 自动完成所有检测流程
~可能产生费用~

### 方式三：Python GUI

```powershell
python infi-gui.py
```

### 方式四：命令行

```powershell
# 查看当前状态
.\infi-manager.ps1 status

# SteamDB 全自动检测并更新
.\infi-manager.ps1 steamdb-check

# 全面验证
.\infi-manager.ps1 verify
```

### 前置条件

- **Windows 10 / 11** (x64)
- Steam 已安装且游戏入库
- **操作前必须完全退出 Steam**（含 `steamwebhelper` 进程）

---

## 功能详解

### GUI 界面

启动后包含以下区域：

| 区域 | 功能 |
|------|------|
| 状态面板 | 深色终端风格，实时显示 ACF 详情和健康检查 |
| 运行日志 | 带时间戳的中文操作日志 |
| 控制面板 | 10 个功能按钮，一键执行 |
| Steam 指示器 | 右上角实时显示 Steam 是否运行 |

**控制按钮**：

| 按钮 | 命令 | 说明 |
|------|------|------|
| 🔄 刷新状态 | `status` | 查看 ACF 状态与健康评分 |
| 🔍 SteamDB 检测 | `steamdb-check` | 全自动从 SteamDB 获取最新版本并更新 ACF |
| 💀 骨架化清理 | `skeletonize` | 移动 X6Game 到备份位置释放空间 |
| 📦 还原 X6Game | `restore` | 从备份还原 X6Game 到 Steam 目录 |
| 🧪 骨架化模拟 | `skeletonize -DryRun` | 预览骨架化操作但不实际执行 |
| 🔒 锁定 ACF | `lock` | 设置 ACF 只读 |
| 🔓 解锁 ACF | `unlock` | 取消 ACF 只读 |
| ✅ 全面验证 | `verify` | 一键检查所有配置是否正确 |
| 🚀 启动器设置 | 内置函数 | 自动检测独立启动器路径并提示配置 |
| 🧹 清空日志 | — | 清空当前日志窗口 |

### 命令行完整参考

所有命令均需在 **Steam 已完全退出** 的前提下执行：

```powershell
.\infi-manager.ps1 <command> [options]
```

| 命令 | 说明 |
|------|------|
| `status` | 显示完整 ACF 状态、Steam 运行检测、X6Game 位置、独立启动器信息、健康检查 |
| `steamdb-check` | 自动启动 Chrome CDP → 爬取 SteamDB → 提取 BuildID/Manifest GID → 对比本地 → 自动更新 ACF → 锁定只读 |
| `update` | 手动更新 ACF（需提供 -BuildID 和 -ManifestGID） |
| `skeletonize` | 移动 Steam 目录下冗余大文件到同盘备份位置 |
| `restore` | 从备份还原游戏数据到 Steam 目录 |
| `lock` / `unlock` | 锁定 / 解锁 ACF 只读属性 |
| `verify` | 校验所有配置健康状态 + 检测独立启动器 |
| `query` | 显示 steamdb 查询链接（供手动查询） |

**通用参数**：

| 参数 | 说明 |
|------|------|
| `-Force` | 跳过确认提示 |
| `-DryRun` | 模拟运行，不实际修改文件 |
| `-NoInteractive` | 非交互模式（用于自动化脚本） |
| `-BuildID <id>` | 手动指定 BuildID |
| `-ManifestGID <id>` | 手动指定 Manifest GID |
| `-DepotID <id>` | 指定 Depot ID（默认 3164332） |

---

## SteamDB 自动检测

`steamdb-check` 是全自动检测流程的核心，执行步骤如下：

```
1. 检测 Steam 是否运行 → 是则自动 -shutdown 退出
2. 读取本地 ACF，提取当前 BuildID 和 Manifest GID
3. 启动 Chrome（--remote-debugging-port=9222）
   → 使用独立用户目录 chrome-profile-steamdb
   → 自动打开 SteamDB depot 页面
4. 通过 Chrome CDP WebSocket 注入 JavaScript
   → Runtime.evaluate 提取页面文本
   → Page.navigate 切换到 manifests 页面
   → 再次提取 Manifest GID 列表
5. 正则解析提取最新 Public BuildID 和 Manifest GID
6. 对比本地版本：
   匹配 → 无需操作
   不匹配 → 备份 ACF → 更新字段 → 锁定只读
7. 同步 SizeOnDisk 为实际目录大小
```

**依赖**：Google Chrome 浏览器（任意安装位置，脚本自动检测）

---

## 独立启动器检测

脚本会自动从以下来源检测国服独立启动器：

| 检测源 | 方法 |
|------|------|
| 注册表 | 扫描 `HKLM\Software\...\Uninstall` 中 Infinity/Nikki/Infold 条目 |
| 配置文件 | 读取常见目录下的 `config.ini`，提取 `game_path` |
| 开始菜单 | 搜索 Infinity/Nikki 相关的 `.lnk` 快捷方式 |

检测到后在 Steam 游戏属性 → 启动选项中填入：
```
"{启动器路径}" %command%
```

---

## 配置文件

### config.json

```json
{
  "app": {
    "name": "Infinity Nikki",
    "appid": "3164330",
    "description": "无限暖暖 - Steam中国版 (sub/1221922)"
  },
  "standalone_launcher": {
    "enabled": true,
    "search_paths": [
      "D:\\Entertainment\\InfinityNikkiLauncher",
      "C:\\InfinityNikkiLauncher"
    ],
    "config_file": "config.ini",
    "launcher_exe": "launcher.exe"
  },
  "paths": {
    "steam_root": "auto",
    "game_dir": "auto",
    "acf_file": "auto"
  },
  "depots": {
    "3164332": {
      "description": "Infinity Nikki Content (Windows - China)",
      "install_script": "InfinityNikki\\X6Game\\installscript\\installscript.vdf"
    }
  },
  "skeleton": {
    "keep_files": [
      "launcher.exe", "msvcp140.dll", "vcruntime140.dll",
      "vcruntime140_1.dll", "steam_appid.txt"
    ],
    "keep_dirs": ["InfinityNikki", "1.3.0"],
    "move_dirs": ["InfinityNikki\\X6Game"],
    "delete_files": []
  },
  "steamdb": {
    "app_url": "https://steamdb.info/app/3164330/",
    "sub_url": "https://steamdb.info/sub/1221922/",
    "depot_url_template": "https://steamdb.info/depot/{depotid}/manifests/"
  }
}
```

`paths` 中 `"auto"` 表示自动检测：注册表 → 常见路径 → 进程定位（四级回退）。

---

## 注意事项

| 序号 | 事项 |
|------|------|
| 1 | **更新 ACF 前必须完全退出 Steam**（含 steamwebhelper 进程） |
| 2 | ACF 只读锁定后 Steam 无法改写，除非手动解锁 |
| 3 | Steam 内「游戏属性 → 更新」须设为「**仅在我启动时更新此游戏**」 |
| 4 | 脚本修改前自动在 `backups\` 目录生成 `.bak` 文件 |
| 5 | 骨架化移动的是同盘物理移动，不跨盘，速度极快 |
| 6 | 每次官方更新后需重新执行 SteamDB 检测 |
| 7 | 单文件 EXE（`infisteam_single.exe`）无需 Python 环境，双击即用 |

---

## 故障排除

| 现象 | 原因 | 解决 |
|------|------|------|
| Steam 仍尝试下载 | ACF 未锁定或 StateFlags ≠ 4 | 运行 `verify` 检查所有字段 |
| SteamDB 检测失败 | Chrome 未安装或 9222 端口被占用 | 确认 Chrome 已装；关闭其他调试工具 |
| 骨架化报错 | Steam 未完全退出 | 任务管理器结束 `steam.exe` 和 `steamwebhelper` |
| 游戏无法启动 | 壳文件缺失 | 运行 `restore` 还原备份 |
| 权限不足 | 非管理员运行 | 以管理员运行 PowerShell |
| GUI 闪退 | ttkbootstrap 未安装 | 使用 `infisteam_single.exe`（内置依赖） |

---

## 参考链接

- [SteamDB App 3164330](https://steamdb.info/app/3164330/)
- [SteamDB Sub 1221922](https://steamdb.info/sub/1221922/)
- [SteamDB Depot 3164332](https://steamdb.info/depot/3164332/manifests/)
- [技术原理：Steam 游戏更新机制与 ACF 文件](https://cloud.tencent.com/developer/article/2468980)

---

<p align="center">
  <sub>
    本工具与 <strong>叠纸游戏</strong> / <strong>Infold Games</strong>、<strong>SteamDB</strong> 以及 <strong>Valve Corporation</strong> 无关。<br />
    无限暖暖 © 2022 Papergames,ALL RIGHTS RESERVED.。Steam 为 Valve Corporation 的商标。
  </sub>
</p>
