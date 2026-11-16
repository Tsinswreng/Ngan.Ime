<#
.SYNOPSIS
    Ngan IME TSF 管道的煙霧測試：直接送按鍵請求給服務端，印回傳。
.DESCRIPTION
    管道名不是固定的：每個安裝（或每個版本識別）一條。本腳本依 -AppDir 指向的目錄
    （服務端 exe 所在目錄）算出同一個名稱；也可以用 -PipeName 直接指定。
    協議版本同理：預設先問服務端（送一條版本 0 的探測，服務端會在回應裡帶自己的版本），
    所以協議升級之後這支腳本不必跟著改；-Version 可以覆蓋。
.EXAMPLE
    .\pipetest.ps1 -AppDir 'C:\NganImeVm\apps\ngan-server-x86'
.EXAMPLE
    .\pipetest.ps1 -AppDir "$env:LOCALAPPDATA\NganIme\v20261115-151236" -Version 2
#>
param(
    [string]$AppDir = 'C:\NganImeVm\apps\ngan-server-x86',
    [string]$PipeName = '',
    # 0 表示「問服務端」。協議版本只有一個出處（C# 的 `TsfIpc.Version`），
    # 腳本不抄常量；要驗某個特定版本才手動指定。
    [int]$Version = 0,
    # 空陣列表示用協議版本自動組兩個樣本鍵（'C' 與空白鍵）。
    [string[]]$Keys = @()
)

# 與 TsfIpc.PipeNameFor 相同的演算法：identity.txt 的 clsid 優先；
# 沒有 identity.txt 時回**固定的前綴**（C# 的 PipeNameFrom 就是這樣做的，不是目錄雜湊）。
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
        # 開發／手動註冊情境：外殼與服務端分別在不同目錄，沒有共同識別，
        # 兩邊都會算出這個固定名稱（見 TsfIpc.PipeNameFrom 的註釋）。
        return 'NganIme.Tsf.v1'
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

# 一次交易：連線、送一行、讀一行、關閉。
function Send-Key([string]$Name, [string]$Payload) {
    $Client = New-Object System.IO.Pipes.NamedPipeClientStream('.', $Name)
    try {
        $Client.Connect(3000)
        $Writer = New-Object System.IO.StreamWriter($Client)
        $Writer.AutoFlush = $true
        $Reader = New-Object System.IO.StreamReader($Client)
        $Writer.WriteLine($Payload)
        return $Reader.ReadLine()
    }
    finally {
        $Client.Dispose()
    }
}

# 問服務端的協議版本：送一條版本 0 的 ECHO，服務端會回 UnsupportedVersion，
# 但回應第二欄就是它自己的版本號。取不到就回 0。
function Get-ServerVersion([string]$Name) {
    try {
        $Line = Send-Key $Name 'E|0'
        if ($Line) {
            $Fields = $Line.Split('|')
            if ($Fields.Length -ge 2 -and $Fields[0] -eq 'R') { return [int]$Fields[1] }
        }
    }
    catch { }
    return 0
}

if ([string]::IsNullOrWhiteSpace($PipeName)) {
    $PipeName = Get-NganPipeName $AppDir
}
Write-Host "管道：$PipeName"

if ($Version -le 0) {
    $Version = Get-ServerVersion $PipeName
    if ($Version -le 0) {
        Write-Host '無法從服務端取得協議版本（連不上，或回應格式不對）。請確認服務端在跑，或用 -Version 指定。'
        exit 2
    }
}
Write-Host "協議版本：$Version"

if ($Keys.Count -eq 0) {
    $Keys = @("K|$Version|67|0|100|100|0", "K|$Version|32|0|100|100|0")
}

foreach ($Key in $Keys) {
    try {
        $Reply = Send-Key $PipeName $Key
        "{0} -> {1}" -f $Key, $Reply
    }
    catch {
        "{0} -> ERROR: {1}" -f $Key, $_.Exception.Message
    }
}
