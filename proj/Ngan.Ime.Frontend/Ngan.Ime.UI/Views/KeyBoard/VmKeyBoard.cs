namespace Ngan.Ime.UI.Views.KeyBoard;

using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.UI.Infra;

[Doc($$"""
鍵盤的 ViewModel：把布局資料變成 {{nameof(VmKey)}}，並把使用者手勢翻譯成輸入動作。

*狀態從哪來*：`IsNumLayout`／`IsShiftLocked` 存在共享的 {{nameof(ImeUiState)}}，
故主鍵盤與分體左右半邊永遠同步；本類只做投影與變更通知。
`IsAsciiMode` 來自引擎狀態（{{nameof(IImeState)}}），本類負責據此刷新鍵帽文字。

*手勢到動作*：{{nameof(KeyGesture)}} 只描述意圖；`Perform` 把它翻成
「送哪些鍵」「切哪個模式」「叫誰隱藏鍵盤」。故新增一種手勢只改這兩處。

*AOT*：沒有任何反射；布局是靜態資料，鍵名是 `KeyChars` 常量。

#Eg[
	```cs
	var Key = Vm.MkKey(Def);
	Key.Invoke?.Invoke(EGestureSlot.Click);
	```
]
""")]
public partial class VmKeyBoard : AppVmBase, IMk<VmKeyBoard>, IDisposable{

	[Doc("引擎狀態（ASCII 模式）。")]
	readonly IImeState State;

	[Doc("按鍵入口。")]
	readonly IImeInput Input;

	[Doc("選項切換（中英）。")]
	readonly IImeOptions Options;

	[Doc("共享界面狀態（鍵盤層、Shift 鎖定）。")]
	readonly ImeUiState Ui;

	[Doc("宿主能力（隱藏鍵盤）。")]
	readonly IKeyboardHost Host;

	[Doc("由本 VM 建立的鍵，供批量刷新鍵帽。")]
	readonly List<(KeyDef Def, VmKey Vm)> Keys = [];

	[Doc("建立鍵盤 VM；引擎與共享狀態由容器注入。")]
	public VmKeyBoard(IImeState State, IImeInput Input, IImeOptions Options, ImeUiState Ui, IKeyboardHost Host){
		this.State = State;
		this.Input = Input;
		this.Options = Options;
		this.Ui = Ui;
		this.Host = Host;
		State.PropertyChanged += OnStateChanged;
		Ui.PropertyChanged += OnUiChanged;
		base.Init();
	}

	[Doc("兜底建立：無依賴實例（設計器預覽用，操作不會生效）。")]
	protected VmKeyBoard(){
		State = default!;
		Input = default!;
		Options = default!;
		Ui = default!;
		Host = default!;
		base.Init();
	}

	[Doc("兜底建立。")]
	public static VmKeyBoard Mk(){
		return new VmKeyBoard();
	}

	[Doc("是否顯示字母層。")]
	public bool ShowMain => !Ui.IsNumLayout;

	[Doc("是否顯示數字層。")]
	public bool ShowNum => Ui.IsNumLayout;

	[Doc("依布局定義建立一顆鍵的 ViewModel，並綁好手勢入口。")]
	public VmKey MkKey(KeyDef Def){
		var Vm = VmKey.Mk();
		Vm.Def = Def;
		Vm.FontSize = UiCfg.Inst.KeyFontSize;
		Vm.HintFontSize = UiCfg.Inst.HintFontSize;
		Vm.Background = UiCfg.Inst.KeyBgColor;
		Vm.Foreground = BrushesFor.Text;
		Vm.Invoke = Slot => Invoke(Def, Slot);
		Vm.Text = DfltText(Def);
		Vm.Hint = Def.Hint ?? "";
		Vm.BottomHint = Def.HintBottom ?? "";
		Keys.Add((Def, Vm));
		return Vm;
	}

	[Doc("鍵帽主文字：動作鍵（123／qwe）顯示標籤，其餘依布局規則。")]
	static str DfltText(KeyDef Def){
		return KeyLayout.LabelOf(Def);
	}

	[Doc("前景色常量；集中在此以免每個鍵各 new 一個 Brush。")]
	static class BrushesFor{
		public static readonly Avalonia.Media.IBrush Text = Avalonia.Media.Brushes.White;
	}

	[Doc("執行一個手勢。")]
	public void Invoke(KeyDef Def, EGestureSlot Slot){
		if(Slot == EGestureSlot.Click){
			SendPress(Def.Key);
			return;
		}
		var Gesture = Def.GetGesture(Slot);
		if(Gesture is null){
			return;
		}
		switch(Gesture.Kind){
			case EGestureKind.SendKey:
				SendPress(Gesture.Keys[0]);
				break;
			case EGestureKind.SendCtrlChord:
				SendCtrlChord(Gesture.Keys[0]);
				break;
			case EGestureKind.SendTextThenSpace:
				SendTextThenSpace(Gesture.Keys);
				break;
			case EGestureKind.ToggleAscii:
				Fire(Options.ToggleAsciiModeAsy());
				break;
			case EGestureKind.ToggleShiftLock:
				if(Ui is not null){
					Ui.IsShiftLocked = !Ui.IsShiftLocked;
				}
				break;
			case EGestureKind.HideKeyboard:
				Host.HideKeyboard();
				break;
			case EGestureKind.SetLayout:
				if(Ui is not null){
					Ui.IsNumLayout = Gesture.NumLayout;
				}
				break;
		}
	}

