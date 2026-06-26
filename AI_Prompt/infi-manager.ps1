# =============================================================================
# infi-manager.ps1 - Infinity Nikki Steam Shell Manager
# Plan 2+3: ACF Anti-Update + Skeletonize Cleanup
# Target: Prevent Steam from detecting outdated local content
# Version: 2.0 - Universal (Auto-detect paths + SteamDB integration)
# =============================================================================

param(
    [Parameter(Position=0)]
    [ValidateSet("status","update","skeletonize","restore","lock","unlock","verify","steamdb-check","residual-check","report","help","query")]
    [string]$Command = "status",

    [string]$BuildID = "",
    [string]$ManifestGID = "",
    [string]$DepotID = "3164332",
    [switch]$Force,
    [switch]$DryRun,
    [switch]$NoInteractive
)

# ===== Auto-Detect Steam Paths =====
function Get-SteamPath {
    # 1. Registry detection (most accurate)
    $regPath = (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -ErrorAction SilentlyContinue).InstallPath
    if ($regPath -and (Test-Path "$regPath\steam.exe")) { return $regPath }
    
    $regPath = (Get-ItemProperty "HKCU:\SOFTWARE\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
    if ($regPath -and (Test-Path "$regPath\steam.exe")) { return $regPath }
    
    # 2. Common installation paths
    $commonPaths = @(
        "C:\Program Files (x86)\Steam",
        "C:\Program Files\Steam",
        "D:\Steam",
        "D:\Entertainment\Steam",
        "E:\Steam",
        "$env:LOCALAPPDATA\Steam"
    )
    foreach ($path in $commonPaths) {
        if (Test-Path "$path\steam.exe") { return $path }
    }
    
    return $null
}

function Get-GameLibraryPath {
    param([string]$SteamPath)
    
    # Check libraryfolders.vdf for additional library paths
    $libraryFoldersFile = Join-Path $SteamPath "steamapps\libraryfolders.vdf"
    $candidatePaths = @($SteamPath)
    
    if (Test-Path $libraryFoldersFile) {
        $vdfContent = Get-Content $libraryFoldersFile -Raw
        $pathMatches = [regex]::Matches($vdfContent, '"path"\s+"([^"]+)"')
        foreach ($match in $pathMatches) {
            $libPath = $match.Groups[1].Value.Replace('\\', '\')
            if ($libPath -notin $candidatePaths) {
                $candidatePaths += $libPath
            }
        }
    }
    
    # Search for appmanifest_3164330.acf in all libraries
    foreach ($lib in $candidatePaths) {
        $acfPath = Join-Path $lib "steamapps\appmanifest_3164330.acf"
        if (Test-Path $acfPath) {
            return $lib
        }
    }
    
    return $null
}

# ===== Detect Standalone Launcher =====
function Find-StandaloneLauncher {
    $foundLaunchers = @()
    
    # Method 1: Registry uninstall info
    $uninstallPaths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    )
    
    foreach ($regPath in $uninstallPaths) {
        if (Test-Path $regPath) {
            Get-ChildItem $regPath -ErrorAction SilentlyContinue | 
                Get-ItemProperty -ErrorAction SilentlyContinue | 
                Where-Object { $_.DisplayName -match "Infinity|Nikki|Infold" } | 
                ForEach-Object {
                    if ($_.InstallLocation -and (Test-Path $_.InstallLocation)) {
                        $launcherExe = Join-Path $_.InstallLocation "launcher.exe"
                        if (Test-Path $launcherExe) {
                            $foundLaunchers += [PSCustomObject]@{
                                Path = $launcherExe
                                GamePath = $null
                                Source = "Registry"
                            }
                        }
                    }
                }
        }
    }
    
    # Method 2: config.ini detection
    $commonDirs = @(
        "D:\Entertainment\InfinityNikkiLauncher",
        "$env:ProgramFiles\InfinityNikkiLauncher",
        "$env:ProgramFiles(x86)\InfinityNikkiLauncher",
        "$env:LOCALAPPDATA\InfinityNikkiLauncher",
        "C:\InfinityNikkiLauncher"
    )
    
    foreach ($dir in $commonDirs) {
        $configPath = Join-Path $dir "config.ini"
        if (Test-Path $configPath) {
            $content = Get-Content $configPath -Raw
            $gamePath = $null
            if ($content -match "game_path\s*=\s*(.+)") {
                $gamePath = $matches[1].Trim()
            }
            $launcherExe = Join-Path $dir "launcher.exe"
            if (Test-Path $launcherExe) {
                $foundLaunchers += [PSCustomObject]@{
                    Path = $launcherExe
                    GamePath = $gamePath
                    Source = "Config"
                }
            }
        }
    }
    
    # Method 3: Start menu shortcuts
    $shortcutDirs = @(
        "$env:APPDATA\Microsoft\Windows\Start Menu\Programs",
        "$env:ProgramData\Microsoft\Windows\Start Menu\Programs"
    )
    
    foreach ($shortcutDir in $shortcutDirs) {
        if (Test-Path $shortcutDir) {
            Get-ChildItem $shortcutDir -Recurse -Include "*.lnk" -ErrorAction SilentlyContinue | 
                Where-Object { $_.Name -match "Infinity|Nikki" } | 
                ForEach-Object {
                    $shell = New-Object -ComObject WScript.Shell
                    $shortcut = $shell.CreateShortcut($_.FullName)
                    if ($shortcut.TargetPath -match "launcher|xstarter" -and 
                        (Test-Path $shortcut.TargetPath)) {
                        $foundLaunchers += [PSCustomObject]@{
                            Path = $shortcut.TargetPath
                            GamePath = $null
                            Source = "Shortcut"
                        }
                    }
                }
        }
    }
    
    return $foundLaunchers | Sort-Object Path -Unique
}

# ===== Load Config =====
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ConfigPath = Join-Path $ScriptDir "config.json"

if (-not (Test-Path $ConfigPath)) {
    Write-Host "[ERROR] Config not found: $ConfigPath" -ForegroundColor Red
    exit 1
}

$Config = Get-Content $ConfigPath -Raw | ConvertFrom-Json

# Auto-detect paths
$SteamRoot = Get-SteamPath
if (-not $SteamRoot) {
    Write-Host "[ERROR] Cannot find Steam installation. Please ensure Steam is installed." -ForegroundColor Red
    exit 1
}

$GameLibrary = Get-GameLibraryPath -SteamPath $SteamRoot
if (-not $GameLibrary) {
    Write-Host "[ERROR] Cannot find Infinity Nikki installation. Please ensure the game is installed." -ForegroundColor Red
    exit 1
}

$AppID      = $Config.app.appid
$AppName    = $Config.app.name
$GameDir    = Join-Path $GameLibrary "steamapps\common\Infinity Nikki"
$AcfPath    = Join-Path $GameLibrary "steamapps\appmanifest_$AppID.acf"
$BackupDir  = Join-Path $ScriptDir "backups"
$ChromeProfileDir = Join-Path $ScriptDir "chrome-profile-steamdb"

# X6Game backup location: same drive as game library to avoid cross-partition moves
$GameLibraryDrive = (Get-Item (Split-Path $GameLibrary -Parent)).PSDrive.Root
$X6GameBackupDir  = Join-Path $GameLibraryDrive "X6Game_backup"

# Verify paths
if (-not (Test-Path $AcfPath)) {
    Write-Host "[ERROR] ACF file not found: $AcfPath" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $GameDir)) {
    Write-Host "[ERROR] Game directory not found: $GameDir" -ForegroundColor Red
    exit 1
}

