# 无限暖暖 Steam 版本状态检测与修复流程

> **📢 重要通知（必须向用户显示）**：在开始执行任何操作之前，Agent **必须**首先向用户输出以下通知内容。其中 `%游戏厂商名%` 替换为该游戏的开发商/发行商名称（如无限暖暖 → Papergames / Infold Games，绝区零 → HoYoVerse / miHoYo 等）。如果用户系统语言/界面语言**不是简体中文**，Agent 必须自动将通知翻译为用户使用的语言后再输出。
>
> **通知原文（简体中文）**：
> ©mocabolka 2026. 本工具与Valve/Steam、SteamDB 及 %游戏厂商名% 无关。仅供学习交流使用，使用时请注意个人数据安全。
>
> **English translation (for non-Simplified Chinese users)**：
> ©mocabolka 2026. This tool is not affiliated with Valve/Steam, SteamDB, or %游戏厂商名%. For learning and exchange purposes only. Please be mindful of personal data security.

> **⚠️ 路径约束**：本流程执行过程中创建的任何临时目录/文件（Chrome 用户目录、SteamDB 缓存等）**必须**在当前工作目录下创建，**如非必要（如中文目录报错等）严禁**写入系统临时目录（`$env:TEMP` 等）或其他任意路径。流程结束后**必须自动清理**以下临时文件：`steamdb_depots.txt`、`steamdb_manifests.txt`、`steamdb_config.txt`、`backups/` 目录中本次生成的 ACF 备份、`chrome-profile-steamdb/` 目录。

> **Agent 前置提醒**：在开始本流程之前，无论目标游戏是否有"更新"选项，都必须提示用户先在 Steam 客户端内完成以下设置，否则 ACF 锁定后 Steam 仍可能在启动时自动触发更新检查：
> **游戏库 → 右键游戏 → 属性 → 更新 → 自动更新 → 设为「等到我启动游戏时」**

## 任务目标

本流程按优先级服务三类游戏：
1. **无限暖暖**（Infinity Nikki AppID: 3164330）— 主目标，完整功能覆盖
2. **绝区零**（Zenless Zone Zero AppID: 4162040）— 次优先，专属 Executable 处理和启动方案
3. **其他 Steam 游戏** — 通用兼容，基础功能可用

默认情况下（用户未指定游戏），Agent 按**无限暖暖**处理。

---

## 第一步：确定目标游戏

1. 如果用户**没有指定**具体游戏 → Agent 默认按**无限暖暖**处理。
2. 如果用户指明**绝区零** → 切换到绝区零参数（AppID 4162040），启用绝区零专属处理。
3. 如果用户指明**其他游戏**：
   - Agent 先确认用户已知该游戏的 AppID 或 SteamDB 链接。
   - 如果用户无法提供，Agent 可尝试在 SteamDB 搜索。
   - 切换到该游戏的通用参数后，继续执行后续流程。

### 防呆设计 — 产权归属提示

> **仅在目标游戏不是无限暖暖时执行**。如果目标游戏是无限暖暖，跳过此提示。
>
> Agent **必须**向用户明确告知：
> 1. **您必须在 Steam 平台拥有该游戏的所有权**（即该游戏已入库），才能对其配置文件进行修改实现高级启动。
> 2. 例如《绝区零》（Zenless Zone Zero）：由于其全球发行商（HoYoVerse）与国服版本发行商（miHoYo）不一致，该游戏**并未在 Steam 中国内地市场上架**。用户需通过其他区域 Steam 商店获取后方可操作。
> 3. Agent **不提供**获取游戏副本的服务，仅对已拥有的游戏进行配置管理。

---

## 第二步：检查游戏下载状态

在执行任何操作前，检查游戏是否已下载到本地。

### 2.1 检测逻辑

1. 定位 Steam 安装路径（见后文"Steam 安装位置检测"）
2. 查找游戏库目录，定位 `appmanifest_{AppID}.acf` 文件
3. 读取 ACF 判断下载状态：

