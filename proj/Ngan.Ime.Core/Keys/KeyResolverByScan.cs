namespace Ngan.Ime.Core.Keys;


[Doc($$"""
把「虛擬鍵碼 ＋ 掃描碼 ＋ 擴展鍵位」還原成一個鍵；掃描碼優先。

*為何需要掃描碼*：`VK_SHIFT`（0x10）對左右 Shift 是同一個值，
但「左 Shift 切英文、右 Shift 切中文」是很常見的配置。
2026-11-15 的事故就是左右 Shift 分不出來；協議第 2 版起殼會帶掃描碼與擴展鍵位。

*為何放在 Core*：這段表是純資料 ＋ 純判斷，與 TSF、Win32 都無關；
放在 Core 才能被測試直接餵數字驗（殼與服務端都碰不到測試專案）。

*掃描碼優先於 Vk*：`Scan != 0` 時只看掃描碼。目前表裡的掃描碼各自只對應那一顆修飾鍵，
不會和字母鍵撞；新增掃描碼時必須先確認這一點，否則一個字母鍵會被當成修飾鍵。

*左右 Win 都映到 `Meta_L`*：`KeyChars` 只有一個 Meta 鍵，
而 Rime 幾乎不用 Super 的左右之分；日後若要區分，先補 `KeyChars` 與協議。

*舊版外殼沒有這兩欄*（`Scan == 0`）時退回 Vk 表，也就是「左右不分、一律算左邊」。
""")]
public static class KeyResolverByScan{

	[Doc("左 Shift 的掃描碼。")]
	public const i32 ScanShiftL = 0x2A;

	[Doc("右 Shift 的掃描碼。")]
	public const i32 ScanShiftR = 0x36;

	[Doc("Ctrl 的掃描碼；擴展鍵位為 1 時是右 Ctrl。")]
	public const i32 ScanCtrl = 0x1D;

	[Doc("Alt 的掃描碼；擴展鍵位為 1 時是右 Alt。")]
	public const i32 ScanAlt = 0x38;

	[Doc("左 Win 的掃描碼。")]
	public const i32 ScanMetaL = 0x5B;

	[Doc("右 Win 的掃描碼；目前與左 Win 一樣映到 `Meta_L`。")]
	public const i32 ScanMetaR = 0x5C;

	[Doc($$"""
	把一次按鍵還原成鍵。
	#Prm[{{nameof(Vk)}}][Win32 虛擬鍵碼。]
	#Prm[{{nameof(Scan)}}][掃描碼；0 表示外殼沒提供。]
	#Prm[{{nameof(Ext)}}][擴展鍵位（1 表示擴展鍵）。]
	#Prm[{{nameof(ByVk)}}][Vk → 鍵的後備表（左右不分的舊行為）。]
	#Rtn[對應的鍵；未登記的鍵回 null（呼叫端回報「沒吃掉」）。]
	""")]
	public static IKeyChar? Resolve(
		i32 Vk
		,i32 Scan
		,i32 Ext
		,IReadOnlyDictionary<i32, IKeyChar> ByVk
	){
		if(Scan != 0){
			switch(Scan){
				case ScanShiftL: return KeyChars.Shift_L;
				case ScanShiftR: return KeyChars.Shift_R;
				case ScanCtrl: return Ext != 0 ? KeyChars.Ctrl_R : KeyChars.Ctrl_L;
				case ScanAlt: return Ext != 0 ? KeyChars.Alt_R : KeyChars.Alt_L;
				// 左 Win 與右 Win 都歸到 Meta_L，理由見類註釋。
				case ScanMetaL:
				case ScanMetaR: return KeyChars.Meta_L;
			}
		}
		if(ByVk is null){
			return null;
		}
		return ByVk.TryGetValue(Vk, out var One) ? One : null;
	}
}
