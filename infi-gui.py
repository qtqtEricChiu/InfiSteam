# -*- coding: utf-8 -*-
"""
infi-gui.py - 无限暖暖 Steam 壳管理 GUI 启动器
Version: 3.0 - Fluent Design + 中文日志 + 编译支持
"""

import subprocess
import threading
import tkinter as tk
from tkinter import scrolledtext, messagebox
import os
import json
import sys
import shutil
from datetime import datetime

# 尝试导入 ttkbootstrap，如果失败则回退到标准 tkinter
try:
    import ttkbootstrap as ttk
    from ttkbootstrap.constants import *
    from ttkbootstrap.scrolled import ScrolledText
    from ttkbootstrap.toast import ToastNotification
    TTKBOOTSTRAP_AVAILABLE = True
except ImportError:
    from tkinter import ttk
    TTKBOOTSTRAP_AVAILABLE = False
    print("[WARN] ttkbootstrap 未安装，使用标准 tkinter")

# 支持 PyInstaller 打包后的路径
if getattr(sys, 'frozen', False):
    # 打包后的环境（PyInstaller）
    # PyInstaller 创建临时文件夹解压文件
    # sys._MEIPASS 指向临时目录
    base_dir = sys._MEIPASS
    # 检查文件是否在 _internal 子目录中（PyInstaller --onedir 模式）
    internal_dir = os.path.join(base_dir, "_internal")
    if os.path.exists(internal_dir):
        SCRIPT_DIR = internal_dir
    else:
        SCRIPT_DIR = base_dir
else:
    # 开发环境
    SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

PS_SCRIPT = os.path.join(SCRIPT_DIR, "infi-manager.ps1")
CONFIG_PATH = os.path.join(SCRIPT_DIR, "config.json")

# 调试信息（开发时使用）
if not getattr(sys, 'frozen', False):
    print(f"[DEBUG] SCRIPT_DIR: {SCRIPT_DIR}")
    print(f"[DEBUG] PS_SCRIPT exists: {os.path.exists(PS_SCRIPT)}")
    print(f"[DEBUG] CONFIG_PATH exists: {os.path.exists(CONFIG_PATH)}")

