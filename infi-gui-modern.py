# -*- coding: utf-8 -*-
"""
infi-gui-modern.py - 无限暖暖 Steam 壳管理 GUI 启动器
Version: 4.0 - Modern Fluent Design UI using customtkinter
"""

import subprocess
import threading
import tkinter as tk
from tkinter import messagebox
import os
import json
import sys
import shutil
from datetime import datetime

# 导入 customtkinter 用于现代 UI
try:
    import customtkinter as ctk
    CUSTOMTKINTER_AVAILABLE = True
except ImportError:
    CUSTOMTKINTER_AVAILABLE = False
    print("[ERROR] customtkinter 未安装，请运行: pip install customtkinter")

# 设置 customtkinter 外观
if CUSTOMTKINTER_AVAILABLE:
    ctk.set_appearance_mode("light")  # "light", "dark", "system"
    ctk.set_default_color_theme("blue")  # "blue", "green", "dark-blue"

# 支持 PyInstaller 打包后的路径
if getattr(sys, 'frozen', False):
    base_dir = sys._MEIPASS
    internal_dir = os.path.join(base_dir, "_internal")
    if os.path.exists(internal_dir):
        SCRIPT_DIR = internal_dir
    else:
        SCRIPT_DIR = base_dir
else:
    SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

PS_SCRIPT = os.path.join(SCRIPT_DIR, "infi-manager.ps1")
CONFIG_PATH = os.path.join(SCRIPT_DIR, "config.json")

# ========== 国际化字符串表（完整中文） ==========
I18N = {
    "zh": {
        "title": "无限暖暖 Steam 壳管理器",
        "app_name": "无限暖暖",
        "version": "版本 4.0",
        "status_panel": "状态面板",
        "log_panel": "运行日志",
        "control_panel": "控制面板",
        "btn_refresh": "刷新状态",
        "btn_steamdb_check": "SteamDB 检测",
        "btn_skeletonize": "骨架化清理",
        "btn_restore": "还原 X6Game",
        "btn_dryrun": "骨架化模拟",
        "btn_lock": "锁定 ACF",
        "btn_unlock": "解锁 ACF",
        "btn_verify": "全面验证",
        "btn_launcher": "启动器设置",
        "btn_clear_log": "清空日志",
        "steam_running": "⚠ Steam 正在运行",
        "steam_not_running": "✓ Steam 未运行",
        "running": "运行中...",
        "ready": "就绪",
        "confirm_skeletonize": "即将执行骨架化清理，是否继续？",
        "confirm_restore": "即将执行还原操作，是否继续？",
        "log_start": "开始执行",
        "log_error": "发生错误",
    }
}

LANG = "zh"
_ = lambda key: I18N[LANG].get(key, key)


def load_config():
    with open(CONFIG_PATH, "r", encoding="utf-8") as f:
        return json.load(f)


config = load_config()
APP_NAME = config["app"]["name"]
APP_ID = config["app"]["appid"]


def run_ps(command, callback=None):
    """运行 PowerShell 脚本"""
    # 强制使用 UTF-8 编码输出
    ps_command = f"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; & '{PS_SCRIPT}' {command}"
    
    cmd = [
        "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
        "-Command", ps_command
    ]
    try:
        proc = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            stdin=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            creationflags=subprocess.CREATE_NO_WINDOW
        )
        # 写入退出命令
        if proc.stdin:
            proc.stdin.close()
        
        # 读取标准输出
        for line in proc.stdout:
            line = line.rstrip()
            if line:  # 只处理非空行
                if callback:
                    callback(line)
        
        # 检查是否有错误输出
        stderr_output = proc.stderr.read()
        if stderr_output:
            print(f"[PowerShell 错误] {stderr_output}")
        
        proc.wait()
        return proc.returncode
    except Exception as e:
        if callback:
            callback(f"❌ [错误] {e}")
        return 1


def get_timestamp():
    return datetime.now().strftime("%H:%M:%S")


