@echo off
title InfiSteam Python GUI Build
setlocal enabledelayedexpansion

echo ===================================
echo 编译 InfiSteam Python GUI Pro
echo ===================================
echo.

set "SOURCE_DIR=Q:\数据\Web\infi\source"
set "RELEASE_DIR=Q:\数据\Web\infi\release\Python_GUI"
set "PS_SCRIPT=Q:\数据\Web\infi\release\AI_Prompt_with_Powershell"
set "ICO_PATH=Q:\数据\Web\infi\ico.ico"
set "PRO_SCRIPT=%SOURCE_DIR%\infi-gui-pro.py"

:: 清理旧输出
if exist "%RELEASE_DIR%\InfiSteam.exe" (
    echo(删除旧的 EXE...
    del /f /q "%RELEASE_DIR%\InfiSteam.exe" 2>nul
)

:: 检查依赖
echo(1/3 检查依赖...
pip show PyInstaller >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo(安装 PyInstaller...
    pip install pyinstaller
)

pip show customtkinter >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo(安装 customtkinter...
    pip install customtkinter
)

:: 编译
echo.
echo(2/3 开始编译...
echo 输入脚本: %PRO_SCRIPT%
echo 图标: %ICO_PATH%

pyinstaller --clean --noconfirm ^
    --onefile ^
    --windowed ^
    --name "InfiSteam" ^
    --icon "%ICO_PATH%" ^
    --add-data "%ICO_PATH%;." ^
    --add-data "%SOURCE_DIR%\config.json;." ^
    --add-data "%PS_SCRIPT%\infi-manager.ps1;." ^
    --add-data "%PS_SCRIPT%\infi_steamdb_fetch.py;." ^
    --distpath "%RELEASE_DIR%" ^
    --workpath "%TEMP%\pyibuild" ^
    --specpath "%TEMP%\pyispec" ^
    --hidden-import "PIL._tkinter_finder" ^
    "%PRO_SCRIPT%"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ 编译失败！错误代码: %ERRORLEVEL%
    pause
    exit /b %ERRORLEVEL%
)

:: 清理临时文件
echo(3/3 清理临时文件...
if exist "%TEMP%\pyibuild" rmdir /s /q "%TEMP%\pyibuild" 2>nul
if exist "%TEMP%\pyispec" rmdir /s /q "%TEMP%\pyispec" 2>nul

echo.
echo ===================================
echo ✅ 构建完成！
echo.
echo 输出目录: %RELEASE_DIR%
dir "%RELEASE_DIR%\InfiSteam.exe" 2>nul | find ".exe"
echo ===================================
pause
