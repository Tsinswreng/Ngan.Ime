using System.Collections.ObjectModel;
using Avalime.Core.Infra;
using Avalime.Core.Language;
using Avalime.ViewModels;

namespace Avalime.UI.Views.VoiceInput;

/// 語音語言選擇視圖的 ViewModel 聲明。
/// 本輪只提供前端展示所需的資料形狀，不實現搜索、載入或選中狀態。
public partial class VmVoiceInputLangSelector : ViewModelBase, IMk<VmVoiceInputLangSelector>{
	/// 語言名稱或 BCP 47 代碼搜索文字。
	public str Input{
		get;
		set{SetProperty(ref field, value);}
	} = "";

	/// 語言列表資料源。
	/// 後續由語音引擎支持語言目錄填充；本輪不硬編碼實際支持範圍。
	public ObservableCollection<NormLangDetail> Rows{
		get;
		set{SetProperty(ref field, value);}
	} = [];

	/// 語言選擇 VM 的兜底創建路徑。
	public static partial VmVoiceInputLangSelector Mk();

	/// 清空搜索條件。
	/// 本輪保留按鈕接口，不實現狀態變更。
	public partial void ClearSearch();

	/// 按輸入文字篩選語言列表。
	/// 本輪只聲明前端操作入口，不訪問語言服務。
	public partial void Search();
}