```powershell
$acfContent = Get-Content $acfPath -Raw -Encoding UTF8

# 尚未开始下载
$notDownloaded = $acfContent -match '"buildid"\s+"0"' -and ($acfContent -notmatch 'InstalledDepots' -or $acfContent -match 'InstalledDepots\s*\{\s*\}')

# 正在下载中
$downloading = Test-Path (Join-Path $steamappsDir "downloading\$AppID")

# 已完整下载
$downloaded = ($acfContent -match '"StateFlags"\s+"4"') -and ($acfContent -notmatch '"buildid"\s+"0"')
```

### 2.2 未下载分支

如果游戏尚未开始下载：
1. **提示用户**：游戏尚未下载到本地，请在 Steam 中创建下载进程。
2. **等待用户操作**：用户在 Steam 中点「安装」→ 开始下载后，提示用户**暂停下载**（不要取消）。
3. **确认 ACF 处于下载中断状态**：`buildid` = `0`，`InstalledDepots` 为空。
4. 确认后**关闭 Steam**（`steam.exe -shutdown`），进入下一步。

### 2.3 已下载分支

如果游戏已完整下载到本地：
- 直接关闭 Steam，进入下一步。

---

## 第三步：Steam 安装位置检测

### SteamDB 反爬机制说明

SteamDB 有严格的反爬机制：
- **IP 封禁**：频繁请求或来自数据中心的 IP 会被封禁，返回 "You have been banned"
- **User-Agent 检测**：非浏览器 User-Agent 会被拦截
- **Cookie/Session 验证**：需要真实浏览器会话
- **Rate Limiting**：单 IP 短时间内多次访问会触发限制

**解决方案**：使用独立 Chrome 用户目录，模拟真实用户浏览行为，通过 Chrome DevTools Protocol (CDP) 获取页面内容。

### Chrome 行为规范

**✅ 正确做法**：
- 优先使用系统安装的 Chrome（检查 `C:\Program Files\Google\Chrome\Application\chrome.exe` 等路径）
- 必须使用 `--remote-debugging-port=9222` 和 `--user-data-dir` 参数启动
- 必须等待 Chrome 完全加载并打开远程调试端口后，才能通过 CDP 获取数据

**❌ 禁止行为**：
- **禁止**直接使用 `curl`、`Invoke-WebRequest`、`requests`、`fetch` 等网络工具访问 SteamDB
- **禁止**使用无头浏览器模式（headless mode）访问 SteamDB
- **禁止**在页面未完全加载时尝试获取数据
- **禁止**跳过 Cloudflare 验证步骤

### 定位 Steam 安装路径

```powershell
# 注册表检测
$steamPath = (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -ErrorAction SilentlyContinue).InstallPath
if (-not $steamPath) {
    $steamPath = (Get-ItemProperty "HKCU:\SOFTWARE\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
}

# 常见路径
$commonPaths = @(
    "C:\Program Files (x86)\Steam", "C:\Program Files\Steam",
    "D:\Steam", "D:\Entertainment\Steam",
    "$env:ProgramFiles\Steam", "$env:ProgramFiles(x86)\Steam", "$env:LOCALAPPDATA\Steam"
)
if (-not $steamPath) {
    foreach ($p in $commonPaths) { if (Test-Path "$p\steam.exe") { $steamPath = $p; break } }
}

# 进程兜底
if (-not $steamPath) {
    $p = Get-Process steam -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) { $steamPath = Split-Path $p.Path }
}
```

### 查找游戏库

```powershell
$libraryFoldersFile = Join-Path $steamPath "steamapps\libraryfolders.vdf"
$libraries = @($steamPath)
if (Test-Path $libraryFoldersFile) {
    $vdfContent = Get-Content $libraryFoldersFile -Raw
    [regex]::Matches($vdfContent, '"path"\s+"([^"]+)"') | ForEach-Object {
        if ($_.Groups[1].Value -notin $libraries) { $libraries += $_.Groups[1].Value }
    }
}

$acfPath = $null; $gameDir = $null; $gameName = $null
foreach ($lib in $libraries) {
    $acf = Join-Path $lib "steamapps\appmanifest_$AppID.acf"
    if (Test-Path $acf) {
        $acfPath = $acf
        # 从 ACF 中读取 installdir 获取实际游戏目录名称
        $acfRaw = Get-Content $acf -Raw -Encoding UTF8
        $gameName = [regex]::Match($acfRaw, '"installdir"\s+"([^"]+)"').Groups[1].Value
        if (-not $gameName) { $gameName = "Infinity Nikki" } # 兜底
        $gameDir = Join-Path $lib "steamapps\common\$gameName"
        break
    }
}
```

