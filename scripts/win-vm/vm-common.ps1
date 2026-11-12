<#
.SYNOPSIS
    VMware guest 操作的小包裝，省去每次手打一長串 vmrun 參數。
.DESCRIPTION
    預設值對應目前這台 Win10 POC 虛擬機；換機器時改下面三個變數即可。
#>

$script:VmRunExe = 'D:\SOFT\VMware\VMware_Workstation17\vmrun.exe'
$script:VmxPath = 'E:\_\VM\Win10\Windows10x64.vmx'
$script:GuestUser = 'Tsinswreng'
$script:GuestPass = 'Tsinswreng'

function Invoke-VmRun {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$VmArgs)
    & $script:VmRunExe -T ws @VmArgs
}

function Copy-ToGuest {
    param([string]$Local, [string]$Remote)
    Invoke-VmRun -VmArgs @('-gu', $script:GuestUser, '-gp', $script:GuestPass,
        'copyFileFromHostToGuest', $script:VmxPath, $Local, $Remote)
}

function Copy-FromGuest {
    param([string]$Remote, [string]$Local)
    Invoke-VmRun -VmArgs @('-gu', $script:GuestUser, '-gp', $script:GuestPass,
        'copyFileFromGuestToHost', $script:VmxPath, $Remote, $Local)
}

function Invoke-Guest {
    param([string]$Command, [string]$Arguments = '/c exit 0')
    Invoke-VmRun -VmArgs @('-gu', $script:GuestUser, '-gp', $script:GuestPass,
        'runProgramInGuest', $script:VmxPath, $Command, $Arguments)
}

function Get-GuestProcesses {
    Invoke-VmRun -VmArgs @('-gu', $script:GuestUser, '-gp', $script:GuestPass,
        'listProcessesInGuest', $script:VmxPath)
}
