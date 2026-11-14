# Ngan IME 安裝腳本（Windows）
#
# 以「管理員」身分執行。路徑**只**來自同目錄的 Ngan.Ime.Rw.jsonc：
# 不猜、不偵測、不讀環境變數、不接受命令列路徑參數。
#   1. 讀 Ngan.Ime.Rw.jsonc 的 DllPath 與 RimeTraits；
#   2. 註冊 TSF 外殼（CLSID、TIP profile、分類）；
#   3. 建立「登入時自動啟動服務端」的排程工作並立即啟動。
#
# 之後還要「以目前使用者身分（不要提權）」執行 install-user.cmd。
# 要換路徑就改 Ngan.Ime.Rw.jsonc。

param(
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$CfgPath = Join-Path $Root 'Ngan.Ime.Rw.jsonc'

function Say([string]$Message) { Write-Host "[install] $Message" }
function Fail([string]$Message) { Write-Host "[install] 失敗：$Message" -ForegroundColor Red; exit 1 }

function Test-Admin {
    $Id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $P = New-Object Security.Principal.WindowsPrincipal($Id)
    return $P.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# ---- 1. 產物齊全 ----
$Shell = Join-Path $Root 'Ngan.Ime.Tsf.dll'
$ServerExe = Join-Path $Root 'Ngan.Ime.Windows.exe'
$Registrar = Join-Path $Root 'Ngan.Ime.Tsf.Registration.exe'
foreach ($One in @($Shell, $ServerExe, $Registrar)) {
    if (-not (Test-Path -LiteralPath $One)) { Fail "缺少檔案：$One" }
}

# ---- 2. 從配置文件取路徑 ----
if (-not (Test-Path -LiteralPath $CfgPath)) {
    Fail "找不到配置文件：$CfgPath。請先填好 DllPath 與 user_data_dir。"
}
try {
    $Cfg = Get-Content -LiteralPath $CfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
}
catch {
    Fail "讀不動配置文件 ${CfgPath}：$($_.Exception.Message)"
}

$Dll = ''
$UserData = ''
$LogDir = ''
if ($Cfg.Librime) {
    $Dll = [string]$Cfg.Librime.DllPath
    if ($Cfg.Librime.RimeTraits) {
        $UserData = [string]$Cfg.Librime.RimeTraits.user_data_dir
        $LogDir = [string]$Cfg.Librime.RimeTraits.log_dir
    }
}
if ([string]::IsNullOrWhiteSpace($Dll)) {
    Fail "配置文件沒有填 Librime.DllPath：$CfgPath"
}
if (-not (Test-Path -LiteralPath $Dll)) {
    Fail "配置文件指的 rime.dll 不存在：$Dll"
}
if ([string]::IsNullOrWhiteSpace($UserData)) {
    Say '（配置沒有填 user_data_dir，Rime 會用它的默認位置）'
}
elseif (-not (Test-Path -LiteralPath $UserData)) {
    Say "（user_data_dir 目前不存在，Rime 會自己建立：$UserData）"
}

Say "rime.dll      : $Dll"
Say "Rime 用戶資料 : $UserData"
Say "日誌目錄      : $LogDir"

# ---- 3. 只印設定、不落地 ----
if ($WhatIf) {
    Say 'WhatIf：只印設定，不註冊、不建排程工作。'
    exit 0
}

if (-not (Test-Admin)) {
    Fail '需要管理員權限：要寫 HKLM 的 COM 登記與輸入法 profile。請以管理員開啟 PowerShell 再跑。'
}

# ---- 4. 註冊 TSF 外殼（機器層）----
Say '註冊 TSF 外殼…'
& $Registrar --register $Shell
if ($LASTEXITCODE -ne 0) { Fail "註冊失敗（exit=$LASTEXITCODE）" }

# ---- 5. 登入時自動啟動服務端 ----
$StartVbs = Join-Path $Root 'StartServer.vbs'
if (Test-Path -LiteralPath $StartVbs) {
    $Action = New-ScheduledTaskAction -Execute 'C:\Windows\System32\wscript.exe' -Argument ('"' + $StartVbs + '"')
    $Trigger = New-ScheduledTaskTrigger -AtLogOn
    $Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
    Register-ScheduledTask -TaskName 'NganIme-Server' -Action $Action -Trigger $Trigger -Settings $Settings -RunLevel Limited -Force | Out-Null
    Start-ScheduledTask -TaskName 'NganIme-Server'
    Say '已建立並啟動排程工作 NganIme-Server（登入時自動啟動）。'
}

Write-Host ''
Write-Host '安裝完成。還有兩步：' -ForegroundColor Green
Write-Host '  1. 以「目前使用者身分」雙擊 install-user.cmd（不要用管理員），把輸入法加進你的清單；'
Write-Host '  2. 登出再登入（或重啟 explorer），讓應用重新載入外殼。'
Write-Host '  之後用 Win+Space 切到 Ngan IME。'
