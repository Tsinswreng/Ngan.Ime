using Avalonia.Controls;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.UI.Views.KeyBoard;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.Layout;

[Doc($$"""
被測對象：`{{nameof(KeyLayout)}}`，鍵盤布局的資料描述。

測三件事：字母層與數字層都排六排、每排鍵數不超過宣告的欄數、布局用到的鍵都在
`{{nameof(KeyChars)}}` 登記表內。

布局是純資料，建 `{{nameof(ColumnDefinitions)}}` 只為把欄數算出來，不需要啟動 UI。
""")]
public class TestKeyLayout : Tester{
	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(BothLayers_Should_HaveSixRows), BothLayers_Should_HaveSixRows!);
		this.AddTestCase(nameof(EachRow_Should_FitColumns), EachRow_Should_FitColumns!);
		this.AddTestCase(nameof(EachRow_Keys_Should_BeRegistered), EachRow_Keys_Should_BeRegistered!);
	}

	[Doc("""
	兩層布局都應是六排；排數改變會讓界面高度與既有截圖全對不上。
	""")]
	public async Task<nil> BothLayers_Should_HaveSixRows(obj? O){
		var T = Assert.IsTrue;
		T(KeyLayout.MainRows.Count == 6, $"字母層應為六排，實際 {KeyLayout.MainRows.Count}");
		T(KeyLayout.NumRows.Count == 6, $"數字層應為六排，實際 {KeyLayout.NumRows.Count}");
		return NIL;
	}

	[Doc("""
	每一排的鍵數不得超過該排宣告的欄數，超過就會有的鍵排不下。
	""")]
	public async Task<nil> EachRow_Should_FitColumns(obj? O){
		var T = Assert.IsTrue;
		foreach(var Rows in new[]{ KeyLayout.MainRows, KeyLayout.NumRows }){
			foreach(var Row in Rows){
				var Cols = new ColumnDefinitions(Row.Cols).Count;
				T(Row.Keys.Count <= Cols, $"一排放不下：{Row.Keys.Count} 鍵 / {Cols} 欄");
			}
		}
		return NIL;
	}

	[Doc("""
	布局裏出現的每個鍵都必須在登記表內，否則轉 Rime 鍵碼時取不到定義。
	""")]
	public async Task<nil> EachRow_Keys_Should_BeRegistered(obj? O){
		var T = Assert.IsTrue;
		foreach(var Rows in new[]{ KeyLayout.MainRows, KeyLayout.NumRows }){
			foreach(var Row in Rows){
				foreach(var Key in Row.Keys){
					T(KeyChars.ByName.ContainsKey(Key.Key.Name), $"布局用了未登記的鍵：{Key.Key.Name}");
				}
			}
		}
		return NIL;
	}
}