	[Doc("送一個鍵（按下＋擡起）；Shift 鎖定時以鍵盤狀態表達修飾，不改寫鍵本身。")]
	void SendPress(IKeyChar Key){
		Input.InputSafely(MkPressEvents(Key), HandleErr);
	}

	[Doc("送 Ctrl + 鍵：按下 Ctrl、按鍵、放鍵、放 Ctrl。")]
	void SendCtrlChord(IKeyChar Key){
		Input.InputSafely([
			new KeyEvent{ KeyChar = KeyChars.Ctrl_L, KeyState = KeyStates.Down, KeyBoardState = KeyBoardState.Mk(KeyChars.Ctrl_L) },
			new KeyEvent{ KeyChar = Key, KeyState = KeyStates.Down, KeyBoardState = KeyBoardState.Mk(KeyChars.Ctrl_L, Key) },
			new KeyEvent{ KeyChar = Key, KeyState = KeyStates.Up, KeyBoardState = KeyBoardState.Mk(KeyChars.Ctrl_L) },
			new KeyEvent{ KeyChar = KeyChars.Ctrl_L, KeyState = KeyStates.Up, KeyBoardState = KeyBoardState.Mk() },
		], HandleErr);
	}

	[Doc("依次送多個鍵，最後補一個空格。")]
	void SendTextThenSpace(IReadOnlyList<IKeyChar> Keys){
		var Events = new List<IKeyEvent>();
		foreach(var One in Keys){
			Events.AddRange(MkPressEvents(One));
		}
		Events.AddRange(MkPressEvents(KeyChars.Space));
		Input.InputSafely(Events, HandleErr);
	}

	[Doc("組出一個鍵的按下／擡起事件；Shift 鎖定時把 Shift 放進鍵盤狀態。")]
	IEnumerable<IKeyEvent> MkPressEvents(IKeyChar Key){
		// Shift 鎖定只透過 KeyBoardState 表達：後端與各平臺各自把它翻成修飾鍵，
		// 故這裡不額外送 Shift 事件、也不改寫 KeyChar。
		var ShiftLocked = Ui is not null && Ui.IsShiftLocked
			&& !Key.Equals(KeyChars.Shift_L) && !Key.Equals(KeyChars.Shift_R);
		if(ShiftLocked){
			return [
				new KeyEvent{ KeyChar = Key, KeyState = KeyStates.Down, KeyBoardState = KeyBoardState.Mk(KeyChars.Shift_L, Key) },
				new KeyEvent{ KeyChar = Key, KeyState = KeyStates.Up, KeyBoardState = KeyBoardState.Mk(KeyChars.Shift_L) },
			];
		}
		return [
			new KeyEvent{ KeyChar = Key, KeyState = KeyStates.Down },
			new KeyEvent{ KeyChar = Key, KeyState = KeyStates.Up },
		];
	}

	[Doc("引擎狀態變更：ASCII 模式切換時刷新全部鍵帽。")]
	void OnStateChanged(obj? Sender, System.ComponentModel.PropertyChangedEventArgs Args){
		if(Args.PropertyName == nameof(IImeState.IsAsciiMode)){
			RefreshKeyCaps();
		}
	}

	[Doc("共享界面狀態變更：鍵盤層切換與 Shift 鎖定高亮。")]
	void OnUiChanged(obj? Sender, System.ComponentModel.PropertyChangedEventArgs Args){
		switch(Args.PropertyName){
			case nameof(ImeUiState.IsNumLayout):
				OnPropertyChanged(nameof(ShowMain));
				OnPropertyChanged(nameof(ShowNum));
				break;
			case nameof(ImeUiState.IsShiftLocked):
				RefreshHighlight();
				break;
		}
	}

	[Doc("刷新所有鍵帽文字：ASCII 模式時字母一律顯示小寫。")]
	void RefreshKeyCaps(){
		foreach(var (Def, Vm) in Keys){
			var Text = KeyLayout.LabelOf(Def);
			if(State.IsAsciiMode){
				var KeyName = Def.Key.Name;
				if(KeyName.Length == 1 && char.IsAsciiLetter(KeyName[0])){
					Text = char.ToLowerInvariant(KeyName[0]).ToString();
				}else if(Text.Length == 1 && char.IsAsciiLetter(Text[0])){
					Text = char.ToLowerInvariant(Text[0]).ToString();
				}
			}
			Vm.Text = Text;
		}
	}

	[Doc("Shift 鎖定時高亮 `$` 鍵（本鍵盤的 Shift 鎖定入口）。")]
	void RefreshHighlight(){
		foreach(var (Def, Vm) in Keys){
			var Highlight = Ui.IsShiftLocked && Def.Key.Equals(KeyChars.Dollar);
			Vm.Background = Highlight ? UiCfg.Inst.MainColor : UiCfg.Inst.KeyBgColor;
		}
	}

	[Doc("解除事件訂閱。")]
	public void Dispose(){
		if(State is not null){
			State.PropertyChanged -= OnStateChanged;
		}
		if(Ui is not null){
			Ui.PropertyChanged -= OnUiChanged;
		}
		GC.SuppressFinalize(this);
	}
}
