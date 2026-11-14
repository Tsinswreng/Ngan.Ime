using Ngan.Ime.Core.Keys;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.Keys;

[Doc($$"""
被測對象：`{{nameof(KeyChar)}}` 的值語義。

測兩件事：同名即相等（大小寫有別）、可作為字典鍵命中同一個條目。
""")]
public class TestKeyChar : Tester{
	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(Equals_Should_CompareByName), Equals_Should_CompareByName!);
		this.AddTestCase(nameof(AsDictKey_Should_HitByName), AsDictKey_Should_HitByName!);
	}

	[Doc("""
	兩個 `KeyChar` 只要名字相同就相等；名字大小寫不同則不相等。
	""")]
	public async Task<nil> Equals_Should_CompareByName(obj? O){
		var T = Assert.IsTrue;
		var Copy = new KeyChar("a");
		T(Copy.Equals(KeyChars.LowerA), "new KeyChar(\"a\") 應等於 KeyChars.LowerA");
		T(!Copy.Equals(KeyChars.A), "\"a\" 不應等於 \"A\"");
		return NIL;
	}

	[Doc("""
	以 `KeyChar` 作字典鍵時，用另一個同名實例也能命中。
	""")]
	public async Task<nil> AsDictKey_Should_HitByName(obj? O){
		var T = Assert.IsTrue;
		var Map = new Dictionary<IKeyChar, i32>{ [KeyChars.LowerA] = 1 };
		var Hit = Map.GetValueOrDefault(new KeyChar("a"));
		T(Hit == 1, $"字典應以鍵名命中，實際取到 {Hit}");
		return NIL;
	}
}
