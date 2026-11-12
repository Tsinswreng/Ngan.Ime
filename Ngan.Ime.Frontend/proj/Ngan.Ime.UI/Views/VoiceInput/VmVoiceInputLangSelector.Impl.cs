//Impl: 聲明見 VmVoiceInputLangSelector.cs。
namespace Ngan.Ime.UI.Views.VoiceInput;

using System.Collections.ObjectModel;
using Ngan.Ime.Core.Language;

public partial class VmVoiceInputLangSelector{

	[Doc("目錄複製成陣列，顯示期間不再查詢 Provider。")]
	public partial void LoadRows(IEnumerable<INormLang> Rows){
		var Snapshot = Rows.ToArray();
		SourceRows = Snapshot;
		this.Rows = new ObservableCollection<INormLang>(Snapshot);
	}

	[Doc("清空關鍵字並恢復 Provider 原始順序。")]
	public partial void ClearSearch(){
		Input = "";
		Rows = new ObservableCollection<INormLang>(SourceRows);
	}

	[Doc("代碼永遠可搜索；本地名／英文名只在實現 INormLangDetail 時參與匹配。")]
	public partial void Search(){
		var Query = Input.Trim();
		if(str.IsNullOrWhiteSpace(Query)){
			ClearSearch();
			return;
		}
		Rows = new ObservableCollection<INormLang>(SourceRows.Where(Lang =>
			Lang.Code.Contains(Query, StringComparison.OrdinalIgnoreCase)
			|| (Lang is INormLangDetail Detail && (
				Detail.NativeName.Contains(Query, StringComparison.OrdinalIgnoreCase)
				|| Detail.EnglishName.Contains(Query, StringComparison.OrdinalIgnoreCase)
			))
		));
	}
}
