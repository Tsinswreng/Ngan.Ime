using System.Collections.Generic;
using System.ComponentModel;
using Ngan.Ime.Core.Infra;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.UI;
using Ngan.Ime.UI.Infra;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using static Ngan.Ime.Core.Keys.KeyChars;
using KeyVm = Ngan.Ime.UI.Views.Key.VmKey;
using KeyView = Ngan.Ime.UI.Views.Key.ViewKey;
using KS = Ngan.Ime.Core.Keys.KeyStates;

namespace Ngan.Ime.UI.Views.KeyBoard;
using Ctx = VmKeyBoard;

public class ViewKeyBoard : AppViewBase<Ctx>
	, IDisposable
{
	readonly List<IDisposable> _disposables = [];
	readonly List<PropertyChangedEventHandler> _ctxHandlers = [];
	readonly ISvcIme _imeState;
	readonly IKeyboardHost _keyboardHost;

	public ViewKeyBoard(){
		Ctx = Di.DiOrMk<Ctx>();
		_imeState = Di.GetRSvc<ISvcIme>();
		_keyboardHost = Di.GetRSvc<IKeyboardHost>();
		Render();
	}

	GridStack Root = new(IsRow: true);
	Grid _mainKeys = default!;
	Grid _numKeys = default!;

	void Render(){
		this.SetContent(Root.Grid);
		var keyboardFont = UiCfg.Inst.KeyboardFontFamily;
		if(keyboardFont is not null){
			TextElement.SetFontFamily(Root.Grid, keyboardFont);
		}
		Root.SetRowDefs([new(1, GUT.Star)]);
		Root.A(MkKeysArea());
	}

	Grid MkKeysArea(){
		var panel = new Grid();
		_mainKeys = MkKeysGrid(isNum: false);
		_numKeys = MkKeysGrid(isNum: true);
		_numKeys.IsVisible = false;
		panel
		.A(_mainKeys)
		.A(_numKeys)
		;

		PropertyChangedEventHandler onLayoutChanged = (s, e) => {
			if(e.PropertyName == nameof(Ctx.IsNumLayout)){
				_mainKeys.IsVisible = !Ctx.IsNumLayout;
				_numKeys.IsVisible = Ctx.IsNumLayout;
			}
		};
		_ctxHandlers.Add(onLayoutChanged);
		Ctx.PropertyChanged += onLayoutChanged;
		return panel;
	}

	Grid MkKeysGrid(bool isNum){
		var root = new GridStack();
		root.Grid.Background = UiCfg.Inst.GapLineBrush;
		root.SetRowDefs([
			new(0.8, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(0.8, GUT.Star),
		]);
		if(isNum){
			root
			.A(MkNumRow1())
			.A(MkNumRow2())
			.A(MkNumRow3())
			.A(MkNumRow4())
			.A(MkNumRow5())
			.A(MkNumRow6())
			;
		}else{
			root
			.A(MkRow1())
			.A(MkRow2())
			.A(MkRow3())
			.A(MkRow4())
			.A(MkRow5())
			.A(MkRow6())
			;
		}
		return root.Grid;
	}

	readonly struct KeyCfg{
		public required IKeyChar Key{get;init;}
		public str? Label{get;init;}
		public str? Hint{get;init;}
		public str? HintBottom{get;init;}
		public IKeyChar? LongClick{get;init;}
		public Func<zero>? LongClickAction{get;init;}
		public IKeyChar? SwipeUp{get;init;}
		public IKeyChar? SwipeDown{get;init;}
		public Func<zero>? SwipeDownAction{get;init;}
		public IKeyChar? SwipeLeft{get;init;}
		public Func<zero>? SwipeLeftAction{get;init;}
		public IKeyChar? SwipeRight{get;init;}
		public Func<zero>? SwipeRightAction{get;init;}
		public Func<zero>? SwipeUpAction{get;init;}
		public bool IsRepeat{get;init;}
	}

	KeyView KView(KeyCfg cfg){
		var vm = new KeyVm(Ctx.ImeState);
		_disposables.Add(vm);
		vm.Key_Click = cfg.Key;
		vm.Click = MkSendKey(cfg.Key);
		vm.FontSize = UiCfg.Inst.KeyFontSize;
		if(cfg.Label is not null) vm.Label = cfg.Label;
		if(cfg.Hint is not null) vm.Hint = cfg.Hint;
		if(cfg.HintBottom is not null) vm.BottomHint = cfg.HintBottom;
		vm.ImeState = Ctx.ImeState;
		if(cfg.LongClick is not null) vm.LongPress = MkSendKey(cfg.LongClick);
		if(cfg.LongClickAction is not null) vm.LongPress = cfg.LongClickAction;
		if(cfg.SwipeUp is not null) vm.SwipeUP = MkSendKey(cfg.SwipeUp);
		if(cfg.SwipeUpAction is not null) vm.SwipeUP = cfg.SwipeUpAction;
		if(cfg.SwipeDown is not null) vm.SwipeDown = MkSendKey(cfg.SwipeDown);
		if(cfg.SwipeDownAction is not null) vm.SwipeDown = cfg.SwipeDownAction;
		if(cfg.SwipeLeft is not null) vm.SwipeLeft = MkSendKey(cfg.SwipeLeft);
		if(cfg.SwipeLeftAction is not null) vm.SwipeLeft = cfg.SwipeLeftAction;
		if(cfg.SwipeRight is not null) vm.SwipeRight = MkSendKey(cfg.SwipeRight);
		if(cfg.SwipeRightAction is not null) vm.SwipeRight = cfg.SwipeRightAction;
		if(cfg.IsRepeat) vm.IsRepeat = true;
		//鎖定shift旹高亮
		if(cfg.Key == Dollar && cfg.Label == "$"){
			void SyncBg(){
				vm.Background = Ctx.IsShiftLocked ? UiCfg.Inst.MainColor : UiCfg.Inst.KeyBgColor;
			}
			SyncBg();
			_ctxHandlers.Add(OnKbPropertyChanged);
			Ctx.PropertyChanged += OnKbPropertyChanged;
			void OnKbPropertyChanged(object? sender, PropertyChangedEventArgs e){
				if(e.PropertyName == nameof(Ctx.IsShiftLocked)){
					SyncBg();
				}
			}
		}
		return new KeyView { Ctx = vm};
	}

	KeyView MkActionKey(str label, Action onClick, str? hint = null){
		var vm = new KeyVm(Ctx.ImeState);
		_disposables.Add(vm);
		vm.Label = label;
		vm.FontSize = UiCfg.Inst.ActionKeyFontSize;
		if(hint is not null) vm.Hint = hint;
		vm.Click = () => { onClick(); return 0; };
		vm.ImeState = Ctx.ImeState;
		return new KeyView { Ctx = vm};
	}

	Func<zero> MkSendKey(IKeyChar key) => () => {
		Ctx.ImeState.InputSafely(MkKeyPressEvents(key));
		return 0;
	};

	IEnumerable<IKeyEvent> MkKeyPressEvents(IKeyChar key){
		if(Ctx.IsShiftLocked && key != Shift_L && key != Shift_R){
			// Shift 鎖定只通過 KeyBoardState.AllDownKeys 表達。
			// UI 層不再自行發送額外的 Shift 按鍵事件，也不再直接改寫 KeyChar，
			// 後端需根據 KeyBoardState 解釋當前有哪些修飾鍵處於按下狀態。
			return [
				new KeyEvent{KeyChar = key, KeyState = KS.Down, KeyBoardState = KeyBoardState.Mk(Shift_L, key)},
				new KeyEvent{KeyChar = key, KeyState = KS.Up, KeyBoardState = KeyBoardState.Mk(Shift_L)},
			];
		}
		return [
			new KeyEvent{KeyChar = key, KeyState = KS.Down},
			new KeyEvent{KeyChar = key, KeyState = KS.Up}
		];
	}

	Func<zero> MkToggleAsciiMode() => () => {
		_ = _imeState.ToggleAsciiModeAsy();
		return 0;
	};

	Func<zero> MkToggleShiftLock() => () => {
		Ctx.IsShiftLocked = !Ctx.IsShiftLocked;
		return 0;
	};

	Func<zero> MkHideKeyboard() => () => {
		_keyboardHost.HideKeyboard();
		return 0;
	};

	/// <summary>
	/// 退格鍵上滑隱藏輸入法先保留爲編譯期關閉的實驗入口。
	/// 原因是 Android IME 內部主動 hide 之後，再次 show 時的 lifecycle
	/// 與“點空白失焦”/“系統返回鍵收起”不等價，之前已反覆引出黑屏與狀態異常。
	/// 在宿主完全證明這條 hide 路徑穩定前，先不要把它掛回正式手勢。
	/// </summary>
	Func<zero>? MkBackspaceSwipeUpHideKeyboardOrNull() {
#if false
		return MkHideKeyboard();
#else
		return null;
#endif
	}

	Func<zero> MkSendCtrlKey(IKeyChar key) => () => {
		Ctx.ImeState.InputSafely(MkCtrlComboKeyEvents(key));
		return 0;
	};

	IEnumerable<IKeyEvent> MkCtrlComboKeyEvents(IKeyChar key){
		return [
			new KeyEvent{KeyChar = Ctrl_L, KeyState = KS.Down, KeyBoardState = KeyBoardState.Mk(Ctrl_L)},
			new KeyEvent{KeyChar = key, KeyState = KS.Down, KeyBoardState = KeyBoardState.Mk(Ctrl_L, key)},
			new KeyEvent{KeyChar = key, KeyState = KS.Up, KeyBoardState = KeyBoardState.Mk(Ctrl_L)},
			new KeyEvent{KeyChar = Ctrl_L, KeyState = KS.Up, KeyBoardState = KeyBoardState.Mk()},
		];
	}

	Func<zero> MkSendTextAndSpace(params IKeyChar[] keys) => () => {
		var keyEvents = new List<IKeyEvent>();
		foreach(var key in keys){
			keyEvents.AddRange(MkKeyPressEvents(key));
		}
		keyEvents.AddRange(MkKeyPressEvents(Space));
		Ctx.ImeState.InputSafely(keyEvents);
		return 0;
	};

	Grid MkRow1(){
		var root = new GridStack(IsRow: false);
		root.SetColDefs([
			new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
			new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
		]);
		root
		.A(KView(new(){Key=D1, Hint="!", LongClick=Exclamation, SwipeUp=Exclamation}))
		.A(KView(new(){Key=D2, Hint="@", LongClick=At, SwipeUp=At}))
		.A(KView(new(){Key=D3, Hint="#", LongClick=HashTag, SwipeUp=HashTag}))
		.A(KView(new(){Key=D4, Hint="$", LongClick=Dollar, SwipeUp=Dollar}))
		.A(KView(new(){Key=D5, Hint="%", LongClick=Percent, SwipeUp=Percent}))
		.A(KView(new(){Key=D6, Hint="^", LongClick=Caret, SwipeUp=Caret}))
		.A(KView(new(){Key=D7, Hint="&", LongClick=Ampersand, SwipeUp=Ampersand}))
		.A(KView(new(){Key=D8, Hint="*", LongClick=Asterisk, SwipeUp=Asterisk}))
		.A(KView(new(){Key=D9, Hint="(", LongClick=Paren_L, SwipeUp=Paren_L}))
		.A(KView(new(){Key=D0, Hint=")", LongClick=Paren_R, SwipeUp=Paren_R}))
		;
		return root.Grid;
	}

	Grid MkRow2(){
		var root = new GridStack(IsRow: false);
		root.SetColDefs([
			new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
			new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
		]);
		root
		.A(KView(new(){Key=q, Label="Q", SwipeUp=Q}))
		.A(KView(new(){Key=w, Label="W", SwipeUp=W}))
		.A(KView(new(){Key=e, Label="E", SwipeUp=E}))
		.A(KView(new(){Key=r, Label="R", SwipeUp=R}))
		.A(KView(new(){Key=t, Label="T", SwipeUp=T}))
		.A(KView(new(){Key=u, Label="U", SwipeUp=U, SwipeLeft=Paren_L, SwipeRight=Paren_R}))
		.A(KView(new(){Key=i, Label="I", SwipeUp=I, SwipeLeft=SquareBracket_L, SwipeRight=SquareBracket_R}))
		.A(KView(new(){Key=o, Label="O", SwipeUp=O, SwipeLeft=Braces_L, SwipeRight=Braces_R}))
		.A(KView(new(){Key=p, Label="Π", SwipeUp=P, SwipeLeft=Less, SwipeRight=Greater}))
		.A(KView(new(){Key=y, Label="Y", Hint="⇆", HintBottom="↷", SwipeUp=Y, SwipeLeftAction=MkToggleAsciiMode(), SwipeRightAction=MkSendCtrlKey(y), LongClickAction=MkSendCtrlKey(y)}))
		;
		return root.Grid;
	}

	Grid MkRow3(){
		var root = new GridStack(IsRow: false);
		root.SetColDefs([
			new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
			new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
		]);
		root
		.A(KView(new(){Key=a, Label="A", HintBottom="☑", SwipeUp=A, SwipeRightAction=MkSendCtrlKey(a), LongClickAction=MkSendCtrlKey(a)}))
		.A(KView(new(){Key=s, Label="Σ", Hint="⇪", SwipeUp=S}))
		.A(KView(new(){Key=d, Label="Δ", SwipeUp=D}))
		.A(KView(new(){Key=f, Label="F", SwipeUp=F}))
		.A(KView(new(){Key=g, Label="G", SwipeUp=G, SwipeLeft=Left, SwipeRight=Right, SwipeDown=Down}))
		.A(KView(new(){Key=h, Label="H", SwipeUp=H}))
		.A(KView(new(){Key=j, Label="J", SwipeUp=J}))
		.A(KView(new(){Key=k, Label="K", SwipeUp=K}))
		.A(KView(new(){Key=l, Label="Λ", SwipeUp=L}))
		.A(KView(new(){Key=Semicolon, Label=";", Hint=":", SwipeUp=Colon, LongClick=Colon}))
		;
		return root.Grid;
	}

	Grid MkRow4(){
		var root = new GridStack(IsRow: false);
		root.SetColDefs([
			new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
			new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
		]);
		root
		.A(KView(new(){Key=z, Label="Z", HintBottom="↶", SwipeUp=Z, SwipeRightAction=MkSendCtrlKey(z), LongClickAction=MkSendCtrlKey(z)}))
		.A(KView(new(){Key=x, Label="X", HintBottom="✁", SwipeUp=X, SwipeRightAction=MkSendCtrlKey(x), LongClickAction=MkSendCtrlKey(x)}))
		.A(KView(new(){Key=c, Label="C", HintBottom="❐", SwipeUp=C, SwipeRightAction=MkSendCtrlKey(c), LongClickAction=MkSendCtrlKey(c)}))
		.A(KView(new(){Key=v, Label="V", HintBottom="▣", SwipeUp=V, SwipeRightAction=MkSendCtrlKey(v), LongClickAction=MkSendCtrlKey(v)}))
		.A(KView(new(){Key=b, Label="B", SwipeUp=B}))
		.A(KView(new(){Key=n, Label="N", SwipeUp=N}))
		.A(KView(new(){Key=m, Label="M", Hint="$m,", SwipeUp=M, SwipeDownAction=MkSendTextAndSpace(Dollar, m, Comma), SwipeLeftAction=MkSendTextAndSpace(Dollar, m, Comma, j), SwipeRightAction=MkSendTextAndSpace(Dollar, m, Comma, i)}))
		.A(KView(new(){Key=Comma, Label=",", Hint="<", SwipeUp=Less, LongClick=Less, SwipeLeft=Less}))
		.A(KView(new(){Key=Period, Label=".", Hint=">", SwipeUp=Greater, LongClick=Greater, SwipeRight=Greater}))
		.A(KView(new(){Key=Apostrophe, Label="'", Hint="\"", SwipeUp=Quote}))
		;
		return root.Grid;
	}

	Grid MkRow5(){
		var root = new GridStack(IsRow: false);
		root.SetColDefs([
			new(2, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
			new(1, GUT.Star), new(1, GUT.Star), new(2, GUT.Star),
		]);
		root
		.A(KView(new(){Key=Enter, Label="↵"}))
		.A(KView(new(){Key=Tab, Label="␉"}))
		// 左右方向鍵補行首/行尾手勢：長按與上滑都走更強的導航語義。
		.A(KView(new(){Key=Left, Label="←", Hint="⇤", LongClick=Home, SwipeUp=Home}))
		.A(KView(new(){Key=Space, Label="", SwipeLeft=Left, SwipeRight=Right}))
		.A(KView(new(){Key=Space, Label="", SwipeLeft=Left, SwipeRight=Right}))
		.A(KView(new(){Key=Right, Label="→", Hint="⇥", LongClick=End, SwipeUp=End}))
		.A(KView(new(){Key=Dollar, Label="$", Hint="⇪", SwipeUpAction=MkToggleShiftLock()}))
		.A(KView(new(){Key=Backspace, Label="⌫", LongClick=Backspace, SwipeUpAction=MkBackspaceSwipeUpHideKeyboardOrNull(), IsRepeat=true}))
		;
		return root.Grid;
	}

	Grid MkRow6(){
		var root = new GridStack(IsRow: false);
		root.SetColDefs([
			new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
			new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
		]);
		root
		.A(KView(new(){Key=Minus, Label="-", Hint="_", SwipeUp=Underscore}))
		.A(KView(new(){Key=Equal, Label="=", Hint="+", SwipeUp=Plus}))
		.A(KView(new(){Key=SquareBracket_L, Label="[", Hint="{", SwipeUp=Braces_L, LongClick=Braces_L}))
		.A(KView(new(){Key=SquareBracket_R, Label="]", Hint="}", SwipeUp=Braces_R, LongClick=Braces_R}))
		.A(KView(new(){Key=Up, Label="↑"}))
		.A(KView(new(){Key=Down, Label="↓"}))
		.A(KView(new(){Key=Slash, Label="/", Hint="?", SwipeUp=Question}))
		.A(KView(new(){Key=BackSlash, Label="\\", Hint="|", SwipeUp=Pipe}))
		.A(KView(new(){Key=Grave, Label="`", Hint="~", SwipeUp=Tilde}))
		.A(MkActionKey("123", () => Ctx.IsNumLayout = true))
		;
		return root.Grid;
	}

	Grid MkNumRow1(){
		var root = new GridStack(IsRow: false);
		root.SetColDefs([
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
		]);
		root
		.A(KView(new(){Key=D1, Hint="!", LongClick=Exclamation, SwipeUp=Exclamation}))
		.A(KView(new(){Key=D2, Hint="@", LongClick=At, SwipeUp=At}))
		.A(KView(new(){Key=D3, Hint="#", LongClick=HashTag, SwipeUp=HashTag}))
		.A(KView(new(){Key=D4, Hint="$", LongClick=Dollar, SwipeUp=Dollar}))
		.A(KView(new(){Key=D5, Hint="%", LongClick=Percent, SwipeUp=Percent}))
		.A(KView(new(){Key=D6, Hint="^", LongClick=Caret, SwipeUp=Caret}))
		.A(KView(new(){Key=D7, Hint="&", LongClick=Ampersand, SwipeUp=Ampersand}))
		.A(KView(new(){Key=D8, Hint="*", LongClick=Asterisk, SwipeUp=Asterisk}))
		.A(KView(new(){Key=D9, Hint="(", LongClick=Paren_L, SwipeUp=Paren_L}))
		.A(KView(new(){Key=D0, Hint=")", LongClick=Paren_R, SwipeUp=Paren_R}))
		;
		return root.Grid;
	}

	Grid MkNumRow2(){
		var root = new GridStack(IsRow: false);
		root.SetColDefs([
			new(1, GUT.Star), new(1, GUT.Star), new(2, GUT.Star), new(2, GUT.Star),
			new(2, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
		]);
		root
		.A(KView(new(){Key=q, Label="Q", SwipeUp=Q}))
		.A(KView(new(){Key=w, Label="W", SwipeUp=W}))
		.A(KView(new(){Key=D7}))
		.A(KView(new(){Key=D8}))
		.A(KView(new(){Key=D9, SwipeUp=I, LongClick=SquareBracket_L, SwipeLeft=SquareBracket_L, SwipeRight=SquareBracket_R}))
		.A(KView(new(){Key=p, Label="Π", SwipeUp=P, SwipeLeft=Less, SwipeRight=Greater}))
		.A(KView(new(){Key=y, Label="Y", Hint="⇆", HintBottom="↷", SwipeUp=Y, SwipeRightAction=MkSendCtrlKey(y), LongClickAction=MkSendCtrlKey(y)}))
		;
		return root.Grid;
	}

	Grid MkNumRow3(){
		var root = new GridStack(IsRow: false);
		root.SetColDefs([
			new(1, GUT.Star), new(1, GUT.Star), new(2, GUT.Star), new(2, GUT.Star),
			new(2, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
		]);
		root
		.A(KView(new(){Key=a, Label="A", SwipeUp=A}))
		.A(KView(new(){Key=s, Label="Σ", Hint="⇪", SwipeUp=S}))
		.A(KView(new(){Key=D4, SwipeUp=D}))
		.A(KView(new(){Key=D5, SwipeUp=G, SwipeLeft=Left, SwipeRight=Right, SwipeDown=Down}))
		.A(KView(new(){Key=D6, SwipeUp=J}))
		.A(KView(new(){Key=l, Label="Λ", SwipeUp=L}))
		.A(KView(new(){Key=Semicolon, Label=";", Hint=":", SwipeUp=Colon, LongClick=Colon}))
		;
		return root.Grid;
	}

	Grid MkNumRow4(){
		var root = new GridStack(IsRow: false);
		root.SetColDefs([
			new(1, GUT.Star), new(1, GUT.Star), new(2, GUT.Star), new(2, GUT.Star),
			new(2, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
		]);
		root
		.A(KView(new(){Key=z, Label="Z", SwipeUp=Z}))
		.A(KView(new(){Key=x, Label="X", SwipeUp=X}))
		.A(KView(new(){Key=D1, SwipeUp=C}))
		.A(KView(new(){Key=D2, SwipeUp=B}))
		.A(KView(new(){Key=D3, SwipeUp=M}))
		.A(KView(new(){Key=Period, Label=".", Hint=">", SwipeUp=Greater, LongClick=Greater, SwipeRight=Greater}))
		.A(KView(new(){Key=Apostrophe, Label="'", Hint="\"", SwipeUp=Quote}))
		;
		return root.Grid;
	}

	Grid MkNumRow5(){
		var root = new GridStack(IsRow: false);
		root.SetColDefs([
			new(2, GUT.Star), new(2, GUT.Star), new(2, GUT.Star), new(2, GUT.Star), new(2, GUT.Star),
		]);
		root
		.A(KView(new(){Key=Enter, Label="↵"}))
		.A(KView(new(){Key=D0}))
		.A(KView(new(){Key=Space, Label="", SwipeLeft=Left, SwipeRight=Right}))
		.A(KView(new(){Key=Period, Label=".", Hint=">", SwipeUp=Greater}))
		.A(KView(new(){Key=Backspace, Label="⌫", LongClick=Backspace, SwipeUpAction=MkBackspaceSwipeUpHideKeyboardOrNull(), IsRepeat=true}))
		;
		return root.Grid;
	}

	Grid MkNumRow6(){
		var root = new GridStack(IsRow: false);
		root.SetColDefs([
			new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
			new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star), new(1, GUT.Star),
		]);
		root
		.A(KView(new(){Key=Minus, Label="-", Hint="_", SwipeUp=Underscore}))
		.A(KView(new(){Key=Equal, Label="=", Hint="+", SwipeUp=Plus}))
		.A(KView(new(){Key=Slash, Label="/", Hint="?", SwipeUp=Question}))
		.A(KView(new(){Key=BackSlash, Label="\\", Hint="|", SwipeUp=Pipe}))
		// 數字層同樣提供行首/行尾手勢，避免切層後丟失導航能力。
		.A(KView(new(){Key=Left, Label="←", Hint="⇤", LongClick=Home, SwipeUp=Home}))
		.A(KView(new(){Key=Right, Label="→", Hint="⇥", LongClick=End, SwipeUp=End}))
		.A(KView(new(){Key=Up, Label="↑"}))
		.A(KView(new(){Key=Down, Label="↓"}))
		.A(KView(new(){Key=Grave, Label="`", Hint="~", SwipeUp=Tilde}))
		.A(MkActionKey("qwe", () => Ctx.IsNumLayout = false))
		;
		return root.Grid;
	}

	public void Dispose()
	{
		if(Ctx is not null){
			foreach(var handler in _ctxHandlers){
				Ctx.PropertyChanged -= handler;
			}
		}
		_ctxHandlers.Clear();
		foreach(var disposable in _disposables){
			disposable.Dispose();
		}
		_disposables.Clear();
	}
}

