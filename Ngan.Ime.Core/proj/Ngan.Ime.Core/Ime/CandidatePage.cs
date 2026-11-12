namespace Ngan.Ime.Core.Ime;

using Tsinswreng.CsCore;
using Tsinswreng.CsPage;

[Doc($$"""
一頁候選詞的快照：候選清單＋分頁與高亮資訊。

*為何是快照而不是活動對象*：引擎的候選在 native context 釋放後就失效，
故每次同步都把需要的欄位複製成托管物件；界面只讀這份快照，不會碰到原生記憶體。

#Eg[
	```cs
	var Page = new CandidatePage{ Data = [new Candidate{ Text = "你" }], PageSize = 5 };
	```
]
""")]
public class CandidatePage : IPage<ICandidate>{

	[Doc("當前頁內高亮候選的下標（從 0 起）；沒有高亮時為 -1 或引擎給的值。")]
	public i32 HighlightedIndex{get;set;}

	[Doc("是否為最後一頁。")]
	public bool IsLastPage{get;set;}

	#region IPage

	[Doc("本頁候選；永不為 null。")]
	public IList<ICandidate>? Data{get;set;} = [];

	[Doc("每頁候選數；0 表示引擎未提供。")]
	public u64 PageSize{get;set;}

	[Doc("候選總數；是否可信取決於各引擎。")]
	public u64 TotCnt { get;set; }

	[Doc("當前頁號，從 0 起。")]
	public u64 PageIdx { get;set; }

	[Doc("總數是否已知。")]
	public bool HasTotCnt { get;set; }

	#endregion
}
