# -*- coding: utf-8 -*-
"""
infi-gui-fluent.py - 无限暖暖 Steam 壳管理 GUI 启动器
Version: 5.0 - 使用 QFluentWidgets 实现真正 Fluent Design
"""

import sys
import os
import json
import subprocess
from datetime import datetime
from PySide6.QtWidgets import (QApplication, QWidget, QVBoxLayout, 
                                QHBoxLayout, QTextEdit, QMessageBox)
from PySide6.QtCore import Qt, QThread, Signal

from qfluentwidgets import (
    FluentWindow, setTheme, Theme,
    NavigationItemPosition, SubtitleLabel, PrimaryPushButton,
    PushButton, TransparentPushButton,
    FluentIcon as FIF,
    CardWidget, TextEdit, InfoBar, InfoBarPosition,
    StateToolTip, BodyLabel, CaptionLabel, StrongBodyLabel,
)


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


def load_config():
    with open(CONFIG_PATH, "r", encoding="utf-8") as f:
        return json.load(f)


config = load_config()
APP_NAME = config["app"]["name"]
APP_ID = config["app"]["appid"]


class WorkerThread(QThread):
    """后台工作线程"""
    output_signal = Signal(str)
    finished_signal = Signal(int)
    
    def __init__(self, command):
        super().__init__()
        self.command = command
    
    def run(self):
        try:
            # 强制 UTF-8 编码
            ps_command = f"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; & '{PS_SCRIPT}' {self.command}"
            
            proc = subprocess.Popen(
                ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
                 "-Command", ps_command],
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                encoding="utf-8",
                errors="replace",
                creationflags=subprocess.CREATE_NO_WINDOW
            )
            
            for line in proc.stdout:
                line = line.rstrip()
                if line:
                    self.output_signal.emit(line)
            
            proc.wait()
            self.finished_signal.emit(proc.returncode)
            
        except Exception as e:
            self.output_signal.emit(f"错误: {e}")
            self.finished_signal.emit(1)


class HomePage(QWidget):
    """主页"""
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setObjectName("homePage")
        self.setup_ui()
    
    def setup_ui(self):
        layout = QVBoxLayout(self)
        layout.setContentsMargins(30, 30, 30, 30)
        layout.setSpacing(20)
        
        # 标题
        title = SubtitleLabel("欢迎使用 无限暖暖 Steam 壳管理器", self)
        layout.addWidget(title)
        
        # 描述
        desc = BodyLabel(f"AppID: {APP_ID} | Depot: 3164332 | 版本 5.0 (Fluent Design)", self)
        desc.setStyleSheet("color: gray;")
        layout.addWidget(desc)
        
        # 状态卡片
        status_card = CardWidget(self)
        status_layout = QVBoxLayout(status_card)
        status_layout.setContentsMargins(20, 20, 20, 20)
        
        status_title = StrongBodyLabel("状态面板", status_card)
        status_layout.addWidget(status_title)
        
        self.status_text = QTextEdit(status_card)
        self.status_text.setReadOnly(True)
        self.status_text.setMinimumHeight(200)
        status_layout.addWidget(self.status_text)
        
        layout.addWidget(status_card)
        
        # 日志卡片
        log_card = CardWidget(self)
        log_layout = QVBoxLayout(log_card)
        log_layout.setContentsMargins(20, 20, 20, 20)
        
        log_header = QHBoxLayout()
        log_title = StrongBodyLabel("运行日志", log_card)
        log_header.addWidget(log_title)
        log_header.addStretch()
        
        clear_btn = TransparentPushButton("清空日志", log_card)
        clear_btn.clicked.connect(self.clear_log)
        log_header.addWidget(clear_btn)
        
        log_layout.addLayout(log_header)
        
        self.log_text = QTextEdit(log_card)
        self.log_text.setReadOnly(True)
        self.log_text.setMaximumHeight(150)
        self.log_text.setStyleSheet("background-color: #1e1e1e; color: #d4d4d4;")
        log_layout.addWidget(self.log_text)
        
        layout.addWidget(log_card)
        layout.addStretch()
    
    def append_status(self, text):
        self.status_text.append(text)
    
    def append_log(self, text, level="info"):
        timestamp = datetime.now().strftime("%H:%M:%S")
        
        if level == "error":
            prefix = f"[错误] [{timestamp}]"
        elif level == "warning":
            prefix = f"[警告] [{timestamp}]"
        elif level == "success":
            prefix = f"[成功] [{timestamp}]"
        else:
            prefix = f"[信息] [{timestamp}]"
        
        self.log_text.append(f"{prefix} {text}")
    
    def clear_log(self):
        self.log_text.clear()
        self.append_log("日志已清空", "info")
    
    def clear_status(self):
        self.status_text.clear()


