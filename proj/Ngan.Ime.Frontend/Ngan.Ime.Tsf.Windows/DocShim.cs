namespace Tsinswreng.CsCore;

using System;
using System.Diagnostics;

/// <summary>
/// `Doc` 屬性在本專案的替身。
/// </summary>
/// <remarks>
/// 本專案是「不含 CLR 的原生 COM 組件」，刻意不引用任何托管程式庫（見 csproj 的
/// `ProjectReference Remove`），因此拿不到 `Tsinswreng.CsCore` 裡真正的 `DocAttribute`。
///
/// 但共用檔 `proj/_shared/TsfIpc.cs` 同時編進本專案、服務端與測試，
/// 若不給它一個能解析的 `Doc`，共用檔就得改寫成純 XML 註釋，
/// 三處的註釋風格會因此分叉。
///
/// 故在此宣告一個最小替身：名稱、命名空間、建構子與真品一致，
/// 且同樣標上 `[Conditional("COMPILE_TIME_ONLY")]`，在正常建置下不會被發出，
/// 對產物沒有影響。本替身只在本專案可見，不會污染其他程式集。
/// </remarks>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
[Conditional("COMPILE_TIME_ONLY")]
public sealed class DocAttribute : Attribute{

	/// <summary>註釋內容。</summary>
	public str Summary{get;}

	/// <summary>以註釋內容建立屬性。</summary>
	/// <param name="Summary">註釋文本。</param>
	public DocAttribute(str Summary){
		this.Summary = Summary;
	}
}
