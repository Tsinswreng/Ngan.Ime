<#
.SYNOPSIS
    在 guest 內註冊 Ngan IME 的 TSF DLL、加入目前使用者輸入法清單，並常駐啟動宿主。
.DESCRIPTION
    這支腳本要在虛擬機內執行（例如用 vm-common.ps1 的 Invoke-Guest 丟進去跑）。
    它會建立三個排程工作：
      <Prefix>Register   SYSTEM，執行 registrar --register <Dll>
      <Prefix>InstallUser 指定使用者，執行 registrar --install-user
      <Prefix>Host      指定使用者，跑 host.bat（設定 Rime 環境變數後以分離方式啟動宿主）
.EXAMPLE
    .\register-install-start.ps1 `
        -Registrar 'C:\NganImeVm\apps\ngan-reg\Ngan.Ime.Tsf.Registration.exe' `
        -Dll 'C:\NganImeVm\apps\ngan-tsf\Ngan.Ime.Tsf.dll' `
        -RimeDll 'C:\NganImeVm\rime\rime.dll' `
        -RimeUserData 'C:\NganImeVm\rime-user' `
        -HostExe 'C:\NganImeVm\apps\ngan-server-x86\Ngan.Ime.Windows.exe'
#>
param(
    [Parameter(Mandatory = $true)][string]$Registrar,
    [Parameter(Mandatory = $true)][string]$Dll,
    [Parameter(Mandatory = $true)][string]$RimeDll,
    [Parameter(Mandatory = $true)][string]$RimeUserData,
    [Parameter(Mandatory = $true)][string]$HostExe,
    [string]$User = 'DESKTOP-VSSDIT1\22900',
    [string]$WorkDir = 'C:\NganImeVm',
    [string]$TaskPrefix = 'NganIme'
)

$ErrorActionPreference = 'Continue'

function New-Task([string]$Name, [string]$Command, [string]$Arguments, [string]$UserId, [string]$LogonType, [string]$RunLevel, [switch]$AtLogOn) {
    $Action = New-ScheduledTaskAction -Execute $Command -Argument $Arguments
    $Principal = New-ScheduledTaskPrincipal -UserId $UserId -LogonType $LogonType -RunLevel $RunLevel
    $Params = @{ TaskName = $Name; Action = $Action; Principal = $Principal; Force = $true }
    if ($AtLogOn) {
        $Params.Trigger = New-ScheduledTaskTrigger -AtLogOn
        $Params.Settings = New-ScheduledTaskSettingsSet -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1)
    }
    Register-ScheduledTask @Params | Out-Null
}

# 1) 系統註冊（需要 SYSTEM／管理員寫 HKLM）。
$RegisterLog = Join-Path $WorkDir 'register.log'
New-Task -Name "$TaskPrefix`Register" -Command 'C:\Windows\System32\cmd.exe' `
    -Arguments ('/c ' + $Registrar + ' --register ' + $Dll + ' > ' + $RegisterLog + ' 2>&1') `
    -UserId 'SYSTEM' -LogonType 'ServiceAccount' -RunLevel 'Highest'
Start-ScheduledTask -TaskName "$TaskPrefix`Register"
Start-Sleep -Seconds 8

# 2) 加入指定使用者的輸入法清單。
$UserLog = Join-Path $WorkDir 'install-user.log'
New-Task -Name "$TaskPrefix`InstallUser" -Command 'C:\Windows\System32\cmd.exe' `
    -Arguments ('/c ' + $Registrar + ' --install-user > ' + $UserLog + ' 2>&1') `
    -UserId $User -LogonType 'Interactive' -RunLevel 'Limited' -AtLogOn
Start-ScheduledTask -TaskName "$TaskPrefix`InstallUser"
Start-Sleep -Seconds 4

# 3) 常駐宿主：host.bat 設好環境變數後用 start /b 分離啟動，關掉視窗不會帶走行程。
$HostBat = Join-Path $WorkDir 'host.bat'
@(
    '@echo off',
    ('set NGAN_RIME_DLL=' + $RimeDll),
    ('set NGAN_RIME_USER_DATA=' + $RimeUserData),
    ('start "" /b "' + $HostExe + '" --tsf')
) | Set-Content -Encoding ascii $HostBat
New-Task -Name "$TaskPrefix`Server" -Command $HostBat -Arguments '' `
    -UserId $User -LogonType 'Interactive' -RunLevel 'Limited' -AtLogOn
Start-ScheduledTask -TaskName "$TaskPrefix`Server"
Start-Sleep -Seconds 6

Write-Output 'register-install-start 完成；可用 pipetest.ps1 驗證管道。'
