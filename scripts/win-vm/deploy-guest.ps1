$ErrorActionPreference = 'Continue'

# ---- 參數 ----
$Cfg = @{}
foreach($Line in Get-Content (Join-Path (Split-Path $PSCommandPath) 'deploy-args.txt')){
    if($Line -match '^\s*#' -or $Line -notmatch '='){ continue }
    $P = $Line.Split('=', 2)
    $Cfg[$P[0].Trim()] = $P[1].Trim()
}
if([string]::IsNullOrWhiteSpace($Cfg.WorkDir)){ Write-Error 'deploy-args.txt 解析失敗（WorkDir 為空）'; exit 2 }
$WorkDir = $Cfg.WorkDir
$In      = Join-Path $WorkDir 'in'
$Apps    = Join-Path $WorkDir 'apps'
$ServerDir = Join-Path $Apps 'ngan-server-x86'
$TsfDir  = Join-Path $Apps 'ngan-tsf'
$RegDir  = Join-Path $Apps 'ngan-reg'
$Log     = Join-Path $WorkDir 'last-run.log'

Set-Content -Path $Log -Value ('deploy ' + (Get-Date).ToString('o')) -Encoding utf8
function Log([string]$M){ Add-Content -Path $Log -Value ((Get-Date).ToString('HH:mm:ss') + ' ' + $M) -Encoding utf8 }
function Fail([string]$M){ Log ('RESULT=FAIL ' + $M); exit 1 }

# 排程工作的動作一律是「跑一支會自我記錄的 .ps1」。
# 不用 cmd /c 加 `>` 重定向：PowerShell 的 -File 不吃 `>`，而 vmrun 在 session 0 起的 cmd 會直接失敗。
function New-PsTask([string]$Name, [string]$ScriptPath, [string]$UserId, [string]$LogonType, [string]$RunLevel){
    $Act = New-ScheduledTaskAction -Execute 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' `
        -Argument ('-NoProfile -ExecutionPolicy Bypass -File "' + $ScriptPath + '"')
    $Prn = New-ScheduledTaskPrincipal -UserId $UserId -LogonType $LogonType -RunLevel $RunLevel
    Register-ScheduledTask -TaskName $Name -Action $Act -Principal $Prn -Force | Out-Null
}

foreach($D in @($In, $Apps, $ServerDir, $TsfDir, $RegDir)){ New-Item -ItemType Directory -Force -Path $D | Out-Null }

# ---- 0 清理改名遺留 ----
# 2026-11-14 把「宿主」正名為「服務端」：舊的排程工作與舊目錄不會自動消失，
# 留著會變成兩份並存、排查時分不清哪一份在跑。
if(Get-ScheduledTask -TaskName ($Cfg.TaskPrefix + '-Host') -ErrorAction SilentlyContinue){
    Unregister-ScheduledTask -TaskName ($Cfg.TaskPrefix + '-Host') -Confirm:$false -ErrorAction SilentlyContinue
    Log '已移除舊排程工作（-Host）'
}
$OldDir = Join-Path $Apps 'ngan-host-x86'
if(Test-Path $OldDir){
    Remove-Item -LiteralPath $OldDir -Recurse -Force -ErrorAction SilentlyContinue
    Log '已移除舊目錄 ngan-host-x86'
}

# ---- 1 停舊服務端 ----
Log '停止舊服務端'
Get-Process -Name 'Ngan.Ime.Windows' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# ---- 2 解包 ----
Log '解包產物'
try {
    Get-ChildItem -Path $ServerDir -Recurse -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    Expand-Archive -Path (Join-Path $In 'server.zip') -DestinationPath $ServerDir -Force
} catch { Fail ('解包服務端失敗：' + $_.Exception.Message) }
try { Expand-Archive -Path (Join-Path $In 'reg.zip') -DestinationPath $RegDir -Force } catch { Fail ('解包註冊工具失敗：' + $_.Exception.Message) }
try { Expand-Archive -Path (Join-Path $In 'tsf.zip') -DestinationPath $TsfDir -Force } catch { Fail ('解包 TSF 失敗：' + $_.Exception.Message) }

