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
  <sub>This document and the accompanying program contain AI-assisted content. Does not reflect personal endorsement.</sub>
</p>

---

<br />

<p align="center">
  <strong>InfiSteam</strong><br />
  ▸ Forges ACF version info + auto-scrapes latest BuildID / Manifest GID from SteamDB via Chrome CDP for one-click version sync<br />
  ▸ Skeletonizes the Steam shell directory, relocating core game data to a same-disk backup to free space with instant restore capability<br />
  ▸ Supports three operation modes: AI Agent auto-detection / Python GUI / PowerShell CLI
</p>

<br />

---

## Quick Start

### Option 1: C# Native Desktop App (WPF · Recommended)

Ready to use — double-click `C#/build/InfiSteam.exe`. No Python or .NET runtime required.

### Option 2: AI Agent Prompt

Instruct OpenClaw, WorkBuddy, Marvis, or QClaw to read `steamdb-check-prompt-glo.md` — the AI Agent will autonomously complete the entire detection workflow.<br />
<sub>~May incur costs~</sub>

### Option 3: Python GUI

```powershell
python infi-gui.py
```

### Option 4: Command Line

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

---

<p align="center">
  <sub>
    This tool is not affiliated with <strong>Paper Games</strong> / <strong>Infold Games</strong>, <strong>SteamDB</strong>, or <strong>Valve Corporation</strong>.<br />
    Infinity Nikki &copy; 2022 Papergames, ALL RIGHTS RESERVED. Steam is a trademark of Valve Corporation.
  </sub>
</p>