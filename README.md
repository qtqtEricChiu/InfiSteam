<p align="right">
  <a href="readme_en.md">English</a>
</p>
<br /><br />

<p align="center">
  <img src="ico.png" width="128" alt="InfiSteam" />
</p>

<h1 align="center">InfiSteam</h1><br />

<p align="center">
  <strong>无限暖暖 Steam 高级启动管理工具</strong>
</p>

<p align="center">
  <em>防止 Steam 误判版本过期并自动重置本地文件完整性（即"更新"），<br />通过始终保持最新，实现无需 Steam 版本游戏包体，无限暖暖亦能通过 Steam 完成高级启动叠纸账号版本游戏。</em>
</p>

<p align="center">
  <sub>v5.1 新增：🎉 WinUI 3 现代版本上线 · AI Prompt Agent 行为规范 · 网络诊断 · Cloudflare 验证自动处理</sub>
</p>

<p align="center">
  <sub>包括本文档及程序在内均包含 AI 辅助生成。不代表本人立场。</sub>
</p>

---

<br />

<p align="center">
  <strong>InfiSteam</strong><br />
  ▸ 通过伪造 ACF 版本信息 + Chrome CDP 自动抓取 SteamDB 最新 BuildID / Manifest GID，实现一键版本同步<br />
  ▸ 骨架化清理 Steam 壳目录，将核心游戏数据外置至同盘备份，释放空间并可随时还原<br />
  ▸ 支持四种操作方式：AI Agent 全自动检测（推荐）/ WinUI 3 现代桌面版 / WPF 桌面版 / PowerShell 命令行
</p>

<br />

---

## 快速开始

> **下载发行版**：所有程序文件请从 [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases) 下载。

### 方式一：AI Agent 提示词（强烈推荐 ⭐）

从 [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases) 下载 `steamdb-check-prompt-glo.md`，命令 OpenClaw、WorkBuddy、Marvis、QClaw 读取该文件，AI Agent 自动完成所有检测流程。<br />
<sub>包含完整的 Agent 行为规范、Cloudflare 处理指导、网络诊断逻辑，最适合调试和自动化场景。</sub><br />
<sub>~可能产生费用~</sub>

### 方式二：C# 原生桌面程序

从 [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases) 下载以下版本：

#### WinUI 3 现代版本（v5.1 全新上线 🎉）
采用 WinUI 3 + Fluent Design 2 现代界面，视觉效果更精美，动画更流畅。
```powershell
# 下载后直接运行，无需任何依赖
.\InfiSteam.WinUI.exe
```

#### WPF 版本（稳定可靠）
采用 WPF + Fluent Design，兼容性好，启动速度快。
```powershell
# 下载后直接运行，无需任何依赖
.\InfiSteam.exe
```

### 方式三：Python GUI（需 Python 环境）

从 [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases) 下载 `InfiSteam.exe`（Python GUI Pro 单文件版），或运行源码：

```powershell
python infi-gui-pro.py
```

### 方式四：命令行

从 [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases) 下载发行版，解压后在目录中运行：

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

## 版本选择建议

| 方式 | 适合人群 | 特点 |
|------|----------|------|
| **AI Agent 提示词** | 所有用户（推荐） | 全自动、无需手动操作、包含完整错误处理 |
| **WinUI 3 桌面版** | 喜欢现代 UI 的玩家 | 最新 Fluent Design 2 界面、动画效果最佳 |
| **WPF 桌面版** | 追求稳定性的玩家 | 兼容性好、资源占用低、经过长期验证 |
| **Python GUI** | Python 环境用户 | 跨平台潜力、便于二次开发 |
| **命令行** | 高级用户 / 脚本自动化 | 可集成到批处理脚本、支持 CI/CD |

---

## 详细文档

完整功能说明、方案原理、配置文件参考、故障排除等详见：

<p align="center">
  <strong><a href="readme_full.md">📖 完整文档（readme_full.md）</a></strong>
</p>

---

## 参考链接

- [SteamDB App 3164330](https://steamdb.info/app/3164330/)
- [SteamDB Sub 1221922](https://steamdb.info/sub/1221922/)
- [SteamDB Depot 3164332](https://steamdb.info/depot/3164332/manifests/)
- [技术原理：Steam 游戏更新机制与 ACF 文件](https://cloud.tencent.com/developer/article/2468980)
- [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases)

---

<p align="center">
  <sub>
    本工具与 <strong>叠纸游戏</strong> / <strong>Infold Games</strong>、<strong>SteamDB</strong> 以及 <strong>Valve Corporation</strong> 无关。<br />
    无限暖暖 © 2022 Papergames, ALL RIGHTS RESERVED. Steam 为 Valve Corporation 的商标。
  </sub>
</p>
