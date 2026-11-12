param(
    [int]$ProcessId = 4708,
    [string]$OutPath
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Cap {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@

[Win32Cap]::SetProcessDPIAware() | Out-Null

$Process = Get-Process -Id $ProcessId
$Handle = $Process.MainWindowHandle
if ($Handle -eq [IntPtr]::Zero) { throw 'VMware window handle not found.' }
if ([Win32Cap]::IsIconic($Handle)) { throw 'VMware window is minimized; restore it first.' }

$Rect = New-Object Win32Cap+RECT
[Win32Cap]::GetWindowRect($Handle, [ref]$Rect) | Out-Null
$Width = $Rect.Right - $Rect.Left
$Height = $Rect.Bottom - $Rect.Top
if ($Width -le 0 -or $Height -le 0) { throw "Invalid window size: ${Width}x${Height}" }

$Bitmap = New-Object System.Drawing.Bitmap $Width, $Height
$Graphics = [System.Drawing.Graphics]::FromImage($Bitmap)
$Graphics.CopyFromScreen($Rect.Left, $Rect.Top, 0, 0, $Bitmap.Size)
$Graphics.Dispose()
$Bitmap.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$Bitmap.Dispose()
Write-Output "Saved $OutPath (${Width}x${Height})"
