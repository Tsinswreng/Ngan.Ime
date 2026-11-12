namespace Ngan.Ime.UI.Views.Clipboard;

using System.Collections.ObjectModel;
using System.ComponentModel;
using Ngan.Ime.UI.Infra;
using Tsinswreng.CsCore;

[Doc($$"""
剪貼板頁 ViewModel：頁面打開時抓一次系統剪貼板，點一項就上屏並退出。

*為何只在打開時抓*：剪貼板內容隨時會變，但輸入法不該常駐監聽（Android 對剪貼板後台讀取有限制）；
打開頁面時抓一次、使用者點完即走，是這個場景最省的做法。

*長文本*：上屏用完整文本，顯示截斷到 {nameof(PreviewMaxChars)} 字——避免整份代碼貼進 TextBlock。

#Eg[
	```cs
	Ui.State.ToggleClipboard(); // VM 會自動刷新
	```
]
""")]
public partial class VmClipboard : AppVmBase, IMk<VmClipboard>, IDisposable{

	[Doc("列表顯示的最大字元數；超過以省略號結尾。")]
	public const i32 PreviewMaxChars = 80;

	[Doc("共享界面狀態（頁面開關）。")]
	public ImeUiState UiState{get;}

	[Doc("剪貼板來源。")]
	public IClipboardService ClipboardService{get;}

	[Doc("上屏目標。")]
	public IKeyboardHost KeyboardHost{get;}

	[Doc("當前列表；每次打開重新抓取。")]
	public ObservableCollection<VmClipboardItem> Items{
		get;
		set{ SetProperty(ref field, value); }
	} = [];

	[Doc("界面狀態變更處理器；Dispose 時解除。")]
	readonly PropertyChangedEventHandler UiStateHandler;

	[Doc("建立並訂閱頁面打開事件。")]
	public VmClipboard(ImeUiState UiState, IClipboardService ClipboardService, IKeyboardHost KeyboardHost){
		this.UiState = UiState;
		this.ClipboardService = ClipboardService;
		this.KeyboardHost = KeyboardHost;
		UiStateHandler = (_, Args) => {
			if(Args.PropertyName == nameof(ImeUiState.IsClipboardVisible) && UiState.IsClipboardVisible){
				Fire(RefreshAsy());
			}
		};
		UiState.PropertyChanged += UiStateHandler;
		base.Init();
	}

	[Doc("兜底建立：無依賴（設計器預覽用）。")]
	protected VmClipboard(){
		UiState = default!;
		ClipboardService = default!;
		KeyboardHost = default!;
		UiStateHandler = (_, _) => { };
		base.Init();
	}

	[Doc("兜底建立。")]
	public static VmClipboard Mk(){
		return new VmClipboard();
	}

	[Doc("抓取剪貼板並建立列表；去重、截斷預覽。")]
	public async Task<nil> RefreshAsy(CT Ct = default){
		var Texts = await ClipboardService.GetItemsAsy(Ct);
		var Items = new ObservableCollection<VmClipboardItem>();
		foreach(var Text in Texts.Distinct(StringComparer.Ordinal)){
			if(string.IsNullOrEmpty(Text)){
				continue;
			}
			Items.Add(new VmClipboardItem{
				Text = Text,
				Preview = MkPreview(Text),
				Click = () => {
					KeyboardHost.CommitText(Text);
					UiState.ExitClipboard();
				},
			});
		}
		this.Items = Items;
		return NIL;
	}

	[Doc("預覽文字：超長時截斷並加省略號，避免把整篇文本塞進界面。")]
	public static str MkPreview(str Text){
		if(Text.Length <= PreviewMaxChars){
			return Text.Replace("\r", " ").Replace("\n", " ");
		}
		return Text[..PreviewMaxChars].Replace("\r", " ").Replace("\n", " ") + "…";
	}

	[Doc("解除界面狀態訂閱。")]
	public void Dispose(){
		if(UiState is not null){
			UiState.PropertyChanged -= UiStateHandler;
		}
		GC.SuppressFinalize(this);
	}
}

[Doc($$"""
剪貼板列表的一項：完整文本、顯示用預覽與點擊行為。

*為何兩份文字*：上屏要完整內容，顯示要短；只留預覽會讓上屏內容被截斷。
""")]
public partial class VmClipboardItem : AppVmBase{

	[Doc("完整文本；上屏用。")]
	public str Text{
		get;
		set{ SetProperty(ref field, value); }
	} = "";

	[Doc("顯示用預覽；截斷到 {{nameof(VmClipboard.PreviewMaxChars)}} 字。")]
	public str Preview{
		get;
		set{ SetProperty(ref field, value); }
	} = "";

	[Doc("點擊行為：上屏並退出剪貼板頁。")]
	public Action? Click{get;set;}
}
