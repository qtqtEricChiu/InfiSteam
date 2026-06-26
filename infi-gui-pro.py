# -*- coding: utf-8 -*-
"""
InfiSteam GUI Pro — 无限暖暖 Steam 壳管理工具
Version: 5.1 — Modern UI + Direct Steam Detection (C# 移植)
学习 C#/WinUI/WPF 设计理念，直接实现 Steam 检测 / ACF 读取 / 启动器检测
"""

import subprocess, threading, os, json, sys, re, locale
from datetime import datetime
from tkinter import messagebox
from pathlib import Path

# ── 全局子进程隐藏配置 (防止任何 cmd 窗口弹出) ──
_SUPPRESS = subprocess.STARTUPINFO()
_SUPPRESS.dwFlags |= subprocess.STARTF_USESHOWWINDOW
_SUPPRESS.wShowWindow = subprocess.SW_HIDE
_CREATE_NO_WINDOW = subprocess.CREATE_NO_WINDOW | 0x08000000  # CREATE_NO_WINDOW + DETACHED_PROCESS

# ── customtkinter ──
try:
    import customtkinter as ctk
    from customtkinter import CTkImage
except ImportError:
    ctk = None

# ── Paths ──
if getattr(sys, 'frozen', False):
    BASE_DIR = sys._MEIPASS
    INTERNAL = os.path.join(BASE_DIR, "_internal")
    SCRIPT_DIR = INTERNAL if os.path.exists(INTERNAL) else BASE_DIR
else:
    SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

PS_SCRIPT    = os.path.join(SCRIPT_DIR, "infi-manager.ps1")
CONFIG_PATH  = os.path.join(SCRIPT_DIR, "config.json")
ICO_PATH     = os.path.join(SCRIPT_DIR, "ico.ico")

# ── Config ──
def load_config():
    with open(CONFIG_PATH, "r", encoding="utf-8") as f:
        return json.load(f)

try:
    config = load_config()
except:
    config = {"app": {"name": "Infinity Nikki", "appid": "3164330", "description": ""},
              "depots": {"3164332": {}}, "paths": {}, "steamdb": {}}
APP_ID = config["app"]["appid"]
DEPOT_ID = "3164332"

