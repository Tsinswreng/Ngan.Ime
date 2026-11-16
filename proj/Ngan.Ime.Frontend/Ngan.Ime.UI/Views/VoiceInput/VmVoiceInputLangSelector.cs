namespace Ngan.Ime.UI.Views.VoiceInput;

using System.Collections.ObjectModel;
using Ngan.Ime.Core.Language;
using Ngan.Ime.Core.Voice;
using Ngan.Ime.UI.Infra;

[Doc($$"""
語音識別語言選擇頁的 ViewModel：目錄來自 {{nameof(IVoiceRecognitionProvider.SupportedLanguages)}}，本類只做過濾。

*為何不內建語言表*：供應商支援的語言會變；UI 自帶一份靜態表就會與服務端能力脫節。
故一律向 Provider 要，換供應商時本類不用改。

*搜索*：對 BCP 47 代碼、本地名稱、英文名稱做大小寫不敏感的包含匹配；
結果保持 Provider 的原始順序（權重已由 Provider 排好），不重新排序。

#Eg[
	```cs
	Vm.Search();
	```
]
""")]
public partial class VmVoiceInputLangSelector : AppVmBase, IMk<VmVoiceInputLangSelector>{

	[Doc("語言目錄來源。")]
	readonly IVoiceRecognitionProvider RecognitionProvider;

	[Doc("搜索關鍵字；按搜索或 Enter 才過濾。")]
	public str Input{
		get;
		set{ SetProperty(ref field, value); }
	} = "";

	[Doc("Provider 支援的完整語言快照；搜索只讀它。")]
	public IReadOnlyList<INormLang> SourceRows{
		get;
		set{ SetProperty(ref field, value); }
	} = [];

	[Doc("當前搜索結果。")]
	public ObservableCollection<INormLang> Rows{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(IsEmpty));
			}
		}
	} = [];

	[Doc("搜索結果是否為空。")]
	public bool IsEmpty => Rows.Count == 0;

	[Doc("建立並載入 Provider 的能力快照。")]
	public VmVoiceInputLangSelector(IVoiceRecognitionProvider RecognitionProvider){
		this.RecognitionProvider = RecognitionProvider;
		LoadRows(RecognitionProvider.SupportedLanguages);
		base.Init();
	}

	[Doc("兜底建立：空目錄（設計器預覽用）。")]
	protected VmVoiceInputLangSelector(){
		RecognitionProvider = default!;
		base.Init();
	}

	[Doc("兜底建立。")]
	public static VmVoiceInputLangSelector Mk(){
		return new VmVoiceInputLangSelector();
	}

	[Doc("以穩定快照載入目錄並重置結果。")]
	public partial void LoadRows(IEnumerable<INormLang> Rows);

	[Doc("清空關鍵字並恢復完整目錄。")]
	public partial void ClearSearch();

	[Doc("依關鍵字過濾；空輸入等於清空搜索。")]
	public partial void Search();
}