# ===== Output Helpers =====
function Write-OK    { Write-Host "  [OK] $args" -ForegroundColor Green }
function Write-INFO  { Write-Host "  [i]  $args" -ForegroundColor Cyan }
function Write-WARN  { Write-Host "  [W]  $args" -ForegroundColor Yellow }
function Write-ERR   { Write-Host "  [X]  $args" -ForegroundColor Red }
function Write-H1    {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor White
    Write-Host "  $args" -ForegroundColor White
    Write-Host "========================================" -ForegroundColor White
}

# ===== Network Diagnostics =====

function Test-NetworkConnectivity {
    <#
    .SYNOPSIS
    检测网络连通性，对 SteamDB 和 Cloudflare 同时进行延迟测试
    #>
    Write-H1 "Network Connectivity Check"
    Write-INFO "检测到访问超时，正在执行网络检测..."

    # Ping SteamDB
    $steamdbPing = Test-Connection -ComputerName "steamdb.info" -Count 4 -ErrorAction SilentlyContinue
    # Ping Cloudflare
    $cloudflarePing = Test-Connection -ComputerName "cloudflare.com" -Count 4 -ErrorAction SilentlyContinue

    function Show-PingResult {
        param($PingResult, $TargetName)
        if ($PingResult) {
            $avgLatency = ($PingResult | Measure-Object -Property ResponseTime -Average).Average
            $latencyStr = $avgLatency.ToString('0.0')

            if ($avgLatency -gt 500) {
                Write-WARN "$TargetName 延迟过高: ${latencyStr} ms"
            } elseif ($avgLatency -gt 200) {
                Write-INFO "$TargetName 延迟较高: ${latencyStr} ms"
            } else {
                Write-OK "$TargetName 延迟正常: ${latencyStr} ms"
            }
            return $avgLatency
        } else {
            Write-ERR "无法连接到 $TargetName"
            return $null
        }
    }

    $steamdbLatency = Show-PingResult -PingResult $steamdbPing -TargetName "SteamDB (steamdb.info)"
    $cloudflareLatency = Show-PingResult -PingResult $cloudflarePing -TargetName "Cloudflare (cloudflare.com)"

    # 综合判断
    if ($null -eq $steamdbLatency -and $null -eq $cloudflareLatency) {
        Write-ERR "两个目标均无法连接，网络可能完全断开"
        Write-INFO "建议：检查网络连接、网线/WiFi、防火墙设置"
    } elseif ($null -eq $steamdbLatency -and $null -ne $cloudflareLatency) {
        Write-WARN "Cloudflare 可访问但 SteamDB 不可达"
        Write-INFO "建议：SteamDB 可能被网络运营商/DNS 屏蔽，尝试更换 DNS 或开启/关闭 VPN"
    } elseif ($steamdbLatency -gt 500 -or $cloudflareLatency -gt 500) {
        Write-WARN "网络延迟过高，可能影响 SteamDB 和 Cloudflare 访问"
        Write-INFO "建议：检查网络稳定性、更换网络环境、关闭 VPN/代理后重试"
    }

    # 检测 DNS 解析
    Write-INFO "正在检测 DNS 解析..."
    foreach ($domain in @("steamdb.info", "cloudflare.com")) {
        try {
            $dnsResult = Resolve-DnsName -Name $domain -ErrorAction Stop
            Write-OK "$domain DNS 解析成功: $($dnsResult.IPAddress)"
        } catch {
            Write-ERR "$domain DNS 解析失败: $_"
            if ($domain -eq "steamdb.info") {
                Write-INFO "建议：尝试更换 DNS 服务器（如 8.8.8.8 或 114.114.114.114）"
            }
        }
    }

    # 检测代理设置
    $proxySettings = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" -ErrorAction SilentlyContinue
    if ($proxySettings.ProxyEnable -eq 1) {
        Write-WARN "系统已启用代理: $($proxySettings.ProxyServer)"
        Write-INFO "代理可能导致 SteamDB 访问缓慢或被拦截，建议临时关闭代理后重试"
    }

    Write-Host ""
    Write-INFO "如果网络延迟过高（>500ms），建议先修复网络再继续操作"
    Write-WARN "在网络明显异常时继续尝试访问 SteamDB 会浪费时间且可能被 IP 封禁"
}

# ===== Lookup Tables =====
$StateFlagsMap = @{
    "0"  = "Invalid"
    "1"  = "Updating"
    "2"  = "Paused"
    "4"  = "Installed/Ready (OK)"
    "6"  = "Needs Update (DANGER)"
    "8"  = "Installing"
    "16" = "Syncing"
    "64" = "Committing"
}

$AutoUpdateMap = @{
    "0" = "Always auto-update"
    "1" = "Only update on launch (RECOMMENDED)"
    "2" = "High priority auto-update"
}

# ===== Utility Functions =====

function Test-SteamRunning {
    $proc = Get-Process -Name "steam" -ErrorAction SilentlyContinue
    if ($proc) { return $true }
    $web = Get-Process -Name "steamwebhelper" -ErrorAction SilentlyContinue
    return ($null -ne $web)
}

function Get-AcfContent {
    if (-not (Test-Path $AcfPath)) {
        Write-ERR "ACF file not found: $AcfPath"
        return $null
    }
    try {
        $raw = Get-Content $AcfPath -Raw -Encoding UTF8
        return $raw
    } catch {
        Write-ERR "Cannot read ACF: $_"
        return $null
    }
}

