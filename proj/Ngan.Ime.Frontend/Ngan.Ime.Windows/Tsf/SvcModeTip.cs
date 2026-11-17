namespace Ngan.Ime.Windows.Tsf;

using System.ComponentModel;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.UI.Views.StatusTip;
using Tsinswreng.CsCore;

[Doc($$"""
模式提示：中／英、簡／繁的狀態圖示。

*照 Weasel 0.15.0 的觸發點*：提示不是每顆鍵都彈，而是**引擎發出 option 通知**時才彈。
Weasel 的 `RimeWithWeasel.cpp` 的 `_ShowMessage` 收到 `m_message_type == "option"`、
值為 `ascii_mode`／`!ascii_mode` 就顯示圖示，其餘模式（簡繁、全半角）顯示文字，
最後 `m_ui->Update(...)` ＋ `ShowWithTimeout(1200 + 200 * tips.length())` 自動收起。
`set_option` 即使值沒變也會發通知，所以「已經是英文模式、又按一次切換鍵」照樣會彈。

*先前錯在哪*：一是訂閱 `PropertyChanged`（值變才發），吃掉「值沒變但引擎通知了」那一種；
二是為了補救而寫成「英文模式下每顆鍵都彈」，於是打英文時每個字母都冒一個 `A`。
現在只認 {{nameof(IImeState.OptionNotified)}}。

*文字*：英文 `A`；中文繁體 `漢`、簡體 `汉`（使用者指定的字，Weasel 0.15.0 是用
「漢字／汉字」兩個字的 aux 文字，這裡照使用者的要求用單字圖示）。
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
		// 只認引擎的 option 通知；PropertyChanged 會在每顆鍵的狀態同步裡亂發。
		State.OptionNotified += OnOptionNotified;
	}

	[Doc("停止監聽；未啟動時無副作用。")]
	public void Stop(){
		if(!Started){
			return;
		}
		Started = false;
		State.OptionNotified -= OnOptionNotified;
		Tip.Hide();
	}

	[Doc($$"""
	引擎發來模式通知：顯示切換後的狀態。
	*不看值有沒有變*：`set_option` 即使值不變也會通知，Weasel 那時照樣顯示。
	#Prm[{{nameof(OptionName)}}][`ascii_mode` 或 `simplification`。]
	#Prm[{{nameof(IsEnabled)}}][新值。]
	""")]
	void OnOptionNotified(str OptionName, bool IsEnabled){
		if(OptionName != OptionNames.AsciiMode && OptionName != OptionNames.Simplification){
			return;
		}
		Show(MkText(), "option:" + OptionName);
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
