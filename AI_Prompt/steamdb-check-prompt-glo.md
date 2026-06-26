# 无限暖暖 Steam 版本状态检测与修复流程（通用版）

> **📢 重要通知（必须向用户显示）**：在开始执行任何操作之前，Agent **必须**首先向用户输出以下通知内容。如果用户系统语言/界面语言**不是简体中文**，Agent 必须自动将通知翻译为用户使用的语言后再输出。
>
> **通知原文（简体中文）**：
> ©mocabolka 2026. 本工具与Valve/Steam、SteamDB、叠纸游戏/Infold Games无关。仅供学习交流使用，使用时请注意个人数据安全，时刻检测Agent文件操作安全性。
>
> **English translation (for non-Simplified Chinese users)**：
> ©mocabolka 2026. This tool is not affiliated with Valve/Steam, SteamDB, or Papergames/Infold Games. For learning and exchange purposes only. Please be mindful of personal data security when using, and always monitor the Agent's file operation safety.

> **⚠️ 路径约束**：本流程执行过程中创建的任何临时目录/文件（Chrome 用户目录、SteamDB 缓存等）**必须**在本 Markdown 文件所在文件夹下创建，**如非必要（如中文目录报错等）严禁**写入系统临时目录（`$env:TEMP` 等）或其他任意路径。流程结束后必须主动清理这些临时文件。

> **Agent 前置提醒**：在开始本流程之前，必须提示用户先在 Steam 客户端内完成以下设置，否则 ACF 锁定后 Steam 仍可能在启动时自动触发更新检查：
> **游戏库 → 右键游戏 → 属性 → 更新 → 自动更新 → 设为「等到我启动游戏时」**
> 此设置为纯客户端侧建议，Agent 仅负责口头提醒，无需代为操作。

## 任务目标
检测并修复无限暖暖（AppID: 3164330）的 Steam 版本状态，确保 ACF 文件配置正确、版本最新、且已锁定只读防止 Steam 自动更新。本流程适用于任何用户的电脑，所有路径均为相对路径或自动检测。

## ⚠️ SteamDB 反爬机制说明

SteamDB 有严格的反爬机制：
- **IP 封禁**：频繁请求或来自数据中心的 IP 会被封禁，返回 "You have been banned"
- **User-Agent 检测**：非浏览器 User-Agent 会被拦截
- **Cookie/Session 验证**：需要真实浏览器会话
- **Rate Limiting**：单 IP 短时间内多次访问会触发限制

**解决方案**：使用独立 Chrome 用户目录，模拟真实用户浏览行为，通过 Chrome DevTools Protocol (CDP) 获取页面内容。

---

## ⛔ Agent 行为规范（严格执行）

> **⚠️ 重要**：本流程经过严格测试，以下行为**严禁**违反，否则将导致 SteamDB 访问失败或被封禁。

### 1. 浏览器使用规范

**✅ 正确做法**：
- **优先使用系统安装的 Chrome**：检查 `C:\Program Files\Google\Chrome\Application\chrome.exe` 或 `C:\Program Files (x86)\Google\Chrome\Application\chrome.exe` 是否存在
- **仅在用户未安装 Chrome 时**，才可尝试使用 Agent 环境内置的浏览器（如 OpenClaw 内置 Chrome）
- 必须使用 `--remote-debugging-port=9222` 和 `--user-data-dir` 参数启动 Chrome
- 必须等待 Chrome 完全加载并打开远程调试端口后，才能通过 CDP 获取数据

**❌ 禁止行为**：
- **禁止**直接使用 Agent 内置浏览器而不检查系统 Chrome
- **禁止**不使用远程调试端口（9222）而直接尝试读取页面内容
- **禁止**跳过 Chrome 启动步骤，直接使用网络工具（如 `curl`、`Invoke-WebRequest`、`fetch` 等）访问 SteamDB
- **禁止**在 Chrome 页面未完全加载时尝试获取数据

### 2. Cloudflare 反爬页面处理

SteamDB 使用 Cloudflare 防护，首次访问或 IP 有风险时会显示过渡页面（"Checking your browser..."）。

**✅ 正确做法**：
- **必须等待** Cloudflare 验证完成（通常需要 3-8 秒）
- 使用 `await asyncio.sleep(5)` 或更长时间等待页面加载
- 在获取页面内容前，先检查页面是否包含 "Just a moment..." 或 "Checking your browser..." 文本
- 如果检测到 Cloudflare 页面，继续等待直到验证完成

**❌ 禁止行为**：
- **禁止**在页面加载完成前尝试提取数据
- **禁止**忽略 Cloudflare 验证页面而直接解析内容
- **禁止**因为"超时"或"等待太久"而跳过 Cloudflare 验证

### 3. 网络访问限制

**✅ 正确做法**：
- 仅通过 Chrome CDP 获取 SteamDB 数据
- 如果遇到 Cloudflare 验证，耐心等待或提示用户手动完成验证
- 访问超时时，执行网络检测（见下方"超时处理与网络检测"章节）