function Get-AcfStatus {
    $acf = Get-AcfContent
    if (-not $acf) { return $null }

    $status = @{}
    if ($acf -match '"StateFlags"\s+"(\d+)"') { $status.StateFlags = $matches[1] }
    if ($acf -match '"buildid"\s+"(\d+)"') { $status.BuildID = $matches[1] }
    if ($acf -match '"TargetBuildID"\s+"(\d+)"') { $status.TargetBuildID = $matches[1] }
    if ($acf -match '"SizeOnDisk"\s+"(\d+)"') { $status.SizeOnDisk = [int64]$matches[1] }
    if ($acf -match '"AutoUpdateBehavior"\s+"(\d+)"') { $status.AutoUpdateBehavior = $matches[1] }
    if ($acf -match '"BytesToDownload"\s+"(\d+)"') { $status.BytesToDownload = [int64]$matches[1] }

    # Extract InstalledDepots with balanced brace counting
    $depots = @{}
    $idIdx = $acf.IndexOf('"InstalledDepots"')
    if ($idIdx -ge 0) {
        $braceStart = $acf.IndexOf('{', $idIdx)
        if ($braceStart -ge 0) {
            $depth = 0
            $pos = $braceStart
            while ($pos -lt $acf.Length) {
                if ($acf[$pos] -eq '{') { $depth++ }
                elseif ($acf[$pos] -eq '}') {
                    $depth--
                    if ($depth -eq 0) { break }
                }
                $pos++
            }
            $depotBlock = $acf.Substring($braceStart, $pos - $braceStart + 1)
            $mp = '"(\d+)"\s*\{\s*"manifest"\s+"(\d+)"'
            $mcoll = [regex]::Matches($depotBlock, $mp)
            foreach ($m in $mcoll) {
                $depots[$m.Groups[1].Value] = @{
                    Manifest = $m.Groups[2].Value
                }
            }
        }
    }
    $status.Depots = $depots

    # ACF file attributes
    $fi = Get-Item $AcfPath -Force -ErrorAction SilentlyContinue
    if ($fi) {
        $status.IsReadOnly = $fi.IsReadOnly
        $status.AcfLastWrite = $fi.LastWriteTime
    }

    # Actual game dir size
    if (Test-Path $GameDir) {
        $files = Get-ChildItem $GameDir -Recurse -File -ErrorAction SilentlyContinue
        $status.ActualSizeOnDisk = ($files | Measure-Object Length -Sum).Sum
    } else {
        $status.ActualSizeOnDisk = 0
    }

    return $status
}

# ===== SteamDB Check Functions =====

