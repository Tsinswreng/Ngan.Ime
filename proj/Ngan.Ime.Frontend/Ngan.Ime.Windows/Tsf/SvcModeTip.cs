namespace Ngan.Ime.Windows.Tsf;

using System.ComponentModel;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.UI.Views.StatusTip;
using Tsinswreng.CsCore;

[Doc($$"""
模式提示：中／英、簡／繁的狀態圖示。

*照 Weasel 的條件*：`ShouldDisplayStatusIcon()`（`WeaselUI/StandardLayout.cpp:391`）是

```
(_status.ascii_mode && !inline_preedit) || !_status.composing || !_context.aux.empty()
```

本專案沒有行內組字、也沒有 aux，所以化簡成「英文模式 → 顯示」。Weasel 是**每次更新面板時
依當下狀態算一次**，不是「狀態值變了才顯示」；所以已經是英文模式再按一次轉英文，
它照樣會顯示。先前我們訂閱 `PropertyChanged`（值有變才發），於是「再按一次」不彈。

*中文模式*：只有切換真的發生（`IsSimplification` 變更）時顯示，切完顯示 `漢`／`汉`。

*不抑制組字中*：Weasel 的圖示畫在候選面板裡，組字中照樣顯示；先前那條
「組字中不打擾」是自創規則，會把簡繁切換的反饋吃掉。
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

	[Doc($$"""
	每顆按鍵（按下）之後呼叫：英文模式就顯示 `A`。

	照 Weasel：只要處於英文模式，面板／圖示就帶着 `A`，與「有沒有切換」無關。
	""")]
	public void OnKeyDown(){
		// 照 Weasel 的第一條條件：英文模式時圖示常駐（每顆鍵都帶 `A`）。
		// 中文模式只在切換時顯示，否則每打一個字都閃一次 `漢`，不是 Weasel 的行為。
		if(!State.IsAsciiMode){
			return;
		}
		Show("A", "key");
	}

	[Doc("狀態變更：中／英、簡／繁切換時顯示切換後的狀態。")]
	void OnStateChanged(object? Sender, PropertyChangedEventArgs Args){
		if(Args.PropertyName != nameof(IImeState.IsAsciiMode)
			&& Args.PropertyName != nameof(IImeState.IsSimplification)){
			return;
		}
		Show(MkText(), "change:" + Args.PropertyName);
	}

	[Doc("依目前狀態顯示提示；還沒有插入點時不顯示（不讓它出現在螢幕角落）。")]
	void Show(str Text, str Why){
		var X = Anchor.X;
		var Y = Anchor.Y;
		var Source = "caret";
		if(!Anchor.Has){
			// 還沒有插入點（例如剛切換、還沒打過字）：退到滑鼠位置。
			// Weasel 的 ascii tip 本來就跟着游標走（`ascii_tip_follow_cursor`）。
			if(!GetCursorPos(out var Cursor)){
				AppLog.Warn($"[Tip] skip text={Text} why={Why} anchor=none cursor=fail");
				return;
			}
			X = Cursor.X;
			Y = Cursor.Y;
			Source = "cursor";
		}
		AppLog.Info($"[Tip] show text={Text} why={Why} src={Source} x={X} y={Y} ascii={(State.IsAsciiMode ? 1 : 0)} simp={(State.IsSimplification ? 1 : 0)}");
		Tip.Show(Text, new Avalonia.PixelPoint(X, Y));
	}

	[Doc("游標位置（螢幕座標）；沒有插入點時的兜底。")]
	[System.Runtime.InteropServices.DllImport("user32.dll")]
	static extern bool GetCursorPos(out NativePoint Point);

	[Doc("Win32 的 POINT 佈局。")]
	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
	struct NativePoint{
		public i32 X;
		public i32 Y;
	}

	[Doc("依目前模式決定提示字：英文 `A`；中文繁體 `漢`、簡體 `汉`。")]
	str MkText(){
		if(State.IsAsciiMode){
			return "A";
		}
		return State.IsSimplification ? "汉" : "漢";
	}
}
