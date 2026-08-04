using System.Runtime.InteropServices;

namespace Ngan.Ime.Windows;

unsafe public class KeySender{
	/// <summary>
	/// keybd_event((byte)Keys.Enter, 0, 0, 0); // 按下
	/// keybd_event((byte)Keys.Enter, 0, 2, 0); // 释放
	/// </summary>
	/// <param name="bVk">虛擬鍵碼</param>
	/// <param name="bScan">通常設0</param>
	/// <param name="dwFlags">0按下 2釋放</param>
	/// <param name="dwExtraInfo">一般設0</param>

	[DllImport("user32.dll")]
	public static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);


}