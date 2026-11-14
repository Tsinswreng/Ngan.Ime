using Ngan.Ime.Core.Keys;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.Keys;

[Doc($$"""
被測對象：`{{nameof(KeyChars)}}`，按鍵登記表。

測三件事：每個鍵名非空且全表唯一、`ByName` 索引覆蓋全表、`Find` 查得到已登記鍵且查不到未登記鍵。
""")]
public class TestKeyChars : Tester{
	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(All_Should_HaveNonEmptyUniqueNames), All_Should_HaveNonEmptyUniqueNames!);
		this.AddTestCase(nameof(ByName_Should_IndexAll), ByName_Should_IndexAll!);
		this.AddTestCase(nameof(Find_Should_ResolveRegisteredKey), Find_Should_ResolveRegisteredKey!);
	}

	[Doc("""
	全表每個鍵的名字都要非空且不重複。

	重名的後果：`ByName` 是字典，同名會靜默覆蓋，查找結果取決於登記順序。
	""")]
	public async Task<nil> All_Should_HaveNonEmptyUniqueNames(obj? O){
		var T = Assert.IsTrue;
		var Names = new HashSet<str>(StringComparer.Ordinal);
		foreach(var Key in KeyChars.All){
			T(!string.IsNullOrEmpty(Key.Name), "KeyChars.All 不得有空鍵名");
			T(Names.Add(Key.Name), $"KeyChars 鍵名重複：{Key.Name}");
		}
		return NIL;
	}

	[Doc("""
	`ByName` 的條目數要與 `All` 一致，否則說明有鍵沒進索引。
	""")]
	public async Task<nil> ByName_Should_IndexAll(obj? O){
		var T = Assert.IsTrue;
		var Indexed = KeyChars.ByName.Count;
		var Total = KeyChars.All.Count;
		T(Indexed == Total, $"ByName 數量 {Indexed} 應等於 All 數量 {Total}");
		return NIL;
	}

	[Doc("""
	`Find` 對已登記鍵要回同一個實例，對未登記鍵要回空。
	""")]
	public async Task<nil> Find_Should_ResolveRegisteredKey(obj? O){
		var T = Assert.IsTrue;
		T(ReferenceEquals(KeyChars.Find("Enter"), KeyChars.Enter), "Find(\"Enter\") 應回同一個實例");
		T(KeyChars.Find("NotAKey") is null, "Find 未登記鍵應回 null");
		return NIL;
	}
}
