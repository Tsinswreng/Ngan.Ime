using Ngan.Ime.Core.Keys;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.Keys;

[Doc($$"""
被測對象：{{nameof(KeyResolverByScan)}}，即「掃描碼 → 鍵」的還原規則。

2026-11-15 的事故是左右 Shift 分不出來（`VK_SHIFT` 對左右是同一個值）。
這些用例把掃描碼表逐條釘死；日後有人動表，這裡會先紅。
""")]
public class TestKeyResolverByScan : Tester{

	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(Shift_Should_SplitLeftRight), Shift_Should_SplitLeftRight!);
		this.AddTestCase(nameof(CtrlAlt_Should_UseExtensionBit), CtrlAlt_Should_UseExtensionBit!);
		this.AddTestCase(nameof(Meta_Should_MapBothSides), Meta_Should_MapBothSides!);
		this.AddTestCase(nameof(NoScan_Should_FallBackToVk), NoScan_Should_FallBackToVk!);
		this.AddTestCase(nameof(UnknownScan_Should_FallBackToVk), UnknownScan_Should_FallBackToVk!);
		this.AddTestCase(nameof(LetterWithScan_Should_UseScanOnlyForModifiers), LetterWithScan_Should_UseScanOnlyForModifiers!);
	}

	[Doc("""
	左 Shift 與右 Shift 的掃描碼不同，必須還原成不同的鍵。
	""")]
	public async Task<nil> Shift_Should_SplitLeftRight(obj? O){
		var T = Assert.IsTrue;
		var L = KeyResolverByScan.Resolve(0x10, KeyResolverByScan.ScanShiftL, 0, Map());
		var R = KeyResolverByScan.Resolve(0x10, KeyResolverByScan.ScanShiftR, 0, Map());
		T(L == KeyChars.Shift_L, $"掃描碼 0x2A 應為左 Shift，實際 {L}");
		T(R == KeyChars.Shift_R, $"掃描碼 0x36 應為右 Shift，實際 {R}");
		T(!Equals(L, R), "左右 Shift 必須是不同的鍵");
		return NIL;
	}

	[Doc("""
	Ctrl／Alt 靠擴展鍵位分左右。
	""")]
	public async Task<nil> CtrlAlt_Should_UseExtensionBit(obj? O){
		var T = Assert.IsTrue;
		T(KeyResolverByScan.Resolve(0x11, KeyResolverByScan.ScanCtrl, 0, Map()) == KeyChars.Ctrl_L, "非擴展的 Ctrl 應為左");
		T(KeyResolverByScan.Resolve(0x11, KeyResolverByScan.ScanCtrl, 1, Map()) == KeyChars.Ctrl_R, "擴展的 Ctrl 應為右");
		T(KeyResolverByScan.Resolve(0x12, KeyResolverByScan.ScanAlt, 0, Map()) == KeyChars.Alt_L, "非擴展的 Alt 應為左");
		T(KeyResolverByScan.Resolve(0x12, KeyResolverByScan.ScanAlt, 1, Map()) == KeyChars.Alt_R, "擴展的 Alt 應為右");
		return NIL;
	}

	[Doc("""
	左右 Win 目前都映到 `Meta_L`（`KeyChars` 沒有左右之分）。
	""")]
	public async Task<nil> Meta_Should_MapBothSides(obj? O){
		var T = Assert.IsTrue;
		T(KeyResolverByScan.Resolve(0x5B, KeyResolverByScan.ScanMetaL, 0, Map()) == KeyChars.Meta_L, "左 Win 應為 Meta_L");
		T(KeyResolverByScan.Resolve(0x5C, KeyResolverByScan.ScanMetaR, 0, Map()) == KeyChars.Meta_L, "右 Win 應為 Meta_L");
		return NIL;
	}

	[Doc("""
	沒有掃描碼（舊版外殼）時退回 Vk 表。
	""")]
	public async Task<nil> NoScan_Should_FallBackToVk(obj? O){
		var T = Assert.IsTrue;
		var ByVk = Map();
		T(KeyResolverByScan.Resolve(0x41, 0, 0, ByVk) == KeyChars.A, "Scan=0 時應查 Vk 表");
		T(KeyResolverByScan.Resolve(0x10, 0, 0, ByVk) == KeyChars.Shift_L, "Vk 表的 Shift 應左右不分");
		return NIL;
	}

	[Doc("""
	掃描碼不在表裡（例如字母鍵、功能鍵）時也要退回 Vk 表，不能回 null。
	""")]
	public async Task<nil> UnknownScan_Should_FallBackToVk(obj? O){
		var T = Assert.IsTrue;
		var ByVk = Map();
		T(KeyResolverByScan.Resolve(0x41, 0x1E, 0, ByVk) == KeyChars.A, "字母鍵的掃描碼不在修飾鍵表裡，應查 Vk");
		T(KeyResolverByScan.Resolve(0x70, 0x3B, 0, ByVk) == KeyChars.F1, "F1 的掃描碼也不在表裡，應查 Vk");
		return NIL;
	}

	[Doc("""
	修飾鍵的掃描碼不會被字母鍵冒用：同一個掃描碼一定還原成修飾鍵。
	（這是要提醒「新增掃描碼前先確認它不與字母鍵共用」。）
	""")]
	public async Task<nil> LetterWithScan_Should_UseScanOnlyForModifiers(obj? O){
		var T = Assert.IsTrue;
		// 0x2A 永遠是左 Shift，即使 Vk 送的是別的鍵：掃描碼優先。
		var Key = KeyResolverByScan.Resolve(0x41, KeyResolverByScan.ScanShiftL, 0, Map());
		T(Key == KeyChars.Shift_L, $"掃描碼 0x2A 應優先於 Vk，實際 {Key}");
		return NIL;
	}

	[Doc("""
	測試用的 Vk → 鍵表；只放本組用例會用到的幾個鍵。
	""")]
	static Dictionary<i32, IKeyChar> Map(){
		return new Dictionary<i32, IKeyChar>{
			[0x10] = KeyChars.Shift_L,
			[0x11] = KeyChars.Ctrl_L,
			[0x12] = KeyChars.Alt_L,
			[0x41] = KeyChars.A,
			[0x70] = KeyChars.F1,
			[0x5B] = KeyChars.Meta_L,
			[0x5C] = KeyChars.Meta_L,
		};
	}
}
