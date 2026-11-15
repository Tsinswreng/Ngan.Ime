# Ngan IME 反安裝腳本（Windows）
#
# 只卸載「這一個版本」：自己的 CLSID、自己的輸入法 profile、自己的排程工作、自己的目錄。
# 其他版本的 Ngan IME 是完全獨立的輸入法，不受影響。
#
# 必須以「管理員」身分執行：要移除 HKLM 的 COM 登記與輸入法 profile。
#
# 用法：
#     .\uninstall.ps1                 卸載本目錄所屬的版本
#     .\uninstall.ps1 -KeepUserData   保留版本目錄（只移除註冊與排程工作）

param(
    [switch]$KeepUserData
)

$ErrorActionPreference = 'Continue'
$Root = $PSScriptRoot
$Registrar = Join-Path $Root 'Ngan.Ime.Tsf.Registration.exe'
$VersionName = Split-Path -Leaf $Root
$TaskName = 'NganIme-Server-' + $VersionName

Write-Host "[uninstall] 版本：$VersionName"

# ---- 1. 停掉「這個版本」的服務端 ----
# 不能像先前那樣把所有 Ngan.Ime.Windows 殺掉：別的版本還在用它們自己的服務端。
Write-Host '[uninstall] 停止本版本的服務端行程…'
try {
    $Stopped = 0
    foreach ($Proc in Get-CimInstance Win32_Process -Filter "Name='Ngan.Ime.Windows.exe'" -ErrorAction SilentlyContinue) {
        $Path = [string]$Proc.ExecutablePath
        if ($Path -and $Path.StartsWith($Root, [StringComparison]::OrdinalIgnoreCase)) {
            Stop-Process -Id $Proc.ProcessId -Force -ErrorAction SilentlyContinue
            $Stopped++
        }
    }
    Write-Host "[uninstall] 已停止 $Stopped 個行程。"
}
catch {
    Write-Host "[uninstall] 停止行程失敗（可稍後手動關閉）：$($_.Exception.Message)"
}

# ---- 2. 移除本版本的排程工作 ----
if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "[uninstall] 已移除排程工作：$TaskName"
}

# ---- 3. 移除註冊（identity.txt 決定是哪一組 CLSID/profile）----
if (Test-Path -LiteralPath $Registrar) {
    Write-Host '[uninstall] 從目前使用者的輸入法清單移除…'
    & $Registrar --uninstall-user
    Write-Host '[uninstall] 移除機器層註冊…'
    & $Registrar --unregister-system
}
else {
    Write-Host "[uninstall] 找不到註冊工具，跳過註冊表清理：$Registrar"
}

# ---- 4. 移除本版本目錄 ----
if (-not $KeepUserData) {
    $NganRoot = Join-Path $env:LOCALAPPDATA 'NganIme'
    if ($Root.StartsWith($NganRoot, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $Root)) {
        Write-Host "[uninstall] 移除本版本目錄：$Root（要保留請加 -KeepUserData）"
        Remove-Item -LiteralPath $Root -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $Root) {
            Write-Host '[uninstall] 目錄仍被行程載入，暫時刪不掉；關掉相關程式後可再試，或用 Scripts 的 CleanWin。'
        }
    }
}

Write-Host '[uninstall] 完成。這個版本已從系統移除；其他版本的 Ngan IME 不受影響。'
