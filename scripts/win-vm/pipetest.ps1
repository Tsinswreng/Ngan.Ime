<#
.SYNOPSIS
    Ngan IME TSF 管道的煙霧測試：直接送按鍵碼給服務端，印回傳。
.EXAMPLE
    .\pipetest.ps1 -Keys 'K|67|0|100|100','K|32|0|100|100'
#>
param(
    [string]$PipeName = 'NganIme.Tsf.v1',
    [string[]]$Keys = @('K|67|0|100|100', 'K|32|0|100|100')
)

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