function Invoke-SteamDBCheck {
    Write-H1 "SteamDB Version Check"
    
    if (Test-SteamRunning) {
        Write-WARN "Steam is RUNNING - exit before modifying"
        Write-Host ""
        Write-Host "Attempting to close Steam..." -ForegroundColor Yellow
        $steamExe = Join-Path $SteamRoot "steam.exe"
        if (Test-Path $steamExe) {
            Start-Process -FilePath $steamExe -ArgumentList "-shutdown" -WindowStyle Hidden
            Start-Sleep -Seconds 5
        }
        if (Test-SteamRunning) {
            Write-ERR "Cannot close Steam automatically. Please exit Steam manually and retry."
            return
        }
        Write-OK "Steam closed"
    }
    
    # Read local ACF
    $st = Get-AcfStatus
    if (-not $st) { return }
    
    $localBuildID = $st.BuildID
    $localManifest = $st.Depots[$DepotID].Manifest
    
    Write-INFO "Local BuildID: $localBuildID"
    Write-INFO "Local Manifest: $localManifest"
    
    # Create Chrome profile directory
    if (-not (Test-Path $ChromeProfileDir)) {
        New-Item -ItemType Directory -Path $ChromeProfileDir -Force | Out-Null
        Write-INFO "Created Chrome profile: $ChromeProfileDir"
    }
    
    # Find Chrome executable
    $chromeExe = "C:\Program Files\Google\Chrome\Application\chrome.exe"
    if (-not (Test-Path $chromeExe)) {
        $chromeExe = "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
    }
    if (-not (Test-Path $chromeExe)) {
        Write-ERR "Chrome not found. Please install Google Chrome."
        return
    }
    
    # Check if Chrome is already running with remote debugging
    try {
        $response = Invoke-RestMethod -Uri "http://127.0.0.1:9222/json/version" -ErrorAction Stop
        Write-INFO "Chrome remote debugging already active"
    } catch {
        # Start Chrome with remote debugging
        Write-INFO "Starting Chrome with remote debugging..."
        Start-Process -FilePath $chromeExe -ArgumentList @(
            "--remote-debugging-port=9222",
            "--user-data-dir=`"$ChromeProfileDir`"",
            "--no-first-run",
            "--no-default-browser-check",
            "https://steamdb.info/app/$AppID/depots/"
        ) -WindowStyle Normal
        
        # Wait for Chrome to start
        $maxWait = 30
        $waited = 0
        while ($waited -lt $maxWait) {
            try {
                $response = Invoke-RestMethod -Uri "http://127.0.0.1:9222/json/version" -ErrorAction Stop
                break
            } catch {
                Start-Sleep -Seconds 1
                $waited++
            }
        }
        
        if ($waited -ge $maxWait) {
            Write-ERR "Chrome failed to start remote debugging"
            # 执行网络检测
            Test-NetworkConnectivity
            return
        }
        
        Write-OK "Chrome remote debugging ready"
        Start-Sleep -Seconds 3
    }
    
    # Use Python to fetch SteamDB data via CDP
    $pythonScript = @"
import asyncio
import websockets
import json
import urllib.request
import os
import sys

async def get_steamdb_data(script_dir):
    try:
        with urllib.request.urlopen('http://127.0.0.1:9222/json/list') as response:
            pages = json.loads(response.read())
            page = [p for p in pages if 'steamdb' in p.get('url', '')]
            if page:
                page_id = page[0]['id']
            else:
                page_id = pages[0]['id']
        
        uri = f'ws://127.0.0.1:9222/devtools/page/{page_id}'
        
        async with websockets.connect(uri) as ws:
            # Get depots page content
            await ws.send(json.dumps({
                'id': 1,
                'method': 'Runtime.evaluate',
                'params': {'expression': 'document.body.innerText'}
            }))
            resp = await ws.recv()
            data = json.loads(resp)
            depots_text = data['result']['result']['value']
            
            with open(os.path.join(script_dir, 'steamdb_depots.txt'), 'w', encoding='utf-8') as f:
                f.write(depots_text)
            
            # Navigate to manifests page
            await ws.send(json.dumps({
                'id': 2,
                'method': 'Page.navigate',
                'params': {'url': 'https://steamdb.info/depot/$DepotID/manifests/'}
            }))
            await ws.recv()
            await asyncio.sleep(4)
            
            # Get manifests page content
            await ws.send(json.dumps({
                'id': 3,
                'method': 'Runtime.evaluate',
                'params': {'expression': 'document.body.innerText'}
            }))
            resp = await ws.recv()
            data = json.loads(resp)
            manifests_text = data['result']['result']['value']
            
            with open(os.path.join(script_dir, 'steamdb_manifests.txt'), 'w', encoding='utf-8') as f:
                f.write(manifests_text)
            
            print('OK')
    except Exception as e:
        print(f'ERROR: {e}')
        sys.exit(1)

asyncio.run(get_steamdb_data(r"$ScriptDir"))
"@
    
    $tempScript = Join-Path $ScriptDir "infi_steamdb_fetch.py"
    $pythonScript | Set-Content $tempScript -Encoding UTF8
    
    Write-INFO "Fetching SteamDB data..."
    $pythonResult = & python $tempScript 2>&1
    
    if ($pythonResult -notmatch "OK") {
        Write-ERR "Failed to fetch SteamDB data: $pythonResult"
        # 执行网络检测
        Test-NetworkConnectivity
        return
    }
    
    # Parse SteamDB data
    $depotsText = Get-Content (Join-Path $ScriptDir "steamdb_depots.txt") -Raw
    $manifestsText = Get-Content (Join-Path $ScriptDir "steamdb_manifests.txt") -Raw
    
    # Extract BuildID from App page (not Depot page)
    # The depots page shows App-level info including branches
    # Format: "public\t\t23458417" (tab-separated)
    $steamdbBuildID = [regex]::Match($depotsText, 'public\s+(\d+)').Groups[1].Value
    if (-not $steamdbBuildID) {
        # Fallback: try to find Build ID in the page
        # App BuildID is usually shown in the header or history link
        $steamdbBuildID = [regex]::Match($depotsText, 'app/' + $AppID + '/history/\s*(\d+)').Groups[1].Value
    }
    if (-not $steamdbBuildID) {
        # If we can't find it, use the local BuildID (assume it's correct)
        # This happens when the page doesn't show the BuildID clearly
        Write-WARN "Could not extract BuildID from SteamDB, using local value"
        $steamdbBuildID = $localBuildID
    }
    
    # Extract Manifest GID (first/latest)
    $steamdbManifest = [regex]::Match($manifestsText, '(\d{19,20})').Groups[1].Value
    
    # Clean up temp files
    Remove-Item -Force (Join-Path $ScriptDir "steamdb_depots.txt") -ErrorAction SilentlyContinue
    Remove-Item -Force (Join-Path $ScriptDir "steamdb_manifests.txt") -ErrorAction SilentlyContinue
    Remove-Item -Force $tempScript -ErrorAction SilentlyContinue
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor White
    Write-Host "  版本对比结果" -ForegroundColor White
    Write-Host "========================================" -ForegroundColor White
    Write-Host ""
    Write-Host "  Build ID:" -NoNewline
    Write-Host "  SteamDB: $steamdbBuildID" -NoNewline -ForegroundColor Cyan
    Write-Host "  本地: $localBuildID" -ForegroundColor Yellow
    Write-Host "  Manifest GID:" -NoNewline
    Write-Host "  SteamDB: $steamdbManifest" -NoNewline -ForegroundColor Cyan
    Write-Host "  本地: $localManifest" -ForegroundColor Yellow
    Write-Host ""
    
    $buildMatch = $steamdbBuildID -eq $localBuildID
    $manifestMatch = $steamdbManifest -eq $localManifest
    
    if ($buildMatch -and $manifestMatch) {
        Write-Host "  [OK] 版本已是最新，无需更新" -ForegroundColor Green
        return
    }
    
    Write-Host "  [!] 发现新版本，需要更新 ACF" -ForegroundColor Yellow
    Write-Host ""
    
    if ($NoInteractive) {
        Write-INFO "Non-interactive mode - auto-updating..."
    } else {
        $confirm = Read-Host "  确认更新? (y/N)"
        if ($confirm -ne 'y' -and $confirm -ne 'Y') {
            Write-Host "  已取消" -ForegroundColor Gray
            return
        }
    }
    
    # Update ACF
    Invoke-UpdateACF -BuildID $steamdbBuildID -ManifestGID $steamdbManifest
}

function Invoke-UpdateACF {
    param([string]$BuildID, [string]$ManifestGID)
    
    $st = Get-AcfStatus
    if (-not $st) { return }
    
    # Backup
    if (-not (Test-Path $BackupDir)) {
        New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
    }
    $backupPath = Join-Path $BackupDir ("appmanifest_$AppID.acf.bak." + (Get-Date -Format "yyyyMMdd_HHmmss"))
    Copy-Item $AcfPath $backupPath -Force
    Write-INFO "Backup: $backupPath"
    
    # Unlock
    $wasRO = $st.IsReadOnly
    if ($wasRO) {
        Set-ItemProperty $AcfPath -Name IsReadOnly -Value $false
        Write-INFO "Temporarily unlocked ACF"
    }
    
    # Read & modify
    $acf = Get-Content $AcfPath -Raw -Encoding UTF8
    $changes = 0
    
    if ($BuildID) {
        $acf = $acf -replace '("buildid"\s+")\d+(")', "`${1}$BuildID`$2"
        $changes++
        Write-OK "buildid -> $BuildID"
    }
    
    if ($ManifestGID) {
        $pattern = "(`"$DepotID`"\s*\{\s*`"manifest`"\s+`")\d+(`")"
        $acf = $acf -replace $pattern, "`${1}$ManifestGID`$2"
        $changes++
        Write-OK "Depot $DepotID manifest -> $ManifestGID"
    }
    
    # Zero TargetBuildID
    $acf = $acf -replace '("TargetBuildID"\s+")\d+(")', '${1}0$2'
    $changes++
    Write-OK "TargetBuildID -> 0"
    
    # Force StateFlags=4
    if ($acf -match '"StateFlags"\s+"(\d+)"' -and $matches[1] -ne "4") {
        $acf = $acf -replace '("StateFlags"\s+")\d+(")', '${1}4$2'
        $changes++
        Write-OK "StateFlags -> 4"
    }
    
    # Zero download/stage fields (避免 Steam 误判有待更新内容)
    @("BytesToDownload","BytesDownloaded","BytesToStage","BytesStaged") | ForEach-Object {
        if ($acf -match """$_\""\s+""\d+""") {
            $acf = $acf -replace """$_\""\s+""\d+""", """$_""		""0"""
            $changes++
            Write-OK "$_ -> 0"
        }
    }
    
    # Sync SizeOnDisk
    $actualSize = (Get-ChildItem $GameDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
    $acf = $acf -replace '("SizeOnDisk"\s+")\d+(")', "`${1}$actualSize`$2"
    
    # Write
    Set-Content $AcfPath -Value $acf -Encoding UTF8 -NoNewline
    Write-OK "ACF updated ($changes modifications)"
    
    # Re-lock
    Set-ItemProperty $AcfPath -Name IsReadOnly -Value $true
    Write-OK "ACF re-locked (read-only)"
    
    Write-Host ""
    Write-Host "=== Update Complete ===" -ForegroundColor Green
}

# ===== Core Commands =====

function Invoke-Status {
    Write-H1 "Infinity Nikki Steam Shell Status"

    if (Test-SteamRunning) {
        Write-WARN "Steam is RUNNING - exit before modifying ACF"
    } else {
        Write-OK "Steam NOT running - safe to modify"
    }

    Write-Host ""
    Write-Host "  Steam Root: $SteamRoot" -ForegroundColor Gray
    Write-Host "  Game Dir:   $GameDir" -ForegroundColor Gray
    Write-Host "  ACF File:   $AcfPath" -ForegroundColor Gray
    Write-Host ""

    if (Test-Path $GameDir) {
        Write-OK "Game dir: $GameDir"
    } else {
        Write-ERR "Game dir not found: $GameDir"
        return
    }

    if (-not (Test-Path $AcfPath)) {
        Write-ERR "ACF not found: $AcfPath"
        return
    }

    $st = Get-AcfStatus
    if (-not $st) { return }

    $sfDesc = $StateFlagsMap[$st.StateFlags]
    if (-not $sfDesc) { $sfDesc = "Unknown" }
    $auDesc = $AutoUpdateMap[$st.AutoUpdateBehavior]
    if (-not $auDesc) { $auDesc = "Unknown" }

    Write-Host ""
    Write-Host "  --- ACF Key Fields ---" -ForegroundColor White
    Write-Host "  AppID:              $AppID"
    Write-Host "  StateFlags:         $($st.StateFlags) ($sfDesc)"
    Write-Host "  BuildID:            $($st.BuildID)"
    Write-Host "  TargetBuildID:      $($st.TargetBuildID)"
    Write-Host "  AutoUpdateBehavior: $($st.AutoUpdateBehavior) ($auDesc)"
    Write-Host "  SizeOnDisk (ACF):   $([math]::Round($st.SizeOnDisk/1GB,2)) GB"
    Write-Host "  BytesToDownload:    $([math]::Round($st.BytesToDownload/1GB,2)) GB"

    Write-Host ""
    Write-Host "  --- InstalledDepots ---" -ForegroundColor White
    foreach ($depot in $st.Depots.Keys) {
        Write-Host "  Depot $depot : manifest=$($st.Depots[$depot].Manifest)"
    }

    Write-Host ""
    Write-Host "  --- File State ---" -ForegroundColor White
    $roStr = if ($st.IsReadOnly) { "YES (locked)" } else { "NO (writable)" }
    Write-Host "  ACF readonly:       $roStr"
    Write-Host "  ACF last modified:  $($st.AcfLastWrite)"
    Write-Host "  Actual disk usage:  $([math]::Round($st.ActualSizeOnDisk/1GB,2)) GB"

    # Check X6Game location
    $x6gInSteam = Join-Path $GameDir "InfinityNikki\X6Game"
    
    if (Test-Path $x6gInSteam) {
        $x6s = (Get-ChildItem $x6gInSteam -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
        Write-WARN "X6Game in Steam dir: $([math]::Round($x6s/1GB,2)) GB - run skeletonize to move"
    } elseif (Test-Path $X6GameBackupDir) {
        $x6s = (Get-ChildItem $X6GameBackupDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
        Write-OK "X6Game moved to backup: $X6GameBackupDir ($([math]::Round($x6s/1GB,2)) GB)"
    } else {
        Write-INFO "X6Game not found in Steam dir or backup"
    }

    # Check standalone launcher
    $standaloneLaunchers = Find-StandaloneLauncher
    if ($standaloneLaunchers) {
        Write-Host ""
        Write-Host "  --- Standalone Launcher ---" -ForegroundColor White
        foreach ($launcher in $standaloneLaunchers) {
            Write-OK "Found: $(Split-Path $launcher.Path -Leaf)"
            Write-INFO "  Path: $($launcher.Path)"
            if ($launcher.GamePath) {
                Write-INFO "  Game: $($launcher.GamePath)"
            }
        }
    }

    # Health check
    Write-Host ""
    Write-Host "  --- Health Check ---" -ForegroundColor White
    $healthy = $true

    if ($st.StateFlags -ne "4") {
        Write-WARN "StateFlags=$($st.StateFlags) (should be 4)"
        $healthy = $false
    } else { Write-OK "StateFlags = 4" }

    if ($st.TargetBuildID -ne "0") {
        Write-WARN "TargetBuildID=$($st.TargetBuildID) (should be 0)"
        $healthy = $false
    } else { Write-OK "TargetBuildID = 0" }

    if ($st.AutoUpdateBehavior -ne "1") {
        Write-WARN "AutoUpdateBehavior=$($st.AutoUpdateBehavior) (should be 1)"
        $healthy = $false
    } else { Write-OK "AutoUpdateBehavior = 1" }

    if (-not $st.IsReadOnly) {
        Write-WARN "ACF NOT read-only"
        $healthy = $false
    } else { Write-OK "ACF is read-only" }

    Write-Host ""
    if ($healthy) {
        Write-Host "  >>> STATUS: HEALTHY <<<" -ForegroundColor Green
    } else {
        Write-Host "  >>> STATUS: NEEDS FIX <<<" -ForegroundColor Yellow
    }
}

function Invoke-Update {
    Write-H1 "Update ACF Version Info"

    if (Test-SteamRunning) {
        Write-ERR "Steam is running! Please exit Steam completely first."
        if (-not $Force) { return }
    }

    $st = Get-AcfStatus
    if (-not $st) { return }

    $newBuildID = $BuildID
    $newManifestGID = $ManifestGID

    if ([string]::IsNullOrWhiteSpace($newBuildID)) {
        if ($NoInteractive) {
            Write-ERR "No BuildID provided (use -BuildID)"
            return
        }
        Write-Host ""
        Write-Host "Enter new BuildID:"
        Write-Host "  Ref: https://steamdb.info/app/$AppID/history/"
        Write-Host "  Current: $($st.BuildID)"
        $newBuildID = Read-Host "  BuildID"
    }

    if ([string]::IsNullOrWhiteSpace($newManifestGID)) {
        if ($NoInteractive) {
            Write-ERR "No ManifestGID provided (use -ManifestGID)"
            return
        }
        Write-Host ""
        Write-Host "Enter new Manifest GID for each depot:"
        foreach ($depot in $st.Depots.Keys) {
            Write-Host "  Ref: https://steamdb.info/depot/$depot/manifests/"
            Write-Host "  Current Depot $depot manifest: $($st.Depots[$depot].Manifest)"
            $gid = Read-Host "  Depot $depot new ManifestGID"
            if ($gid) { $newManifestGID = $gid }
        }
    }

    if ([string]::IsNullOrWhiteSpace($newBuildID) -and [string]::IsNullOrWhiteSpace($newManifestGID)) {
        Write-ERR "No update info provided"
        return
    }

    Write-Host ""
    Write-Host "Will apply:" -ForegroundColor Yellow
    if ($newBuildID) {
        Write-Host "  BuildID: $($st.BuildID) -> $newBuildID"
    }
    if ($newManifestGID) {
        Write-Host "  Depot $DepotID Manifest: $($st.Depots[$DepotID].Manifest) -> $newManifestGID"
    }
    Write-Host "  TargetBuildID: $($st.TargetBuildID) -> 0"
    Write-Host "  StateFlags: $($st.StateFlags) -> 4 (if not already)"

    if (-not $Force) {
        $confirm = Read-Host "Confirm? (y/N)"
        if ($confirm -ne 'y' -and $confirm -ne 'Y') {
            Write-Host "Cancelled"
            return
        }
    }
    
    Invoke-UpdateACF -BuildID $newBuildID -ManifestGID $newManifestGID
}

function Invoke-Skeletonize {
    Write-H1 "Skeletonize Cleanup (Move Mode)"

    if (Test-SteamRunning) {
        Write-ERR "Steam is running! Please exit Steam completely first."
        if (-not $Force) { return }
    }

    if (-not (Test-Path $GameDir)) {
        Write-ERR "Game dir not found: $GameDir"
        return
    }

    $toMove = @()
    foreach ($d in $Config.skeleton.move_dirs) {
        $p = Join-Path $GameDir $d
        if (Test-Path $p) {
            $sz = (Get-ChildItem $p -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
            $toMove += [PSCustomObject]@{Path=$p; Type="Dir"; Size=$sz; Relative=$d}
        }
    }
    foreach ($f in $Config.skeleton.delete_files) {
        $p = Join-Path $GameDir $f
        if (Test-Path $p) {
            $fi = Get-Item $p
            $toMove += [PSCustomObject]@{Path=$p; Type="File"; Size=$fi.Length; Relative=$f}
        }
    }

    if ($toMove.Count -eq 0) {
        Write-OK "Nothing to move - directory is minimal skeleton"
        return
    }

    Write-Host ""
    Write-Host "Will move to backup:" -ForegroundColor Yellow
    $total = 0
    foreach ($item in $toMove) {
        if ($item.Size -gt 1GB) { $s = "$([math]::Round($item.Size/1GB,2)) GB" }
        elseif ($item.Size -gt 1MB) { $s = "$([math]::Round($item.Size/1MB,1)) MB" }
        else { $s = "$($item.Size) B" }
        Write-Host "  [$($item.Type)] $($item.Relative) ($s)"
        $total += $item.Size
    }
    Write-Host "  Total: $([math]::Round($total/1GB,2)) GB" -ForegroundColor Green
    Write-Host "  Backup location: $X6GameBackupDir" -ForegroundColor Cyan
    Write-Host "  (Same drive as game library for fast move)" -ForegroundColor Gray

    if ($DryRun) {
        Write-Host ""
        Write-Host "[DryRun] No actual changes" -ForegroundColor Cyan
        return
    }

    if (-not $Force) {
        $c = Read-Host "Confirm move? (y/N)"
        if ($c -ne 'y' -and $c -ne 'Y') { Write-Host "Cancelled"; return }
    }

    # Create backup directory on same drive as game library
    if (-not (Test-Path $X6GameBackupDir)) {
        New-Item -ItemType Directory -Path $X6GameBackupDir -Force | Out-Null
    }

    foreach ($item in $toMove) {
        try {
            $dest = Join-Path $X6GameBackupDir (Split-Path $item.Relative -Leaf)
            Move-Item $item.Path $dest -Force -ErrorAction Stop
            Write-OK "Moved: $($item.Relative) -> $X6GameBackupDir"
        } catch {
            Write-ERR "Failed: $($item.Relative) - $_"
        }
    }

    Write-Host ""
    foreach ($kf in $Config.skeleton.keep_files) {
        $p = Join-Path $GameDir $kf
        if (Test-Path $p) { Write-OK "Key file: $kf" }
        else { Write-WARN "MISSING: $kf" }
    }

    # Update ACF SizeOnDisk
    if (Test-Path $AcfPath) {
        $wasRO = (Get-Item $AcfPath -Force).IsReadOnly
        if ($wasRO) { Set-ItemProperty $AcfPath -Name IsReadOnly -Value $false }
        $acf = Get-Content $AcfPath -Raw -Encoding UTF8
        $act = (Get-ChildItem $GameDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
        $acf = $acf -replace '("SizeOnDisk"\s+")\d+(")', "`${1}$act`$2"
        Set-Content $AcfPath -Value $acf -Encoding UTF8 -NoNewline
        if ($wasRO) { Set-ItemProperty $AcfPath -Name IsReadOnly -Value $true }
        Write-OK "ACF SizeOnDisk synced"
    }

    Write-Host ""
    Write-Host "=== Skeletonize Complete ===" -ForegroundColor Green
    Write-Host "To restore X6Game, run: .\infi-manager.ps1 restore" -ForegroundColor Cyan
}

function Invoke-Restore {
    Write-H1 "Restore X6Game to Steam Directory"

    if (Test-SteamRunning) {
        Write-ERR "Steam is running! Please exit Steam completely first."
        if (-not $Force) { return }
    }

    if (-not (Test-Path $X6GameBackupDir)) {
        Write-ERR "Backup not found: $X6GameBackupDir"
        Write-INFO "Run skeletonize first to create backup"
        return
    }

    # Check what needs to be restored
    $toRestore = Get-ChildItem $X6GameBackupDir -ErrorAction SilentlyContinue
    if (-not $toRestore) {
        Write-ERR "Backup directory is empty"
        return
    }

    Write-Host ""
    Write-Host "Will restore from backup:" -ForegroundColor Yellow
    Write-Host "  Source: $X6GameBackupDir" -ForegroundColor Gray
    foreach ($item in $toRestore) {
        if ($item.PSIsContainer) {
            Write-Host "  [Dir] $($item.Name) -> InfinityNikki\X6Game"
        } else {
            Write-Host "  [File] $($item.Name) -> InfinityNikki\X6Game"
        }
    }

    if ($DryRun) {
        Write-Host ""
        Write-Host "[DryRun] No actual changes" -ForegroundColor Cyan
        return
    }

    if (-not $Force) {
        $c = Read-Host "Confirm restore? (y/N)"
        if ($c -ne 'y' -and $c -ne 'Y') { Write-Host "Cancelled"; return }
    }

    # Create destination directory
    $destDir = Join-Path $GameDir "InfinityNikki\X6Game"
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }

    foreach ($item in $toRestore) {
        try {
            $dest = Join-Path $destDir $item.Name
            if (Test-Path $dest) {
                Remove-Item $dest -Recurse -Force -ErrorAction SilentlyContinue
            }
            Move-Item $item.FullName $dest -Force -ErrorAction Stop
            Write-OK "Restored: $($item.Name) -> InfinityNikki\X6Game"
        } catch {
            Write-ERR "Failed: $($item.Name) - $_"
        }
    }

    # Update ACF SizeOnDisk
    if (Test-Path $AcfPath) {
        $wasRO = (Get-Item $AcfPath -Force).IsReadOnly
        if ($wasRO) { Set-ItemProperty $AcfPath -Name IsReadOnly -Value $false }
        $acf = Get-Content $AcfPath -Raw -Encoding UTF8
        $act = (Get-ChildItem $GameDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
        $acf = $acf -replace '("SizeOnDisk"\s+")\d+(")', "`${1}$act`$2"
        Set-Content $AcfPath -Value $acf -Encoding UTF8 -NoNewline
        if ($wasRO) { Set-ItemProperty $AcfPath -Name IsReadOnly -Value $true }
        Write-OK "ACF SizeOnDisk synced"
    }

    Write-Host ""
    Write-Host "=== Restore Complete ===" -ForegroundColor Green
}

function Invoke-Lock {
    if (-not (Test-Path $AcfPath)) {
        Write-ERR "ACF not found: $AcfPath"
        return
    }
    $cur = (Get-Item $AcfPath -Force).IsReadOnly
    if ($Command -eq "unlock") {
        if (-not $cur) { Write-INFO "ACF already unlocked"; return }
        Set-ItemProperty $AcfPath -Name IsReadOnly -Value $false
        Write-OK "ACF unlocked (Steam can modify)"
    } else {
        if ($cur) { Write-INFO "ACF already locked"; return }
        Set-ItemProperty $AcfPath -Name IsReadOnly -Value $true
        Write-OK "ACF locked as read-only"
    }
}

function Invoke-Verify {
    Write-H1 "Full Verification"
    $ok = $true

    if (-not (Test-Path $AcfPath)) { Write-ERR "ACF missing"; $ok = $false }
    else {
        Write-OK "ACF present"
        $st = Get-AcfStatus
        if ($st) {
            if ($st.StateFlags -eq "4") { Write-OK "StateFlags=4" }
            else { Write-ERR "StateFlags=$($st.StateFlags) (should be 4)"; $ok = $false }
            if ($st.TargetBuildID -eq "0") { Write-OK "TargetBuildID=0" }
            else { Write-ERR "TargetBuildID=$($st.TargetBuildID) (should be 0)"; $ok = $false }
            if ($st.AutoUpdateBehavior -eq "1") { Write-OK "AutoUpdateBehavior=1" }
            else { Write-WARN "AutoUpdateBehavior=$($st.AutoUpdateBehavior) (suggest 1)" }
            if ($st.IsReadOnly) { Write-OK "ACF read-only" }
            else { Write-ERR "ACF NOT read-only"; $ok = $false }
        }
    }

    if (-not (Test-Path $GameDir)) { Write-ERR "Game dir missing"; $ok = $false }
    else {
        Write-OK "Game dir present"
        foreach ($kf in $Config.skeleton.keep_files) {
            $p = Join-Path $GameDir $kf
            if (Test-Path $p) { Write-OK "Key file: $kf" }
            else { Write-WARN "MISSING: $kf" }
        }
    }

    # Check X6Game location
    $x6gInSteam = Join-Path $GameDir "InfinityNikki\X6Game"
    if (Test-Path $x6gInSteam) {
        Write-WARN "X6Game still in Steam dir - run skeletonize to move"
    } elseif (Test-Path $X6GameBackupDir) {
        Write-OK "X6Game moved to backup: $X6GameBackupDir"
    } else {
        Write-INFO "X6Game not found"
    }

    # Check standalone launcher
    $standaloneLaunchers = Find-StandaloneLauncher
    if ($standaloneLaunchers) {
        Write-Host ""
        Write-Host "  --- Standalone Launcher Detected ---" -ForegroundColor White
        foreach ($launcher in $standaloneLaunchers) {
            Write-OK "Found: $($launcher.Path)"
            if ($launcher.GamePath) {
                Write-INFO "Game path: $($launcher.GamePath)"
            }
        }
    }

    if (Test-SteamRunning) { Write-WARN "Steam IS running - exit before modifying" }
    else { Write-OK "Steam not running" }

    Write-Host ""
    if ($ok) { Write-Host "  >>> ALL CHECKS PASSED <<<" -ForegroundColor Green }
    else { Write-Host "  >>> ISSUES FOUND - run fix commands <<<" -ForegroundColor Red }
}

function Invoke-ResidualCheck {
    Write-H1 "Residual Files Check"
    $found = $false
    
    # 1) ACF temp files
    $steamappsDir = Split-Path $AcfPath -Parent
    $acfTmps = Get-ChildItem $steamappsDir -Filter "appmanifest_$AppID.acf.*.tmp" -ErrorAction SilentlyContinue
    if ($acfTmps) {
        Write-WARN "ACF temp files found:"
        foreach ($f in $acfTmps) { Write-WARN "  $($f.FullName) ($([math]::Round($f.Length/1KB,1)) KB)" }
        $found = $true
    } else { Write-OK "No ACF temp files" }
    
    # 2) ACF bak files
    $acfBaks = Get-ChildItem $steamappsDir -Filter "appmanifest_$AppID.acf.bak.*" -ErrorAction SilentlyContinue
    if ($acfBaks) {
        Write-WARN "ACF backup files found:"
        foreach ($f in $acfBaks) { Write-WARN "  $($f.FullName) ($([math]::Round($f.Length/1KB,1)) KB)" }
        $found = $true
    } else { Write-OK "No ACF backup files" }
    
    # 3) downloading directory
    $dlDir = Join-Path $steamappsDir "downloading\$AppID"
    if (Test-Path $dlDir) {
        $dlSize = (Get-ChildItem $dlDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
        Write-WARN "Downloading dir: $dlDir ($([math]::Round($dlSize/1MB,1)) MB)"
        $found = $true
    } else { Write-OK "No downloading directory" }
    
    # 4) temp directory
    $tempDir = Join-Path $steamappsDir "temp\$AppID"
    if (Test-Path $tempDir) {
        $tmpSize = (Get-ChildItem $tempDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
        Write-WARN "Temp dir: $tempDir ($([math]::Round($tmpSize/1MB,1)) MB)"
        $found = $true
    } else { Write-OK "No temp directory" }
    
    Write-Host ""
    if ($found) {
        Write-WARN "Residual files found - consider cleaning manually"
    } else {
        Write-OK "No residual files - clean"
    }
}

function Invoke-Report {
    Write-H1 "===== InfiSteam Full Report ====="
    
    # Steam status
    $steamRunning = Test-SteamRunning
    Write-Host "Steam status: $(if ($steamRunning) {'RUNNING'} else {'Not running'})"
    
    # Paths
    $st = Get-AcfStatus
    if (-not $st) {
        Write-ERR "Cannot read ACF status"
        return
    }
    
    Write-Host ""
    Write-Host "--- Paths ---"
    Write-Host "  Steam Root:        $SteamRoot"
    Write-Host "  Game Dir:          $GameDir"
    Write-Host "  ACF File:          $AcfPath"
    Write-Host "  Backup Dir:        $BackupDir"
    Write-Host "  X6Game Backup:     $X6GameBackupDir"
    
    # Version info
    Write-Host ""
    Write-Host "--- Version ---"
    $isChina = $st.AcfContent -match "sub/1221922" -or $st.AcfContent -match "schinese"
    $verType = if ($isChina) { "中国版" } else { "国际版" }
    Write-Host "  Type:              $verType"
    Write-Host "  BuildID:           $($st.BuildID)"
    Write-Host "  TargetBuildID:     $($st.TargetBuildID)"
    Write-Host "  AutoUpdate:        $($st.AutoUpdateBehavior)"
    
    foreach ($depot in $st.Depots.Keys) {
        Write-Host "  Depot $depot manifest: $($st.Depots[$depot].Manifest)"
    }
    
    # ACF state
    Write-Host ""
    Write-Host "--- ACF State ---"
    Write-Host "  StateFlags:        $($st.StateFlags) $(if($st.StateFlags -eq '4'){'✓'}else{'✗'})"
    Write-Host "  ReadOnly:          $(if($st.IsReadOnly){'YES ✓'}else{'NO ✗'})"
    Write-Host "  BytesToDownload:   $($st.BytesToDownload)"
    Write-Host "  BytesDownloaded:   $($st.BytesDownloaded)"
    
    # X6Game location
    Write-Host ""
    Write-Host "--- X6Game ---"
    $x6gInSteam = Join-Path $GameDir "InfinityNikki\X6Game"
    if (Test-Path $x6gInSteam) {
        $s = (Get-ChildItem $x6gInSteam -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
        Write-Host "  Location: Steam dir: $([math]::Round($s/1GB,2)) GB"
        Write-Host "  Action:   Run skeletonize to free space"
    } elseif (Test-Path $X6GameBackupDir) {
        $s = (Get-ChildItem $X6GameBackupDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
        Write-Host "  Location: Backup: $X6GameBackupDir ($([math]::Round($s/1GB,2)) GB)"
    } else {
        Write-Host "  Location: Not found"
    }
    
    # Launcher
    Write-Host ""
    Write-Host "--- Standalone Launcher ---"
    $launchers = Find-StandaloneLauncher
    if ($launchers) {
        foreach ($l in $launchers) {
            Write-Host "  Found: $($l.Path)"
            if ($l.GamePath) { Write-Host "  Game:  $($l.GamePath)" }
        }
    } else {
        Write-Host "  Not detected"
    }
    
    Write-Host ""
    Write-Host "===== End of Report ====="
}

function Invoke-Query {
    Write-H1 "SteamDB Query Helper"
    Write-Host "SteamDB blocks automated scraping. Open these URLs manually:"
    Write-Host ""
    Write-Host "  1. Latest BuildID:" -ForegroundColor Cyan
    Write-Host "     $($Config.steamdb.app_url)history/"
    Write-Host ""
    Write-Host "  2. China Sub info:" -ForegroundColor Cyan
    Write-Host "     $($Config.steamdb.sub_url)"
    Write-Host ""
    Write-Host "  3. Depot Manifest GID:" -ForegroundColor Cyan
    $st = Get-AcfStatus
    foreach ($depot in $st.Depots.Keys) {
        $url = $Config.steamdb.depot_url_template -replace '\{depotid\}', $depot
        Write-Host "     Depot $depot : $url"
    }
    Write-Host ""
    Write-Host "  Or run automatic check:" -ForegroundColor Green
    Write-Host "    .\infi-manager.ps1 steamdb-check"
}

function Invoke-Help {
    Write-Host @"
========================================
  Infinity Nikki Steam Shell Manager
  Plan 2+3: ACF Anti-Update + Skeletonize
  Version: 2.0 Universal
========================================

Usage:
  .\infi-manager.ps1 <command> [options]

Commands:
  status         Show current status and health check
  update         Update ACF version info (prevents update detection)
  skeletonize    Move X6Game from Steam directory to backup
  restore        Restore X6Game from backup to Steam directory
  lock           Lock ACF as read-only
  unlock         Unlock ACF read-only
  verify         Run full verification
  residual-check Check and report residual files (tmp/bak/downloading/temp)
  report         Display complete status report
  steamdb-check  Automatic SteamDB version check and update
  query          Show steamdb URLs for manual version lookup
  help           Show this help

Update options:
  -BuildID <id>      New BuildID from steamdb history page
  -ManifestGID <id>  New Manifest GID from steamdb depot page
  -DepotID <id>      Target Depot ID (default: 3164332)

General:
  -Force             Skip confirmation prompts
  -DryRun            Simulate without actual changes
  -NoInteractive     Non-interactive mode (for automation)

Typical workflow:
  1. steamdb-check -> Automatic version check and update
  2. status        -> Check current state
  3. skeletonize   -> Move redundant content to backup
  4. restore       -> Restore content when needed
  5. verify        -> Confirm everything is OK

IMPORTANT:
  - Steam must be COMPLETELY EXITED before update/skeletonize/restore
  - Set game update to "Only update on launch" in Steam
  - Run steamdb-check after each official game update
  - X6Game backup is stored on the same drive as your game library for fast move

Standalone Launcher:
  If you have the non-Steam version installed, the script will detect it
  and show the path in status/verify output. You can configure Steam to
  launch the standalone version via Properties -> Launch Options.

"@
}

# ===== Main Entry =====
switch ($Command) {
    "status"        { Invoke-Status }
    "update"        { Invoke-Update }
    "skeletonize"   { Invoke-Skeletonize }
    "restore"       { Invoke-Restore }
    "lock"          { Invoke-Lock }
    "unlock"        { Invoke-Lock }
    "verify"        { Invoke-Verify }
    "steamdb-check" { Invoke-SteamDBCheck }
    "query"         { Invoke-Query }
    "residual-check" { Invoke-ResidualCheck }
    "report"        { Invoke-Report }
    "help"          { Invoke-Help }
    default         { Invoke-Help }
}