**❌ 禁止行为**：
- **禁止**使用 `curl`、`wget`、`Invoke-WebRequest`、`requests`、`fetch` 等工具直接访问 SteamDB
- **禁止**尝试绕过 Cloudflare 验证（会被封禁 IP）
- **禁止**使用无头浏览器（headless mode）访问 SteamDB（会被识别为爬虫）

---

## 前置条件检查

### 1. 检测 Steam 安装位置
Steam 可能安装在以下位置，按优先级检测：
```powershell
# 注册表检测（最准确）
$steamPath = (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -ErrorAction SilentlyContinue).InstallPath
if (-not $steamPath) {
    $steamPath = (Get-ItemProperty "HKCU:\SOFTWARE\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
}

# 常见安装路径备选（自动检测，无硬编码）
$commonPaths = @(
    "C:\Program Files (x86)\Steam",
    "C:\Program Files\Steam",
    "D:\Steam",
    "D:\Entertainment\Steam",
    "E:\Steam",
    "F:\Steam",
    "G:\Steam",
    "$env:ProgramFiles\Steam",
    "$env:ProgramFiles(x86)\Steam",
    "$env:LOCALAPPDATA\Steam"
)

foreach ($path in $commonPaths) {
    if (Test-Path "$path\steam.exe") {
        $steamPath = $path
        break
    }
}

# 如果仍未找到，尝试从进程定位
if (-not $steamPath) {
    $steamProc = Get-Process -Name "steam" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($steamProc) {
        $steamPath = Split-Path $steamProc.Path -Parent
    }
}
```

### 2. 检测无限暖暖安装位置
基于 Steam 安装路径推导：
```powershell
$steamapps = Join-Path $steamPath "steamapps"
$acfFile = Join-Path $steamapps "appmanifest_3164330.acf"
$gameDir = Join-Path $steamapps "common\Infinity Nikki"
```

验证文件是否存在：
```powershell
Test-Path $acfFile    # ACF 文件必须存在
Test-Path $gameDir    # 游戏目录必须存在
```

### 3. 检测 Steam 版本类型（中国市场 SubPackage）
读取 ACF 文件判断：
```powershell
$acfContent = Get-Content $acfFile -Raw

# 检查是否为中国市场版本特征
$isChinaVersion = $acfContent -match "sub/1221922" -or 
                  $acfContent -match "schinese" -or
                  (Test-Path "$gameDir\InfinityNikki\X6Game\Content\Paks\*China*")

# 检查 InstallScript 路径（中国版特征）
$hasChinaInstallScript = $acfContent -match "InfinityNikki\\X6Game\\installscript"
```

中国市场版本特征：
- ACF 中 `UserConfig.language` = `schinese`
- 存在 `X6Game` 目录结构
- Depot 3164332 描述包含 "China"
- SubPackage: sub/1221922

### 4. 检测非 Steam 版本启动器（用于高级启动设置）

**推荐方法**：直接调用 `infi-manager.ps1` 中的 `Find-StandaloneLauncher` 函数：

```powershell
# 从 infi-manager.ps1 中加载 Find-StandaloneLauncher 函数
$scriptPath = Join-Path $PSScriptRoot "infi-manager.ps1"
$scriptContent = Get-Content $scriptPath -Raw

# 提取函数定义并执行
$pattern = '(function Find-StandaloneLauncher\s*\{[\s\S]*?^\})'
$match = [regex]::Match($scriptContent, $pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)

if ($match.Success) {
    Invoke-Expression $match.Value
    $standaloneLaunchers = Find-StandaloneLauncher
    
    if ($standaloneLaunchers) {
        Write-Host "检测到非 Steam 版本启动器：" -ForegroundColor Cyan
        foreach ($launcher in $standaloneLaunchers) {
            Write-Host "  - $($launcher.Path)" -ForegroundColor White
            if ($launcher.GamePath) {
                Write-Host "    游戏路径: $($launcher.GamePath)" -ForegroundColor Gray
            }
            Write-Host "    来源: $($launcher.Source)" -ForegroundColor Gray
        }
    } else {
        Write-Host "[i] 未检测到非 Steam 版本启动器" -ForegroundColor Gray
    }
} else {
    Write-Host "[WARN] 无法从 infi-manager.ps1 加载启动器检测函数" -ForegroundColor Yellow
}
```

**说明**：
- `Find-StandaloneLauncher` 函数会自动通过三种方法检测：
  1. 注册表卸载信息
  2. 常见目录下的 `config.ini` 文件
  3. 开始菜单快捷方式
- 无需硬编码任何路径，完全自动检测
- 返回对象包含：`Path`（启动器路径）、`GamePath`（游戏路径）、`Source`（检测来源）

---

## 超时处理与网络检测

### 判断访问 SteamDB 超时

在以下步骤中，如果遇到超时或访问失败，应立即执行网络检测：

1. **Chrome 启动超时**：启动 Chrome 后，30 秒内未成功连接到 `http://127.0.0.1:9222/json/version`
2. **页面加载超时**：Chrome 打开 SteamDB 页面后，60 秒内未成功获取页面内容
3. **CDP 通信超时**：通过 CDP 发送命令后，30 秒内未收到响应
4. **Cloudflare 验证超时**：页面显示 "Checking your browser..." 超过 30 秒仍未完成

