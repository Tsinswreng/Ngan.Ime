<#
.SYNOPSIS
    Ngan IME TSF 管道的煙霧測試：直接送按鍵碼給服務端，印回傳。
.DESCRIPTION
    管道名不是固定的：每個安裝（或每個版本識別）一條。本腳本依 -AppDir 指向的目錄
    （服務端 exe 所在目錄）算出同一個名稱；也可以用 -PipeName 直接指定。
.EXAMPLE
    .\pipetest.ps1 -AppDir 'C:\NganImeVm\apps\ngan-server-x86'
.EXAMPLE
    .\pipetest.ps1 -AppDir "$env:LOCALAPPDATA\NganIme\v20261115-151236"
#>
param(
    [string]$AppDir = 'C:\NganImeVm\apps\ngan-server-x86',
    [string]$PipeName = '',
    [string[]]$Keys = @('K|1|67|0|100|100|0', 'K|1|32|0|100|100|0')
)

# 與 TsfIpc.PipeNameFor 相同的演算法：identity.txt 的 clsid 優先，否則用目錄本身；
# 再取 FNV-1a 32 位元的十六進位。
function Get-NganPipeName([string]$Dir) {
    $Key = $null
    $Identity = Join-Path $Dir 'identity.txt'
    if (Test-Path -LiteralPath $Identity) {
        foreach ($Line in Get-Content -LiteralPath $Identity) {
            $Index = $Line.IndexOf('=')
            if ($Index -le 0) { continue }
            if ($Line.Substring(0, $Index).Trim() -eq 'clsid') {
                $Value = $Line.Substring($Index + 1).Trim()
                if ($Value.Length -gt 0) { $Key = 'clsid:' + $Value.ToUpperInvariant() }
                break
            }
        }
    }
    if (-not $Key) {
        $Key = 'dir:' + $Dir.Trim().TrimEnd('\', '/').ToUpperInvariant()
    }
    $Hash = [uint32]2166136261
    foreach ($Ch in $Key.ToCharArray()) {
        # 用 % 2^32 而不是 -band 0xFFFFFFFF：PowerShell 會把 0xFFFFFFFF 當成 Int32 的 -1，
        # 遮罩等於沒做，乘完就溢出成 Int64 而丟掉低位。
        $Hash = [uint32](([uint64]$Hash -bxor [uint64][int]$Ch))
        $Hash = [uint32](([uint64]$Hash * 16777619) % 4294967296)
    }
    return ('NganIme.Tsf.v1.{0:x8}' -f $Hash)
}

if ([string]::IsNullOrWhiteSpace($PipeName)) {
    $PipeName = Get-NganPipeName $AppDir
}
Write-Host "管道：$PipeName"

foreach ($Key in $Keys) {
    try {
        $Client = New-Object System.IO.Pipes.NamedPipeClientStream('.', $PipeName)
        $Client.Connect(3000)
        $Writer = New-Object System.IO.StreamWriter($Client)
        $Writer.AutoFlush = $true
        $Reader = New-Object System.IO.StreamReader($Client)
        $Writer.WriteLine($Key)
        $Reply = $Reader.ReadLine()
        $Client.Dispose()
        "{0} -> {1}" -f $Key, $Reply
    }
    catch {
        "{0} -> ERROR: {1}" -f $Key, $_.Exception.Message
    }
}
