# Ngan IME 版本化安裝（Windows）
#
# 每一次以管理員身分執行，都會裝成一個「全新的、獨立的輸入法」：
#     %LOCALAPPDATA%\NganIme\v<yyyyMMdd-HHmmss>
# 它有自己的 CLSID、自己的輸入法 profile、自己的服務端、自己的管道名、自己的 Rime 用戶資料，
# 與舊版本完全沒有關聯：可以各自安裝、各自運作、各自卸載，互不影響。
#
# 舊版本原封不動留著——TSF 外殼是行程載入的 DLL，跑着的行程不放手，任何安裝器都換不掉它，
# 所以只能「裝到新路徑即時生效、舊路徑等行程結束後回收」。回收用 Scripts 的 CleanWin。
#
# 用法（管理員）：
#     .\install.ps1                     安裝到新版本目錄並切換過去
#     .\install.ps1 -WhatIf             只印出將要做的事
#     .\install.ps1 -ShareUserData      不複製 Rime 用戶資料，直接與來源共用（預設各自一份）
#     .\install.ps1 -IgnoreUserList     不把新 profile 加進目前使用者的輸入法清單
#
# 注意：提權之後仍然是同一個使用者帳號，故 HKCU 就是使用者自己的那份設定；
# 「加進輸入法清單」這件事在提權的這一步做是正確的（先前把它留給使用者手動做，
# 結果是「裝了新版本卻還是在用舊的」）。