# ---- 2.5 修正服務端配置裡的本機路徑 ----
# 部署包裡的 jsonc 是在主機上跑的配置，DllPath 之類會指向主機路徑；
# 直接啟動會得到 DllNotFoundException 並讓服務端當場退出，故在 guest 內改寫成本機路徑。
$CfgFile = Join-Path $ServerDir 'Ngan.Ime.Rw.jsonc'
if(Test-Path $CfgFile){
    try {
        $Json = Get-Content $CfgFile -Raw -Encoding utf8 | ConvertFrom-Json
        $Json.Librime.DllPath = $Cfg.RimeDll
        $Json.Librime.RimeTraits.user_data_dir = $Cfg.RimeUserData
        $RimeLogDir = Join-Path $WorkDir 'rime-log'
        New-Item -ItemType Directory -Force -Path $RimeLogDir | Out-Null
        $Json.Librime.RimeTraits.log_dir = $RimeLogDir
        ($Json | ConvertTo-Json -Depth 12) | Set-Content -Path $CfgFile -Encoding utf8
        Log ('已改寫配置：DllPath=' + $Cfg.RimeDll)
    } catch { Fail ('改寫配置失敗：' + $_.Exception.Message) }
} else {
    Log '警告：部署包沒有 Ngan.Ime.Rw.jsonc，將改用環境變數與默認值'
}
if(-not (Test-Path $Cfg.RimeDll)){ Fail ('guest 缺少 rime.dll：' + $Cfg.RimeDll) }

# ---- 3 TSF DLL 版本化：被 explorer 載入的檔案不能覆寫 ----
$TsfSrc = Join-Path $TsfDir 'Ngan.Ime.Tsf.dll'
if(-not (Test-Path $TsfSrc)){ Fail ('TSF 產物缺少 Ngan.Ime.Tsf.dll：' + $TsfDir) }
$TsfDll = Join-Path $TsfDir ('Ngan.Ime.Tsf.' + $Cfg.TsfVersion + '.dll')
Copy-Item -Path $TsfSrc -Destination $TsfDll -Force
Log ('TSF DLL = ' + $TsfDll)

# ---- 4 系統註冊（SYSTEM） ----
$Registrar = Join-Path $RegDir 'Ngan.Ime.Tsf.Registration.exe'
if(-not (Test-Path $Registrar)){ Fail ('缺少註冊工具：' + $Registrar) }
$RegLog  = Join-Path $WorkDir 'register.log'
$RegPs1  = Join-Path $In 'do-register.ps1'
@"
`$out = '$RegLog'
& '$Registrar' --register '$TsfDll' *> `$out
'exit=' + `$LASTEXITCODE | Add-Content -Path `$out -Encoding utf8
"@ | Set-Content -Path $RegPs1 -Encoding utf8
New-PsTask -Name ($Cfg.TaskPrefix + '-Register') -ScriptPath $RegPs1 -UserId 'SYSTEM' -LogonType 'ServiceAccount' -RunLevel 'Highest'
Start-ScheduledTask -TaskName ($Cfg.TaskPrefix + '-Register')
Start-Sleep -Seconds 8
Log ('register.log：' + ((Get-Content $RegLog -Encoding utf8 -ErrorAction SilentlyContinue) -join ' | '))

# ---- 5 加入桌面使用者的輸入法清單 ----
$UserLog = Join-Path $WorkDir 'install-user.log'
$UserPs1 = Join-Path $In 'do-install-user.ps1'
@"
`$out = '$UserLog'
& '$Registrar' --install-user *> `$out
'exit=' + `$LASTEXITCODE | Add-Content -Path `$out -Encoding utf8
"@ | Set-Content -Path $UserPs1 -Encoding utf8
New-PsTask -Name ($Cfg.TaskPrefix + '-InstallUser') -ScriptPath $UserPs1 -UserId $Cfg.DesktopUser -LogonType 'Interactive' -RunLevel 'Limited'
Start-ScheduledTask -TaskName ($Cfg.TaskPrefix + '-InstallUser')
Start-Sleep -Seconds 4
Log ('install-user.log：' + ((Get-Content $UserLog -Encoding utf8 -ErrorAction SilentlyContinue) -join ' | '))