class ModernInfiGUI:
    """现代 Fluent Design 风格的 GUI 类"""
    
    def __init__(self, root):
        self.root = root
        self.root.title(_("title"))
        self.root.geometry("1000x750")
        self.root.minsize(900, 650)
        
        # 设置窗口图标
        try:
            icon_path = os.path.join(SCRIPT_DIR, "ico.ico")
            if os.path.exists(icon_path):
                self.root.iconbitmap(icon_path)
        except Exception:
            pass
        
        # 创建主容器
        self._create_widgets()
        
        # 初始加载状态
        self.root.after(300, self.cmd_status)
    
    def _create_widgets(self):
        """创建所有界面组件"""
        # 主容器
        main_container = ctk.CTkFrame(self.root, fg_color="transparent")
        main_container.pack(fill=tk.BOTH, expand=True, padx=20, pady=20)
        
        # === 顶部标题栏 ===
        header_frame = ctk.CTkFrame(main_container, height=80, fg_color="transparent")
        header_frame.pack(fill=tk.X, pady=(0, 20))
        header_frame.pack_propagate(False)
        
        # 左侧标题
        title_left = ctk.CTkFrame(header_frame, fg_color="transparent")
        title_left.pack(side=tk.LEFT, fill=tk.Y)
        
        ctk.CTkLabel(
            title_left,
            text=_("app_name"),
            font=ctk.CTkFont(family="Microsoft YaHei UI", size=24, weight="bold"),
            text_color=("#1a1a1a", "#ffffff")
        ).pack(anchor=tk.W, pady=(10, 5))
        
        ctk.CTkLabel(
            title_left,
            text=f"AppID: {APP_ID} | {_('version')}",
            font=ctk.CTkFont(family="Microsoft YaHei UI", size=12),
            text_color=("#666666", "#aaaaaa")
        ).pack(anchor=tk.W)
        
        # 右侧 Steam 状态
        self.steam_status_frame = ctk.CTkFrame(header_frame, fg_color="transparent")
        self.steam_status_frame.pack(side=tk.RIGHT, fill=tk.Y, pady=10)
        
        self.steam_status_label = ctk.CTkLabel(
            self.steam_status_frame,
            text="检测中...",
            font=ctk.CTkFont(family="Microsoft YaHei UI", size=12),
            text_color=("#666666", "#aaaaaa")
        )
        self.steam_status_label.pack(pady=10)
        
        # 分隔线
        ctk.CTkFrame(main_container, height=2, fg_color=("#e0e0e0", "#333333")).pack(fill=tk.X, pady=(0, 20))
        
        # === 中间内容区 ===
        content_frame = ctk.CTkFrame(main_container, fg_color="transparent")
        content_frame.pack(fill=tk.BOTH, expand=True)
        content_frame.grid_columnconfigure(0, weight=3)
        content_frame.grid_columnconfigure(1, weight=1)
        content_frame.grid_rowconfigure(0, weight=1)
        
        # 左侧：状态 + 日志
        left_frame = ctk.CTkFrame(content_frame, fg_color="transparent")
        left_frame.grid(row=0, column=0, sticky="nsew", padx=(0, 20))
        left_frame.grid_rowconfigure(0, weight=2)
        left_frame.grid_rowconfigure(1, weight=1)
        
        # 状态面板
        status_frame = ctk.CTkFrame(left_frame, corner_radius=10)
        status_frame.grid(row=0, column=0, sticky="nsew", pady=(0, 10))
        
        ctk.CTkLabel(
            status_frame,
            text=_("status_panel"),
            font=ctk.CTkFont(family="Microsoft YaHei UI", size=14, weight="bold"),
            anchor="w"
        ).pack(fill=tk.X, padx=15, pady=(15, 10))
        
        self.status_text = ctk.CTkTextbox(
            status_frame,
            font=ctk.CTkFont(family="Consolas", size=11),
            corner_radius=5,
            wrap="word"
        )
        self.status_text.pack(fill=tk.BOTH, expand=True, padx=15, pady=(0, 15))
        
        # 日志面板
        log_frame = ctk.CTkFrame(left_frame, corner_radius=10)
        log_frame.grid(row=1, column=0, sticky="nsew", pady=(10, 0))
        
        log_header = ctk.CTkFrame(log_frame, fg_color="transparent")
        log_header.pack(fill=tk.X, padx=15, pady=(15, 10))
        
        ctk.CTkLabel(
            log_header,
            text=_("log_panel"),
            font=ctk.CTkFont(family="Microsoft YaHei UI", size=14, weight="bold"),
            anchor="w"
        ).pack(side=tk.LEFT)
        
        ctk.CTkButton(
            log_header,
            text=_("btn_clear_log"),
            font=ctk.CTkFont(family="Microsoft YaHei UI", size=11),
            width=80,
            height=30,
            corner_radius=5,
            command=self._clear_log
        ).pack(side=tk.RIGHT)
        
        self.log_text = ctk.CTkTextbox(
            log_frame,
            font=ctk.CTkFont(family="Consolas", size=10),
            corner_radius=5,
            wrap="word",
            text_color=("#666666", "#999999")
        )
        self.log_text.pack(fill=tk.BOTH, expand=True, padx=15, pady=(0, 15))
        
        # 右侧：控制面板
        right_frame = ctk.CTkFrame(content_frame, fg_color="transparent")
        right_frame.grid(row=0, column=1, sticky="nsew")
        
        control_frame = ctk.CTkFrame(right_frame, corner_radius=10)
        control_frame.pack(fill=tk.BOTH, expand=True)
        
        ctk.CTkLabel(
            control_frame,
            text=_("control_panel"),
            font=ctk.CTkFont(family="Microsoft YaHei UI", size=14, weight="bold"),
            anchor="w"
        ).pack(fill=tk.X, padx=15, pady=(15, 20))
        
        # 按钮列表
        buttons = [
            {
                "text": _("btn_refresh"),
                "command": self.cmd_status,
                "fg_color": ("#0078D4", "#0078D4"),
                "hover_color": ("#106EBE", "#106EBE"),
            },
            {
                "text": _("btn_steamdb_check"),
                "command": self.cmd_steamdb_check,
                "fg_color": ("#0078D4", "#0078D4"),
                "hover_color": ("#106EBE", "#106EBE"),
            },
            {"separator": True},
            {
                "text": _("btn_skeletonize"),
                "command": self.cmd_skeletonize,
                "fg_color": ("#D13438", "#D13438"),
                "hover_color": ("#B71C1C", "#B71C1C"),
            },
            {
                "text": _("btn_restore"),
                "command": self.cmd_restore,
                "fg_color": ("#107C10", "#107C10"),
                "hover_color": ("#0E6E0E", "#0E6E0E"),
            },
            {
                "text": _("btn_dryrun"),
                "command": self.cmd_dryrun,
            },
            {"separator": True},
            {
                "text": _("btn_lock"),
                "command": self.cmd_lock,
            },
            {
                "text": _("btn_unlock"),
                "command": self.cmd_unlock,
            },
            {"separator": True},
            {
                "text": _("btn_verify"),
                "command": self.cmd_verify,
                "fg_color": ("#0078D4", "#0078D4"),
                "hover_color": ("#106EBE", "#106EBE"),
            },
            {
                "text": _("btn_launcher"),
                "command": self.cmd_launcher,
            },
        ]
        
        for item in buttons:
            if item.get("separator"):
                ctk.CTkFrame(control_frame, height=1, fg_color=("gray70", "gray30")).pack(fill=tk.X, padx=15, pady=8)
                continue
            
            btn = ctk.CTkButton(
                control_frame,
                text=item["text"],
                command=item["command"],
                font=ctk.CTkFont(family="Microsoft YaHei UI", size=12),
                height=40,
                corner_radius=8,
                fg_color=item.get("fg_color", ("#666666", "#666666")),
                hover_color=item.get("hover_color", ("#444444", "#444444")),
                text_color=("white", "white"),
            )
            btn.pack(fill=tk.X, padx=15, pady=4)
        
        # === 底部状态栏 ===
        self.status_bar = ctk.CTkLabel(
            main_container,
            text=_("ready"),
            font=ctk.CTkFont(family="Microsoft YaHei UI", size=11),
            anchor="w",
            height=30,
            corner_radius=5,
            fg_color=("gray90", "gray20")
        )
        self.status_bar.pack(fill=tk.X, pady=(20, 0), ipadx=10)
    
    def _log(self, text, level="info"):
        """添加日志"""
        timestamp = get_timestamp()
        
        if level == "error":
            prefix = f"❌ [{timestamp}]"
        elif level == "warning":
            prefix = f"⚠ [{timestamp}]"
        elif level == "success":
            prefix = f"✅ [{timestamp}]"
        else:
            prefix = f"ℹ [{timestamp}]"
        
        self.log_text.insert("end", f"{prefix} {text}\n")
        self.log_text.see("end")
    
    def _clear_log(self):
        """清空日志"""
        self.log_text.delete("1.0", "end")
        self._log("日志已清空", "info")
    
    def _clear_status(self):
        """清空状态显示"""
        self.status_text.delete("1.0", "end")
    
    def _append_status(self, text):
        """添加状态文本"""
        self.status_text.insert("end", text + "\n")
        self.status_text.see("end")
    
    def _set_steam_status(self, running):
        """设置 Steam 状态"""
        if running:
            self.steam_status_label.configure(
                text=_("steam_running"),
                text_color=("#D13438", "#D13438")
            )
        else:
            self.steam_status_label.configure(
                text=_("steam_not_running"),
                text_color=("#107C10", "#107C10")
            )
    
    def _check_steam_running(self):
        """检查 Steam 是否运行"""
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
        """异步运行命令"""
        self._clear_status()
        self._log(f"{_('log_start')}: {command}", "info")
        self.status_bar.configure(text=_("running"))
        
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
            self._log(f"完成 (退出码={retcode}): {command}", "success")
            self.status_bar.configure(text=_("ready"))
        else:
            self._log(f"失败 (退出码={retcode}): {command}", "error")
            self.status_bar.configure(text=_("running"))
        
        steam_running = self._check_steam_running()
        self._set_steam_status(steam_running)
    
    # === 命令方法 ===
    def cmd_status(self):
        self._run_async("status")
    
    def cmd_verify(self):
        self._run_async("verify")
    
    def cmd_steamdb_check(self):
        self._run_async("steamdb-check")
    
    def cmd_skeletonize(self):
        if not messagebox.askyesno("确认", _("confirm_skeletonize")):
            return
        self._run_async("skeletonize")
    
    def cmd_restore(self):
        if not messagebox.askyesno("确认", _("confirm_restore")):
            return
        self._run_async("restore")
    
    def cmd_dryrun(self):
        self._run_async("skeletonize -DryRun")
    
    def cmd_lock(self):
        self._run_async("lock")
    
    def cmd_unlock(self):
        self._run_async("unlock")
    
    def cmd_launcher(self):
        """检测启动器"""
        try:
            self._log("正在检测非 Steam 启动器...", "info")
            messagebox.showinfo("启动器", "启动器检测功能开发中...")
        except Exception as e:
            self._log(f"检测启动器失败: {e}", "error")


def main():
    if not CUSTOMTKINTER_AVAILABLE:
        print("错误: 请先安装 customtkinter")
        print("运行: pip install customtkinter")
        return
    
    root = ctk.CTk()
    app = ModernInfiGUI(root)
    root.mainloop()


if __name__ == "__main__":
    main()
