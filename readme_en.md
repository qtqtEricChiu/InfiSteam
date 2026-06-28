<p align="right">
  <a href="README.md">中文</a>
</p>

<br /><br /><br />


<p align="center">
  <img src="ico.png" width="128" alt="InfiSteam" />
</p>

<h1 align="center">InfiSteam</h1><br />

<p align="center">
  <strong>Infinity Nikki Advanced Steam Launch Manager</strong>
</p>

<p align="center">
  <em>Prevents Steam from incorrectly flagging the local version as outdated and auto-resetting file integrity (i.e., "updating").<br />By always staying current, enables the Papergames account version of Infinity Nikki to launch through Steam without needing the Steam-distributed game package.</em>
</p>

<p align="center">
  <sub>v5.1.3 Latest: 📋 Multi-Game Prompt · Zenless Zone Zero/Wuthering Waves Support · Executable Auto-Handling · DX12 Startup</sub>
</p>

<p align="center">
  <sub>This document and the accompanying program contain AI-assisted content. Does not reflect personal endorsement.</sub>
</p>

---

<br />

<p align="center">
  <strong>InfiSteam</strong><br />
  ▸ Forges ACF version info + auto-scrapes latest BuildID / Manifest GID from SteamDB via Chrome CDP for one-click version sync<br />
  ▸ Skeletonizes the Steam shell directory, relocating core game data to a same-disk backup to free space with instant restore capability<br />
  ▸ Supports multiple operation modes: AI Agent auto-detection (Recommended) / WinUI 3 Modern Edition / WPF Desktop / PowerShell CLI
</p>

<br />

---

## Quick Start

> **Download releases**: All program files are available at [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases).

### Option 1: AI Agent Prompt (Highly Recommended ⭐)

Download `steamdb-check-prompt-glo.md` from [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases), instruct OpenClaw, WorkBuddy, Marvis, or QClaw to read it — the AI Agent will autonomously complete the entire detection workflow.<br />
<sub>Includes complete Agent behavior guidelines, Cloudflare handling instructions, and network diagnostics. Best for debugging and automation.</sub><br />
<sub>~May incur costs~</sub>

### Option 2: C# Native Desktop App

Download from [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases):

#### WinUI 3 Modern Edition (v5.1 New 🎉)
Built with WinUI 3 + Fluent Design 2 modern interface, featuring beautiful visuals and smooth animations.
```powershell
# Ready to use, no dependencies required
.\InfiSteam.WinUI.exe
```

#### WPF Version (Stable & Reliable)
Built with WPF + Fluent Design, excellent compatibility and fast startup.
```powershell
# Ready to use, no dependencies required
.\InfiSteam.exe
```

### Option 3: Python GUI (Python environment required)

Download `InfiSteam.exe` (Python GUI Pro standalone) from [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases), or run the source directly:

```powershell
python infi-gui-pro.py
```

### Option 4: Command Line

Download the release archive from [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases), extract it, and run:

```powershell
# Check current status
.\infi-manager.ps1 status

# Fully automated SteamDB detection & update
.\infi-manager.ps1 steamdb-check

# Comprehensive verification
.\infi-manager.ps1 verify
```

### Prerequisites

- **Windows 10 / 11** (x64)
- Steam installed with the game in library
- **Steam must be fully exited before any operation** (including `steamwebhelper` processes)

---

## Which Version Should I Choose?

| Method | Best For | Features |
|--------|----------|----------|
| **AI Agent Prompt** | All users (Recommended) | Fully automated, no manual operation, complete error handling |
| **WinUI 3 Desktop** | Users who love modern UI | Latest Fluent Design 2 interface, best animation effects |
| **WPF Desktop** | Users who prefer stability | Excellent compatibility, low resource usage, long-term proven |
| **Python GUI** | Python environment users | Cross-platform potential, easy for secondary development |
| **Command Line** | Advanced users / Automation | Can be integrated into batch scripts, supports CI/CD |

---

## Full Documentation

For detailed feature descriptions, methodology, configuration reference, and troubleshooting, see:

<p align="center">
  <strong><a href="readme_full.md">📖 Full Documentation (readme_full.md)</a></strong>
</p>

---

## References

- [SteamDB App 3164330](https://steamdb.info/app/3164330/)
- [SteamDB Sub 1221922](https://steamdb.info/sub/1221922/)
- [SteamDB Depot 3164332](https://steamdb.info/depot/3164332/manifests/)
- [Technical Deep-Dive: Steam Update Mechanism & ACF Files](https://cloud.tencent.com/developer/article/2468980)
- [GitHub Releases](https://github.com/qtqtEricChiu/InfiSteam/releases)

---

<p align="center">
  <sub>
    This tool is not affiliated with <strong>Paper Games</strong> / <strong>Infold Games</strong>, <strong>SteamDB</strong>, or <strong>Valve Corporation</strong>.<br />
    Infinity Nikki &copy; 2022 Papergames, ALL RIGHTS RESERVED. Steam is a trademark of Valve Corporation.
  </sub>
</p>
