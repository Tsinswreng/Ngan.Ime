namespace Ngan.Ime;

using Tsinswreng.CsCore;

[Doc($$"""
本應用的異常標籤——只宣告「例外情況」。

*約定：不帶標籤 ＝ 公開可見的業務異常*。這是絕大多數錯誤的形態，也是 {{nameof(KeysErr)}} 全部鍵的默認：
`ErrNode.Mk` 不加任何東西就表示「可示予用戶、由前端按鍵翻文案」。
故這裡不提供 BizErr／Public 之類的「正向」標籤——那會讓最常見的情況反而要多說一句。

只有兩種情況需要標籤：
1. 這個錯誤**不可**示予用戶（消息含內部細節、路徑、堆棧等）→ 加 {{nameof(Private)}}；
2. 這是系統級故障（IO、外部服務、啟動期不變式被破壞）→ 加 {{nameof(SysErr)}}。

*標籤為何在應用這一側*：標籤是應用的分類策略，不是異常機制的必需品；
`Tsinswreng.CsErr` 只認「一串字符串標籤」，不規定詞彙。各應用自己聲明自己的一份。
""")]
public static class AppErrTags {
	[Doc("系統異常：IO、外部服務、啟動設定缺失等。對外只給一句通用提示。")]
	public static str SysErr = nameof(SysErr);

	[Doc("不可示予用戶：詳情只進日誌，回應裡不帶。沒標這個就是默認可示人。")]
	public static str Private = nameof(Private);
}
