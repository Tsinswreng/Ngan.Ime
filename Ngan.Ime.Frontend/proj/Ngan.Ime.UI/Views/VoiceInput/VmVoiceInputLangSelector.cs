using System.Collections.ObjectModel;
using Ngan.Ime.Core.Infra;
using Ngan.Ime.Core.Language;
using Ngan.Ime.ViewModels;

namespace Ngan.Ime.UI.Views.VoiceInput;

/// 語音識別語言選擇頁的 ViewModel 聲明與資料流設計。
///
/// # 資料來源
/// 最終語言集合應由當前 `IVoiceRecognitionProvider` 公布的支持語言目錄提供，
/// 不允許 UI 自行假設某個供應商支持全部 BCP 47 語言。
/// 第一個火山 Provider 可提供其流式 API 明確支持的25種語言；未來切換阿里或其他 Provider 時，
/// 只替換資料來源，不修改搜索與列表 View。
///
/// # 搜索規則
/// 搜索對 BCP 47 代碼、本地名稱和英文名稱做大小寫不敏感的包含匹配。
/// 空搜索顯示全部支持語言；搜索結果保持 Provider 給出的原始排序，常用語言可由 Provider 權重提前。
public partial class VmVoiceInputLangSelector : ViewModelBase, IMk<VmVoiceInputLangSelector>{
	/// 提供當前供應商實際支持的語言目錄。
	readonly Ngan.Ime.Core.Voice.IVoiceRecognitionProvider RecognitionProvider;

	/// 建立語言選擇 VM，並以 Provider 的 IReadOnlyList&lt;INormLang&gt; 初始化列表。
	public partial VmVoiceInputLangSelector(Ngan.Ime.Core.Voice.IVoiceRecognitionProvider RecognitionProvider);

	/// 語言名稱或 BCP 47 代碼搜索文字。
	/// setter 本身只保存輸入；按搜索按鈕或 Enter 時才更新結果，避免每次鍵入都重建控件列表。
	public str Input{
		get;
		set{SetProperty(ref field, value);}
	} = "";

	/// 當前 Provider 支持的完整語言集合快照。
	/// 搜索只讀取此集合，不直接向網絡服務重新請求；Provider 切換後由上層重新載入。
	public IReadOnlyList<INormLang> SourceRows{
		get;
		set{SetProperty(ref field, value);}
	} = [];

	/// 當前搜索結果，供语言列表展示。
	/// 集合順序與 `SourceRows` 一致；搜索不應偷偷按名稱重新排序，避免常用語言權重失效。
	public ObservableCollection<INormLang> Rows{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(IsEmpty));
			}
		}
	} = [];

	/// 搜索結果是否為空。
	/// View 在空結果時顯示提示，而不是留下一整片沒有解釋的黑色區域。
	public bool IsEmpty{
		get{return Rows.Count == 0;}
	}

	/// 語言選擇 VM 的兜底創建路徑。
	/// 兜底路徑從全局 DI 取得 Provider；不再維護與服務端能力可能不一致的靜態語言表。
	public static partial VmVoiceInputLangSelector Mk();

	/// 使用當前 Provider 支持語言初始化完整集合與可見結果。
	/// 輸入集合在此轉成穩定快照，避免 Provider 在顯示期間修改集合造成枚舉競態。
	public partial void LoadRows(IEnumerable<INormLang> Rows);

	/// 清空搜索條件並恢復完整语言列表。
	public partial void ClearSearch();

	/// 按 `Input` 過濾 BCP 47 代碼、本地名稱和英文名稱。
	/// 匹配應忽略大小寫及首尾空白；空輸入等價於恢復全部結果。
	public partial void Search();
}