# ---- 5.5 讓新註冊的 TSF 生效 ----
# TSF 是「行程載入時」決定的：已開着的記事本／explorer 會繼續用舊 DLL 的邏輯
# （症狀就是方向鍵、Escape 這些新加的路由看起來沒生效）。故這裡結束測試用的記事本，
# 並把仍持有舊版 DLL 的行程列出來，讓使用者知道要重開哪些。
$CurTsf = (Get-Item $TsfDll).FullName
Get-Process -Name 'notepad' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if($Cfg.RestartExplorer -eq '1'){
    Log '重啟 explorer（-RestartExplorer）'
    Get-Process -Name 'explorer' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-Process -FilePath 'C:\Windows\explorer.exe'
    Start-Sleep -Seconds 3
}
$Stale = @()
foreach($P in (Get-Process -ErrorAction SilentlyContinue)){
    try {
        foreach($M in $P.Modules){
            if($M.ModuleName -like 'Ngan.Ime.Tsf*' -and $M.FileName -ne $CurTsf){
                $Stale += ($P.ProcessName + '(' + $P.Id + ')->' + (Split-Path $M.FileName -Leaf))
                break
            }
        }
    } catch { }
}
if($Stale.Count -gt 0){
    Log ('仍載入舊版 TSF（需重開才會用新邏輯）：' + (($Stale | Select-Object -Unique) -join ', '))
} else {
    Log '沒有行程載入舊版 TSF'
}

# ---- 6 以桌面使用者身分常駐啟動服務端 ----
$ServerExe = Join-Path $ServerDir 'Ngan.Ime.Windows.exe'
if(-not (Test-Path $ServerExe)){ Fail ('缺少服務端：' + $ServerExe) }
$ServerPs1 = Join-Path $In 'do-start-server.ps1'
@"
`$env:NGAN_RIME_DLL = '$($Cfg.RimeDll)'
`$env:NGAN_RIME_USER_DATA = '$($Cfg.RimeUserData)'
Start-Process -FilePath '$ServerExe' -ArgumentList '--tsf' -WindowStyle Hidden
"@ | Set-Content -Path $ServerPs1 -Encoding utf8
New-PsTask -Name ($Cfg.TaskPrefix + '-Server') -ScriptPath $ServerPs1 -UserId $Cfg.DesktopUser -LogonType 'Interactive' -RunLevel 'Limited'
Start-ScheduledTask -TaskName ($Cfg.TaskPrefix + '-Server')
Start-Sleep -Seconds 8

$Proc = Get-Process -Name 'Ngan.Ime.Windows' -ErrorAction SilentlyContinue
if($null -eq $Proc){
    $ServerLog = Join-Path $ServerDir 'logs/Ngan.Ime.Windows.log'
    if(Test-Path $ServerLog){
        Log '服務端日誌尾段：'
        Get-Content $ServerLog -Tail 12 -Encoding utf8 | ForEach-Object { Log ('  ' + $_) }
    }
    Fail '服務端沒有起來（行程不存在）'
}
Log ('服務端 pid=' + ($Proc | Select-Object -First 1).Id)