### 网络检测步骤

**当检测到超时时，必须执行以下网络检测**：

```powershell
# 1. Ping SteamDB 和 Cloudflare 检测网络延迟
Write-Host "[i] 检测到 SteamDB 访问超时，正在执行网络检测..." -ForegroundColor Cyan

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
            Write-Host "[WARN] $TargetName 延迟过高: ${latencyStr} ms" -ForegroundColor Yellow
        } elseif ($avgLatency -gt 200) {
            Write-Host "[i] $TargetName 延迟较高: ${latencyStr} ms" -ForegroundColor Yellow
        } else {
            Write-Host "[OK] $TargetName 延迟正常: ${latencyStr} ms" -ForegroundColor Green
        }
        return $avgLatency
    } else {
        Write-Host "[ERROR] 无法连接到 $TargetName" -ForegroundColor Red
        return $null
    }
}

$steamdbLatency = Show-PingResult -PingResult $steamdbPing -TargetName "SteamDB (steamdb.info)"
$cloudflareLatency = Show-PingResult -PingResult $cloudflarePing -TargetName "Cloudflare (cloudflare.com)"

# 综合判断
if ($null -eq $steamdbLatency -and $null -eq $cloudflareLatency) {
    Write-Host "[ERROR] 两个目标均无法连接，网络可能完全断开" -ForegroundColor Red
    Write-Host "[i] 建议：" -ForegroundColor Cyan
    Write-Host "  1. 检查网络是否正常连接" -ForegroundColor White
    Write-Host "  2. 检查网线/WiFi 是否正常" -ForegroundColor White
    Write-Host "  3. 检查防火墙是否阻止了出站连接" -ForegroundColor White
} elseif ($null -eq $steamdbLatency -and $null -ne $cloudflareLatency) {
    Write-Host "[WARN] Cloudflare 可访问但 SteamDB 不可达" -ForegroundColor Yellow
    Write-Host "[i] 建议：" -ForegroundColor Cyan
    Write-Host "  1. SteamDB 可能被网络运营商/DNS 屏蔽" -ForegroundColor White
    Write-Host "  2. 尝试更换 DNS 服务器（如 8.8.8.8 或 114.114.114.114）" -ForegroundColor White
    Write-Host "  3. 尝试开启/关闭 VPN 后重试" -ForegroundColor White
} elseif ($steamdbLatency -gt 500 -or $cloudflareLatency -gt 500) {
    Write-Host "[WARN] 网络延迟过高，可能影响 SteamDB 和 Cloudflare 访问" -ForegroundColor Yellow
    Write-Host "[i] 建议：" -ForegroundColor Cyan
    Write-Host "  1. 检查网络连接是否稳定" -ForegroundColor White
    Write-Host "  2. 尝试更换网络环境（如切换 WiFi/有线）" -ForegroundColor White
    Write-Host "  3. 检查是否开启了 VPN/代理，可能导致延迟增加" -ForegroundColor White
    Write-Host "  4. 如果使用了代理，请尝试关闭后重试" -ForegroundColor White
}

# 2. 检测 DNS 解析（对两个域名）
Write-Host "[i] 正在检测 DNS 解析..." -ForegroundColor Cyan
foreach ($domain in @("steamdb.info", "cloudflare.com")) {
    try {
        $dnsResult = Resolve-DnsName -Name $domain -ErrorAction Stop
        Write-Host "[OK] $domain DNS 解析成功: $($dnsResult.IPAddress)" -ForegroundColor Green
    } catch {
        Write-Host "[ERROR] $domain DNS 解析失败: $_" -ForegroundColor Red
        if ($domain -eq "steamdb.info") {
            Write-Host "[i] 建议：尝试更换 DNS 服务器（如 8.8.8.8 或 114.114.114.114）" -ForegroundColor Yellow
        }
    }
}

# 3. 检测代理设置
$proxySettings = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" -ErrorAction SilentlyContinue
if ($proxySettings.ProxyEnable -eq 1) {
    Write-Host "[WARN] 系统已启用代理: $($proxySettings.ProxyServer)" -ForegroundColor Yellow
    Write-Host "[i] 代理可能导致 SteamDB 访问缓慢或被拦截，建议临时关闭代理后重试" -ForegroundColor Cyan
}
```

### 超时后的处理建议

完成网络检测后，根据检测结果向用户提供建议：

1. **网络延迟正常（<200ms）**：
   - 可能是 Cloudflare 验证较慢，建议等待更长时间（60-90秒）
   - 或提示用户手动完成 Cloudflare 验证（如果页面显示验证挑战）

2. **网络延迟较高（200-500ms）**：
   - 建议用户耐心等待，或尝试更换网络环境
   - 可以增加超时时间到 90-120 秒

3. **网络延迟过高（>500ms）或无法连接**：
   - 必须提醒用户检查网络设置
   - 建议关闭 VPN/代理后重试
   - 如果多次尝试失败，建议用户手动访问 SteamDB 获取版本信息

4. **DNS 解析失败**：
   - 建议用户更换 DNS 服务器
   - 可以手动指定 DNS 后重试

5. **代理已启用**：
   - 提醒用户代理可能影响访问
   - 建议临时关闭代理后重试

