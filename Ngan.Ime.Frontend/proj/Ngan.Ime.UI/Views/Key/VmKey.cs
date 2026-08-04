using System;
using System.ComponentModel;
using Ngan.Ime.Core.Infra;
using Ngan.Ime.Core.Infra.Log;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.UI;
using Ngan.Ime.ViewModels;
using Avalonia.Media;
using KS = Ngan.Ime.Core.Keys.KeyStates;

namespace Ngan.Ime.UI.Views.Key;
using Ctx = VmKey;

public partial class VmKey : ViewModelBase, IKeyViewModel
	, IMk<VmKey>
	, IDisposable
{
	readonly PropertyChangedEventHandler _imePropertyChangedHandler;
	str _baseLabel = "";
	str _displayLabel = "";

	public VmKey(ISvcIme ImeState){
		this.ImeState = ImeState;
		// 先綁定 ImeState，再做 label 同步；split overlay 預熱時會在構造階段直接算顯示文本。
		// 若此時還沒賦值就讀 IsAsciiMode，會把整個 IME 啓動路徑崩掉。
		SyncDisplayLabel();
		_imePropertyChangedHandler = (_, e) => {
			if(e.PropertyName == nameof(ISvcIme.IsAsciiMode)){
				SyncDisplayLabel();
			}
		};
		this.ImeState.PropertyChanged += _imePropertyChangedHandler;

		Click = ()=>{
			var state = this.ImeState;
			try{
				var sw = System.Diagnostics.Stopwatch.StartNew();
				AppLog.Debug($"[Perf] VmKey.Click→Input start: {sw.ElapsedMilliseconds}ms");
				state?.InputSafely([
					new KeyEvent{
						KeyChar = Key_Click,
						KeyState = KS.Down
					},
					new KeyEvent{
						KeyChar = Key_Click,
						KeyState = KS.Up
					}
				], e => HandleErr(e));
				AppLog.Debug($"[Perf] VmKey.Click→Input done: {sw.ElapsedMilliseconds}ms");
			}
			catch(Exception e){
				HandleErr(e);
			}
			return 0;
		};
	}

	/// <summary>
	/// 單鍵 VM 的兜底創建路徑。
	/// </summary>
	public static Ctx Mk()
	{
		return new Ctx(Di.GetRSvc<ISvcIme>());
	}

	public Func<zero>? Click{get;set;}
	public Func<zero>? LongPress{get;set;}
	public Func<zero>? SwipeLeft{get;set;}
	public Func<zero>? SwipeDown{get;set;}
	public Func<zero>? SwipeUP{get;set;}
	public Func<zero>? SwipeRight{get;set;}

	public bool IsRepeat{get;set;}

	public ISvcIme ImeState{get;set;}

	public IKeyChar Key_Click{
		get => field;
		set{
			_baseLabel = value.Name ?? "";
			SetProperty(ref field, value);
			SyncDisplayLabel();
		}
	}

	public str Label{
		get => _displayLabel;
		set{
			_baseLabel = value;
			SyncDisplayLabel();
		}
	}

	void SyncDisplayLabel()
	{
		var label = _baseLabel;
		if(ImeState is not null && ImeState.IsAsciiMode){
			var keyName = Key_Click?.Name;
			// ASCII 模式下鍵帽一律回到實際鍵值對應的小寫拉丁字母。
			// 例如平時可顯示 Σ / Δ / Π，但切到 ASCII 時必須顯示 s / d / p。
			if(!string.IsNullOrEmpty(keyName) && keyName.Length == 1 && char.IsAsciiLetter(keyName[0])){
				label = char.ToLowerInvariant(keyName[0]).ToString();
			}else if(label.Length == 1 && char.IsAsciiLetter(label[0])){
				label = char.ToLowerInvariant(label[0]).ToString();
			}
		}
		if(_displayLabel == label){
			return;
		}
		_displayLabel = label;
		OnPropertyChanged(nameof(Label));
	}

	public str Hint{
		get => field;
		set => SetProperty(ref field, value);
	} = "";

	public str BottomHint{
		get => field;
		set => SetProperty(ref field, value);
	} = "";

	public IBrush Background{
		get => field;
		set => SetProperty(ref field, value);
	} = UiCfg.Inst.KeyBgColor;

	public double FontSize{
		get => field;
		set => SetProperty(ref field, value);
	} = UiCfg.Inst.KeyFontSize;

	public void Dispose()
	{
		if(ImeState is not null){
			ImeState.PropertyChanged -= _imePropertyChangedHandler;
		}
	}
}
