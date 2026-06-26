<p align="center">
  <img src="ico.png" width="128" alt="Icon" />
</p>

<h1 align="center">InfiSteam / 无限暖暖 Steam 壳管理器</h1>

<p align="center">
  <strong>Infinity Nikki Steam Shell Manager</strong><br />
  <em>防止 Steam 误判版本过期并自动重置本地文件（即"更新"），通过始终保持最新，实现无需Steam版本游戏包体，无限暖暖亦能通过Steam完成高级启动叠纸账号版本游戏。</em>
</p>

<p align="center">
  <sub>包括本文档及程序在内均包含AI辅助生成。不代表本人立场。</sub>
  <sub>Version 5.1 &middot; WinUI 3 + WPF &middot; Fluent Design &middot; SteamDB Auto-Check &middot; ACF Anti-Update &middot; AI Agent Prompt</sub>
</p>

---

## 目录

- [背景](#背景)
- [方案原理](#方案原理)
- [快速开始](#快速开始)
- [WinUI 3 现代版本（v5.1 新增）](#winui-3-现代版本v51-新增)
- [功能详解](#功能详解)
- [SteamDB 自动检测](#steamdb-自动检测)
- [AI Agent 提示词使用指南](#ai-agent-提示词使用指南)
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

## 快速开始

> **下载发行版**：所有程序文件请从 [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases) 下载。

### 方式一：AI Agent 提示词（强烈推荐 ⭐）

从 [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases) 下载 `steamdb-check-prompt-glo.md`，命令 OpenClaw、WorkBuddy、Marvis、QClaw 读取该文件，AI Agent 将自动完成所有检测流程。

<sub>💡 这是最适合调试和自动化场景的方式，包含完整的 Agent 行为规范、Cloudflare 处理指导、网络诊断逻辑。</sub><br />
<sub>~可能产生费用~</sub>

### 方式二：WinUI 3 现代桌面版（v5.1 全新上线 🎉）

从 [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases) 下载 `C#_WinUI3` 版本：

```powershell
# 下载后直接运行
.\InfiSteam.WinUI.exe
```

采用 WinUI 3 + Fluent Design 2 现代界面，视觉效果更精美，动画更流畅。

### 方式三：WPF 稳定桌面版

从 [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases) 下载 `C#_WPF` 版本：

```powershell
# 下载后直接运行
.\InfiSteam.exe
```

采用 WPF + Fluent Design，兼容性好，启动速度快，资源占用低。

### 方式四：Python GUI（需 Python 环境）

从 [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases) 下载 `InfiSteam.exe`（Python GUI Pro 单文件版），或运行源码：

```powershell
python infi-gui-pro.py
```

### 方式五：命令行

从 [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases) 下载发行版，解压后运行：

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
- AI Agent 方式需要安装支持的 AI Agent 工具
- SteamDB 检测需要安装 Google Chrome

---

## WinUI 3 现代版本（v5.1 新增）

### 简介

WinUI 3 版本是 InfiSteam 的全新现代桌面版本，采用微软最新的 WinUI 3 框架 + Fluent Design 2 设计语言，为用户带来更精美、更流畅的使用体验。

### 主要特性

| 特性 | 说明 |
|------|------|
| 🎨 现代界面 | 采用 WinUI 3 + Fluent Design 2，视觉效果大幅提升 |
| ✨ 流畅动画 | 所有交互均带有流畅的动画效果 |
| 🌙 主题支持 | 支持浅色/深色/跟随系统三种主题模式 |
| 📊 实时状态 | 右上角实时显示 Steam 运行状态 |
| 🔄 独立启动器检测 | 自动检测国服独立启动器并提示配置 |
| 📝 终端风格日志 | 深色终端风格状态面板，带时间戳中文日志 |

### 与 WPF 版本对比

| 对比项 | WinUI 3 | WPF |
|--------|----------|------|
| 界面风格 | WinUI 3 + Fluent Design 2（现代） | WPF + Fluent Design（经典） |
| 动画效果 | 流畅的现代动画 | 基础动画 |
| 主题支持 | 浅色/深色/跟随系统 | 浅色/深色 |
| 启动速度 | 稍慢（需加载 WinUI 3 运行时） | 更快 |
| 资源占用 | 稍高 | 更低 |
| 兼容性 | Windows 10 2004+ | Windows 7+ |
| 推荐场景 | 追求现代 UI 体验 | 追求稳定性和兼容性 |

### 使用建议

- **首次使用**：推荐先尝试 WinUI 3 版本，体验现代界面
- **遇到问题**：如 WinUI 3 版本出现兼容性问题，可切换至 WPF 版本
- **自动化场景**：推荐使用 AI Agent 提示词方式，无需手动操作

---

## 功能详解

### GUI 界面（WPF / WinUI 3）

启动后包含以下区域：

| 区域 | 功能 |
|------|------|
| 状态面板 | 深色终端风格，实时显示 ACF 详情和健康检查 |
| 运行日志 | 带时间戳的中文操作日志 |
| 控制面板 | 功能按钮，一键执行 |
| Steam 指示器 | 右上角实时显示 Steam 是否运行 |

**控制按钮（WPF 版本）**：

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

**WinUI 3 版本**按钮布局类似，采用现代图标和动画效果。

### 命令行完整参考

所有命令均需在 **Steam 已完全退出** 的前提下执行（需先从 [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases) 下载发行版）：

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

### v5.1 新增：网络诊断功能

当访问 SteamDB 超时时，自动执行网络诊断：

- 同时 Ping `steamdb.info` 和 `cloudflare.com` 检测延迟
- DNS 解析检测（对两个域名）
- 代理设置检测
- 综合判断建议（区分网络断开、SteamDB 被屏蔽、延迟过高等场景）

---

## AI Agent 提示词使用指南

### 为什么推荐使用 AI Agent 方式？

AI Agent 方式是最智能、最自动化的使用方式：

- **零手动操作**：Agent 自动完成所有检测、对比、更新流程
- **完整错误处理**：包含 Cloudflare 验证处理、网络诊断、路径约束等完整逻辑
- **行为规范内置**：Agent 会严格遵守浏览器使用规范、Cloudflare 处理规范
- **自动翻译**：非简体中文用户会自动翻译通知内容

### 使用方法

1. 从 [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases) 下载 `steamdb-check-prompt-glo.md`
2. 打开支持的 AI Agent 工具（OpenClaw / WorkBuddy / Marvis / QClaw）
3. 命令 Agent 读取该文件
4. Agent 会自动完成所有流程，包括：
   - 检测 Steam 状态
   - 启动 Chrome 并获取 SteamDB 数据
   - 对比本地 ACF 版本
   - 自动更新 ACF（如需要）
   - 锁定 ACF 只读
   - 网络诊断（如超时）

### v5.1 新增：Agent 行为规范

为防止 AI Agent 执行错误操作，v5.1 新增了严格的 Agent 行为规范：

1. **浏览器使用规范**：优先使用系统安装的 Chrome，禁止直接使用 `curl`/`Invoke-WebRequest` 等网络工具访问 SteamDB
2. **Cloudflare 处理规范**：必须等待验证完成，禁止跳过或忽略 Cloudflare 过渡页
3. **网络访问限制**：仅通过 Chrome CDP 获取数据，禁止无头浏览器模式
4. **路径约束**：临时文件必须创建在脚本所在目录下，禁止写入系统临时目录

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
| 7 | WinUI 3 版本需要 Windows 10 2004 或更高版本 |
| 8 | AI Agent 方式可能产生 AI 服务费用 |

---

## 故障排除

| 现象 | 原因 | 解决 |
|------|------|------|
| Steam 仍尝试下载 | ACF 未锁定或 StateFlags ≠ 4 | 运行 `verify` 检查所有字段 |
| SteamDB 检测失败 | Chrome 未安装或 9222 端口被占用 | 确认 Chrome 已装；关闭其他调试工具 |
| SteamDB 访问超时 | 网络问题或被防火墙拦截 | 运行 `Test-NetworkConnectivity` 诊断 |
| Cloudflare 验证卡住 | 验证超时（默认 2 分钟） | 手动完成验证或等待重试 |
| 骨架化报错 | Steam 未完全退出 | 任务管理器结束 `steam.exe` 和 `steamwebhelper` |
| 游戏无法启动 | 壳文件缺失 | 运行 `restore` 还原备份 |
| 权限不足 | 非管理员运行 | 以管理员运行 PowerShell |
| GUI 闪退 | 依赖缺失 | 使用发行版（GitHub Releases）而非源代码 |
| WinUI 3 启动失败 | Windows 版本过低或运行时缺失 | 升级 Windows 或切换至 WPF 版本 |
| Toast 重复弹出（WinUI 3） | v5.1 已修复 | 更新至最新版本 |

---

## v5.1 更新亮点

### 新增功能

- **WinUI 3 现代版本**：全新 WinUI 3 + Fluent Design 2 界面，视觉效果和动画效果大幅提升
- **AI Prompt 重要通知**：Agent 必须在开始操作前向用户输出免责声明（支持自动翻译）
- **Agent 行为规范**：严格限制浏览器使用、Cloudflare 处理、网络访问等行为
- **网络诊断功能**：访问 SteamDB 超时自动执行网络诊断（Ping + DNS + 代理检测）
- **Python GUI Pro**：全新 customtkinter 现代界面，移植 C# 核心检测功能，单文件 EXE 仅 20 MB

### 修复问题

- **WinUI 3 Toast 重复弹出**：修复窗口激活时重复触发日志和 Toast 的问题
- **路径约束同步**：`infi-manager.ps1` 临时文件路径与 Prompt 指令保持一致
- **图标加载**：修复 release 版本无法加载图标的问题
- **Python GUI 窗口闪烁**：所有子进程彻底隐藏，后台静默运行

### 优化改进

- **文件夹重组**：按功能独立分包（AI_Prompt / WPF / WinUI3 / Python_GUI）
- **构建脚本**：`build_csharp.bat` / `build_python_gui.bat` 按需编译
- **网络检测**：同时检测 SteamDB 和 Cloudflare，诊断更准确
- **体积精简**：Python GUI Pro 单文件仅 20 MB，远小于 C# 自包含版本

---

## 参考链接

- [SteamDB App 3164330](https://steamdb.info/app/3164330/)
- [SteamDB Sub 1221922](https://steamdb.info/sub/1221922/)
- [SteamDB Depot 3164332](https://steamdb.info/depot/3164332/manifests/)
- [技术原理：Steam 游戏更新机制与 ACF 文件](https://cloud.tencent.com/developer/article/2468980)
- [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases)
- [WinUI 3 官方文档](https://learn.microsoft.com/en-us/windows/apps/winui/)

---

<p align="center">
  <sub>
    本工具与 <strong>叠纸游戏</strong> / <strong>Infold Games</strong>、<strong>SteamDB</strong> 以及 <strong>Valve Corporation</strong> 无关。<br />
    无限暖暖 © 2022 Papergames,ALL RIGHTS RESERVED.。Steam 为 Valve Corporation 的商标。
  </sub>
</p>