# ── I18N ──
I18N = {
    "zh": {
        "title":"InfiSteam",
        "app_name":"无限暖暖","version":"v5.1 Pro",
        "steam_info":"Steam 信息","game_info":"游戏信息",
        "status_panel":"状态面板","log_panel":"运行日志","control_panel":"控制面板",
        "btn_refresh":"刷新状态","btn_steamdb":"SteamDB 检测","btn_skeletonize":"骨架化清理",
        "btn_restore":"还原 X6Game","btn_dryrun":"骨架化模拟","btn_lock":"锁定 ACF",
        "btn_unlock":"解锁 ACF","btn_verify":"全面验证","btn_launcher":"启动器设置",
        "btn_clear_log":"清空日志","btn_detect":"检测 Steam","btn_read_acf":"读取 ACF",
        "steam_running":"⚠ Steam 正在运行","steam_not_running":"✓ Steam 未运行",
        "ready":"就绪","running":"运行中...","error":"错误",
        "confirm_skeletonize":"即将执行骨架化清理，是否继续？",
        "confirm_restore":"即将执行还原操作，是否继续？",
        "log_start":"开始执行","log_done":"完成","log_fail":"失败",
        "log_steam_ok":"检测到 Steam","log_steam_miss":"未找到 Steam 安装",
        "log_acf_ok":"ACF 文件读取成功","log_acf_miss":"ACF 文件未找到",
        "log_launcher_detected":"检测到独立启动器","log_launcher_none":"未检测到独立启动器",
        "launcher_title":"独立启动器","no_launcher":"未检测到独立启动器",
        "menu_file":"文件","menu_tools":"工具","menu_help":"帮助",
        "menu_exit":"退出","menu_config":"打开配置","menu_readme":"查看说明",
        "acf_state_fmt":"StateFlags: {}\nBuildID: 本地 {}\nManifest: 本地 {}\n只读: {}",
        "acf_readonly_yes":"是","acf_readonly_no":"否",
        "buildid":"BuildID","manifest":"Manifest GID","manifest_header":"Manifest GID",
        "detected":"已检测","not_found":"未找到",
        "log_steam_path":"Steam 路径: {}","log_acf_path":"ACF 路径: {}",
        "log_game_path":"游戏路径: {}",
        "btn_guide":"❓ 功能说明",
        "guide_title":"新手引导 — 功能说明",
        "guide_intro":"欢迎使用 InfiSteam！以下是各功能的简要说明：",
        "tip_detect":"检测 Steam 安装路径、ACF 文件、游戏目录，自动识别版本类型",
        "tip_read_acf":"重新读取 ACF 文件中的详细配置字段",
        "tip_steamdb":"从 SteamDB 获取最新版本号，对比本地 ACF 并自动更新",
        "tip_skeletonize":"将核心游戏数据 (X6Game, ~110GB) 从 Steam 目录移至同盘备份，释放 Steam 目录空间。需要时可一键还原",
        "tip_dryrun":"预览骨架化操作：显示将要移动的文件、大小和备份位置，不实际执行。安全预览后再决定是否执行",
        "tip_restore":"从备份位置将 X6Game 还原到 Steam 目录，恢复完整游戏文件",
        "tip_lock":"将 ACF 文件设为只读，防止 Steam 自动改写",
        "tip_unlock":"取消 ACF 文件的只读属性，允许 Steam 正常写入",
        "tip_verify":"全面检查 ACF 配置、Steam 状态、游戏完整性",
        "tip_launcher":"扫描注册表 + 常见路径 + 开始菜单，检测国服独立启动器",
        "btn_residual":"🧹 残留检查",
        "tip_residual":"检查 ACF 临时文件、残留备份、downloading/temp 目录中的游戏残留",
        "btn_report":"📋 输出报告",
        "tip_report":"生成完整的检测报告：版本、ACF 状态、X6Game 位置、启动器检测等",
        "btn_disclaimer":"© 版权声明",
        "tip_disclaimer":"查看版权声明",
        "disclaimer_title":"版权声明",
        "disclaimer_text":"© mocabolka 2026\n\n本工具与 Valve/Steam、SteamDB、叠纸游戏/Infold Games 无关。\n仅供学习交流使用，请在理解操作后果后自行使用。",
    },
    "en": {
        "title":"InfiSteam",
        "app_name":"Infinity Nikki","version":"v5.1 Pro",
        "steam_info":"Steam Info","game_info":"Game Info",
        "status_panel":"Status","log_panel":"Log","control_panel":"Controls",
        "btn_refresh":"Refresh","btn_steamdb":"SteamDB Check","btn_skeletonize":"Skeletonize",
        "btn_restore":"Restore X6Game","btn_dryrun":"Dry Run","btn_lock":"Lock ACF",
        "btn_unlock":"Unlock ACF","btn_verify":"Verify","btn_launcher":"Launcher",
        "btn_clear_log":"Clear Log","btn_detect":"Detect Steam","btn_read_acf":"Read ACF",
        "steam_running":"⚠ Steam is RUNNING","steam_not_running":"✓ Steam NOT running",
        "ready":"Ready","running":"Running...","error":"Error",
        "confirm_skeletonize":"Skeletonize — continue?",
        "confirm_restore":"Restore — continue?",
        "log_start":"Start","log_done":"Done","log_fail":"Failed",
        "log_steam_ok":"Steam detected","log_steam_miss":"Steam not found",
        "log_acf_ok":"ACF file read OK","log_acf_miss":"ACF file not found",
        "log_launcher_detected":"Standalone launcher detected","log_launcher_none":"No launcher detected",
        "launcher_title":"Launcher","no_launcher":"No standalone launcher detected",
        "menu_file":"File","menu_tools":"Tools","menu_help":"Help",
        "menu_exit":"Exit","menu_config":"Open Config","menu_readme":"View README",
        "acf_state_fmt":"StateFlags: {}\nBuildID: Local {}\nManifest: Local {}\nRead-only: {}",
        "acf_readonly_yes":"Yes","acf_readonly_no":"No",
        "buildid":"BuildID","manifest":"Manifest GID","manifest_header":"Manifest GID",
        "detected":"Detected","not_found":"Not Found",
        "log_steam_path":"Steam path: {}","log_acf_path":"ACF path: {}",
        "log_game_path":"Game path: {}",
        "btn_guide":"❓ Guide",
        "guide_title":"Beginner's Guide — Feature Overview",
        "guide_intro":"Welcome to InfiSteam! Here is a quick overview of each feature:",
        "tip_detect":"Detect Steam installation, ACF file, and game directory; auto-identify version type",
        "tip_read_acf":"Re-read detailed ACF configuration fields",
        "tip_steamdb":"Fetch latest version from SteamDB, compare with local ACF, and auto-update",
        "tip_skeletonize":"Move core game data (X6Game, ~110GB) from Steam directory to backup on same drive. Frees Steam dir space. One-click restore available",
        "tip_dryrun":"Preview skeletonize: shows files to move, sizes, and backup location. No actual changes. Safe preview before committing",
        "tip_restore":"Restore X6Game from backup back to Steam directory",
        "tip_lock":"Set ACF file to read-only to prevent Steam from overwriting",
        "tip_unlock":"Remove ACF read-only attribute, allowing Steam to write normally",
        "tip_verify":"Full check of ACF configuration, Steam status, and game integrity",
        "tip_launcher":"Scan registry + common paths + Start Menu to detect standalone launcher",
        "btn_residual":"🧹 Residual Check",
        "tip_residual":"Check for ACF temp files, residual backups, and downloading/temp dirs",
        "btn_report":"📋 Report",
        "tip_report":"Generate complete status report: version, ACF state, X6Game location, launcher",
        "btn_disclaimer":"© Notice",
        "tip_disclaimer":"View copyright notice",
        "disclaimer_title":"Copyright Notice",
        "disclaimer_text":"© mocabolka 2026\n\nThis tool is not affiliated with Valve/Steam, SteamDB, or Papergames/Infold Games.\nFor learning and exchange purposes only. Use at your own risk.",
    }
}

LANG = "zh"
try:
    lang_code, _ = locale.getdefaultlocale()
    if lang_code and not lang_code.lower().startswith("zh"):
        LANG = "en"
except:
    pass
TR = lambda k: I18N.get(LANG, I18N["zh"]).get(k, k)


# ═══════════════════════════════════════════════════════════════
# 核心引擎 — 移植自 C# WPF 版本 (SteamDetector / AcfManager / Launcher)
# ═══════════════════════════════════════════════════════════════

class SteamInfo:
    """Steam 检测结果 (移植自 C# SteamDetector)"""
    def __init__(self):
        self.steam_path = ""
        self.steamapps_path = ""
        self.acf_path = ""
        self.game_path = ""
        self.found = False
        self.build_id = ""
        self.manifest_gid = ""
        self.state_flags = ""
        self.target_build_id = ""
        self.auto_update = ""
        self.bytes_to_download = ""
        self.is_readonly = False
        self.is_china = False