### 重要提醒

> **Agent 必须注意**：
> - 网络检测应在**每次**访问 SteamDB 超时时执行
> - 检测结果显示给用户，并**明确说明**是否需要检查网络设置
> - 如果用户网络环境明显有问题，应**主动建议**用户先修复网络再继续
> - **禁止**在网络明显异常时继续尝试访问 SteamDB（会浪费时间且可能被 IP 封禁）

---

### 步骤 1：检查 Steam 是否运行
```powershell
$steamProcess = Get-Process -Name "steam","steamwebhelper" -ErrorAction SilentlyContinue
if ($steamProcess) {
    Write-Host "[ERROR] Steam 正在运行，请先退出 Steam 再继续" -ForegroundColor Red
    exit 1
}
```

### 步骤 2：读取本地 ACF 文件
```powershell
$acfPath = Join-Path $steamapps "appmanifest_3164330.acf"
$acfContent = Get-Content $acfPath -Raw

# 提取关键字段
$buildID = [regex]::Match($acfContent, '"buildid"\s+"(\d+)"').Groups[1].Value
$manifestGID = [regex]::Match($acfContent, '"manifest"\s+"(\d+)"').Groups[1].Value
$stateFlags = [regex]::Match($acfContent, '"StateFlags"\s+"(\d+)"').Groups[1].Value
$targetBuildID = [regex]::Match($acfContent, '"TargetBuildID"\s+"(\d+)"').Groups[1].Value
$autoUpdate = [regex]::Match($acfContent, '"AutoUpdateBehavior"\s+"(\d+)"').Groups[1].Value
$bytesToDownload = [regex]::Match($acfContent, '"BytesToDownload"\s+"(\d+)"').Groups[1].Value
$bytesToStage = [regex]::Match($acfContent, '"BytesToStage"\s+"(\d+)"').Groups[1].Value
$bytesStaged = [regex]::Match($acfContent, '"BytesStaged"\s+"(\d+)"').Groups[1].Value
```

### 步骤 2.5：检查残留文件
```powershell
# 查找所有与 3164330 相关的残留文件
$relatedFiles = Get-ChildItem -Path $steamapps -Filter "*3164330*" -Recurse -ErrorAction SilentlyContinue
if ($relatedFiles) {
    Write-Host "[WARN] 发现以下 3164330 相关残留文件：" -ForegroundColor Yellow
    foreach ($f in $relatedFiles) {
        Write-Host "  $($f.FullName) ($($f.Length) bytes)"
    }
}

# 检查 downloading 目录
$dlDir = Join-Path $steamapps "downloading\3164330"
if (Test-Path $dlDir) {
    Write-Host "[WARN] 发现 downloading 残留目录: $dlDir" -ForegroundColor Yellow
}

# 检查 temp 目录
$tempDir = Join-Path $steamapps "temp\3164330"
if (Test-Path $tempDir) {
    Write-Host "[WARN] 发现 temp 残留目录: $tempDir" -ForegroundColor Yellow
}
```

### 步骤 3：创建/使用独立 Chrome 用户目录

在 infi 目录下创建专用 Chrome 用户目录：
```powershell
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$chromeProfileDir = Join-Path $scriptDir "chrome-profile-steamdb"

# 创建目录（如果不存在）
if (-not (Test-Path $chromeProfileDir)) {
    New-Item -ItemType Directory -Path $chromeProfileDir -Force | Out-Null
    Write-Host "[i] 已创建 SteamDB 专用 Chrome 用户目录: $chromeProfileDir" -ForegroundColor Cyan
}
```

### 步骤 4：启动 Chrome 并访问 SteamDB
```powershell
$chromeExe = "C:\Program Files\Google\Chrome\Application\chrome.exe"
if (-not (Test-Path $chromeExe)) {
    $chromeExe = "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
}

# 启动 Chrome（独立用户目录 + 远程调试）
Start-Process -FilePath $chromeExe -ArgumentList @(
    "--remote-debugging-port=9222",
    "--user-data-dir=$chromeProfileDir",
    "--no-first-run",
    "--no-default-browser-check",
    "https://steamdb.info/app/3164330/depots/"
) -WindowStyle Normal

Write-Host "[i] 等待 Chrome 加载 SteamDB 页面（5秒）..." -ForegroundColor Cyan
Start-Sleep -Seconds 5
```

### 步骤 5：通过 CDP 获取 SteamDB 数据
```python
import asyncio
import websockets
import json
import urllib.request
import sys
import os

async def get_steamdb_data():
    script_dir = os.path.dirname(os.path.abspath(__file__)) if '__file__' in dir() else os.getcwd()
    # 获取 Chrome 页面列表
    with urllib.request.urlopen('http://127.0.0.1:9222/json/list') as response:
        pages = json.loads(response.read())
        page_id = pages[0]['id']
    
    uri = f'ws://127.0.0.1:9222/devtools/page/{page_id}'
    
    async with websockets.connect(uri) as ws:
        # 获取 Depots 页面内容
        await ws.send(json.dumps({
            'id': 1,
            'method': 'Runtime.evaluate',
            'params': {'expression': 'document.body.innerText'}
        }))
        resp = await ws.recv()
        data = json.loads(resp)
        depots_text = data['result']['result']['value']
        
        # 保存到脚本所在目录（遵守路径约束）
        with open(os.path.join(script_dir, 'steamdb_depots.txt'), 'w', encoding='utf-8') as f:
            f.write(depots_text)
        
        # 导航到 Manifests 页面
        await ws.send(json.dumps({
            'id': 2,
            'method': 'Page.navigate',
            'params': {'url': 'https://steamdb.info/depot/3164332/manifests/'}
        }))
        await ws.recv()
        await asyncio.sleep(3)
        
        # 获取 Manifests 页面内容
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

asyncio.run(get_steamdb_data())
```