# ========== 国际化字符串表（完整中文） ==========
I18N = {
    "zh": {
        # 窗口标题
        "title": "无限暖暖 Steam 壳管理器",
        "title_suffix": " - Steam 壳管理器",
        "app_name": "无限暖暖",
        "version": "版本 3.0",
        
        # 面板标题
        "status_panel": "📊 状态面板",
        "log_panel": "📝 运行日志",
        "control_panel": "🎮 控制面板",
        "info_panel": "ℹ️ 系统信息",
        
        # 按钮文本
        "btn_refresh": "🔄 刷新状态",
        "btn_steamdb_check": "🔍 SteamDB 自动检测",
        "btn_skeletonize": "💀 骨架化清理",
        "btn_restore": "📦 还原 X6Game",
        "btn_dryrun": "🧪 骨架化模拟",
        "btn_lock": "🔒 锁定 ACF",
        "btn_unlock": "🔓 解锁 ACF",
        "btn_verify": "✅ 全面验证",
        "btn_launcher": "🚀 启动器设置",
        "btn_clear_log": "🧹 清空日志",
        
        # 状态文本
        "steam_running_warn": "⚠️ Steam 正在运行 - 修改前请先退出",
        "steam_not_running_ok": "✅ Steam 未运行 - 可以安全修改",
        "running": "⏳ 运行中...",
        "ready": "✅ 就绪",
        "error": "❌ 错误",
        "checking": "🔍 检测中...",
        
        # 确认对话框
        "confirm_title": "⚠️ 确认操作",
        "confirm_skeletonize": "即将执行骨架化清理：\n\n"
                              "• 将 X6Game 从 Steam 目录移动至备份位置\n"
                              "• 释放 Steam 目录空间\n"
                              "• 保留游戏核心文件\n\n"
                              "是否继续？",
        "confirm_restore": "即将执行还原操作：\n\n"
                          "• 将 X6Game 从备份位置还原到 Steam 目录\n"
                          "• 恢复完整游戏文件\n\n"
                          "是否继续？",
        
        # 命令名称
        "cmd_status": "状态检测",
        "cmd_verify": "全面验证",
        "cmd_skeletonize": "骨架化清理",
        "cmd_restore": "还原 X6Game",
        "cmd_dryrun": "骨架化模拟",
        "cmd_lock": "锁定 ACF",
        "cmd_unlock": "解锁 ACF",
        "cmd_steamdb": "SteamDB 检测",
        "cmd_launcher": "启动器设置",
        
        # 完成信息
        "finished": "✅ 完成 (退出码={})",
        "finished_error": "❌ 失败 (退出码={})",
        "steamdb_success": "🎉 SteamDB 检测完成 - 版本已是最新",
        "steamdb_error": "❌ SteamDB 检测失败",
        
        # 启动器信息
        "launcher_title": "🚀 非 Steam 启动器",
        "launcher_info": "检测到以下非 Steam 版本启动器：\n\n{launchers}\n\n"
                        "💡 配置建议：\n"
                        "在 Steam 游戏属性 -> 启动选项中输入：\n"
                        '"{path}" %command%',
        "no_launcher": "未检测到非 Steam 版本启动器\n\n"
                      "可能原因：\n"
                      "• 未安装独立启动器\n"
                      "• 启动器不在常见位置\n"
                      "• 启动器名称不匹配",
        "launcher_btn_config": "配置启动选项",
        
        # 日志前缀
        "log_start": "▶️ 开始执行",
        "log_end": "⏹️ 执行结束",
        "log_error": "💥 发生错误",
        "log_time": "🕐",
        
        # 菜单
        "menu_file": "文件",
        "menu_tools": "工具",
        "menu_help": "帮助",
        "menu_exit": "退出",
        "menu_config": "打开配置",
        "menu_readme": "查看说明",
        
        # 工具提示
        "tip_refresh": "刷新当前状态",
        "tip_steamdb": "从 SteamDB 获取最新版本信息",
        "tip_skeletonize": "移动 X6Game 到备份位置",
        "tip_restore": "从备份还原 X6Game",
        "tip_verify": "验证所有配置是否正确",
    },
    "en": {
        "title": "Infinity Nikki Steam Shell Manager",
        "title_suffix": " - Steam Shell Manager",
        "app_name": "Infinity Nikki",
        "version": "Version 3.0",
        "status_panel": "📊 Status",
        "log_panel": "📝 Log",
        "control_panel": "🎮 Controls",
        "info_panel": "ℹ️ Info",
        "btn_refresh": "🔄 Refresh",
        "btn_steamdb_check": "🔍 SteamDB Check",
        "btn_skeletonize": "💀 Skeletonize",
        "btn_restore": "📦 Restore X6Game",
        "btn_dryrun": "🧪 Dry Run",
        "btn_lock": "🔒 Lock ACF",
        "btn_unlock": "🔓 Unlock ACF",
        "btn_verify": "✅ Verify",
        "btn_launcher": "🚀 Launcher",
        "btn_clear_log": "🧹 Clear Log",
        "steam_running_warn": "⚠️ Steam is RUNNING - exit before modifying",
        "steam_not_running_ok": "✅ Steam NOT running - safe to modify",
        "running": "⏳ Running...",
        "ready": "✅ Ready",
        "error": "❌ Error",
        "checking": "🔍 Checking...",
        "confirm_title": "⚠️ Confirm",
        "confirm_skeletonize": "This will MOVE X6Game from Steam dir to backup.\n\nContinue?",
        "confirm_restore": "This will RESTORE X6Game from backup to Steam dir.\n\nContinue?",
        "cmd_status": "Status",
        "cmd_verify": "Verify",
        "cmd_skeletonize": "Skeletonize",
        "cmd_restore": "Restore",
        "cmd_dryrun": "Dry Run",
        "cmd_lock": "Lock",
        "cmd_unlock": "Unlock",
        "cmd_steamdb": "SteamDB Check",
        "cmd_launcher": "Launcher",
        "finished": "✅ Finished (exit={})",
        "finished_error": "❌ Failed (exit={})",
        "steamdb_success": "🎉 SteamDB check completed",
        "steamdb_error": "❌ SteamDB check failed",
        "launcher_title": "🚀 Standalone Launcher",
        "launcher_info": "Detected standalone launcher(s):\n\n{launchers}\n\nConfigure in Steam Properties -> Launch Options:\n\"{path}\" %command%",
        "no_launcher": "No standalone launcher detected",
        "launcher_btn_config": "Configure",
        "log_start": "▶️ Start",
        "log_end": "⏹️ End",
        "log_error": "💥 Error",
        "log_time": "🕐",
        "menu_file": "File",
        "menu_tools": "Tools",
        "menu_help": "Help",
        "menu_exit": "Exit",
        "menu_config": "Open Config",
        "menu_readme": "View README",
        "tip_refresh": "Refresh current status",
        "tip_steamdb": "Get latest version from SteamDB",
        "tip_skeletonize": "Move X6Game to backup",
        "tip_restore": "Restore X6Game from backup",
        "tip_verify": "Verify all configurations",
    }
}


