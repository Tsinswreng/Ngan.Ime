namespace Ngan.Ime.UI.Views.CandidatePopup;

using Ngan.Ime.Core.Ime;

[Doc($$"""
{{nameof(VmCandidatePopup)}} 的實現。

*覆蓋而非累加*：每一顆按鍵都送一份完整快照過來，故這裡一律整份替換。
不累加、不保留上一輪的殘留，否則退格刪到空之後會留下舊候選。
""")]
public partial class VmCandidatePopup{

	[Doc("建立無依賴實例；測試與設計器用它，不必先建容器。")]
	public static partial VmCandidatePopup Mk(){
		return new VmCandidatePopup();
	}

	[Doc("以快照覆蓋組字串、候選與高亮；高亮超出範圍時退回第一個。最後發一次 Applied。")]
	public partial void Apply(str Preedit, CandidatePage? Page){
		try{
			this.Preedit = Preedit ?? "";
			var Items = Page?.Data;
			Candidates = Items is null ? [] : [.. Items];
			var Highlight = Page?.HighlightedIndex ?? -1;
			if(Highlight < 0 || Highlight >= Candidates.Count){
				Highlight = Candidates.Count > 0 ? 0 : -1;
			}
			Highlighted = Highlight;
			HasContent = this.Preedit.Length > 0 || Candidates.Count > 0;
		}finally{
			// 不管中途哪一步拋，浮窗都要收到一次通知，否則畫面會停在上一次的快照。
			Applied?.Invoke();
		}
	}

	[Doc("清空所有內容。最後發一次 Applied。")]
	public partial void Clear(){
		try{
			Preedit = "";
			Candidates = [];
			Highlighted = -1;
			HasContent = false;
		}finally{
			Applied?.Invoke();
		}
	}
}