### 步骤 6：解析 SteamDB 数据
从页面文本中提取版本信息：

**Depots 页面提取 BuildID：**
```powershell
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$depotsText = Get-Content (Join-Path $scriptDir "steamdb_depots.txt") -Raw
$steamdbBuildID = [regex]::Match($depotsText, 'public\s+(\d+)').Groups[1].Value
```

**Manifests 页面提取 Manifest GID：**
```powershell
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifestsText = Get-Content (Join-Path $scriptDir "steamdb_manifests.txt") -Raw
# 提取第一行的 ManifestID（最新的）
$steamdbManifest = [regex]::Match($manifestsText, '(\d{19})').Groups[1].Value
```

### 步骤 7：版本对比与决策
```powershell
Write-Host ""
Write-Host "========================================" -ForegroundColor White
Write-Host "  版本对比结果" -ForegroundColor White
Write-Host "========================================" -ForegroundColor White
Write-Host ""
Write-Host "  Build ID:" -NoNewline
Write-Host "  SteamDB: $steamdbBuildID" -NoNewline -ForegroundColor Cyan
Write-Host "  本地: $buildID" -ForegroundColor Yellow
Write-Host "  Manifest GID:" -NoNewline
Write-Host "  SteamDB: $steamdbManifest" -NoNewline -ForegroundColor Cyan
Write-Host "  本地: $manifestGID" -ForegroundColor Yellow
Write-Host ""

if ($steamdbBuildID -eq $buildID -and $steamdbManifest -eq $manifestGID) {
    Write-Host "  [OK] 版本已是最新，无需更新" -ForegroundColor Green
    $needsUpdate = $false
} else {
    Write-Host "  [!] 发现新版本，需要更新 ACF" -ForegroundColor Yellow
    $needsUpdate = $true
}
```

### 步骤 8：更新 ACF（仅当需要时）
```powershell
if ($needsUpdate) {
    # 解除只读
    Set-ItemProperty $acfPath -Name IsReadOnly -Value $false
    
    # 更新字段
    $acfContent = $acfContent -replace '"buildid"\s+"\d+"', "`"buildid`"        `"$steamdbBuildID`""
    $acfContent = $acfContent -replace '"manifest"\s+"\d+"', "`"manifest`"        `"$steamdbManifest`""
    $acfContent = $acfContent -replace '"StateFlags"\s+"\d+"', '"StateFlags"        "4"'
    $acfContent = $acfContent -replace '"TargetBuildID"\s+"\d+"', '"TargetBuildID"        "0"'
    $acfContent = $acfContent -replace '"AutoUpdateBehavior"\s+"\d+"', '"AutoUpdateBehavior"        "1"'
    $acfContent = $acfContent -replace '"BytesToDownload"\s+"\d+"', '"BytesToDownload"        "0"'
    $acfContent = $acfContent -replace '"BytesDownloaded"\s+"\d+"', '"BytesDownloaded"        "0"'
    $acfContent = $acfContent -replace '"BytesToStage"\s+"\d+"', '"BytesToStage"        "0"'
    $acfContent = $acfContent -replace '"BytesStaged"\s+"\d+"', '"BytesStaged"        "0"'
    
    # 写回文件
    $acfContent | Set-Content $acfPath -Encoding UTF8 -NoNewline
    
    # 重新锁定只读
    Set-ItemProperty $acfPath -Name IsReadOnly -Value $true
    
    Write-Host "  [OK] ACF 已更新并锁定" -ForegroundColor Green
}
```

### 步骤 9：运行验证
```powershell
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$managerScript = Join-Path $scriptDir "infi-manager.ps1"