def detect_language():
    """根据系统区域设置检测语言，默认中文"""
    import locale
    try:
        lang, _ = locale.getdefaultlocale()
        if lang and lang.lower().startswith("zh"):
            return "zh"
    except Exception:
        pass
    env_lang = os.environ.get("LANG", "")
    if "zh" in env_lang.lower():
        return "zh"
    return "zh"


LANG = detect_language()
_ = lambda key: I18N[LANG].get(key, key)


def load_config():
    with open(CONFIG_PATH, "r", encoding="utf-8") as f:
        return json.load(f)


config = load_config()
APP_NAME = config["app"]["name"]
APP_ID = config["app"]["appid"]


def run_ps(command, callback=None):
    """Run PowerShell infi-manager.ps1 and stream output to callback"""
    cmd = [
        "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
        "-File", PS_SCRIPT, command
    ]
    try:
        proc = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
            errors="replace",
            creationflags=subprocess.CREATE_NO_WINDOW
        )
        for line in proc.stdout:
            line = line.rstrip()
            if callback:
                callback(line)
        proc.wait()
        return proc.returncode
    except Exception as e:
        if callback:
            callback(f"❌ [错误] {e}")
        return 1


def get_timestamp():
    """获取当前时间戳"""
    return datetime.now().strftime("%H:%M:%S")


