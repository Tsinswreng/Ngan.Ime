# Ngan IME 反安裝腳本（Windows）
# 必須以「管理員」身分執行：要移除 HKLM 的 COM 登記與輸入法 profile。

param(
    [switch]$KeepUserData
)

$ErrorActionPreference = 'Continue'
$Root = $PSScriptRoot
$Registrar = Join-Path $Root 'Ngan.Ime.Tsf.Registration.exe'

Write-Host '[uninstall] 停止服務端與排程工作…'
Get-Process -Name 'Ngan.Ime.Windows' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Unregister-ScheduledTask -TaskName 'NganIme-Server' -Confirm:$false -ErrorAction SilentlyContinue

if (Test-Path -LiteralPath $Registrar) {
    Write-Host '[uninstall] 從目前使用者的輸入法清單移除…'
    & $Registrar --uninstall-user
    Write-Host '[uninstall] 移除機器層註冊…'
    & $Registrar --unregister-system
}
else {
    Write-Host "[uninstall] 找不到註冊工具，跳過註冊表清理：$Registrar"
}

if (-not $KeepUserData) {
    $OwnUserData = Join-Path $env:LOCALAPPDATA 'NganIme'
    if (Test-Path -LiteralPath $OwnUserData) {
        Write-Host "[uninstall] 移除本機用戶資料：$OwnUserData（要保留請加 -KeepUserData）"
        Remove-Item -LiteralPath $OwnUserData -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host '[uninstall] 完成。請登出再登入，讓應用卸載外殼。'
