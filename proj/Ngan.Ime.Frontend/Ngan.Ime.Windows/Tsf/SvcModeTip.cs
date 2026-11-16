namespace Ngan.Ime.Windows.Tsf;

using System.ComponentModel;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.UI.Views.StatusTip;
using Tsinswreng.CsCore;

[Doc($$"""
模式切換提示：中／英、簡／繁一變就在光標旁閃一下（照 Weasel 的狀態圖示）。

*文字*：英文模式 `A`；中文模式繁體 `漢`、簡體 `汉`。簡繁切換後顯示切換後的狀態。

*為何訂閱 {{nameof(IImeState)}}*：引擎的 option 變更與每次按鍵後的狀態快照都會走
`SetProperty`（`SvcImeBase`），所以不必另開協議事件；`IsAsciiMode`／`IsSimplification`
變更就是切換發生的時刻。

*組字中不打擾*：正在組字時候選浮窗已經在顯示，狀態提示再冒出來會蓋住它，故跳過；
`IsComposing` 變為 true 時也主動把提示收起。
""")]
public sealed class SvcModeTip{

	[Doc("引擎狀態；切換時由它通知。")]
	readonly IImeState State;

	[Doc("顯示提示的界面服務。")]
	readonly IStatusTip Tip;

	[Doc("最近一次量到的插入點；提示貼着它顯示。")]
	readonly CaretAnchor Anchor;

	[Doc("是否已訂閱；保證 Start 冪等。")]
	bool Started;

	[Doc("建立服務；依賴由容器給。")]
	public SvcModeTip(IImeState State, IStatusTip Tip, CaretAnchor Anchor){
		this.State = State;
		this.Tip = Tip;
		this.Anchor = Anchor;
	}

	[Doc("開始監聽模式變更；重複呼叫無副作用。")]
	public void Start(){
		if(Started){
			return;
		}
		Started = true;
		State.PropertyChanged += OnStateChanged;
	}

	[Doc("停止監聽；未啟動時無副作用。")]
	public void Stop(){
		if(!Started){
			return;
		}
		Started = false;
		State.PropertyChanged -= OnStateChanged;
		Tip.Hide();
	}

	[Doc("狀態變更：模式類的變更顯示提示，組字開始收起提示。")]
	void OnStateChanged(object? Sender, PropertyChangedEventArgs Args){
		if(Args.PropertyName == nameof(IImeState.IsComposing)){
			if(State.IsComposing){
				Tip.Hide();
			}
			return;
		}
		if(Args.PropertyName != nameof(IImeState.IsAsciiMode)
			&& Args.PropertyName != nameof(IImeState.IsSimplification)){
			return;
		}
		if(State.IsComposing){
			return;
		}
		if(!Anchor.Has){
			// 還沒量到插入點：貼上去只會出現在螢幕角落，不如不顯示。
			return;
		}
		Tip.Show(MkText(), new Avalonia.PixelPoint(Anchor.X, Anchor.Y));
	}

	[Doc("依目前模式決定提示字：英文 `A`；中文繁體 `漢`、簡體 `汉`。")]
	str MkText(){
		if(State.IsAsciiMode){
			return "A";
		}
		return State.IsSimplification ? "汉" : "漢";
	}
}