class InfiGUI:
    def __init__(self, root):
        self.root = root
        self.root.title(_("title"))
        self.root.geometry("900x700")
        self.root.minsize(800, 600)
        
        # 设置 DPI 感知
        try:
            from ctypes import windll
            windll.shcore.SetProcessDpiAwareness(1)
        except Exception:
            pass
        
        # 应用 Fluent Design 风格
        self._setup_fluent_style()
        
        # 创建菜单栏
        self._create_menu()
        
        # 创建主布局
        self._create_layout()
        
        # 初始状态加载
        self.root.after(300, self.cmd_status)
    
    def _setup_fluent_style(self):
        """设置 Fluent Design 风格"""
        if TTKBOOTSTRAP_AVAILABLE:
            # 使用 morph 主题（最接近 Fluent Design）
            self.style = ttk.Style(theme="morph")
            
            # 自定义 Fluent Design 颜色
            self.colors = {
                "primary": "#0078D4",      # WinUI 3 主蓝色
                "primary_light": "#106EBE", # 悬停蓝色
                "primary_dark": "#005A9E",  # 按下蓝色
                "bg": "#F3F3F3",           # 背景色
                "surface": "#FFFFFF",       # 卡片背景
                "text": "#323130",         # 主文本
                "text_secondary": "#605E5C", # 次要文本
                "success": "#107C10",      # 成功绿
                "warning": "#FFC107",      # 警告黄
                "error": "#D13438",        # 错误红
                "border": "#E1DFDD",       # 边框色
            }
            
            # 配置自定义样式
            self.style.configure("Fluent.TButton",
                font=("Microsoft YaHei UI", 10),
                padding=(20, 10),
            )
            
            self.style.configure("FluentPrimary.TButton",
                font=("Microsoft YaHei UI", 10, "bold"),
                padding=(20, 10),
            )
            
            self.style.configure("Card.TLabelframe",
                background=self.colors["surface"],
                borderwidth=1,
                relief="solid",
            )
            
            self.style.configure("Card.TLabelframe.Label",
                font=("Microsoft YaHei UI", 11, "bold"),
                foreground=self.colors["text"],
            )
            
            self.style.configure("Header.TLabel",
                font=("Microsoft YaHei UI", 16, "bold"),
                foreground=self.colors["text"],
            )
            
            self.style.configure("Subheader.TLabel",
                font=("Microsoft YaHei UI", 10),
                foreground=self.colors["text_secondary"],
            )
            
            self.style.configure("Status.TLabel",
                font=("Microsoft YaHei UI", 9),
                padding=5,
            )
            
            # 日志样式
            self.style.configure("Log.TScrolledText",
                font=("Cascadia Code", 9),
                background="#1E1E1E",
                foreground="#D4D4D4",
            )
        else:
            # 标准 tkinter 回退
            self.colors = {
                "primary": "#0078D4",
                "bg": "#F3F3F3",
                "surface": "#FFFFFF",
                "text": "#323130",
                "text_secondary": "#605E5C",
                "success": "#107C10",
                "warning": "#FFC107",
                "error": "#D13438",
                "border": "#E1DFDD",
            }
    
    def _create_menu(self):
        """创建菜单栏"""
        menubar = tk.Menu(self.root)
        self.root.config(menu=menubar)
        
        # 文件菜单
        file_menu = tk.Menu(menubar, tearoff=0)
        menubar.add_cascade(label=_("menu_file"), menu=file_menu)
        file_menu.add_command(label=_("menu_config"), command=self._open_config)
        file_menu.add_separator()
        file_menu.add_command(label=_("menu_exit"), command=self.root.quit)
        
        # 工具菜单
        tools_menu = tk.Menu(menubar, tearoff=0)
        menubar.add_cascade(label=_("menu_tools"), menu=tools_menu)
        tools_menu.add_command(label=_("btn_clear_log"), command=self._clear_log)
        
        # 帮助菜单
        help_menu = tk.Menu(menubar, tearoff=0)
        menubar.add_cascade(label=_("menu_help"), menu=help_menu)
        help_menu.add_command(label=_("menu_readme"), command=self._open_readme)
    
    def _create_layout(self):
        """创建主布局"""
        # 主容器
        main_container = ttk.Frame(self.root, padding=15)
        main_container.pack(fill=tk.BOTH, expand=True)
        
        # === 顶部标题栏 ===
        header_frame = ttk.Frame(main_container)
        header_frame.pack(fill=tk.X, pady=(0, 15))
        
        # 应用图标/标题
        title_frame = ttk.Frame(header_frame)
        title_frame.pack(side=tk.LEFT)
        
        ttk.Label(title_frame, text=_("app_name"), 
                 style="Header.TLabel" if TTKBOOTSTRAP_AVAILABLE else "").pack(anchor=tk.W)
        
        ttk.Label(title_frame, text=f"AppID: {APP_ID} | Depot: 3164332 | {_('version')}",
                 style="Subheader.TLabel" if TTKBOOTSTRAP_AVAILABLE else "").pack(anchor=tk.W)
        
        # Steam 状态指示器
        self.steam_status_frame = ttk.Frame(header_frame)
        self.steam_status_frame.pack(side=tk.RIGHT)
        
        self.steam_status_icon = ttk.Label(self.steam_status_frame, text="●", 
                                          font=("Microsoft YaHei UI", 12))
        self.steam_status_icon.pack(side=tk.LEFT, padx=(0, 5))
        
        self.steam_status_text = ttk.Label(self.steam_status_frame, text="",
                                          font=("Microsoft YaHei UI", 9))
        self.steam_status_text.pack(side=tk.LEFT)
        
        # === 中间内容区（左右分栏）===
        content_frame = ttk.Frame(main_container)
        content_frame.pack(fill=tk.BOTH, expand=True)
        content_frame.columnconfigure(0, weight=3)
        content_frame.columnconfigure(1, weight=1)
        
        # 左侧面板：状态 + 日志
        left_frame = ttk.Frame(content_frame)
        left_frame.grid(row=0, column=0, sticky="nsew", padx=(0, 10))
        left_frame.rowconfigure(0, weight=2)
        left_frame.rowconfigure(1, weight=1)
        
        # 状态面板
        status_card = ttk.LabelFrame(left_frame, text=_("status_panel"))
        status_card.grid(row=0, column=0, sticky="nsew", pady=(0, 10), padx=10)
        
        self.status_text = ScrolledText(
            status_card, wrap=tk.WORD,
            font=("Cascadia Code", 9),
            bg="#1E1E1E", fg="#D4D4D4",
            insertbackground="#D4D4D4",
            relief=tk.FLAT,
            state=tk.DISABLED,
        )
        self.status_text.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        # 日志面板
        log_card = ttk.LabelFrame(left_frame, text=_("log_panel"))
        log_card.grid(row=1, column=0, sticky="nsew", padx=10, pady=(0, 10))
        
        self.log_text = ScrolledText(
            log_card, wrap=tk.WORD, height=8,
            font=("Cascadia Code", 8),
            bg="#1E1E1E", fg="#808080",
            insertbackground="#808080",
            relief=tk.FLAT,
            state=tk.DISABLED,
        )
        self.log_text.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        # 右侧面板：控制按钮
        right_frame = ttk.Frame(content_frame)
        right_frame.grid(row=0, column=1, sticky="nsew", padx=(0, 10))
        
        # 控制面板
        control_card = ttk.LabelFrame(right_frame, text=_("control_panel"))
        control_card.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        # 按钮配置
        buttons = [
            {
                "text": _("btn_refresh"),
                "command": self.cmd_status,
                "style": "FluentPrimary.TButton" if TTKBOOTSTRAP_AVAILABLE else None,
                "tip": _("tip_refresh"),
            },
            {
                "text": _("btn_steamdb_check"),
                "command": self.cmd_steamdb_check,
                "style": "FluentPrimary.TButton" if TTKBOOTSTRAP_AVAILABLE else None,
                "tip": _("tip_steamdb"),
            },
            {"type": "separator"},
            {
                "text": _("btn_skeletonize"),
                "command": self.cmd_skeletonize,
                "style": "Fluent.TButton" if TTKBOOTSTRAP_AVAILABLE else None,
                "tip": _("tip_skeletonize"),
            },
            {
                "text": _("btn_restore"),
                "command": self.cmd_restore,
                "style": "Fluent.TButton" if TTKBOOTSTRAP_AVAILABLE else None,
                "tip": _("tip_restore"),
            },
            {
                "text": _("btn_dryrun"),
                "command": self.cmd_dryrun,
                "style": "Fluent.TButton" if TTKBOOTSTRAP_AVAILABLE else None,
                "tip": _("tip_skeletonize"),
            },
            {"type": "separator"},
            {
                "text": _("btn_lock"),
                "command": self.cmd_lock,
                "style": "Fluent.TButton" if TTKBOOTSTRAP_AVAILABLE else None,
            },
            {
                "text": _("btn_unlock"),
                "command": self.cmd_unlock,
                "style": "Fluent.TButton" if TTKBOOTSTRAP_AVAILABLE else None,
            },
            {"type": "separator"},
            {
                "text": _("btn_verify"),
                "command": self.cmd_verify,
                "style": "FluentPrimary.TButton" if TTKBOOTSTRAP_AVAILABLE else None,
                "tip": _("tip_verify"),
            },
            {
                "text": _("btn_launcher"),
                "command": self.cmd_launcher,
                "style": "Fluent.TButton" if TTKBOOTSTRAP_AVAILABLE else None,
            },
        ]
        
        for item in buttons:
            if item.get("type") == "separator":
                ttk.Separator(control_card, orient=tk.HORIZONTAL).pack(fill=tk.X, pady=8)
                continue
            
            btn = ttk.Button(
                control_card,
                text=item["text"],
                command=item["command"],
                style=item.get("style", "TButton"),
                width=25,
            )
            btn.pack(fill=tk.X, pady=3)
            
            # 添加工具提示
            if "tip" in item:
                self._add_tooltip(btn, item["tip"])
        
        # === 底部状态栏 ===
        status_bar = ttk.Frame(main_container, padding=(10, 5))
        status_bar.pack(fill=tk.X, side=tk.BOTTOM)
        
        self.status_bar_label = ttk.Label(status_bar, text=_("ready"),
                                         font=("Microsoft YaHei UI", 9))
        self.status_bar_label.pack(side=tk.LEFT)
        
        ttk.Separator(main_container, orient=tk.HORIZONTAL).pack(fill=tk.X, side=tk.BOTTOM)
    
    def _add_tooltip(self, widget, text):
        """添加工具提示"""
        def enter(event):
            self.tooltip = tk.Toplevel(self.root)
            self.tooltip.wm_overrideredirect(True)
            self.tooltip.wm_geometry(f"+{event.x_root+10}+{event.y_root+10}")
            ttk.Label(self.tooltip, text=text, 
                     background="#FFFFCC", foreground="#000000",
                     relief=tk.SOLID, borderwidth=1,
                     font=("Microsoft YaHei UI", 9),
                     padding=5).pack()
        
        def leave(event):
            if hasattr(self, "tooltip"):
                self.tooltip.destroy()
                delattr(self, "tooltip")
        
        widget.bind("<Enter>", enter)
        widget.bind("<Leave>", leave)
    
    def _log(self, text, level="info"):
        """添加日志（带时间戳和中文前缀）"""
        timestamp = get_timestamp()
        
        # 根据级别添加前缀
        if level == "error":
            prefix = f"❌ [{timestamp}]"
        elif level == "warning":
            prefix = f"⚠️ [{timestamp}]"
        elif level == "success":
            prefix = f"✅ [{timestamp}]"
        elif level == "info":
            prefix = f"ℹ️ [{timestamp}]"
        else:
            prefix = f"📝 [{timestamp}]"
        
        self.log_text.configure(state=tk.NORMAL)
        self.log_text.insert(tk.END, f"{prefix} {text}\n")
        self.log_text.see(tk.END)
        self.log_text.configure(state=tk.DISABLED)
    
    def _clear_log(self):
        """清空日志"""
        self.log_text.configure(state=tk.NORMAL)
        self.log_text.delete(1.0, tk.END)
        self.log_text.configure(state=tk.DISABLED)
        self._log("日志已清空", "info")
    
    def _clear_status(self):
        self.status_text.configure(state=tk.NORMAL)
        self.status_text.delete(1.0, tk.END)
        self.status_text.configure(state=tk.DISABLED)
    
    def _append_status(self, text):
        self.status_text.configure(state=tk.NORMAL)
        self.status_text.insert(tk.END, text + "\n")
        self.status_text.see(tk.END)
        self.status_text.configure(state=tk.DISABLED)
    
    def _set_steam_status(self, running):
        """设置 Steam 状态指示器"""
        if running:
            self.steam_status_icon.configure(foreground=self.colors["error"])
            self.steam_status_text.configure(text=_("steam_running_warn"),
                                            foreground=self.colors["error"])
        else:
            self.steam_status_icon.configure(foreground=self.colors["success"])
            self.steam_status_text.configure(text=_("steam_not_running_ok"),
                                            foreground=self.colors["success"])
    
    def _check_steam_running(self):
        """检查 Steam 是否正在运行"""
        try:
            result = subprocess.run(
                ["powershell", "-NoProfile", "-Command",
                 "Get-Process steam,steamwebhelper -ErrorAction SilentlyContinue | Select-Object -First 1"],
                capture_output=True, text=True, encoding="utf-8"
            )
            return bool(result.stdout.strip())
        except Exception:
            return False
    
    def _run_async(self, command, callback=None):
        """在后台运行 PS 脚本，记录输出"""
        self._clear_status()
        self._log(f"{_('log_start')}: {command}", "info")
        self.status_bar_label.configure(text=_("running"))
        
        # 检查 Steam 状态
        steam_running = self._check_steam_running()
        self._set_steam_status(steam_running)
        
        def run():
            def on_line(line):
                self.root.after(0, self._append_status, line)
            
            ret = run_ps(command, callback=on_line)
            self.root.after(0, lambda: self._on_done(command, ret))
        
        threading.Thread(target=run, daemon=True).start()
    
    def _on_done(self, command, retcode):
        """命令完成回调"""
        if retcode == 0:
            self._log(f"{_('finished').format(retcode)}: {command}", "success")
            self.status_bar_label.configure(text=_("ready"))
        else:
            self._log(f"{_('finished_error').format(retcode)}: {command}", "error")
            self.status_bar_label.configure(text=_("error"))
        
        # 刷新 Steam 状态
        steam_running = self._check_steam_running()
        self._set_steam_status(steam_running)
    
    def _open_config(self):
        """打开配置文件"""
        if os.path.exists(CONFIG_PATH):
            os.startfile(CONFIG_PATH)
        else:
            messagebox.showerror("Error", f"Config file not found: {CONFIG_PATH}")
    
    def _open_readme(self):
        """打开 README"""
        readme_path = os.path.join(SCRIPT_DIR, "README.md")
        if os.path.exists(readme_path):
            os.startfile(readme_path)
        else:
            messagebox.showerror("Error", f"README not found: {readme_path}")
    
    # === 按钮命令 ===
    def cmd_status(self):
        self._run_async("status")
    
    def cmd_verify(self):
        self._run_async("verify")
    
    def cmd_steamdb_check(self):
        self._run_async("steamdb-check")
    
    def cmd_skeletonize(self):
        if not messagebox.askyesno(_("confirm_title"), _("confirm_skeletonize")):
            return
        self._run_async("skeletonize")
    
    def cmd_restore(self):
        if not messagebox.askyesno(_("confirm_title"), _("confirm_restore")):
            return
        self._run_async("restore")
    
    def cmd_dryrun(self):
        self._run_async("skeletonize -DryRun")
    
    def cmd_lock(self):
        self._run_async("lock")
    
    def cmd_unlock(self):
        self._run_async("unlock")
    
    def cmd_launcher(self):
        """检测并显示非 Steam 启动器信息"""
        try:
            self._log("正在检测非 Steam 启动器...", "info")
            
            # 从 infi-manager.ps1 加载 Find-StandaloneLauncher 函数
            cmd = [
                "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
                "-Command",
                "& {" +
                "$scriptPath = '" + PS_SCRIPT + "'; " +
                "$scriptContent = Get-Content $scriptPath -Raw; " +
                "$pattern = '(function Find-StandaloneLauncher\\s*\\{[\\s\\S]*?^\\})'; " +
                "$match = [regex]::Match($scriptContent, $pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline); " +
                "if ($match.Success) { " +
                "  Invoke-Expression $match.Value; " +
                "  $result = Find-StandaloneLauncher; " +
                "  if ($result) { " +
                "    foreach ($r in $result) { " +
                "      Write-Output \"LAUNCHER_PATH:$($r.Path)\"; " +
                "      if ($r.GamePath) { Write-Output \"LAUNCHER_GAME:$($r.GamePath)\" } " +
                "      Write-Output \"LAUNCHER_SOURCE:$($r.Source)\" " +
                "    } " +
                "  } else { " +
                "    Write-Output \"NO_LAUNCHER\" " +
                "  } " +
                "} else { " +
                "  Write-Output \"NO_LAUNCHER\" " +
                "}" +
                "}"
            ]
            
            proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
            output = proc.stdout.strip()
            
            if "NO_LAUNCHER" in output or not output:
                self._log("未检测到非 Steam 启动器", "warning")
                messagebox.showinfo(_("launcher_title"), _("no_launcher"))
                return
            
            # 解析启动器信息
            launchers = []
            current = {}
            for line in output.split("\n"):
                line = line.strip()
                if line.startswith("LAUNCHER_PATH:"):
                    if current:
                        launchers.append(current)
                    current = {"path": line.replace("LAUNCHER_PATH:", "").strip()}
                elif line.startswith("LAUNCHER_GAME:"):
                    current["gamepath"] = line.replace("LAUNCHER_GAME:", "").strip()
                elif line.startswith("LAUNCHER_SOURCE:"):
                    current["source"] = line.replace("LAUNCHER_SOURCE:", "").strip()
            if current:
                launchers.append(current)
            
            if not launchers:
                self._log("未检测到非 Steam 启动器", "warning")
                messagebox.showinfo(_("launcher_title"), _("no_launcher"))
                return
            
            # 构建信息文本
            launcher_text = ""
            for i, launcher in enumerate(launchers, 1):
                launcher_text += f"  {i}. {launcher['path']}\n"
                if "gamepath" in launcher:
                    launcher_text += f"     游戏路径: {launcher['gamepath']}\n"
                if "source" in launcher:
                    launcher_text += f"     检测来源: {launcher['source']}\n"
                launcher_text += "\n"
            
            primary_path = launchers[0]["path"]
            info_text = _("launcher_info").format(
                launchers=launcher_text,
                path=primary_path
            )
            
            self._log(f"检测到 {len(launchers)} 个启动器", "success")
            messagebox.showinfo(_("launcher_title"), info_text)
            
        except Exception as e:
            self._log(f"检测启动器失败: {e}", "error")
            messagebox.showerror("❌ 错误", f"检测启动器失败: {e}")


def main():
    if TTKBOOTSTRAP_AVAILABLE:
        # 使用 ttkbootstrap 创建窗口
        root = ttk.Window(
            title=I18N["zh"]["title"],
            themename="morph",
            size=(900, 700),
            resizable=(True, True),
        )
        # 设置最小尺寸
        root.minsize(800, 600)
    else:
        # 回退到标准 tkinter
        root = tk.Tk()
        root.title(I18N["zh"]["title"])
        root.geometry("900x700")
        root.minsize(800, 600)
    
    # 设置窗口图标（如果存在）
    try:
        icon_path = os.path.join(SCRIPT_DIR, "ico.ico")
        if os.path.exists(icon_path):
            root.iconbitmap(icon_path)
    except Exception:
        pass
    
    app = InfiGUI(root)
    root.mainloop()


if __name__ == "__main__":
    main()
