@echo off
chcp 65001 >nul 2>&1
title InfiSteam Build All

cls
echo ===================================
echo InfiSteam Build All Script
echo ===================================
echo.
echo Select version to build:
echo.
echo 1. Python GUI
echo 2. C# WPF
echo 3. C# WinUI 3
echo 4. Build All
echo.
set /p choice="Enter option (1/2/3/4): "
echo.

if "%choice%"=="1" goto build_python
if "%choice%"=="2" goto build_wpf
if "%choice%"=="3" goto build_winui
if "%choice%"=="4" goto build_all
goto done

:build_all
call :build_python
if errorlevel 1 goto error
call :build_wpf
if errorlevel 1 goto error
call :build_winui
if errorlevel 1 goto error
goto done

:build_python
echo.
echo === Building Python GUI ===
set "SOURCE_DIR=Q:\数据\Web\infi\source"
set "RELEASE_DIR=Q:\数据\Web\infi\release\Python_GUI"
set "PS_SCRIPT=Q:\数据\Web\infi\release\AI_Prompt_with_Powershell"
set "ICO_PATH=Q:\数据\Web\infi\ico.ico"
set "PRO_SCRIPT=%SOURCE_DIR%\infi-gui-pro.py"

if exist "%RELEASE_DIR%\InfiSteam.exe" del /f /q "%RELEASE_DIR%\InfiSteam.exe" 2>nul

echo Checking dependencies...
pip show PyInstaller >nul 2>&1
if errorlevel 1 pip install pyinstaller
pip show customtkinter >nul 2>&1
if errorlevel 1 pip install customtkinter

echo Building...
pyinstaller --clean --noconfirm --onefile --windowed --name "InfiSteam" --icon "%ICO_PATH%" --add-data "%ICO_PATH%;." --add-data "%SOURCE_DIR%\config.json;." --add-data "%PS_SCRIPT%\infi-manager.ps1;." --add-data "%PS_SCRIPT%\infi_steamdb_fetch.py;." --distpath "%RELEASE_DIR%" --workpath "%TEMP%\pyibuild" --specpath "%TEMP%\pyispec" --hidden-import "PIL._tkinter_finder" "%PRO_SCRIPT%"

if errorlevel 1 exit /b 1
echo Python GUI OK
if exist "%TEMP%\pyibuild" rmdir /s /q "%TEMP%\pyibuild" 2>nul
if exist "%TEMP%\pyispec" rmdir /s /q "%TEMP%\pyispec" 2>nul
exit /b 0

:build_wpf
echo.
echo === Building C# WPF ===
set "PROJECT_DIR=Q:\数据\Web\infi\C#\InfiSteam"
set "OUTPUT_DIR=C:\temp\InfiSteamBuild"
set "RELEASE_DIR=Q:\数据\Web\infi\release\C#_WPF"

if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
if exist "%RELEASE_DIR%" rmdir /s /q "%RELEASE_DIR%"
mkdir "%OUTPUT_DIR%" 2>nul

dotnet publish "%PROJECT_DIR%\InfiSteam.csproj" -c Release -r win-x64 --self-contained true -o "%OUTPUT_DIR%"
if errorlevel 1 exit /b 1

mkdir "%RELEASE_DIR%" 2>nul
taskkill /F /IM "InfiSteam.exe" >nul 2>&1
timeout /t 2 /nobreak >nul
xcopy "%OUTPUT_DIR%" "%RELEASE_DIR%\" /E /I /Y >nul
echo WPF OK
exit /b 0

:build_winui
echo.
echo === Building C# WinUI 3 ===
set "SRC_DIR=Q:\数据\Web\infi\C#\InfiSteam.WinUI"
set "TEMP_DIR=Q:\temp\InfiSteam.WinUI"
set "RELEASE_DIR=Q:\数据\Web\infi\release\C#_WinUI3"

echo Copying source...
if not exist "%TEMP_DIR%" mkdir "%TEMP_DIR%"
powershell -Command "Copy-Item -Path '%SRC_DIR%\*' -Destination '%TEMP_DIR%\' -Recurse -Force -Exclude bin,obj,.vs"

echo Cleaning...
if exist "%TEMP_DIR%\bin" rmdir /s /q "%TEMP_DIR%\bin"
if exist "%TEMP_DIR%\obj" rmdir /s /q "%TEMP_DIR%\obj"

echo Building...
cd /d "%TEMP_DIR%"
dotnet publish -c Release -r win-x64 --self-contained true
if errorlevel 1 exit /b 1

echo Copying to release...
set "PUBLISH_DIR=%TEMP_DIR%\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"
if not exist "%RELEASE_DIR%" mkdir "%RELEASE_DIR%" 2>nul
xcopy "%PUBLISH_DIR%\*" "%RELEASE_DIR%\" /E /I /Y >nul
echo WinUI 3 OK
exit /b 0

:error
echo.
echo Build failed!
:done
echo.
echo Press any key to exit...
pause >nul