# ---- 7 管道煙霧測試（以桌面使用者身分） ----
# 只驗「連通」是不夠的：先前回 R|0|0||0|0（完全沒吃鍵）也會被判成 OK。
# 現在逐條斷言：版本、是否吃掉、候選數、擡起可解析、F4 方案選單。
if($Cfg.SkipSmoke -ne '1'){
    $SmokeLog = Join-Path $WorkDir 'smoke.log'
    $SmokePs1 = Join-Path $In 'do-smoke.ps1'
    @"
`$out = '$SmokeLog'
function Send-Key([string]`$payload){
    `$c = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'NganIme.Tsf.v1')
    `$c.Connect(5000)
    `$w = New-Object System.IO.StreamWriter(`$c); `$w.AutoFlush = `$true
    `$r = New-Object System.IO.StreamReader(`$c)
    `$w.WriteLine(`$payload)
    `$line = `$r.ReadLine()
    `$c.Dispose()
    return `$line
}
function Fields([string]`$line){ return `$line.Split('|') }
`$fail = @()
try {
    `$down = Send-Key 'K|1|67|0|100|100|0'
    `$f = Fields `$down
    if(`$f[0] -ne 'R'){ `$fail += 'a 按下：首欄不是 R' }
    if(`$f[1] -ne '1'){ `$fail += 'a 按下：協議版本不是 1' }
    if(`$f[2] -ne '1'){ `$fail += 'a 按下：引擎沒有吃掉' }
    if([string]::IsNullOrEmpty(`$f[5])){ `$fail += 'a 按下：沒有組字串' }
    if([int]`$f[6] -lt 1){ `$fail += 'a 按下：沒有候選' }
    `$up = Send-Key 'K|1|67|0|100|100|1'
    if((Fields `$up)[0] -ne 'R'){ `$fail += 'a 擡起：回應無法解析' }
    Send-Key 'K|1|27|0|100|100|0' | Out-Null
    `$f4 = Send-Key 'K|1|115|0|100|100|0'
    `$f4f = Fields `$f4
    if(`$f4f[2] -ne '1'){ `$fail += 'F4：引擎沒有吃掉（方案選單熱鍵失效）' }
    if([int]`$f4f[6] -lt 1){ `$fail += 'F4：沒有候選（方案選單沒開）' }
    Send-Key 'K|1|27|0|100|100|0' | Out-Null
} catch {
    `$fail += ('例外：' + `$_.Exception.Message)
}
if(`$fail.Count -eq 0){
    'SMOKE OK: a=' + `$down + ' up=' + `$up + ' f4=' + `$f4 | Set-Content -Path `$out -Encoding utf8
} else {
    ('SMOKE FAIL: ' + (`$fail -join '；')) | Set-Content -Path `$out -Encoding utf8
}
"@ | Set-Content -Path $SmokePs1 -Encoding utf8
    New-PsTask -Name ($Cfg.TaskPrefix + '-Smoke') -ScriptPath $SmokePs1 -UserId $Cfg.DesktopUser -LogonType 'Interactive' -RunLevel 'Limited'
    Start-ScheduledTask -TaskName ($Cfg.TaskPrefix + '-Smoke')
    Start-Sleep -Seconds 10
    $Smoke = (Get-Content $SmokeLog -Encoding utf8 -ErrorAction SilentlyContinue) -join ' | '
    Log ('smoke：' + $Smoke)
    if($Smoke -notmatch 'SMOKE OK'){ Fail ('管道煙霧測試失敗：' + $Smoke) }
}

# ---- 8 可選：開記事本 ----
if($Cfg.OpenNotepad -eq '1'){
    $NpPs1 = Join-Path $In 'do-notepad.ps1'
    @"
Start-Process -FilePath 'C:\Windows\System32\notepad.exe'
"@ | Set-Content -Path $NpPs1 -Encoding utf8
    New-PsTask -Name ($Cfg.TaskPrefix + '-Notepad') -ScriptPath $NpPs1 -UserId $Cfg.DesktopUser -LogonType 'Interactive' -RunLevel 'Limited'
    Start-ScheduledTask -TaskName ($Cfg.TaskPrefix + '-Notepad')
    Log '已開記事本'
}

# ---- 9 清理太舊的 TSF DLL（保留最近 3 個；被載入的刪不掉就忽略） ----
Get-ChildItem -Path $TsfDir -Filter 'Ngan.Ime.Tsf.*.dll' -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -Skip 3 |
    ForEach-Object { Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue }

Log 'RESULT=OK'
exit 0
