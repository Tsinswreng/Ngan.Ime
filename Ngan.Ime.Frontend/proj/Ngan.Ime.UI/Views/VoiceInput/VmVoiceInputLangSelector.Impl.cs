using System.Collections.ObjectModel;
using Ngan.Ime.Core.Infra;
using Ngan.Ime.Core.Language;
using Ngan.Ime.Core.Voice;

namespace Ngan.Ime.UI.Views.VoiceInput;

/// 語音語言選擇的 Provider 目錄與搜索實現。
public partial class VmVoiceInputLangSelector{
	/// 建立後立即載入 Provider 的能力快照，避免 UI 自帶不可靠的靜態語言表。
	public partial VmVoiceInputLangSelector(IVoiceRecognitionProvider RecognitionProvider){
		this.RecognitionProvider = RecognitionProvider;
		LoadRows(RecognitionProvider.SupportedLanguages);
	}

	/// 兜底建立與正常 DI 路徑使用同一個 Provider。
	public static partial VmVoiceInputLangSelector Mk(){
		return new VmVoiceInputLangSelector(Di.GetRSvc<IVoiceRecognitionProvider>());
	}

	/// 將目錄複製爲穩定快照，搜索期間不重新查詢 Provider。
	public partial void LoadRows(IEnumerable<INormLang> Rows){
		var Snapshot = Rows.ToArray();
		SourceRows = Snapshot;
		this.Rows = new ObservableCollection<INormLang>(Snapshot);
	}

	/// 清除關鍵字並恢復 Provider 原始排序。
	public partial void ClearSearch(){
		Input = "";
		Rows = new ObservableCollection<INormLang>(SourceRows);
	}

	/// 代碼始終可搜索；詳細語言名稱僅在實現 INormLangDetail 時參與匹配。
	public partial void Search(){
		var Query = Input.Trim();
		if(str.IsNullOrWhiteSpace(Query)){
			ClearSearch();
			return;
		}
		Rows = new ObservableCollection<INormLang>(SourceRows.Where(Lang=>
			Lang.Code.Contains(Query, StringComparison.OrdinalIgnoreCase)
			|| (Lang is INormLangDetail Detail && (
				Detail.NativeName.Contains(Query, StringComparison.OrdinalIgnoreCase)
				|| Detail.EnglishName.Contains(Query, StringComparison.OrdinalIgnoreCase)
			))
		));
	}
}