if (Test-Path $managerScript) {
    powershell -NoProfile -ExecutionPolicy Bypass -File $managerScript verify
} else {
    # 内置验证逻辑
    Write-Host ""
    Write-Host "========================================" -ForegroundColor White
    Write-Host "  手动验证" -ForegroundColor White
    Write-Host "========================================" -ForegroundColor White
    
    $checks = @(
        @{Name="ACF 存在"; Test={Test-Path $acfPath}},
        @{Name="StateFlags=4"; Test={$acfContent -match '"StateFlags"\s+"4"'}},
        @{Name="TargetBuildID=0"; Test={$acfContent -match '"TargetBuildID"\s+"0"'}},
        @{Name="AutoUpdateBehavior=1"; Test={$acfContent -match '"AutoUpdateBehavior"\s+"1"'}},
        @{Name="ACF 只读"; Test={(Get-ItemProperty $acfPath).IsReadOnly}},
        @{Name="游戏目录存在"; Test={Test-Path $gameDir}},
        @{Name="启动器存在"; Test={Test-Path "$gameDir\launcher.exe"}}
    )
    
    foreach ($check in $checks) {
        $result = & $check.Test
        if ($result) {
            Write-Host "  [OK] $($check.Name)" -ForegroundColor Green
        } else {
            Write-Host "  [X] $($check.Name)" -ForegroundColor Red
        }
    }
}
```

### 步骤 10：关闭 Chrome 窗口
```powershell
# 仅关闭本流程打开的 SteamDB 专用 Chrome 实例，不影响机主其他 Chrome 窗口
# 通过命令行参数精确匹配：remote-debugging-port=9222 且 chrome-profile-steamdb
$targetChromes = Get-Process -Name "chrome" -ErrorAction SilentlyContinue | Where-Object {
    $cmd = (Get-CimInstance Win32_Process -Filter "ProcessId = $($_.Id)").CommandLine
    $cmd -match "remote-debugging-port=9222" -and $cmd -match "chrome-profile-steamdb"
}

if ($targetChromes) {
    foreach ($p in $targetChromes) {
        Stop-Process -Id $p.Id -Force
        Write-Host "  已关闭 Chrome 进程 (PID: $($p.Id))" -ForegroundColor Gray
    }
    Write-Host "  [OK] SteamDB 专用 Chrome 已关闭" -ForegroundColor Green
} else {
    Write-Host "  [i] 未找到需要关闭的 Chrome 实例" -ForegroundColor Gray
}
```

### 步骤 10.5：询问是否清理 Steam 端游戏本体（X6Game）

流程全部完成后，向用户询问是否需要清理 Steam 端的游戏本体数据以释放磁盘空间。

**特别说明**：此处清理的是 Steam 版本的游戏数据，不是非 Steam 版本（如国服独立启动器版本）。必须严格区分，防止误删。

**执行逻辑**：

1. 基于已检测到的 Steam 游戏安装路径（`$gameDir`，即 `common\Infinity Nikki`），进入下一层 `InfinityNikki` 子目录：
```powershell
$steamGameCore = Join-Path $gameDir "InfinityNikki"
```

2. **二次路径校验（必须执行）**：在清理前必须验证该路径确为 Steam 版本游戏目录，而非其他版本（如国服非 Steam 版本）：
```powershell
# 验证路径特征：必须同时满足以下条件才确认为 Steam 版本
$isSteamVersion = $true

# 条件 1：路径必须包含 "\steamapps\common\Infinity Nikki"
if ($steamGameCore -notmatch "steamapps\\common\\Infinity Nikki") {
    $isSteamVersion = $false
}

# 条件 2：该目录下必须存在 Steam 版本特征文件（如 steam_appid.txt 内容为 3164330）
$steamAppIdFile = Join-Path $gameDir "steam_appid.txt"
if (-not (Test-Path $steamAppIdFile) -or (Get-Content $steamAppIdFile -Raw).Trim() -ne "3164330") {
    $isSteamVersion = $false
}

