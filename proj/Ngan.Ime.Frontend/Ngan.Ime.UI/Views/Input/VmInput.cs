namespace Ngan.Ime.UI.Views.Input;

using Avalonia.Threading;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.UI.Infra;

[Doc($$"""
預編輯列（preedit）的 ViewModel。

*資料流*：引擎每處理完一批按鍵就把 `Preedit` 寫回狀態並發 {{nameof(IImeInput.AfterInput)}}；
本類只在那時讀一次快照、切回 UI 線程寫 `Text`，界面綁定它即可。
不在這裡做字符串拼接或候選邏輯——那是引擎的事。

#Eg[
	```cs
	var Vm = VmInput.Mk(text => Dispatcher.UIThread.Post(text));
	```
]
""")]
public partial class VmInput : AppVmBase, IMk<VmInput>, IDisposable{

	[Doc("引擎狀態；AfterInput 後讀它的 Preedit。")]
	readonly IImeState State;

	[Doc("引擎輸入入口；只用它的 AfterInput 事件。")]
	readonly IImeInput Input;

	[Doc("預編輯文字；界面綁定它。")]
	public str Text{
		get;
		set{ SetProperty(ref field, value); }
	} = "";

	[Doc("建立並訂閱引擎輸入事件。")]
	public VmInput(IImeState State, IImeInput Input){
		this.State = State;
		this.Input = Input;
		Input.AfterInput += OnAfterInput;
		base.Init();
	}

	[Doc("兜底建立：不訂閱任何事件（設計器預覽用）。")]
	protected VmInput(){
		State = default!;
		Input = default!;
		base.Init();
	}

	[Doc("兜底建立。")]
	public static VmInput Mk(){
		return new VmInput();
	}

	[Doc("一批按鍵處理完：把引擎的 preedit 快照切回 UI 線程。")]
	void OnAfterInput(obj? Sender, IEnumerable<IKeyEvent> Keys){
		var Text = State.Preedit;
		if(Dispatcher.UIThread.CheckAccess()){
			this.Text = Text;
			return;
		}
		Dispatcher.UIThread.Post(() => this.Text = Text);
	}

	[Doc("解除事件訂閱。")]
	public void Dispose(){
		if(Input is not null){
			Input.AfterInput -= OnAfterInput;
		}
		GC.SuppressFinalize(this);
	}
}
