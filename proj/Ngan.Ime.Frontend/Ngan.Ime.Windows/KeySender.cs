namespace Ngan.Ime.Windows;

using System.Runtime.InteropServices;

[Doc($$"""
Win32 `keybd_event` 的薄封裝。

*為何留一個只有一行的類*：`DllImport` 宣告與呼叫分開，測試要替換時只需換掉本類；
也讓呼叫點不必再寫 `[DllImport]`。

*與 `SendInput` 的取捨*：`SendInput` 是官方推薦的替代品，但它需要組 `INPUT` 結構、
且同樣受 UIPI 限制；本專案目前只做「非提權場景可用」的桌面預覽，
故保留更簡單的 `keybd_event`。要支援提權視窗時再整體換成 `SendInput`。

#Eg[
	```cs
	KeySender.keybd_event(0x0D, 0, 0, 0); // Enter 按下
	```
]
""")]
public static class KeySender{
	[Doc($$"""
	合成一次鍵盤事件。
	#Prm[{{nameof(bVk)}}][虛擬鍵碼。]
	#Prm[{{nameof(bScan)}}][掃描碼；通常為 0。]
	#Prm[{{nameof(dwFlags)}}][0=按下，2=擡起（`KEYEVENTF_KEYUP`）。]
	#Prm[{{nameof(dwExtraInfo)}}][附加資訊；通常為 0。]
	""")]
	[DllImport("user32.dll", SetLastError = true)]
	public static extern void keybd_event(byte bVk, byte bScan, i32 dwFlags, i32 dwExtraInfo);
}