---

## 第四步：读取本地 ACF

读取 ACF 文件，提取以下关键字段：

| 字段 | 期望值 | 说明 |
|------|--------|------|
| `buildid` | > 0 | 当前构建版本号 |
| `manifest` | 19 位数字 | Depot manifest GID |
| `StateFlags` | 4 | 已安装就绪 |
| `TargetBuildID` | 0 | 无待更新目标 |
| `AutoUpdateBehavior` | 1 | 等到启动时更新 |
| `BytesToDownload` / `BytesDownloaded` | 0 | 清零 |
| `BytesToStage` / `BytesStaged` | 0 | 清零（关键！否则仍提示更新） |

### 版本类型检测（仅无限暖暖）

无限暖暖区分中国内地市场版和国际版，其他游戏跳过此步骤：

```powershell
$isChina = $acfContent -match '"sub/1221922"' -or $acfContent -match 'schinese'
if (-not $isChina) {
    $paks = "$gameDir\InfinityNikki\X6Game\Content\Paks"
    $isChina = (Test-Path $paks) -and (Get-ChildItem $paks -Filter "*China*").Count -gt 0
}
```

---

## 第五步：获取 SteamDB 数据

### 5.1 必需访问的页面

| 页面 | 用途 | URL |
|------|------|-----|
| **Depots** | 获取最新 BuildID | `https://steamdb.info/app/{AppID}/depots/` |
| **Manifests** | 获取 Manifest GID | `https://steamdb.info/depot/{DepotID}/manifests/` |
| **Config** | 获取 Executable | `https://steamdb.info/app/{AppID}/config/` |

#### 已知游戏的直接链接