def detect_steam():
    """检测 Steam 安装 (移植自 C# SteamDetector)"""
    info = SteamInfo()
    steam_path = None

    # 1) 注册表
    try:
        import winreg
        for hive, subkey in [
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Valve\Steam"),
            (winreg.HKEY_CURRENT_USER, r"SOFTWARE\Valve\Steam"),
        ]:
            try:
                with winreg.OpenKey(hive, subkey) as key:
                    val, _ = winreg.QueryValueEx(key, "InstallPath")
                    if val and os.path.isfile(os.path.join(val, "steam.exe")):
                        steam_path = val
                        break
            except:
                continue
        if steam_path:
            pass
    except:
        pass

    # 2) 进程
    if not steam_path:
        try:
            out = subprocess.run(
                ["powershell", "-NoProfile", "-WindowStyle", "Hidden", "-Command",
                 "(Get-Process steam -ErrorAction SilentlyContinue | Select-Object -First 1).Path"],
                capture_output=True, text=True, encoding="utf-8", timeout=5,
                startupinfo=_SUPPRESS, creationflags=_CREATE_NO_WINDOW
            )
            p = out.stdout.strip()
            if p and os.path.isfile(p):
                steam_path = os.path.dirname(p)
        except:
            pass

    # 3) 常见路径
    if not steam_path:
        for base in [os.environ.get(x, "") for x in
                     ["ProgramFiles(x86)", "ProgramFiles", "LOCALAPPDATA"]]:
            if not base:
                continue
            cand = os.path.join(base, "Steam")
            if os.path.isfile(os.path.join(cand, "steam.exe")):
                steam_path = cand
                break

    if not steam_path:
        return info

    info.steam_path = steam_path

    # 查找游戏库 (libraryfolders.vdf)
    libs = [steam_path]
    vdf = os.path.join(steam_path, "steamapps", "libraryfolders.vdf")
    if os.path.isfile(vdf):
        try:
            txt = open(vdf, "r", encoding="utf-8").read()
            for m in re.finditer(r'"path"\s+"([^"]+)"', txt):
                p = m.group(1).replace("\\\\", "\\")
                if p not in libs:
                    libs.append(p)
        except:
            pass

    for lib in libs:
        acf = os.path.join(lib, "steamapps", f"appmanifest_{APP_ID}.acf")
        if os.path.isfile(acf):
            info.steamapps_path = os.path.join(lib, "steamapps")
            info.acf_path = acf
            info.game_path = os.path.join(lib, "steamapps", "common", "Infinity Nikki")
            info.found = True
            # 读取 ACF
            read_acf(info)
            break

    return info

def read_acf(info):
    """读取 ACF 文件 (移植自 C# AcfManager)"""
    if not info.acf_path or not os.path.isfile(info.acf_path):
        return
    try:
        txt = open(info.acf_path, "r", encoding="utf-8").read()
        info.build_id = _extract_acf(txt, "buildid")
        info.manifest_gid = _extract_acf(txt, "manifest")
        info.state_flags = _extract_acf(txt, "StateFlags")
        info.target_build_id = _extract_acf(txt, "TargetBuildID")
        info.auto_update = _extract_acf(txt, "AutoUpdateBehavior")
        info.bytes_to_download = _extract_acf(txt, "BytesToDownload")
        info.is_readonly = not os.access(info.acf_path, os.W_OK)
        # 中国版检测 — 与 C# AcfManager.IsChinaVersion / Prompt 保持一致的三路检测
        info.is_china = (
            "sub/1221922" in txt   # ① SubPackage 标识（最准确）
            or "schinese" in txt    # ② 语言设置为简体中文
            or (info.game_path and (     # ③ China 命名的 pak 文件
                os.path.isdir(paks := os.path.join(info.game_path, "InfinityNikki", "X6Game", "Content", "Paks"))
                and any("China" in f for f in os.listdir(paks) if f.endswith(".pak"))))
        )
    except:
        pass

def _extract_acf(text, key):
    m = re.search(rf'"{key}"\s+"([^"]*)"', text)
    return m.group(1) if m else ""