class ControlPage(QWidget):
    """控制面板页"""
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setObjectName("controlPage")
        self.setup_ui()
    
    def setup_ui(self):
        layout = QVBoxLayout(self)
        layout.setContentsMargins(30, 30, 30, 30)
        layout.setSpacing(20)
        
        # 标题
        title = SubtitleLabel("控制面板", self)
        layout.addWidget(title)
        
        # 按钮卡片
        btn_card = CardWidget(self)
        btn_layout = QVBoxLayout(btn_card)
        btn_layout.setContentsMargins(20, 20, 20, 20)
        btn_layout.setSpacing(10)
        
        # 主要操作按钮
        self.add_button(btn_layout, "刷新状态", self.on_refresh, primary=True)
        self.add_button(btn_layout, "SteamDB 检测", self.on_steamdb, primary=True)
        
        btn_layout.addSpacing(10)
        
        # 骨架化操作
        self.add_button(btn_layout, "骨架化清理", self.on_skeletonize, color="#D13438")
        self.add_button(btn_layout, "还原 X6Game", self.on_restore, color="#107C10")
        self.add_button(btn_layout, "骨架化模拟", self.on_dryrun)
        
        btn_layout.addSpacing(10)
        
        # ACF 操作
        self.add_button(btn_layout, "锁定 ACF", self.on_lock)
        self.add_button(btn_layout, "解锁 ACF", self.on_unlock)
        
        btn_layout.addSpacing(10)
        
        # 其他操作
        self.add_button(btn_layout, "全面验证", self.on_verify, primary=True)
        self.add_button(btn_layout, "启动器设置", self.on_launcher)
        
        layout.addWidget(btn_card)
        layout.addStretch()
    
    def add_button(self, layout, text, slot, primary=False, color=None):
        if primary:
            btn = PrimaryPushButton(text, self)
        elif color:
            btn = PushButton(text, self)
            btn.setStyleSheet(f"background-color: {color}; color: white;")
        else:
            btn = PushButton(text, self)
        
        btn.clicked.connect(slot)
        layout.addWidget(btn)
    
    def on_refresh(self):
        main_window = self.window()
        if hasattr(main_window, 'run_command'):
            main_window.run_command("status")
    
    def on_steamdb(self):
        main_window = self.window()
        if hasattr(main_window, 'run_command'):
            main_window.run_command("steamdb-check")
    
    def on_skeletonize(self):
        main_window = self.window()
        if hasattr(main_window, 'show_confirm'):
            if main_window.show_confirm("骨架化清理", "即将执行骨架化清理，是否继续？"):
                main_window.run_command("skeletonize")
    
    def on_restore(self):
        main_window = self.window()
        if hasattr(main_window, 'show_confirm'):
            if main_window.show_confirm("还原操作", "即将执行还原操作，是否继续？"):
                main_window.run_command("restore")
    
    def on_dryrun(self):
        main_window = self.window()
        if hasattr(main_window, 'run_command'):
            main_window.run_command("skeletonize -DryRun")
    
    def on_lock(self):
        main_window = self.window()
        if hasattr(main_window, 'run_command'):
            main_window.run_command("lock")
    
    def on_unlock(self):
        main_window = self.window()
        if hasattr(main_window, 'run_command'):
            main_window.run_command("unlock")
    
    def on_verify(self):
        main_window = self.window()
        if hasattr(main_window, 'run_command'):
            main_window.run_command("verify")
    
    def on_launcher(self):
        QMessageBox.information(self, "启动器", "启动器检测功能开发中...")


