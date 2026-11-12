namespace Ngan.Ime.UI.Views.KeyBoard;

using Ngan.Ime.Core.Keys;
using Tsinswreng.CsCore;

[Doc($$"""
一個鍵位在布局中的完整描述：送哪個鍵、鍵帽顯示什麼、五個手勢各做什麼。

*為何 Label 可空*：多數鍵的鍵帽文字就是鍵名（`q`→`Q`）；只有少數要顯示別的符號（`p`→`Π`）。
故空表示「用默認規則」，不是「沒有文字」。

#Eg[
	```cs
	KeyDef Key = new(){
		Key = KeyChars.LowerQ,
		Label = "Q",
		Gestures = KeyDef.G(EGestureSlot.SwipeUp, KeyGesture.Send(KeyChars.Q)),
	};
	```
]
""")]
public sealed class KeyDef{

	[Doc("點擊時送出的鍵。")]
	public required IKeyChar Key{get;init;}

	[Doc("鍵帽主文字；null 表示用鍵名的默認顯示規則。")]
	public str? Label{get;init;}

	[Doc("右上角提示（上檔符號等）。")]
	public str? Hint{get;init;}

	[Doc("左下角提示（手勢提示圖標）。")]
	public str? HintBottom{get;init;}

	[Doc("長按／連發是否重複送鍵（退格用）。")]
	public bool IsRepeat{get;init;}

	[Doc("手勢表；沒有掛的手勢不回應。")]
	public IReadOnlyDictionary<EGestureSlot, KeyGesture> Gestures{get;init;} =
		new Dictionary<EGestureSlot, KeyGesture>();

	[Doc("取某個手勢；沒掛時回 null。")]
	public KeyGesture? GetGesture(EGestureSlot Slot){
		return Gestures.GetValueOrDefault(Slot);
	}

	[Doc("建立手勢表的輔助：把 (位置, 手勢) 對收成字典，避免每個鍵寫一大段初始化器。")]
	public static IReadOnlyDictionary<EGestureSlot, KeyGesture> G(params (EGestureSlot Slot, KeyGesture Gesture)[] Items){
		var Map = new Dictionary<EGestureSlot, KeyGesture>(Items.Length);
		foreach(var One in Items){
			Map[One.Slot] = One.Gesture;
		}
		return Map;
	}
}

[Doc($$"""
布局中的一排鍵。

*為何帶 {{nameof(Cols)}}*：每排的欄寬比例不同（例如第五排 Enter 佔兩格），
把它跟鍵清單放在一起，View 才能完全由資料驅動、不必為每一排寫一個方法。

#Eg[
	```cs
	KeyRow Row = new(){ Cols = "*,*,*,*,*,*,*,*,*,*", Keys = [ /* … */ ] };
	```
]
""")]
public sealed class KeyRow{

	[Doc("欄寬定義字符串（Avalonia `ColumnDefinitions` 語法）。")]
	public required str Cols{get;init;}

	[Doc("本排的鍵，依序填入欄位。")]
	public required IReadOnlyList<KeyDef> Keys{get;init;}
}