def detect_launchers():
    """检测独立启动器 (移植自 C# StandaloneLauncherDetector)"""
    found = []
    try:
        import winreg
        # 注册表
        for hive, subkey in [
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (winreg.HKEY_CURRENT_USER, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        ]:
            try:
                with winreg.OpenKey(hive, subkey) as key:
                    for i in range(winreg.QueryInfoKey(key)[0]):
                        try:
                            with winreg.OpenKey(key, winreg.EnumKey(key, i)) as app_key:
                                dn = ""
                                try:
                                    dn, _ = winreg.QueryValueEx(app_key, "DisplayName")
                                except:
                                    continue
                                if not any(kw in dn.lower() for kw in ["infinity", "nikki", "infold"]):
                                    continue
                                try:
                                    loc, _ = winreg.QueryValueEx(app_key, "InstallLocation")
                                except:
                                    continue
                                if not loc or not os.path.isdir(loc):
                                    continue
                                exe = os.path.join(loc, "launcher.exe")
                                if os.path.isfile(exe):
                                    found.append({"path": exe, "source": f"注册表: {dn}"})
                        except:
                            pass
            except:
                pass
    except:
        pass

    # 常见路径
    env_map = {
        "${ProgramFiles}": os.environ.get("ProgramFiles", ""),
        "${ProgramFiles(x86)}": os.environ.get("ProgramFiles(x86)", ""),
        "${LOCALAPPDATA}": os.environ.get("LOCALAPPDATA", ""),
    }
    search_paths = config.get("standalone_launcher", {}).get("search_paths", [])
    for sp in search_paths:
        for key, val in env_map.items():
            sp = sp.replace(key, val)
        exe = os.path.join(sp, "launcher.exe")
        cfg = os.path.join(sp, "config.ini")
        if os.path.isfile(exe):
            gp = None
            if os.path.isfile(cfg):
                try:
                    ct = open(cfg, "r", encoding="utf-8").read()
                    m = re.search(r"game_path\s*=\s*(.+)", ct, re.I)
                    if m:
                        gp = m.group(1).strip()
                except:
                    pass
            found.append({"path": exe, "game_path": gp, "source": "配置文件" if os.path.isfile(cfg) else "常见目录"})

    # 开始菜单
    for start_dir in [
        os.path.join(os.environ.get("APPDATA", ""), r"Microsoft\Windows\Start Menu\Programs"),
        os.path.join(os.environ.get("ALLUSERSPROFILE", ""), r"Microsoft\Windows\Start Menu\Programs"),
    ]:
        if not os.path.isdir(start_dir):
            continue
        for root, dirs, files in os.walk(start_dir):
            for f in files:
                if f.endswith(".lnk") and any(kw in f.lower() for kw in ["infinity", "nikki", "infold"]):
                    # 解析 .lnk
                    try:
                        import ctypes
                        from ctypes import wintypes
                        # 简化: 只记录路径
                        found.append({"path": os.path.join(root, f), "source": "开始菜单"})
                    except:
                        pass
    return found

def check_steam_running():
    """检查 Steam 进程"""
    try:
        r = subprocess.run(
            ["powershell", "-NoProfile", "-WindowStyle", "Hidden", "-Command",
             "Get-Process steam,steamwebhelper -ErrorAction SilentlyContinue | Select-Object -First 1"],
            capture_output=True, text=True, encoding="utf-8", timeout=5,
            startupinfo=_SUPPRESS, creationflags=_CREATE_NO_WINDOW
        )
        return bool(r.stdout.strip())
    except:
        return False


# ═══════════════════════════════════════════════════════════════
# GUI — customtkinter 现代界面
# ═══════════════════════════════════════════════════════════════

class InfiSteamPro(ctk.CTk):
    def __init__(self):
        super().__init__()
        self.title(TR("title"))
        self.geometry("960x720")
        self.minsize(800, 600)

        # 图标
        try:
            self.iconbitmap(ICO_PATH)
        except:
            pass

        ctk.set_appearance_mode("dark")
        ctk.set_default_color_theme("blue")

        # Steam 信息
        self.steam_info = SteamInfo()
        self.worker = None
        self.running = False

        self._build_ui()
        self.after(500, self.auto_detect)

    # ── UI ──
    def _build_ui(self):
        self.grid_columnconfigure(0, weight=1)
        self.grid_rowconfigure(1, weight=1)

        # ─── 顶部标题栏 ───
        header = ctk.CTkFrame(self, height=64, fg_color="transparent")
        header.grid(row=0, column=0, sticky="ew", padx=20, pady=(15,5))
        header.grid_columnconfigure(0, weight=1)

        title_frame = ctk.CTkFrame(header, fg_color="transparent")
        title_frame.grid(row=0, column=0, sticky="w")

        ctk.CTkLabel(title_frame, text=TR("app_name"),
                     font=("Microsoft YaHei UI", 22, "bold")).pack(anchor="w")
        ctk.CTkLabel(title_frame,
                     text=f"AppID: {APP_ID} | {TR('version')}",
                     font=("Microsoft YaHei UI", 11),
                     text_color=("gray60", "gray50")).pack(anchor="w")

        # Steam 状态指示器
        self.steam_indicator = ctk.CTkFrame(header, fg_color="transparent")
        self.steam_indicator.grid(row=0, column=1, sticky="e")
        self.steam_dot = ctk.CTkLabel(self.steam_indicator, text="●",
                                      font=("Segoe UI", 14), text_color="gray60")
        self.steam_dot.pack(side="left", padx=(0,5))
        self.steam_label = ctk.CTkLabel(self.steam_indicator, text="检测中...",
                                        font=("Microsoft YaHei UI", 11),
                                        text_color="gray60")
        self.steam_label.pack(side="left")

        # ❓ 功能引导按钮
        guide_btn = ctk.CTkButton(header, text=TR("btn_guide"), width=30, height=28,
                                  command=self.show_guide, font=("Segoe UI", 12),
                                  fg_color="gray40", hover_color="gray30")
        guide_btn.grid(row=0, column=2, sticky="e", padx=(10,0))

        # ─── 主区域 (左右分栏，响应式，透明背景透出 Mica) ───
        main = ctk.CTkFrame(self, fg_color="transparent")
        main.grid(row=1, column=0, sticky="nsew", padx=15, pady=5)
        main.grid_columnconfigure(0, weight=3)
        main.grid_columnconfigure(1, weight=1)
        main.grid_rowconfigure(0, weight=1)

        # 绑定窗口尺寸变化事件
        self._responsive_mode = "wide"
        self.bind("<Configure>", self._on_resize)

        # 左栏
        left = ctk.CTkFrame(main, fg_color="transparent")
        left.grid(row=0, column=0, sticky="nsew", padx=(0,8))
        left.grid_rowconfigure(0, weight=0)
        left.grid_rowconfigure(1, weight=0)
        left.grid_rowconfigure(2, weight=0)
        left.grid_rowconfigure(3, weight=1)

        # 状态卡片
        status_card = ctk.CTkFrame(left, corner_radius=10)
        status_card.grid(row=0, column=0, sticky="ew", pady=(0,8))

        ctk.CTkLabel(status_card, text=TR("steam_info"),
                     font=("Microsoft YaHei UI", 13, "bold"),
                     anchor="w").pack(fill="x", padx=15, pady=(12,5))

        self.steam_frame = ctk.CTkFrame(status_card, fg_color="transparent")
        self.steam_frame.pack(fill="x", padx=15, pady=(0,12))

        rows = [
            ("Steam", "steam_val", "gray"),
            ("ACF", "acf_val", "gray"),
            ("BuildID", "build_val", "gray"),
            ("Manifest", "manifest_val", "gray"),
            ("版本", "ver_val", "gray"),
        ]
        self.info_labels = {}
        for i, (label, key, color) in enumerate(rows):
            f = ctk.CTkFrame(self.steam_frame, fg_color="transparent")
            f.pack(fill="x", pady=1)
            ctk.CTkLabel(f, text=label, font=("Microsoft YaHei UI", 11),
                        width=80, anchor="w").pack(side="left")
            lbl = ctk.CTkLabel(f, text="—", font=("Consolas", 11),
                              anchor="w", text_color=color)
            lbl.pack(side="left", fill="x", expand=True)
            self.info_labels[key] = lbl

        # 状态栏 (彩色)
        self.status_bar = ctk.CTkFrame(left, height=36, corner_radius=8)
        self.status_bar.grid(row=1, column=0, sticky="ew", pady=(0,8))
        self.status_text = ctk.CTkLabel(self.status_bar, text=TR("ready"),
                                        font=("Microsoft YaHei UI", 11))
        self.status_text.pack(side="left", padx=12, pady=4)

        self.progress = ctk.CTkProgressBar(left, height=4, mode="indeterminate")
        self.progress.grid(row=2, column=0, sticky="ew", pady=(0,8))
        self.progress.grid_remove()

        # 日志面板 — 使用半透明 Mica 风格背景
        log_card = ctk.CTkFrame(left, corner_radius=10,
                                fg_color=("gray92", "gray17"))  # 半透明灰
        log_card.grid(row=3, column=0, sticky="nsew")
        log_card.grid_rowconfigure(1, weight=1)
        log_card.grid_columnconfigure(0, weight=1)

        log_header = ctk.CTkFrame(log_card, fg_color="transparent")
        log_header.grid(row=0, column=0, sticky="ew", padx=15, pady=(10,5))
        ctk.CTkLabel(log_header, text=TR("log_panel"),
                     font=("Microsoft YaHei UI", 13, "bold")).pack(side="left")

        # 清空日志 + 展开按钮
        log_actions = ctk.CTkFrame(log_header, fg_color="transparent")
        log_actions.pack(side="right")
        ctk.CTkButton(log_actions, text="🗖", width=28, height=24,
                      command=self._toggle_log_expand,
                      font=("Segoe UI", 11)).pack(side="right", padx=(4,0))
        ctk.CTkButton(log_actions, text=TR("btn_clear_log"), width=60, height=24,
                      command=self.clear_log, font=("Microsoft YaHei UI", 10)).pack(side="right")

        self.log_text = ctk.CTkTextbox(log_card, font=("Consolas", 11),
                                       corner_radius=6, wrap="word",
                                       fg_color=("gray88", "gray20"))
        self.log_text.grid(row=1, column=0, sticky="nsew", padx=15, pady=(0,15))
        self.log_text.insert("1.0", f"[{self._ts()}] {TR('ready')}\n")

        # ─── 右栏 (控制面板，可滚动) ───
        right = ctk.CTkFrame(main, fg_color="transparent")
        right.grid(row=0, column=1, sticky="nsew", padx=(8,0))
        right.grid_rowconfigure(0, weight=1)

        scroll_right = ctk.CTkScrollableFrame(right, corner_radius=10,
                                               fg_color="transparent")
        scroll_right.grid(row=0, column=0, sticky="nsew")

        control_card = ctk.CTkFrame(scroll_right, corner_radius=10)
        control_card.pack(fill="both", expand=True)

        ctk.CTkLabel(control_card, text=TR("control_panel"),
                     font=("Microsoft YaHei UI", 13, "bold"),
                     anchor="w").pack(fill="x", padx=15, pady=(12,15))

        buttons = [
            ("btn_detect",   self.cmd_detect_steam,   "#0078D4", "tip_detect"),
            ("btn_read_acf", self.cmd_read_acf,        "#107C10", "tip_read_acf"),
            ("btn_steamdb",  self.cmd_steamdb,         "#0078D4", "tip_steamdb"),
            None,
            ("btn_skeletonize", self.cmd_skeletonize,  "#D13438", "tip_skeletonize"),
            ("btn_restore",     self.cmd_restore,      "#107C10", "tip_restore"),
            ("btn_dryrun",      self.cmd_dryrun,       "#666666", "tip_dryrun"),
            None,
            ("btn_lock",  self.cmd_lock,  "#666666", "tip_lock"),
            ("btn_unlock",self.cmd_unlock,"#666666", "tip_unlock"),
            None,
            ("btn_verify",   self.cmd_verify,   "#0078D4", "tip_verify"),
            ("btn_residual", self.cmd_residual, "#666666", "tip_residual"),
            ("btn_report",   self.cmd_report,   "#666666", "tip_report"),
            None,
            ("btn_launcher", self.cmd_launcher, "#666666", "tip_launcher"),
            ("btn_disclaimer", self.cmd_disclaimer, "#666666", "tip_disclaimer"),
        ]

        self._button_widgets = []
        for item in buttons:
            if item is None:
                ctk.CTkFrame(control_card, height=1,
                            fg_color=("gray70","gray30")).pack(fill="x", padx=20, pady=6)
                continue
            key, cmd, color, tip_key = item
            btn = ctk.CTkButton(control_card, text=TR(key),
                         command=cmd, height=38, corner_radius=8,
                         fg_color=color, hover_color=color,
                         font=("Microsoft YaHei UI", 12))
            btn.pack(fill="x", padx=15, pady=4)
            self._button_widgets.append(btn)
            self._bind_tooltip(btn, TR(tip_key))

    # ── 响应式布局 ──
    def _on_resize(self, event=None):
        if event and event.widget == self:
            w = self.winfo_width()
            # 窄屏模式：左栏占满，右栏隐藏（用 toggle 按钮）
            if w < 720 and self._responsive_mode != "narrow":
                self._responsive_mode = "narrow"
                self._toggle_responsive(compact=True)
            elif w >= 720 and self._responsive_mode != "wide":
                self._responsive_mode = "wide"
                self._toggle_responsive(compact=False)

    def _toggle_responsive(self, compact):
        """窄屏下右栏改为可切换的浮动面板"""
        # 简化处理：右栏控件间距自适应
        for btn in self._button_widgets:
            try:
                new_font = ("Microsoft YaHei UI", 10) if compact else ("Microsoft YaHei UI", 12)
                new_h = 32 if compact else 38
                btn.configure(font=new_font, height=new_h)
            except:
                pass

    def _toggle_log_expand(self):
        """展开/折叠日志区域"""
        pass  # 预留：可扩展为弹出独立日志窗口

    # ── 日志 / 状态 ──
    def _ts(self):
        return datetime.now().strftime("%H:%M:%S")

    def log(self, msg, level="info"):
        ts = self._ts()
        prefix = {"info":"ℹ","success":"✓","warning":"⚠","error":"✗"}.get(level, "ℹ")
        text = f"{prefix} [{ts}] {msg}\n"
        self.log_text.insert("end", text)
        self.log_text.see("end")

    def set_status(self, text, kind="info"):
        self.status_text.configure(text=text)
        colors = {"info":"#0078D4","success":"#107C10","warning":"#D13438","error":"#D13438"}
        self.status_bar.configure(fg_color=colors.get(kind, "#0078D4"))
        self.log(text, kind)

    def show_progress(self, show=True):
        if show:
            self.progress.grid()
            self.progress.start()
        else:
            self.progress.stop()
            self.progress.grid_remove()

    def clear_log(self):
        self.log_text.delete("1.0", "end")
        self.log(f"{TR('ready')}", "info")

    def update_steam_indicator(self, running):
        if running:
            self.steam_dot.configure(text_color="#D13438", text="●")
            self.steam_label.configure(text=TR("steam_running"), text_color="#D13438")
        else:
            self.steam_dot.configure(text_color="#107C10", text="●")
            self.steam_label.configure(text=TR("steam_not_running"), text_color="#107C10")

    def update_info_panel(self, info):
        def set_val(key, text, is_ok=True):
            if key in self.info_labels:
                self.info_labels[key].configure(
                    text=text if is_ok else "—",
                    text_color=("#107C10","#4CAF50") if is_ok else ("gray60","gray50"))

        if info.found:
            set_val("steam_val", info.steam_path)
            set_val("acf_val", info.acf_path if info.acf_path else "—",
                    bool(info.acf_path))
            set_val("build_val", info.build_id if info.build_id else "—",
                    bool(info.build_id))
            set_val("manifest_val", info.manifest_gid if info.manifest_gid else "—",
                    bool(info.manifest_gid))
            ver = "中国市场版" if LANG == "zh" else "China"
            if not info.is_china:
                ver = "国际版" if LANG == "zh" else "Global"
            set_val("ver_val", ver)
        else:
            for k in self.info_labels:
                self.info_labels[k].configure(text="—", text_color=("gray60","gray50"))

    # ── 工具提示 ──
    _tooltip = None
    _tooltip_timer = None

    def _bind_tooltip(self, widget, text):
        """绑定悬停工具提示"""
        def enter(e):
            if self._tooltip_timer:
                self.after_cancel(self._tooltip_timer)
            self._tooltip_timer = self.after(500, lambda: self._show_tooltip(widget, text))
        def leave(e):
            if self._tooltip_timer:
                self.after_cancel(self._tooltip_timer)
                self._tooltip_timer = None
            self._hide_tooltip()
        widget.bind("<Enter>", enter, add="+")
        widget.bind("<Leave>", leave, add="+")

    def _show_tooltip(self, widget, text):
        """显示工具提示"""
        self._hide_tooltip()
        self._tooltip = ctk.CTkToplevel(self)
        self._tooltip.wm_overrideredirect(True)
        self._tooltip.attributes("-topmost", True)
        x = widget.winfo_rootx() + 20
        y = widget.winfo_rooty() - 30
        self._tooltip.geometry(f"+{x}+{y}")
        label = ctk.CTkLabel(self._tooltip, text=text,
                            font=("Microsoft YaHei UI", 11),
                            fg_color=("#FFFFCC", "#333333"),
                            text_color=("#000000", "#FFFFFF"),
                            corner_radius=6, padx=10, pady=6,
                            wraplength=320, justify="left")
        label.pack()

    def _hide_tooltip(self):
        if self._tooltip:
            try: self._tooltip.destroy()
            except: pass
            self._tooltip = None
        if self._tooltip_timer:
            self.after_cancel(self._tooltip_timer)
            self._tooltip_timer = None

    # ── 新手引导 ──
    def show_guide(self):
        """显示功能说明引导"""
        if LANG == "zh":
            guide = """📖 InfiSteam 功能说明

🔍 检测 Steam  — 自动检测 Steam 安装路径、ACF 文件、游戏目录和版本类型
🗂 读取 ACF   — 重新读取 ACF 文件中的详细配置字段
🌐 SteamDB 检测 — 从 SteamDB 获取最新版本号，对比并自动更新 ACF

💀 骨架化清理 — 将核心游戏数据 (X6Game, ~110GB) 从 Steam 目录移至同盘备份。
    释放 Steam 目录空间。需要时可一键还原。
🧪 骨架化模拟 — 预览骨架化操作：显示将要移动的文件、大小和备份位置，
    不实际执行任何操作。安全预览后再决定是否执行。
📦 还原 X6Game — 从备份位置将 X6Game 还原到 Steam 目录，恢复完整游戏文件。

🔒 锁定 ACF   — 将 ACF 文件设为只读，防止 Steam 自动改写
🔓 解锁 ACF   — 取消 ACF 只读，允许 Steam 正常写入
✅ 全面验证   — 检查 ACF 配置、Steam 状态、游戏完整性
🧹 残留检查   — 检查 ACF 临时文件、残留备份、downloading/temp 目录
📋 输出报告   — 生成完整的检测报告：路径、版本、ACF 状态、X6Game 位置
🚀 启动器设置 — 扫描注册表 + 常见路径 + 开始菜单，检测独立启动器

💡 新手建议：
1. 先点击「检测 Steam」确认安装
2. 运行 SteamDB 检测获取最新版本
3. 运行「全面验证」确保配置正确
4. 如需释放空间，使用「骨架化模拟」预览后再「骨架化清理」"""
        else:
            guide = """📖 InfiSteam Feature Guide

🔍 Detect Steam  — Detect Steam installation, ACF, game directory & version type
🗂 Read ACF     — Re-read detailed ACF configuration fields
🌐 SteamDB Check — Fetch latest version from SteamDB, compare & auto-update

💀 Skeletonize  — Move core game data (X6Game, ~110GB) from Steam dir to backup.
    Frees Steam directory space. One-click restore available.
🧪 Dry Run     — Preview skeletonize: shows files, sizes & backup location.
    No actual changes. Safe preview before committing.
📦 Restore X6Game — Restore X6Game from backup to Steam directory.

🔒 Lock ACF    — Set ACF read-only to prevent Steam from overwriting
🔓 Unlock ACF  — Remove ACF read-only attribute
✅ Verify      — Check ACF config, Steam status, game integrity
🧹 Residual    — Check for ACF temp files, residual backups, downloading/temp dirs
📋 Report      — Generate complete status report: paths, versions, ACF state, X6Game
🚀 Launcher    — Scan registry + paths + Start Menu for standalone launcher

💡 Beginner Tips:
1. Click "Detect Steam" first to confirm installation
2. Run SteamDB Check to get latest version
3. Run "Verify" to ensure correct configuration
4. To free space, use "Dry Run" to preview before "Skeletonize" """

        msg = ctk.CTkToplevel(self)
        msg.title(TR("guide_title"))
        msg.geometry("520x550")
        msg.resizable(False, False)
        msg.transient(self)
        msg.grab_set()
        try: self._set_window_icon(msg)
        except: pass
        try: msg.attributes("-topmost", True)
        except: pass

        frame = ctk.CTkFrame(msg, corner_radius=12)
        frame.pack(fill="both", expand=True, padx=15, pady=15)

        ctk.CTkLabel(frame, text=TR("guide_title"),
                     font=("Microsoft YaHei UI", 16, "bold"),
                     anchor="w").pack(fill="x", padx=15, pady=(15,5))

        textbox = ctk.CTkTextbox(frame, font=("Microsoft YaHei UI", 12),
                                 wrap="word", corner_radius=8)
        textbox.pack(fill="both", expand=True, padx=15, pady=(5,15))
        textbox.insert("1.0", guide)
        textbox.configure(state="disabled")

        ctk.CTkButton(frame, text="关闭" if LANG == "zh" else "Close",
                     command=msg.destroy, width=80, height=32).pack(pady=(0,12))

    # ── 命令 ──
    def auto_detect(self):
        """启动时自动检测"""
        self.cmd_detect_steam()

    def cmd_detect_steam(self):
        """检测 Steam (移植 C# SteamDetector)"""
        self.show_progress(True)
        self.set_status(TR("log_start")+": Steam 检测...", "info")

        def run():
            info = detect_steam()
            self.steam_info = info
            self.after(0, lambda: self._on_detect_done(info))

        threading.Thread(target=run, daemon=True).start()

    def _on_detect_done(self, info):
        self.show_progress(False)
        steam_running = check_steam_running()
        self.update_steam_indicator(steam_running)
        self.update_info_panel(info)

        if info.found:
            self.set_status(TR("log_steam_ok"), "success")
            self.log(TR("log_steam_path").format(info.steam_path), "success")
            if info.acf_path:
                self.log(TR("log_acf_path").format(info.acf_path), "success")
            if info.game_path:
                self.log(TR("log_game_path").format(info.game_path), "success")
            self.log(f"BuildID={info.build_id}, Manifest={info.manifest_gid}", "info")
            if info.is_readonly:
                self.log("ACF 已锁定只读", "warning")
        else:
            self.set_status(TR("log_steam_miss"), "warning")

    def cmd_read_acf(self):
        """读取 ACF"""
        if not self.steam_info.acf_path:
            self.set_status(TR("log_acf_miss"), "warning")
            return
        read_acf(self.steam_info)
        self.update_info_panel(self.steam_info)
        info = self.steam_info
        self.log(TR("log_acf_ok"), "success")
        self.log(f"StateFlags={info.state_flags}, TargetBuildID={info.target_build_id}", "info")
        self.log(f"AutoUpdate={info.auto_update}, BytesToDownload={info.bytes_to_download}", "info")
        self.log(f"只读: {TR('acf_readonly_yes') if info.is_readonly else TR('acf_readonly_no')}", "info")
        if info.is_china:
            ver = "中国市场版" if LANG == "zh" else "China Edition"
        else:
            ver = "国际版" if LANG == "zh" else "Global Edition"
        self.log(f"版本类型: {ver}", "info")

    def _run_ps(self, command, callback=None):
        """异步执行 PowerShell 命令"""
        self.running = True
        self.show_progress(True)
        self.set_status(f"{TR('running')}: {command}", "info")

        def run():
            try:
                ps_cmd = f"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; & '{PS_SCRIPT}' {command}"
                proc = subprocess.Popen(
                    ["powershell", "-NoProfile", "-WindowStyle", "Hidden", "-ExecutionPolicy", "Bypass",
                     "-Command", ps_cmd],
                    stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                    text=True, encoding="utf-8", errors="replace",
                    startupinfo=_SUPPRESS, creationflags=_CREATE_NO_WINDOW
                )
                for line in proc.stdout:
                    line = line.rstrip()
                    if line and callback:
                        self.after(0, callback, line)
                proc.wait()
                self.after(0, lambda: self._on_ps_done(command, proc.returncode))
            except Exception as e:
                self.after(0, lambda: self.set_status(f"{TR('error')}: {e}", "error"))
            finally:
                self.running = False

        threading.Thread(target=run, daemon=True).start()

    def _on_ps_done(self, command, code):
        self.show_progress(False)
        steam_running = check_steam_running()
        self.update_steam_indicator(steam_running)
        if code == 0:
            self.set_status(f"{TR('log_done')}: {command}", "success")
        else:
            self.set_status(f"{TR('log_fail')} (code={code}): {command}", "error")

    def _output_callback(self, line):
        self.log(line, "info")

    def cmd_steamdb(self):
        self._run_ps("steamdb-check", self._output_callback)

    def cmd_skeletonize(self):
        if messagebox.askyesno("⚠️", TR("confirm_skeletonize")):
            self._run_ps("skeletonize", self._output_callback)

    def cmd_restore(self):
        if messagebox.askyesno("⚠️", TR("confirm_restore")):
            self._run_ps("restore", self._output_callback)

    def cmd_dryrun(self):
        self._run_ps("skeletonize -DryRun", self._output_callback)

    def cmd_lock(self):
        self._run_ps("lock", self._output_callback)

    def cmd_unlock(self):
        self._run_ps("unlock", self._output_callback)

    def cmd_verify(self):
        self._run_ps("verify", self._output_callback)

    def cmd_launcher(self):
        """检测独立启动器 (移植 C# StandaloneLauncherDetector)"""
        self.show_progress(True)
        self.set_status("正在检测独立启动器...", "info")

        def run():
            launchers = detect_launchers()
            self.after(0, lambda: self._on_launcher_done(launchers))

        threading.Thread(target=run, daemon=True).start()

    def _on_launcher_done(self, launchers):
        self.show_progress(False)
        if not launchers:
            self.set_status(TR("log_launcher_none"), "warning")
            messagebox.showinfo(TR("launcher_title"), TR("no_launcher"))
            return

        text = ""
        for i, l in enumerate(launchers, 1):
            text += f"  {i}. {l['path']}\n"
            if l.get("game_path"):
                text += f"     游戏: {l['game_path']}\n"
            if l.get("source"):
                text += f"     来源: {l['source']}\n"
            text += "\n"

        launch_opt = f'"{launchers[0]["path"]}" %command%'
        text += f"\n推荐启动选项:\n{launch_opt}"

        self.log(f"检测到 {len(launchers)} 个启动器", "success")
        for l in launchers:
            self.log(f"  {l['path']} ({l.get('source','?')})", "success")
        messagebox.showinfo(TR("launcher_title"), text)

    # ── 新增功能：残留检查、输出报告、免责声明 ──

    def cmd_residual(self):
        """残留文件检查"""
        self._run_ps("residual-check", self._output_callback)

    def cmd_report(self):
        """输出完整报告（以窗口形式弹出）"""
        self.show_progress(True)
        self.set_status("正在生成报告...", "info")
        self.running = True

        def run():
            lines = []
            try:
                ps_cmd = f"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; & '{PS_SCRIPT}' report"
                proc = subprocess.Popen(
                    ["powershell", "-NoProfile", "-WindowStyle", "Hidden", "-ExecutionPolicy", "Bypass",
                     "-Command", ps_cmd],
                    stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                    text=True, encoding="utf-8", errors="replace",
                    startupinfo=_SUPPRESS, creationflags=_CREATE_NO_WINDOW
                )
                for line in proc.stdout:
                    lines.append(line.rstrip())
                proc.wait()
            except Exception as e:
                lines.append(f"[ERROR] {e}")

            self.after(0, lambda: self._show_report_window(TR("btn_report"), "\n".join(lines)))
            self.after(0, lambda: self.show_progress(False))
            self.after(0, lambda: setattr(self, 'running', False))

        threading.Thread(target=run, daemon=True).start()

    def cmd_disclaimer(self):
        """显示免责声明"""
        self.show_disclaimer()

    def show_disclaimer(self):
        """弹出版权声明对话框"""
        title = TR("disclaimer_title")
        text = TR("disclaimer_text")
        try:
            msg = ctk.CTkToplevel(self)
        except:
            return
        msg.title(title)
        msg.geometry("480x320")
        msg.resizable(False, False)
        msg.transient(self)
        msg.grab_set()
        self._set_window_icon(msg)
        try:
            msg.attributes("-topmost", True)
        except:
            pass

        frame = ctk.CTkFrame(msg, corner_radius=12)
        frame.pack(fill="both", expand=True, padx=15, pady=15)

        ctk.CTkLabel(frame, text=title,
                     font=("Microsoft YaHei UI", 16, "bold")).pack(pady=(15, 10))

        textbox = ctk.CTkTextbox(frame, font=("Microsoft YaHei UI", 12),
                                 wrap="word", corner_radius=8, height=140)
        textbox.pack(fill="both", expand=True, padx=15, pady=(0, 15))
        textbox.insert("1.0", text)
        textbox.configure(state="disabled")

        ctk.CTkButton(frame, text="关闭" if LANG == "zh" else "Close",
                      command=msg.destroy, width=80, height=32).pack(pady=(0, 12))

    def _set_window_icon(self, window):
        """为弹出窗口设置自定义图标"""
        try:
            window.after(100, lambda: window.iconbitmap(ICO_PATH))
        except:
            pass

    def _show_report_window(self, title, content):
        """以窗口形式显示报告内容"""
        msg = ctk.CTkToplevel(self)
        msg.title(title)
        msg.geometry("680x500")
        msg.resizable(True, True)
        msg.transient(self)
        msg.grab_set()
        self._set_window_icon(msg)
        try: msg.attributes("-topmost", True)
        except: pass

        frame = ctk.CTkFrame(msg, corner_radius=12)
        frame.pack(fill="both", expand=True, padx=15, pady=15)
        frame.grid_rowconfigure(1, weight=1)
        frame.grid_columnconfigure(0, weight=1)

        ctk.CTkLabel(frame, text=title,
                     font=("Microsoft YaHei UI", 16, "bold"),
                     anchor="w").grid(row=0, column=0, sticky="ew", padx=15, pady=(15,5))

        textbox = ctk.CTkTextbox(frame, font=("Consolas", 11),
                                 wrap="word", corner_radius=8)
        textbox.grid(row=1, column=0, sticky="nsew", padx=15, pady=(5,15))
        textbox.insert("1.0", content)
        textbox.configure(state="disabled")

        ctk.CTkButton(frame, text="关闭" if LANG == "zh" else "Close",
                      command=msg.destroy, width=80, height=32).grid(row=2, column=0, pady=(0,12))


# ═══════════════════════════════════════════════════════════════
# 入口
# ═══════════════════════════════════════════════════════════════

def main():
    if ctk is None:
        messagebox.showerror("依赖缺失",
            "请先安装 customtkinter:\n\npip install customtkinter")
        return

    # 高 DPI
    try:
        from ctypes import windll
        windll.shcore.SetProcessDpiAwareness(1)
    except:
        pass

    app = InfiSteamPro()

    # 启用 Windows 11 100% Mica 背景 (Win11 22000+)
    try:
        from ctypes import windll, c_int, c_long, byref, Structure

        class MARGINS(Structure):
            _fields_ = [("cxLeftWidth", c_long), ("cxRightWidth", c_long),
                        ("cyTopHeight", c_long), ("cyBottomHeight", c_long)]

        app.update_idletasks()
        hwnd = windll.user32.GetParent(app.winfo_id())

        # 1) 沉浸式深色模式
        windll.dwmapi.DwmSetWindowAttribute(hwnd, 20, byref(c_int(1)), c_int(4))
        # 2) 启用 Mica 效果
        windll.dwmapi.DwmSetWindowAttribute(hwnd, 1029, byref(c_int(1)), c_int(4))
        # 3) 将 Mica 延展至整个客户区 → 100% 云母覆盖，消除白色底边
        margins = MARGINS(-1, -1, -1, -1)
        windll.dwmapi.DwmExtendFrameIntoClientArea(hwnd, byref(margins))
        # 4) 让 CTk 自身背景透明，仅保留控件自身的颜色
        app.configure(fg_color=("transparent", "transparent"))
    except:
        pass

    app.mainloop()

if __name__ == "__main__":
    main()
