# Rebuild and replace the Windows package while its shell DLL is in use.
#
# Why this exists:
#   PubWinX86 wipes its output directory first, and that directory is also the
#   install directory on this machine. Ngan.Ime.Tsf.dll is loaded by every process
#   that has used the IME (explorer, editors, chat apps), so the DLL stays locked
#   and the directory cannot be deleted. This script finds the holders, stops them,
#   runs PubWinX86, then brings explorer and the server back.
#
# Usage:
#   .\pub-and-replace.ps1            stop only explorer and the server, then publish
#   .\pub-and-replace.ps1 -All       also stop every process holding the shell DLL
#   .\pub-and-replace.ps1 -All -Yes  same, without the confirmation prompt
#
# Notes:
#   - Stopped programs are not restarted; unsaved work is lost. -All lists them first.
#   - Processes already running keep the old shell; test with a newly opened window.
#   - Run this in a normal (non-elevated) PowerShell so explorer stays in your session.
#
# This file is ASCII-only on purpose: Windows PowerShell 5.1 reads a .ps1 without a
# UTF-8 BOM as ANSI, and non-ASCII comments then break the parser.

param(
    [switch]$All,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$PublishDir = Join-Path $ProjectRoot 'artifacts\publish\win'
$ShellDll = Join-Path $PublishDir 'Ngan.Ime.Tsf.dll'
$ServerExe = Join-Path $PublishDir 'Ngan.Ime.Windows.exe'

# Never stop these, even if they hold the DLL.
$ProtectedNames = @(
    'System', 'Idle', 'smss', 'csrss', 'wininit', 'winlogon', 'services', 'lsass',
    'svchost', 'dwm', 'fontdrvhost', 'ctfmon', 'sihost', 'LogonUI', 'MsMpEng',
    'powershell', 'pwsh', 'cmd', 'conhost', 'WindowsTerminal', 'wt'
)

function Find-Holders([string]$DllPath) {
    $Names = [IO.Path]::GetFileName($DllPath)
    $Result = @()
    foreach ($Proc in Get-Process -ErrorAction SilentlyContinue) {
        if ($Proc.Id -eq $PID) { continue }
        try {
            foreach ($Module in $Proc.Modules) {
                if ($Module.ModuleName -ieq $Names) {
                    $ProcPath = ''
                    try { $ProcPath = $Proc.Path } catch { }
                    $Result += [pscustomobject]@{
                        Id   = $Proc.Id
                        Name = $Proc.ProcessName
                        Path = $ProcPath
                    }
                    break
                }
            }
        }
        catch {
            # Protected or already exiting: cannot read modules, skip it.
        }
    }
    return $Result
}

Write-Host '=== Processes holding the shell DLL ==='
$Holders = Find-Holders $ShellDll
if ($Holders.Count -eq 0) {
    Write-Host '  (none)'
}
else {
    $Holders | ForEach-Object { Write-Host ("  {0,-6} {1}" -f $_.Id, $_.Name) }
}

if ($All) {
    $ToStop = @($Holders | Where-Object { $ProtectedNames -notcontains $_.Name })
}
else {
    $ToStop = @($Holders | Where-Object { $_.Name -in @('explorer', 'Ngan.Ime.Windows') })
}

# The server exe is also locked by its own running process even when it does not
# hold the shell DLL, so always stop it before wiping the directory.
foreach ($Server in @(Get-Process 'Ngan.Ime.Windows' -ErrorAction SilentlyContinue)) {
    if ($ToStop | Where-Object { $_.Id -eq $Server.Id }) { continue }
    $ToStop += [pscustomobject]@{
        Id   = $Server.Id
        Name = $Server.ProcessName
        Path = $Server.Path
    }
}

# explorer holds the DLL only while it is running; stop it only when it is a holder
# or when -All was given. (When the directory is wiped with explorer up, it comes
# back a few lines later either way.)

if ($ToStop.Count -gt 0) {
    Write-Host ''
    Write-Host '=== About to stop ==='
    $ToStop | ForEach-Object { Write-Host ("  {0,-6} {1}" -f $_.Id, $_.Name) }
    if (-not $Yes) {
        $Answer = Read-Host 'Continue? (type yes)'
        if ($Answer -ne 'yes') {
            Write-Host 'Cancelled.'
            exit 1
        }
    }
    foreach ($Proc in $ToStop) {
        try { Stop-Process -Id $Proc.Id -Force -ErrorAction Stop }
        catch { Write-Host ("  failed to stop {0}: {1}" -f $Proc.Name, $_.Exception.Message) }
    }
    Start-Sleep -Seconds 2
}

Write-Host ''
Write-Host '=== Publish ==='
Push-Location $ProjectRoot
try {
    & dotnet run --project (Join-Path $ProjectRoot 'proj\Ngan.Ime.Scripts\Ngan.Ime.Scripts.csproj') -- PubWinX86
    $PublishExit = $LASTEXITCODE
}
finally {
    Pop-Location
}

if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) {
    Write-Host '=== Restart explorer ==='
    Start-Process explorer.exe
    Start-Sleep -Seconds 3
}

if ($PublishExit -ne 0) {
    Write-Host ''
    Write-Host '=== Publish failed ==='
    Write-Host 'Some processes still hold the shell DLL. Close them (or rerun with -All):'
    Find-Holders $ShellDll | ForEach-Object { Write-Host ("  {0,-6} {1}" -f $_.Id, $_.Name) }
    exit $PublishExit
}

if (-not (Get-Process 'Ngan.Ime.Windows' -ErrorAction SilentlyContinue)) {
    if (Test-Path -LiteralPath $ServerExe) {
        Write-Host '=== Start server ==='
        Start-Process -FilePath $ServerExe -ArgumentList '--tsf' -WorkingDirectory $PublishDir -WindowStyle Hidden
        Start-Sleep -Seconds 3
    }
}

Write-Host ''
Write-Host 'Done. Already-running processes keep the old shell; test with a new window.'
