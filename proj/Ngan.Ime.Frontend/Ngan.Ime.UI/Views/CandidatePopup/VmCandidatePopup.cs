namespace Ngan.Ime.UI.Views.CandidatePopup;

using System.Collections.Generic;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.UI.Infra;
using Tsinswreng.CsCore;

[Doc($$"""
候選浮窗的 ViewModel：只描述「顯示甚麼」，不碰視窗定位與控件。

*為何與 {{nameof(ImeUiState)}} 分開*：那個是鍵盤界面的共享狀態（任何時候都在），
本類是一個轉瞬即逝的浮窗內容（候選來了才有）。混在一起會讓鍵盤界面跟著浮窗抖動。

*候選清單為何是 {{nameof(ICandidate)}} 而不是自訂型別*：引擎給的就是它，
浮窗只是顯示端；換引擎不必改這裡。

#Eg[
	```cs
	Vm.Apply("ni", Page);
	```
]
""")]
public partial class VmCandidatePopup : AppVmBase, IMk<VmCandidatePopup>{

	[Doc("組字串；空串表示目前沒有組字。")]
	public str Preedit{
		get;
		set{ SetProperty(ref field, value); }
	} = "";

	[Doc("本頁候選；永不為 null。")]
	public IList<ICandidate> Candidates{
		get;
		set{ SetProperty(ref field, value); }
	} = [];

	[Doc("高亮候選的序號；沒有候選時為 -1。")]
	public i32 Highlighted{
		get;
		set{ SetProperty(ref field, value); }
	} = -1;

	[Doc("是否還有內容可顯示；組字與候選都沒有時為 false。")]
	public bool HasContent{
		get;
		set{ SetProperty(ref field, value); }
	}

	[Doc("建立無依賴實例；無參建構子供 IMk 與測試使用。")]
	protected VmCandidatePopup(){}

	[Doc("建立無依賴實例；測試與設計器用它，不必先建容器。")]
	public static partial VmCandidatePopup Mk();

	[Doc($$"""
	一次快照套用完成的通知。

	*為何不是逐屬性訂閱*：{{nameof(Apply)}} 會改四個屬性，逐屬性通知等於一次按鍵刷新四次浮窗
	（每次重建行文字、可能觸發排版）。以「整份快照套完」為一個單位通知，浮窗每顆鍵只刷新一次。
	事件由 {{nameof(Apply)}}／{{nameof(Clear)}} 在最後一步發出；例外或提早返回時也要發（用 finally）。
	""")]
	public event Action? Applied;

	[Doc("用一次快照覆蓋本 VM 的狀態。")]
	public partial void Apply(str Preedit, CandidatePage? Page);

	[Doc("清空內容（不影響浮窗本身的可見性，由視圖決定）。")]
	public partial void Clear();
}