| 游戏 | Depots | Manifests | Config |
|------|--------|-----------|--------|
| 无限暖暖 (3164330) | [depots](https://steamdb.info/app/3164330/depots/) | [depot/3164332](https://steamdb.info/depot/3164332/manifests/) | [config](https://steamdb.info/app/3164330/config/) |
| 绝区零 (4162040) | [depots](https://steamdb.info/app/4162040/depots/) | 动态获取 | [config](https://steamdb.info/app/4162040/config/) |

#### 其他游戏

Agent 先向用户索要 SteamDB 链接或 AppID。如果用户无法提供，Agent 可尝试在 SteamDB 官网搜索。

### 5.2 启动 Chrome

```powershell
$chromeExe = "C:\Program Files\Google\Chrome\Application\chrome.exe"
if (-not (Test-Path $chromeExe)) { $chromeExe = "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe" }
if (-not (Test-Path $chromeExe)) { Write-Error "Chrome not found"; return }

$chromeProfileDir = Join-Path $PSScriptRoot "chrome-profile-steamdb"
if (-not (Test-Path $chromeProfileDir)) { New-Item -ItemType Directory -Path $chromeProfileDir -Force | Out-Null }

Start-Process $chromeExe -ArgumentList @(
    "--remote-debugging-port=9222", "--user-data-dir=`"$chromeProfileDir`"",
    "--no-first-run", "--no-default-browser-check",
    "https://steamdb.info/app/$AppID/depots/"
) -WindowStyle Normal

# 等待 CDP 就绪（最长 30 秒）
$connected = $false
for ($i = 0; $i -lt 30; $i++) {
    try { $r = Invoke-RestMethod "http://127.0.0.1:9222/json/version" -ErrorAction Stop; $connected = $true; break }
    catch { Start-Sleep 1 }
}
if (-not $connected) { Test-NetworkConnectivity; return }
Start-Sleep 3
```

### 5.3 Cloudflare 验证

检测到 `Checking your browser...` / `请稍候` 等过渡页时，自动等待验证完成（最长 120 秒，每 2 秒检测一次）。验证完成前禁止解析内容。

### 5.4 通过 CDP 获取数据

```powershell
# 使用 Python 脚本通过 WebSocket 连接 Chrome CDP
$pythonScript = @"
import asyncio, websockets, json, urllib.request, os

async def get_data(dir):
    with urllib.request.urlopen('http://127.0.0.1:9222/json/list') as r:
        pages = json.loads(r.read())
        p = [x for x in pages if 'steamdb' in x['url']] or pages
        pid = p[0]['id']
    async with websockets.connect(f'ws://127.0.0.1:9222/devtools/page/{pid}') as ws:
        # Depots
        await ws.send(json.dumps({'id':1,'method':'Runtime.evaluate',
            'params':{'expression':'document.body.innerText'}}))
        d = json.loads(await ws.recv())
        open(os.path.join(dir,'steamdb_depots.txt'),'w').write(d['result']['result']['value'])
        # Manifests
        await ws.send(json.dumps({'id':2,'method':'Page.navigate',
            'params':{'url':'https://steamdb.info/depot/{DepotID}/manifests/'}}))
        await ws.recv(); await asyncio.sleep(4)
        await ws.send(json.dumps({'id':3,'method':'Runtime.evaluate',
            'params':{'expression':'document.body.innerText'}}))
        d = json.loads(await ws.recv())
        open(os.path.join(dir,'steamdb_manifests.txt'),'w').write(d['result']['result']['value'])
        # Config
        await ws.send(json.dumps({'id':4,'method':'Page.navigate',
            'params':{'url':'https://steamdb.info/app/{AppID}/config/'}}))
        await ws.recv(); await asyncio.sleep(4)
        await ws.send(json.dumps({'id':5,'method':'Runtime.evaluate',
            'params':{'expression':'document.body.innerText'}}))
        d = json.loads(await ws.recv())
        open(os.path.join(dir,'steamdb_config.txt'),'w').write(d['result']['result']['value'])
    print('OK')
asyncio.run(get_data(r'$ScriptDir'))
"@
$pythonScript | Set-Content (Join-Path $ScriptDir "infi_steamdb_fetch.py") -Encoding UTF8
python (Join-Path $ScriptDir "infi_steamdb_fetch.py") 2>&1
```

### 5.5 解析数据

```powershell
$depotsText = Get-Content (Join-Path $ScriptDir "steamdb_depots.txt") -Raw
$manifestsText = Get-Content (Join-Path $ScriptDir "steamdb_manifests.txt") -Raw
$configText = Get-Content (Join-Path $ScriptDir "steamdb_config.txt") -Raw

# BuildID
$steamdbBuildID = [regex]::Match($depotsText, 'public\s+(\d+)').Groups[1].Value
# Manifest GID
$steamdbManifest = [regex]::Match($manifestsText, '\d{19}').Groups[1].Value
# Executable（从 Config 页面的 "executable" 或 "Executable" 字段提取）
$executable = [regex]::Match($configText, '"executable"\s+"([^"]+)"').Groups[1].Value
if (-not $executable) {
    $executable = [regex]::Match($configText, '"Executable"\s+"([^"]+)"').Groups[1].Value
}
```

### 5.6 已知游戏的 Executable

| 游戏 | 默认 Executable | 说明 |
|------|----------------|------|
| 无限暖暖 | `launcher.exe` | 支持三端。Windows → `launcher.exe`，Linux/macOS 对应处理，默认 Windows |
| 绝区零 | `HYP.exe` | 仅 Windows |

> **优先级说明**：下方 Executable 放置方案中，「无限暖暖」为默认路径；「绝区零」为二级专属处理；其他游戏参考通用方法。

---

## 第六步：Executable 放置

### 6.1 检查是否存在

```powershell
$exePath = Join-Path $gameDir $executable
if (-not (Test-Path $exePath)) {
    Write-Warn "缺少可执行文件: $exePath — 将无法在 Steam 内启动"
}
```

### 6.2 缺失时处理

当可执行文件缺失时，按以下优先级处理：

#### 第一步：验证游戏文件完整性（所有游戏通用）

如果该游戏由 Steam 自动提供启动文件（如无限暖暖的 `launcher.exe`），优先提示用户：
```
Steam 库 → 右键游戏 → 属性 → 已安装文件 → 验证游戏文件的完整性
```

#### 第二步：从本地其他渠道复制（适用于拥有多渠道版本的游戏）

如果用户通过其他渠道（国服启动器、Epic 等）也安装了同一游戏，可尝试：
- 定位其他渠道版本的游戏目录
- 将其主执行文件复制到 SteamLibrary 对应的游戏目录，重命名为 `%executable%`

以绝区零为例：将国服 `~\HoYoPlay\HYP.exe` 复制到 `{SteamLibrary}\steamapps\common\ZenlessZoneZero\HYP.exe`

#### 第三步：创建 stub 可执行文件（通用兜底方案）

> **适用场景**：以上两步均不可行时，或该游戏的 Steam 版本本身不提供启动文件。

创建一个极简的 Win32 stub 程序，其功能为拉起真实游戏进程并**挂住等待**（确保 Steam 会话持续）：

```c
// stub.c — 编译为 {executable}.exe，放置在 common\{GameName}\ 下
#include <windows.h>
int WINAPI WinMain(HINSTANCE, HINSTANCE, LPSTR, int) {
    // 将下方路径替换为游戏真实主程序路径
    const wchar_t* realExe = L"C:\\Path\\To\\Real\\Game.exe";
    STARTUPINFOW si = { sizeof(si) };
    PROCESS_INFORMATION pi;
    if (CreateProcessW(realExe, NULL, NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi)) {
        WaitForSingleObject(pi.hProcess, INFINITE);
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
    }
    return 0;
}
```

> **技术说明**：
> - ❌ **不可**使用"瞬间 return"的空 exe——Steam 会立即结束会话，计时/手柄/Remote Play 等功能全部失效。
> - ✅ 必须用 `CreateProcess` + `WaitForSingleObject(INFINITE)` **挂住**真实进程，Steam 才会持续追踪会话。
> - stub 编译后仅几 KB，Agent 可提供源码让用户自行编译，或在用户明确同意后代为创建。
> - 如果用户没有真实游戏主程序路径，Agent 需引导用户提供。

#### 第四步：修改 ACF 的 "exe" 字段（替代方案）

如果不希望替换原启动文件名，可在游戏目录下放一个自定义中转 exe，然后将 ACF 中的 `"exe"` 字段改为指向该中转文件。

---

## 第七步：更新 ACF

### 7.1 备份

```powershell
$backupDir = [System.IO.Path]::GetFullPath((Join-Path (Split-Path $acfPath -Parent) "..\backups"))
if (-not (Test-Path $backupDir)) { New-Item -ItemType Directory -Path $backupDir -Force | Out-Null }
$backupPath = Join-Path $backupDir "appmanifest_$AppID.acf.bak.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Copy-Item $acfPath $backupPath -Force
```

### 7.2 更新字段

```powershell
$acf = Get-Content $acfPath -Raw -Encoding UTF8

# 解除只读
Set-ItemProperty $acfPath -Name IsReadOnly -Value $false

# BuildID
$acf = $acf -replace '("buildid"\s+")\d+(")', "`${1}$steamdbBuildID`$2"
# Manifest（针对目标 Depot）
$acf = $acf -replace "("$DepotID"\s*\{\s*"manifest"\s+")\d+(")", "`${1}$steamdbManifest`$2"
# TargetBuildID → 0
$acf = $acf -replace '("TargetBuildID"\s+")\d+(")', '${1}0$2'
# StateFlags → 4
$acf = $acf -replace '("StateFlags"\s+")\d+(")', '${1}4$2'
# 下载/暂存字段清零
@("BytesToDownload","BytesDownloaded","BytesToStage","BytesStaged") | ForEach-Object {
    $acf = $acf -replace """$_\""\s+""\d+""", """$_""		""0"""
}
# SizeOnDisk 同步
$actualSize = (Get-ChildItem $gameDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
$acf = $acf -replace '("SizeOnDisk"\s+")\d+(")', "`${1}$actualSize`$2"

# 写入 + 重新锁定
Set-Content $acfPath -Value $acf -Encoding UTF8 -NoNewline
Set-ItemProperty $acfPath -Name IsReadOnly -Value $true
```

---

## 第八步：配置启动选项

### 启动器与游戏本体分离模式

以无限暖暖、绝区零、鸣潮为代表的大型游戏，普遍采用**启动器 + 游戏本体分离**的结构：

| 组件 | 作用 | 示例 |
|------|------|------|
| **启动器**（Launcher） | 更新分发、账号登录、设置管理 | `launcher.exe`、`HYP.exe`、`Wuthering Waves.exe` |
| **游戏本体**（Game Executable） | 实际游戏进程 | `X6Game\Binaries\...exe`、`ZenlessZoneZero.exe`、`Wuthering Waves\...exe` |

### 检测方式

Agent 应通过以下方式判断该游戏的启动器与游戏本体是否分离：

1. **SteamDB Config 页面**：查看 `executable` 字段是否为启动器（而非游戏本体）
2. **ACF 字段**：ACF 中的 `"exe"` 指向的文件名
3. **游戏目录扫描**：在 `gameDir` 下查找 `*.exe`，按文件大小排序，通常启动器较小、游戏本体较大

### 处理策略

| 场景 | 处理方式 |
|------|----------|
| **启动器作为 Steam 入口** | ACF 的 `"exe"` 指向启动器；Steam 启动选项填入启动器路径 + `%command%` |
| **游戏本体作为 Steam 入口** | ACF 的 `"exe"` 指向游戏本体；Steam 启动选项可直接填入游戏本体路径 |
| **用户自定义启动器** | 提供多版本启动选项供用户选择（如第八节绝区零方案） |

**关键原则**：无论选择哪种入口方式，Steam 都会把 Overlay / 计时 / 手柄 / Remote Play 绑定在入口进程的进程树上。选择启动器作为入口时，需要确保启动器拉起游戏本体后 Steam Overlay 能正常注入。

### 8.1 路径格式要求

所有向用户输出的 Steam 启动选项，路径**必须用英文双引号包裹**：

```
"路径" "%command%" （可选参数）
```

### 8.2 无限暖暖

```
"路径" %command%
```
路径为检测到的独立启动器或 Steam `launcher.exe` 的完整路径。

> **启动器检测（无限暖暖/绝区零/鸣潮）**：
> Agent 应自动扫描以下来源检测独立启动器：
> 1. **注册表**：扫描 `HKLM/HKCU\Uninstall` 下 DisplayName 匹配游戏关键词的条目
> 2. **配置文件夹**：常见安装路径下查找 `config.ini` 等配置文件，解析 `game_path`
> 3. **开始菜单**：扫描 `.lnk` 快捷方式匹配游戏名
> 检测到后生成 Steam 启动选项：`"路径" "%command%"`

### 8.3 绝区零（专属处理）

Agent **必须实际检测**到对应文件存在后再输出路径，禁止凭空捏造路径。

**版本 1 — 通过米哈游启动器/第三方启动器**

```powershell
# 实际检测启动器路径，以下是检测方法
$launcherPaths = @(
    (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" -EA 0 |
        Where-Object { $_.DisplayName -match "HoYoPlay|米哈游" } | Select-Object -First 1).InstallLocation,
    (Get-ItemProperty "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" -EA 0 |
        Where-Object { $_.DisplayName -match "HoYoPlay|米哈游" } | Select-Object -First 1).InstallLocation,
    "C:\Program Files\HoYoPlay",
    "C:\Program Files (x86)\HoYoPlay",
    "$env:LOCALAPPDATA\Programs\HoYoPlay"
)
$realLauncher = $null
foreach ($p in $launcherPaths) {
    if ($p -and (Test-Path (Join-Path $p "HYP.exe"))) { $realLauncher = Join-Path $p "HYP.exe"; break }
}
if ($realLauncher) {
    Write-Output "推荐启动选项: `"$realLauncher`" `"%command%`""
}
# 如果未找到，向用户说明未检测到米哈游启动器，引导用户自行定位
```
> 注意：`C:\Program Files\HoYoPlay\HYP.exe` 是举例，**不是所有用户的默认路径**。Agent 必须通过注册表或遍历目录实际查找，确认文件存在后再输出。如果找不到，告知用户并请用户自行定位。

**版本 2 — 直接启动游戏本体（国服非 Steam 版本）**

> ⚠️ 这里的路径是**国服/非 Steam 版本**的游戏本体目录，**不是** SteamLibrary 下的目录。SteamLibrary 下的 `common\Zenless Zone Zero\` 仅有壳文件，无实际游戏数据。

```powershell
# 实际检测国服游戏本体路径
$gamePaths = @(
    (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" -EA 0 |
        Where-Object { $_.DisplayName -match "Zenless Zone Zero|绝区零" } | Select-Object -First 1).InstallLocation,
    "C:\Program Files\HoYoPlay\games\ZenlessZoneZero Game",
    "$env:LOCALAPPDATA\Programs\HoYoPlay\games\ZenlessZoneZero Game",
    "$env:ProgramFiles\HoYoPlay\games\ZenlessZoneZero Game"
)
$realGameExe = $null
foreach ($p in $gamePaths) {
    $candidate = Join-Path $p "ZenlessZoneZero.exe"
    if (Test-Path $candidate) { $realGameExe = $candidate; break }
}
if ($realGameExe) {
    Write-Output "游戏本体启动选项: `"$realGameExe`" `"%command%`""
}
# 如果未找到，告知用户未检测到国服游戏本体，引导用户自行定位
```

**版本 3 — DirectX 12 模式（高配可选）**

在上方版本 2 路径确认存在后，追加参数 `-use-d3d12`：
```
"实际检测到的游戏本体路径\ZenlessZoneZero.exe" "%command%" -use-d3d12
```

> **Agent 必须先检测用户显卡信息，再给出是否推荐 DX12 的建议**。禁止不做检测直接跳过或默认推荐。

```powershell
# 检测显卡型号及驱动版本
$gpu = Get-CimInstance Win32_VideoController | Select-Object -First 1
$gpuName = $gpu.Name
$driverVer = $gpu.DriverVersion
$driverDate = $gpu.DriverDate

# 判断是否为 Nvidia 显卡
$isNvidia = $gpuName -match "NVIDIA"
# 提取驱动版本号（如 591.86 从 32.0.15.9186 中提取）
$driverMajor = $null; $driverMinor = $null
if ($driverVer -match '(\d+)\.(\d+)\.(\d+)\.(\d+)') {
    $driverMajor = [int]$matches[3]
    $driverMinor = [int]$matches[4]
}

# 推荐逻辑
$recommendDX12 = $false
$reason = ""
if ($isNvidia -and $driverMajor -ge 591 -and $driverMinor -ge 86) {
    $recommendDX12 = $true
    $reason = "Nvidia 显卡驱动 $driverMajor.$driverMinor 满足 DX12 最低要求（591.86），建议尝试。"
} elseif ($isNvidia) {
    $reason = "Nvidia 显卡驱动 $driverMajor.$driverMinor 未达到 591.86，不建议使用 DX12。请升级驱动后再试。"
} else {
    $reason = "非 Nvidia 显卡，DX12 光追/超分功能可能受限，请用户自行决定是否尝试。"
}

Write-Output "检测到显卡: $gpuName"
Write-Output "驱动版本: $driverVer"
Write-Output "建议: $reason"
```

> **Agent 必须向用户展示显卡检测结果和推荐理由，并将三个版本全部列出**，由用户自行选择，不得自作主张省略版本 3 或替用户做决定。

---

## 第九步：骨架化空间清理（通用）

骨架化指将游戏的核心数据文件从 Steam 目录移至同盘备份位置，释放 Steam 目录空间，需要时可一键还原。

### 适用说明

| 游戏 | 骨架化目标 | 备份位置 |
|------|-----------|----------|
| 无限暖暖 | `InfinityNikki\X6Game`（~110GB） | `{盘符}:\X6Game_backup` |
| 其他游戏 | 游戏中体积最大的子目录（由 Agent 判断或用户指定） | 同盘 `{目录名}_backup` |

### 执行流程

```powershell
# 1. 识别骨架化目标
# 无限暖暖：InfinityNikki\X6Game
# 其他游戏：扫描 gameDir 下的子目录，找到体积最大的目录，询问用户确认

# 2. 确认 Steam 未运行
if (Get-Process steam -ErrorAction SilentlyContinue) { Write-Error "请先退出 Steam"; return }

# 3. DryRun 预览（推荐先执行）
# 显示将要移动的目录、大小、目标备份路径

# 4. 执行移动
Move-Item $sourceDir $backupDir

# 5. 验证关键文件是否保留
# 如 launcher.exe、steam_appid.txt 等

# 6. 同步更新 ACF 的 SizeOnDisk
```

### 还原

```powershell
# 从备份位置移回 Steam 目录
Move-Item $backupDir $sourceDir
```

---

## 第十步：全面验证（含可执行文件检查）

| 检查项 | 期望 | 说明 |
|--------|------|------|
| ACF 存在 | 存在 | |
| StateFlags | `4` | |
| TargetBuildID | `0` | |
| AutoUpdateBehavior | `1` | |
| ACF 只读 | 是 | |
| 游戏目录存在 | 存在 | |
| **可执行文件存在** | 存在 | 新增 |
| Steam 未运行 | 未运行 | |

---

## 第十一步：关闭 Chrome 与自动清理

> **⚠️ 重要顺序**：必须先关闭 Chrome 进程，再清理 Chrome 用户目录。如果 Chrome 仍在运行，`chrome-profile-steamdb/` 会被锁定无法删除。

### 1. 关闭 Chrome

通过命令行参数匹配 `remote-debugging-port=9222` + `chrome-profile-steamdb`，只关闭本流程启动的实例，不影响用户其他 Chrome 窗口：

```powershell
Get-CimInstance Win32_Process -Filter "Name = 'chrome.exe'" | Where-Object {
    $_.CommandLine -match 'remote-debugging-port=9222' -and $_.CommandLine -match 'chrome-profile-steamdb'
} | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
```

### 2. 等待进程完全退出

```powershell
Start-Sleep -Seconds 2
```

### 3. 自动清理临时文件（Chrome 已退出，目录可正常删除）

```powershell
# 删除 SteamDB 页面缓存
@("steamdb_depots.txt","steamdb_manifests.txt","steamdb_config.txt") | ForEach-Object {
    $p = Join-Path $PSScriptRoot $_; if (Test-Path $p) { Remove-Item $p -Force }
}
# 删除本次生成的 ACF 备份
Get-ChildItem (Join-Path $PSScriptRoot "backups") -Filter "appmanifest_$AppID.acf.bak.*" -EA 0 |
    ForEach-Object { Remove-Item $_.FullName -Force }
# 删除 Chrome 用户目录（⚠️ 必须在 Chrome 关闭后执行，否则文件被占用）
$chromeDir = Join-Path $PSScriptRoot "chrome-profile-steamdb"
if (Test-Path $chromeDir) { Remove-Item $chromeDir -Recurse -Force }
```

---

## 已知游戏参数速查

| 参数 | 无限暖暖（一级） | 绝区零（二级） |
|------|-----------------|----------------|
| **优先级** | 🥇 默认目标 | 🥈 次优先 |
| AppID | `3164330` | `4162040` |
| DepotID | `3164332` | 动态获取 |
| 默认 Executable | `launcher.exe` | `HYP.exe` |
| 有中国版 | ✅ | ❌ |
| 启动方式 | 需启动器 | 可直接启动 |
| 平台 | Win/Linux/macOS | Windows |

---

## 故障排查

### SteamDB 访问失败
- Chrome 启动超时（30s）：检查 Chrome 安装、9222 端口占用
- 页面加载超时（60s）：检查网络
- CDP 超时（30s）：重启 Chrome
- Cloudflare 超时（30s + 120s 等待）：执行网络诊断

### 网络诊断

```powershell
Test-Connection steamdb.info -Count 2
Resolve-DnsName steamdb.info
Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" | Select-Object ProxyEnable, ProxyServer
```

### ACF 更新后 Steam 仍提示更新
1. StateFlags = 4？TargetBuildID = 0？
2. BytesToStage / BytesStaged 清零？
3. ACF 只读？
4. steamapps 下是否有 `.tmp` / `downloading\{AppID}` 残留？
5. 可执行文件是否存在？

### Executable 缺失
- 无法在 Steam 内启动游戏
- 按第六步对应方案处理

---

## 输出报告

完成后输出：Steam 路径、游戏路径、版本对比（BuildID + Manifest）、ACF 状态、可执行文件状态、验证结果、启动器检测结果、启动选项建议、最终结论。
