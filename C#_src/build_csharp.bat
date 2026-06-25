@echo off
chcp 65001 > nul
echo 开始编译 InfiSteam C# 程序...
echo.

set "PROJECT_DIR=Q:\数据\Web\infi\C#\InfiSteam"
set "OUTPUT_DIR=C:\temp\InfiSteamBuild"
set "RELEASE_DIR=Q:\数据\Web\infi\release\C#\build"

REM 清理旧编译输出
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
if exist "%RELEASE_DIR%" rmdir /s /q "%RELEASE_DIR%"

mkdir "%OUTPUT_DIR%" 2>nul

REM 执行编译（使用短路径避免中文问题）
dotnet publish "%PROJECT_DIR%\InfiSteam.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o "%OUTPUT_DIR%"

echo.
if %ERRORLEVEL% EQU 0 (
  echo ✅ 编译成功！正在复制到 release 目录...
  mkdir "%RELEASE_DIR%" 2>nul

  REM 关闭正在运行的 InfiSteam.exe 以释放文件占用
  taskkill /F /IM "InfiSteam.exe" >nul 2>&1
  timeout /t 2 /nobreak >nul

  xcopy "%OUTPUT_DIR%" "%RELEASE_DIR%\" /E /I /Y >nul
  echo ✅ 已复制到 release\C#\build\
  dir "%RELEASE_DIR%\InfiSteam.exe" 2>nul | find ".exe"
) else (
  echo ❌ 编译失败，错误代码：%ERRORLEVEL%
)
pause