param(
    [switch]$WhatIf,
    [switch]$ShareUserData,
    [switch]$IgnoreUserList
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$CfgPath = Join-Path $Root 'Ngan.Ime.Rw.jsonc'
$Version = Get-Date -Format 'yyyyMMdd-HHmmss'
$InstallRoot = Join-Path $env:LOCALAPPDATA ('NganIme\v' + $Version)
$TaskName = 'NganIme-Server-v' + $Version

function Say([string]$Message) { Write-Host "[install] $Message" }
function Warn([string]$Message) { Write-Host "[install] $Message" -ForegroundColor Yellow }
function Fail([string]$Message) { Write-Host "[install] 失敗：$Message" -ForegroundColor Red; exit 1 }

function Test-Admin {
    $Id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $P = New-Object Security.Principal.WindowsPrincipal($Id)
    return $P.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# 列出所有載入了某個目錄下檔案的行程；用來告訴使用者「哪些要重開」。
function Find-Holders([string]$Dir) {
    $Result = @()
    foreach ($Proc in Get-Process -ErrorAction SilentlyContinue) {
        if ($Proc.Id -eq $PID) { continue }
        try {
            $Main = $Proc.MainModule
            if ($Main -and $Main.FileName -and $Main.FileName.StartsWith($Dir, [StringComparison]::OrdinalIgnoreCase)) {
                $Result += [pscustomobject]@{ Id = $Proc.Id; Name = $Proc.ProcessName; File = $Main.FileName }
                continue
            }
            foreach ($Module in $Proc.Modules) {
                if ($Module.FileName -and $Module.FileName.StartsWith($Dir, [StringComparison]::OrdinalIgnoreCase)) {
                    $Result += [pscustomobject]@{ Id = $Proc.Id; Name = $Proc.ProcessName; File = $Module.FileName }
                    break
                }
            }
        }
        catch { }
    }
    return $Result
}

# ---- 1. 產物與配置 ----
$Shell = Join-Path $Root 'Ngan.Ime.Tsf.dll'
$ServerExe = Join-Path $Root 'Ngan.Ime.Windows.exe'
$Registrar = Join-Path $Root 'Ngan.Ime.Tsf.Registration.exe'
foreach ($One in @($Shell, $ServerExe, $Registrar)) {
    if (-not (Test-Path -LiteralPath $One)) { Fail "缺少檔案：$One" }
}
if (-not (Test-Path -LiteralPath $CfgPath)) { Fail "找不到配置文件：$CfgPath" }
$Cfg = Get-Content -LiteralPath $CfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
$Dll = [string]$Cfg.Librime.DllPath
if ([string]::IsNullOrWhiteSpace($Dll)) { Fail "配置文件沒有填 Librime.DllPath：$CfgPath" }
if (-not (Test-Path -LiteralPath $Dll)) { Fail "配置文件指的 rime.dll 不存在：$Dll" }

$UserDataSrc = [string]$Cfg.Librime.RimeTraits.user_data_dir

Say "版本目錄      : $InstallRoot"
Say "rime.dll      : $Dll"
Say "Rime 用戶資料 : $UserDataSrc"
Say "排程工作      : $TaskName"

if ($WhatIf) {
    Say 'WhatIf：只印計劃，不複製、不註冊、不建排程工作。'
    exit 0
}
if (-not (Test-Admin)) {
    Fail '需要管理員權限：要寫 HKLM 的 COM 登記。請以管理員開啟 PowerShell 再跑。'
}

# ---- 2. 複製到新版本目錄 ----
Say '複製到新版本目錄…'
New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
Copy-Item -Path (Join-Path $Root '*') -Destination $InstallRoot -Recurse -Force
$NewShell = Join-Path $InstallRoot 'Ngan.Ime.Tsf.dll'
$NewRegistrar = Join-Path $InstallRoot 'Ngan.Ime.Tsf.Registration.exe'
$NewServer = Join-Path $InstallRoot 'Ngan.Ime.Windows.exe'
$NewCfg = Join-Path $InstallRoot 'Ngan.Ime.Rw.jsonc'

# ---- 3. 版本識別：每次安裝都是一個獨立輸入法 ----
# 自己的 CLSID 與 profile GUID；寫進 identity.txt，註冊、加入清單、卸載都讀同一份。
# 外殼（Ngan.Ime.Tsf.dll）與服務端（Ngan.Ime.Windows.exe）也會用同一份目錄算出自己的管道名，
# 所以兩個版本各有一條管道、各有一個服務端，不會互相搶請求。
$Clsid = [guid]::NewGuid().ToString('B').ToUpper()
$Profile = [guid]::NewGuid().ToString('B').ToUpper()
@(
    "clsid=$Clsid",
    "profile=$Profile",
    "name=Ngan IME v$Version"
) | Set-Content -LiteralPath (Join-Path $InstallRoot 'identity.txt') -Encoding ASCII
Say "本版 CLSID   : $Clsid"
Say "本版 profile : $Profile"

# ---- 4. Rime 用戶資料：預設每個版本一份 ----
# 兩個 Rime 引擎共用同一份 user data 會撞 deployment 的檔案鎖，而且一邊改方案會影響另一邊。
# 複製失敗（例如舊版本引擎還開着、檔案被鎖）不算致命：退回共用並說清楚。
$NewUserData = $UserDataSrc
$NewLogDir = [string]$Cfg.Librime.RimeTraits.log_dir
if (-not $ShareUserData) {
    $TargetUserData = Join-Path $InstallRoot 'rime-user'
    if (-not [string]::IsNullOrWhiteSpace($UserDataSrc) -and (Test-Path -LiteralPath $UserDataSrc)) {
        try {
            Copy-Item -LiteralPath $UserDataSrc -Destination $TargetUserData -Recurse -Force -ErrorAction Stop
            Say "Rime 用戶資料已複製：$TargetUserData"
        }
        catch {
            Warn "複製 Rime 用戶資料失敗（$($_.Exception.Message)），改用共用：$UserDataSrc"
            $TargetUserData = $UserDataSrc
        }
    }
    else {
        # 來源不存在：給這個版本一個空目錄，讓 Rime 自己部署。共用一個不存在的目錄更糟。
        New-Item -ItemType Directory -Force -Path $TargetUserData | Out-Null
        Say "Rime 用戶資料來源不存在，改用本版本目錄（第一次啟動時由 Rime 部署）：$TargetUserData"
    }
    $NewUserData = $TargetUserData
    $NewLogDir = Join-Path $InstallRoot 'rime-log'
}
if ($NewUserData -ne $UserDataSrc) {
    # 只改這兩個路徑；其他鍵與註釋保持原樣會被 ConvertTo-Json 弄丟，故先確認檔案裏沒有註釋。
    $Raw = Get-Content -LiteralPath $NewCfg -Raw -Encoding UTF8
    if ($Raw -match '//') {
        Warn '配置文件含註釋，無法安全改寫路徑；這個版本會與來源共用 Rime 用戶資料。'
        $NewUserData = $UserDataSrc
    }
    else {
        $Cfg.Librime.RimeTraits.user_data_dir = $NewUserData
        if (-not [string]::IsNullOrWhiteSpace($NewLogDir)) {
            $Cfg.Librime.RimeTraits.log_dir = $NewLogDir
        }
        $Cfg | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $NewCfg -Encoding UTF8
        Say "本版用戶資料 : $NewUserData"
        Say "本版日誌目錄 : $NewLogDir"
    }
}

# ---- 5. 註冊新版本外殼（機器層：自己的 CLSID）----
Say '註冊新版本外殼…'
& $NewRegistrar --register $NewShell
if ($LASTEXITCODE -ne 0) { Fail "註冊失敗（exit=$LASTEXITCODE）" }

# ---- 6. 把新 profile 加進目前使用者的輸入法清單 ----
# 少了這一步，Windows 只知道機器層註冊過這個 CLSID，使用者的輸入法清單仍是舊 profile，
# 於是「裝了新版本卻還是在用舊的」。提權仍是同一帳號，HKCU 就是使用者自己的設定。
if (-not $IgnoreUserList) {
    Say '加入目前使用者的輸入法清單…'
    & $NewRegistrar --install-user
    if ($LASTEXITCODE -ne 0) { Warn "加入輸入法清單失敗（exit=$LASTEXITCODE）；可稍後以本人身分執行 install-user.cmd。" }
}

# ---- 7. 本版本的排程工作：登入時啟動、失敗重啟 ----
# 每個版本一個工作名，舊版本的工作不動（那是另一個獨立輸入法）。
$Action = New-ScheduledTaskAction -Execute $NewServer -Argument '--tsf' -WorkingDirectory $InstallRoot
$Trigger = New-ScheduledTaskTrigger -AtLogOn
$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName $TaskName -Action $Action -Trigger $Trigger -Settings $Settings -RunLevel Limited -Force | Out-Null
Start-ScheduledTask -TaskName $TaskName
Say "排程工作 $TaskName 已建立並啟動。"

# ---- 8. 告訴使用者哪些程式還在用舊版本 ----
# 這一段不是錯誤，是「TSF 外殼是行程載入的 DLL」這件事的直接後果：
# 跑着的行程不放手，Windows 不允許換掉映像，所以舊版只能等那些行程重開。
$NganRoot = Join-Path $env:LOCALAPPDATA 'NganIme'
$OldDirs = Get-ChildItem -Path $NganRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'v*' -and $_.FullName -ne $InstallRoot }
$OldWithHolders = @()
foreach ($Old in $OldDirs) {
    $Holders = Find-Holders $Old.FullName
    if ($Holders.Count -gt 0) {
        $OldWithHolders += $Old.FullName
        Write-Host ''
        Write-Host ("舊版本仍在使用中：" + $Old.FullName) -ForegroundColor Yellow
        $Holders | ForEach-Object { Write-Host ("  {0,-6} {1}" -f $_.Id, $_.Name) }
        Write-Host '  上面這些行程已經載入了舊版外殼，重開它們才會換到這一版；'
        Write-Host '  不重開也能正常打字——那個行程會繼續用舊版與舊版自己的服務端。'
    }
}

Write-Host ''
Write-Host '安裝完成：這一版是一個獨立的輸入法（自己的 CLSID、profile、服務端、管道、Rime 用戶資料）。' -ForegroundColor Green
Write-Host ("  1. 按 Win+Space，在輸入法清單裏選「Ngan IME v$Version」——")
Write-Host '     不選的話 Windows 仍停在舊的那一個，新開的程式也會繼續載入舊版外殼。'
Write-Host '  2. 已經開着的程式要重開（或登出再登入）才會從舊外殼換到這一版。'
Write-Host '  3. 舊版本目錄還會留著（正在跑的行程抓着它的 DLL 不放）。確認這一版可用之後：'
Write-Host '     在舊版本目錄裏執行 uninstall.ps1（管理員）——它只移除那一版的 profile／CLSID／排程工作；'
Write-Host '     或等它不再被載入後，用 Scripts 的 CleanWin -UninstallOld 一次清掉舊版本。'