class SettingsPage(QWidget):
    """设置页"""
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setObjectName("settingsPage")
        self.setup_ui()
    
    def setup_ui(self):
        layout = QVBoxLayout(self)
        layout.setContentsMargins(30, 30, 30, 30)
        layout.setSpacing(20)
        
        # 标题
        title = SubtitleLabel("设置", self)
        layout.addWidget(title)
        
        # 主题设置卡片
        theme_card = CardWidget(self)
        theme_layout = QVBoxLayout(theme_card)
        theme_layout.setContentsMargins(20, 20, 20, 20)
        
        theme_label = StrongBodyLabel("主题设置", theme_card)
        theme_layout.addWidget(theme_label)
        
        theme_desc = CaptionLabel("切换浅色/深色主题", theme_card)
        theme_layout.addWidget(theme_desc)
        
        self.theme_btn = PushButton("切换主题", theme_card)
        self.theme_btn.clicked.connect(self.toggle_theme)
        theme_layout.addWidget(self.theme_btn)
        
        layout.addWidget(theme_card)
        layout.addStretch()
    
    def toggle_theme(self):
        from qfluentwidgets import qconfig
        if qconfig.theme == Theme.LIGHT:
            setTheme(Theme.DARK)
        else:
            setTheme(Theme.LIGHT)


class MainWindow(FluentWindow):
    """主窗口"""
    def __init__(self):
        super().__init__()
        self.setWindowTitle("无限暖暖 Steam 壳管理器")
        self.resize(1200, 800)
        
        # 创建页面
        self.homePage = HomePage(self)
        self.controlPage = ControlPage(self)
        self.settingsPage = SettingsPage(self)
        
        # 添加导航项
        self.addSubInterface(self.homePage, FIF.HOME, "主页",
                           position=NavigationItemPosition.TOP)
        self.addSubInterface(self.controlPage, FIF.GAME, "控制面板",
                           position=NavigationItemPosition.TOP)
        self.addSubInterface(self.settingsPage, FIF.SETTING, "设置",
                           position=NavigationItemPosition.BOTTOM)
        
        # 状态提示
        self.state_tool_tip = None
        
        # 检测 Steam 状态
        self.check_steam_status()
    
    def run_command(self, command):
        """运行命令"""
        self.homePage.clear_status()
        self.homePage.append_log(f"开始执行: {command}", "info")
        
        # 显示状态提示
        self.state_tool_tip = StateToolTip("运行中...", f"正在执行: {command}", self)
        self.state_tool_tip.show()
        
        # 启动工作线程
        self.worker = WorkerThread(command)
        self.worker.output_signal.connect(self.homePage.append_status)
        self.worker.finished_signal.connect(self.on_command_finished)
        self.worker.start()
    
    def on_command_finished(self, returncode):
        """命令完成回调"""
        if self.state_tool_tip:
            self.state_tool_tip.hide()
        
        if returncode == 0:
            self.homePage.append_log(f"完成 (退出码={returncode})", "success")
            InfoBar.success("成功", "操作已完成", parent=self)
        else:
            self.homePage.append_log(f"失败 (退出码={returncode})", "error")
            InfoBar.error("错误", f"操作失败 (退出码={returncode})", parent=self)
        
        self.check_steam_status()
    
    def show_confirm(self, title, content):
        """显示确认对话框"""
        msg_box = QMessageBox(self)
        msg_box.setWindowTitle(title)
        msg_box.setText(content)
        msg_box.setStandardButtons(QMessageBox.Yes | QMessageBox.No)
        msg_box.setDefaultButton(QMessageBox.No)
        return msg_box.exec() == QMessageBox.Yes
    
    def check_steam_status(self):
        """检查 Steam 状态"""
        try:
            result = subprocess.run(
                ["powershell", "-NoProfile", "-Command",
                 "Get-Process steam -ErrorAction SilentlyContinue | Select-Object -First 1"],
                capture_output=True, text=True, encoding="utf-8"
            )
            is_running = bool(result.stdout.strip())
            
            if is_running:
                InfoBar.warning("警告", "Steam 正在运行 - 修改前请先退出", parent=self)
            else:
                InfoBar.info("提示", "Steam 未运行 - 可以安全修改", parent=self)
        except Exception:
            pass


def main():
    app = QApplication(sys.argv)
    
    # 设置主题（浅色）
    setTheme(Theme.LIGHT)
    
    window = MainWindow()
    window.show()
    
    sys.exit(app.exec())


if __name__ == "__main__":
    main()
