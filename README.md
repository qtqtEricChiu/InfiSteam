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
  <sub>v5.0 新增：Cloudflare 验证自动处理、解析失败可重试（无需重启 Chrome）、ACF 自动备份</sub>
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
  ▸ 支持 AI Agent 全自动检测 / Python GUI 图形界面 / PowerShell 命令行三种操作方式
</p>

<br />

---

## 快速开始

### 方式一：C# 原生桌面程序

开箱即用，双击 `C#/build/InfiSteam.exe` 即可运行。

### 方式二：AI Agent 提示词（推荐）

命令 OpenClaw、WorkBuddy、Marvis、QClaw 读取 `steamdb-check-prompt-glo.md`，AI Agent 自动完成所有检测流程。<br />
<sub>~可能产生费用~</sub>

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

---

<p align="center">
  <sub>
    本工具与 <strong>叠纸游戏</strong> / <strong>Infold Games</strong>、<strong>SteamDB</strong> 以及 <strong>Valve Corporation</strong> 无关。<br />
    无限暖暖 © 2022 Papergames, ALL RIGHTS RESERVED. Steam 为 Valve Corporation 的商标。
  </sub>
</p>