# 条件 3：与已知的非 Steam 版本路径不同（如果找到了非 Steam 启动器）
$standaloneLaunchers = Find-StandaloneLauncher  # 复用 infi-manager.ps1 的函数
if ($standaloneLaunchers) {
    foreach ($l in $standaloneLaunchers) {
        if ($l.GamePath) {
            $standaloneNorm = [System.IO.Path]::GetFullPath($l.GamePath).TrimEnd('\')
            $steamNorm = [System.IO.Path]::GetFullPath($steamGameCore).TrimEnd('\')
            if ($standaloneNorm -eq $steamNorm) {
                $isSteamVersion = $false
                Write-Host "[WARN] 路径与非 Steam 版本重合，拒绝清理: $steamGameCore" -ForegroundColor Red
            }
        }
    }
}
```

3. 检查 X6Game 文件夹是否存在并计算大小：
```powershell
$x6gamePath = Join-Path $steamGameCore "X6Game"
if (Test-Path $x6gamePath) {
    $x6gameSize = (Get-ChildItem $x6gamePath -Recurse -File | Measure-Object -Property Length -Sum).Sum
    $x6gameSizeGB = [math]::Round($x6gameSize / 1GB, 2)
    Write-Host "X6Game 文件夹大小: ${x6gameSizeGB}GB" -ForegroundColor Cyan
}
```

4. **向用户确认（必须）**：在清理前展示以下信息并要求用户明确确认：
   - Steam 游戏路径
   - X6Game 文件夹完整路径
   - X6Game 文件夹大小
   - 明确告知：仅删除 X6Game 核心游戏数据，保留启动器、配置文件等
   - 二次确认路径是否为 Steam 版本

5. 仅在用户明确确认后执行删除：
```powershell
# 将 X6Game 移至回收站
Remove-Item $x6gamePath -Recurse -Force
Write-Host "[OK] X6Game 已删除，释放约 ${x6gameSizeGB}GB" -ForegroundColor Green
```

**注意**：
- 只删除 X6Game 文件夹，保留 InfinityNikki 目录下的其他文件（启动器、配置文件等）
- 清理后游戏将无法通过 Steam 启动游玩，需重新下载才能恢复
- 若用户表示需保留游戏数据则跳过此步骤
- 不要对非 Steam 版本的 X6Game 执行清理

---

> **Agent 执行提醒**：在流程全部完成后，必须主动向用户询问是否清理本次生成的 Chrome 用户文件夹（`chrome-profile-steamdb/`）。该文件夹包含 SteamDB 的缓存和 Cookie，若用户不再需要或出于隐私考虑，应将其删除。用 `ask_user` 或自然语言提示即可，格式示例：「本次检测生成的 Chrome 临时用户文件夹需要清理吗？」

---

## 非 Steam 版本启动器检测与配置建议

### 检测逻辑

**推荐方法**：直接调用 `infi-manager.ps1` 中已定义的 `Find-StandaloneLauncher` 函数。

```powershell
# 从 infi-manager.ps1 动态加载函数（无需硬编码路径）
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$psScript = Join-Path $scriptDir "infi-manager.ps1"

if (Test-Path $psScript) {
    # 提取函数并加载
    $content = Get-Content $psScript -Raw
    $pattern = '(function Find-StandaloneLauncher\s*\{[\s\S]*?^\})'
    $match = [regex]::Match($content, $pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    
    if ($match.Success) {
        Invoke-Expression $match.Value
        $launchers = Find-StandaloneLauncher
        
        if ($launchers) {
            Write-Host "检测到 $($launchers.Count) 个启动器" -ForegroundColor Green
            foreach ($l in $launchers) {
                Write-Host "  Path: $($l.Path)"
                if ($l.GamePath) { Write-Host "  Game: $($l.GamePath)" }
                Write-Host "  Source: $($l.Source)"
            }
        }
    }
}
```

**说明**：
- 此函数已在 `infi-manager.ps1` 中定义，无需重复定义
- 自动检测，无硬编码路径
- 检测来源：`Registry`（注册表）、`Config`（配置文件）、`Shortcut`（快捷方式）

### 配置建议
如果检测到非 Steam 版本启动器，建议配置 Steam 高级启动选项：

```powershell
$launchers = Find-StandaloneLauncher
if ($launchers.Count -gt 0) {
    $primaryLauncher = $launchers[0]
    Write-Host ""
    Write-Host "========================================" -ForegroundColor White
    Write-Host "  检测到非 Steam 版本启动器" -ForegroundColor White
    Write-Host "========================================" -ForegroundColor White
    Write-Host "  路径: $($primaryLauncher.Path)" -ForegroundColor Cyan
    if ($primaryLauncher.GamePath) {
        Write-Host "  游戏路径: $($primaryLauncher.GamePath)" -ForegroundColor Cyan
    }
    Write-Host ""
    Write-Host "  建议配置 Steam 高级启动选项：" -ForegroundColor Yellow
    Write-Host "  1. 在 Steam 库中右键点击'无限暖暖'" -ForegroundColor White
    Write-Host "  2. 选择'属性' -> '通用'" -ForegroundColor White
    Write-Host "  3. 在'启动选项'中输入：" -ForegroundColor White
    Write-Host "     `"$($primaryLauncher.Path)`" %command%" -ForegroundColor Green
    Write-Host ""
    Write-Host "  这样可以通过 Steam 启动非 Steam 版本，同时保持 Steam 覆盖层和成就系统。" -ForegroundColor Gray
}
```

### Steam 启动选项配置
对于中国市场版本（SubPackage），建议的启动选项（以下路径为示例，实际以 `$gameDir` 和检测到的启动器路径为准）：

```
# 标准配置（使用 Steam 目录下的启动器）
"<Steam游戏目录>\launcher.exe" %command%

# 如果 launcher.exe 无法启动，尝试直接启动游戏
"<Steam游戏目录>\InfinityNikki\InfinityNikki.exe" %command%

# 如果需要指定工作目录
"<Steam游戏目录>\InfinityNikki\InfinityNikki.exe" -workingdir="<Steam游戏目录>\InfinityNikki" %command%

# 非 Steam 版本启动器配置（路径由 Find-StandaloneLauncher 检测得出）
"<非Steam启动器路径>\launcher.exe" %command%
```

---

## 异常处理

### SteamDB 访问失败
```powershell
# 检查 Chrome 是否启动
$chromeDebug = Invoke-RestMethod -Uri "http://127.0.0.1:9222/json/version" -ErrorAction SilentlyContinue
if (-not $chromeDebug) {
    Write-Host "[ERROR] Chrome 远程调试未启动，尝试重新启动..." -ForegroundColor Red
    # 重新启动 Chrome
    # ...（重复步骤 4）
}

# 检查是否被封禁
$depotsText = Get-Content (Join-Path $scriptDir "steamdb_depots.txt") -Raw
if ($depotsText -match "banned") {
    Write-Host "[ERROR] IP 被 SteamDB 封禁，请等待 1 小时后重试，或更换网络环境" -ForegroundColor Red
    exit 1
}
```

### ACF 更新后 Steam 仍提示更新
1. 检查 `StateFlags` 是否为 `4`
2. 检查 `TargetBuildID` 是否为 `0`
3. 确认 ACF 文件确实为只读属性
4. 重新运行骨架化清理

### 找不到 Steam 安装
1. 手动指定 Steam 路径：
```powershell
$steamPath = Read-Host "请输入 Steam 安装路径（如 C:\Program Files (x86)\Steam）"
```

---

## 补充排查经验（2026-06-24）

### 根因：BytesToStage / BytesStaged 非零导致误判更新

即使 ACF 中 buildid、manifest、StateFlags、TargetBuildID、AutoUpdateBehavior 全部正确且版本匹配，Steam 仍可能提示需要更新。深层原因是 ACF 中可能存在 `BytesToStage` 和 `BytesStaged` 字段，这些字段在正常已安装完成游戏的 ACF 中不应存在。Steam 检测到非零暂存数据后会判定游戏需要完成更新流程。

**排查步骤**：
1. 读取 ACF 全部内容，不仅检查常规字段
2. 特别关注任何 download / stage / staged 相关字段（BytesToDownload、BytesDownloaded、BytesToStage、BytesStaged、StagingSize、StagingFolder 等）
3. 所有此类字段必须清零或确保不存在

### 残留文件清理

更新操作后可能在 steamapps 目录下留下残留文件，也会干扰 Steam 判断：
- `appmanifest_3164330.acf.*.tmp` — ACF 临时文件，需删除
- `appmanifest_3164330.acf.bak.*` — 备份文件，需删除
- `steamapps\downloading\3164330` — 下载临时目录，为空则删除
- `steamapps\temp\3164330` — 临时目录，为空则删除

排查命令：
```powershell
Get-ChildItem -Path "$steamapps" -Filter "*3164330*" -Recurse | Select FullName, Length
Get-ChildItem -Path "$steamapps\downloading" -ErrorAction SilentlyContinue
```

### 正常游戏 ACF 字段参考

对比正常已安装游戏的 ACF（如 McPixel 3），无限暖暖 ACF 中不应存在的字段包括：
- UpdateResult
- BytesToDownload
- BytesDownloaded
- BytesToStage
- BytesStaged
- TargetBuildID（应设为 0，不应有非零值）

这些字段均属于下载/更新暂存数据，安装完成后应清零或移除。

---

## 输出格式

执行完成后，输出以下信息：

```
========================================
  无限暖暖 Steam 版本状态检测报告
========================================

Steam 安装路径: [路径]
游戏安装路径: [路径]
版本类型: [中国市场版 / 国际版]

--- 版本对比 ---
Build ID:    SteamDB: [值]    本地: [值]    [✅/❌]
Manifest ID: SteamDB: [值]    本地: [值]    [✅/❌]

--- ACF 配置状态 ---
StateFlags: [值] [✅/❌]
TargetBuildID: [值] [✅/❌]
AutoUpdateBehavior: [值] [✅/❌]
ACF 只读锁定: [是/否] [✅/❌]

--- 验证结果 ---
[OK] 项目1
[OK] 项目2
...

--- 非 Steam 启动器检测 ---
[找到/未找到] 路径: [路径]
建议启动选项: [命令]

最终结论: [版本已最新 / 已更新 / 需要手动处理]
```

> **Agent 恢复指引**：检测报告输出完毕后，Agent 应向用户说明如何恢复到更新前的原始状态（用户可能需要回退）：
> 1. 由 AI Agent 解除 ACF 只读，将 buildid、manifest、StateFlags、AutoUpdateBehavior、TargetBuildID 等字段恢复为原始值
> 2. 重启 Steam 客户端
> 3. 若重启后 Steam 库中未显示「待更新」，手动前往 **游戏库 → 右键游戏 → 属性 → 已安装文件 → 验证游戏文件的完整性** 触发 Steam 重新识别版本

---

## 文件清单

本流程涉及的文件（相对 infi 目录）：
- `config.json` - 配置文件（自动检测路径）
- `infi-manager.ps1` - 管理脚本（含 `Find-StandaloneLauncher` 函数，供步骤 4 和步骤 10.5 复用）
- `chrome-profile-steamdb/` - SteamDB 专用 Chrome 用户目录（自动创建，步骤 10 后询问是否清理）
- `steamdb-check-prompt.md` - 本说明文档

### 流程步骤概览
1. 检查 Steam 是否运行
2. 读取本地 ACF 文件（含 BytesToStage/BytesStaged 等暂存字段提取）
3. 检查残留文件（tmp/bak/downloading/temp 目录）
4. 创建独立 Chrome 用户目录
5. 启动 Chrome 访问 SteamDB
6. 通过 CDP 获取 SteamDB 数据
7. 解析 SteamDB 数据
8. 版本对比与决策
9. 更新 ACF（含暂存字段清零）
10. 运行验证
11. 关闭 Chrome 窗口
12. **步骤 10.5**：询问是否清理 Steam 端游戏本体（X6Game）—— 二次路径校验后删除 Steam 版本 X6Game 文件夹，释放磁盘空间
13. 询问清理 chrome-profile-steamdb 用户目录
