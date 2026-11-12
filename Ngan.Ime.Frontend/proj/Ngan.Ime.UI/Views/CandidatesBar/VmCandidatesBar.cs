namespace Ngan.Ime.UI.Views.CandidatesBar;

using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.UI.Infra;
using Ngan.Ime.UI.Views.Candidate;
using Tsinswreng.CsCore;

[Doc($$"""
候選欄 ViewModel：把引擎的候選頁映射到固定 16 個 {{nameof(VmCandidate)}} 槽位。

*為何是固定槽位*：候選欄在鍵盤上方，寬度固定、最多顯示一頁；池化後刷新只改文字與可見性，
不重建控件樹（這是舊版就做對的效能設計，本輪保留）。

*選詞鍵*：Rime 的默認選詞鍵是 `1`～`9` 與 `0`（第 1～10 個候選）。
第 11 個以後**沒有對應鍵**，故不可點——舊版用 `_ => D1` 兜底，會把第 1 個候選上屏。

*高度*：頂欄高度由註釋顯示狀態決定，暴露 {{nameof(TopBarHeight)}} 給視圖綁定。

#Eg[
	```cs
	Vm.SetSlotMinWidth(Width / 10);
	```
]
""")]
public partial class VmCandidatesBar : AppVmBase, IMk<VmCandidatesBar>, IDisposable{

	[Doc("選詞鍵：第 1～9 個候選用 1～9，第 10 個用 0。")]
	static readonly IKeyChar[] SelectKeys = [
		KeyChars.D1, KeyChars.D2, KeyChars.D3, KeyChars.D4, KeyChars.D5,
		KeyChars.D6, KeyChars.D7, KeyChars.D8, KeyChars.D9, KeyChars.D0,
	];

	[Doc("引擎狀態（候選頁快照）。")]
	readonly IImeState State;

	[Doc("按鍵入口（點候選即送選詞鍵）。")]
	readonly IImeInput Input;

	[Doc("共享界面狀態（註釋顯示）。")]
	readonly ImeUiState Ui;

	[Doc("引擎輸入事件處理器；Dispose 時解除。")]
	readonly EventHandler<IEnumerable<IKeyEvent>> AfterInputHandler;

	[Doc("槽位集合；永遠 16 個，順序即下標。")]
	public ObservableCollection<VmCandidate> CandVms{get;} = [];

	[Doc("目前欄寬對應的槽位最小寬度；-1 表示尚未算過。")]
	f64 SlotMinWidth = -1;

	[Doc("頂欄高度：顯示註釋時較高。")]
	public f64 TopBarHeight => Ui.IsCandidateCommentVisible
		? UiCfg.Inst.TopBarHeight
		: UiCfg.Inst.TopBarHeightNoComment;

	[Doc("建立候選欄：預建槽位並訂閱引擎與 UI 狀態。")]
	public VmCandidatesBar(IImeState State, IImeInput Input, ImeUiState Ui){
		this.State = State;
		this.Input = Input;
		this.Ui = Ui;
		WarmCandidatePool();
		AfterInputHandler = (_, _) => OnAfterInput();
		Input.AfterInput += AfterInputHandler;
		Ui.PropertyChanged += OnUiChanged;
		base.Init();
	}

	[Doc("兜底建立：無依賴（設計器預覽用）。")]
	protected VmCandidatesBar(){
		State = default!;
		Input = default!;
		Ui = default!;
		AfterInputHandler = (_, _) => { };
		WarmCandidatePool();
		base.Init();
	}

	[Doc("兜底建立。")]
	public static VmCandidatesBar Mk(){
		return new VmCandidatesBar();
	}

	[Doc("預建 16 個隱藏槽位，首顯時只切內容。")]
	void WarmCandidatePool(){
		for(i32 Index = 0; Index < VmCandidate.PoolSize; Index++){
			var Vm = VmCandidate.Mk();
			Vm.Background = UiCfg.Inst.CandidateBgColor;
			Vm.Height = UiCfg.Inst.TopBarHeightNoComment;
			Vm.IsVisible = false;
			Vm.ShowComment = false;
			CandVms.Add(Vm);
		}
	}

	[Doc("按欄寬設定所有槽位的最小寬度；寬度沒變就不做事。")]
	public void SetSlotMinWidth(f64 Width){
		if(Width <= 0){
			return;
		}
		var MinWidth = Width / 10.0;
		if(Math.Abs(MinWidth - SlotMinWidth) < 0.5){
			return;
		}
		SlotMinWidth = MinWidth;
		foreach(var Vm in CandVms){
			Vm.MinWidth = MinWidth;
		}
	}

	[Doc("引擎輸入完畢：讀一次候選快照，切回 UI 線程套用。")]
	void OnAfterInput(){
		var Page = State.Candidates;
		if(Dispatcher.UIThread.CheckAccess()){
			Apply(Page);
			return;
		}
		Dispatcher.UIThread.Post(() => Apply(Page));
	}

	[Doc("把候選頁套用到槽位：前 N 個填內容、其餘隱藏。")]
	void Apply(CandidatePage Page){
		var Items = Page.Data ?? [];
		var VisibleCount = Math.Min(Items.Count, CandVms.Count);
		if(Items.Count > CandVms.Count){
			AppLog.Warn($"[Candidates] 本頁候選 {Items.Count} 個，超過槽位池 {CandVms.Count}，其餘不再顯示");
		}
		for(i32 Index = 0; Index < VisibleCount; Index++){
			Fill(CandVms[Index], Items[Index], Index, Index == Page.HighlightedIndex);
		}
		for(i32 Index = VisibleCount; Index < CandVms.Count; Index++){
			Hide(CandVms[Index]);
		}
	}

	[Doc("填一個槽位：文字、註釋、高亮色與點擊行為都隨當前內容重設。")]
	void Fill(VmCandidate Vm, ICandidate One, i32 Index, bool Highlighted){
		Vm.Text = One.Text ?? "";
		Vm.Comment = One.Comment ?? "";
		Vm.Foreground = Highlighted ? UiCfg.Inst.MainColor : Brushes.White;
		Vm.ShowComment = Ui.IsCandidateCommentVisible;
		Vm.IsVisible = true;
		// 只有前 10 個候選有選詞鍵；其餘不可點，避免誤選。
		if(Index < SelectKeys.Length){
			var Key = SelectKeys[Index];
			Vm.IsClickable = true;
			Vm.Click = () => {
				Input.InputSafely([
					new KeyEvent{ KeyChar = Key, KeyState = KeyStates.Down },
					new KeyEvent{ KeyChar = Key, KeyState = KeyStates.Up },
				], HandleErr);
			};
		}else{
			Vm.IsClickable = false;
			Vm.Click = null;
		}
	}

	[Doc("收起一個槽位：清空文字並隱藏，但保留實例。")]
	static void Hide(VmCandidate Vm){
		Vm.Text = "";
		Vm.Comment = "";
		Vm.Foreground = Brushes.White;
		Vm.IsVisible = false;
		Vm.IsClickable = false;
		Vm.Click = null;
	}

	[Doc("註釋顯示切換時，同步槽位高度與註釋可見性。")]
	void OnUiChanged(obj? Sender, System.ComponentModel.PropertyChangedEventArgs Args){
		if(Args.PropertyName != nameof(ImeUiState.IsCandidateCommentVisible)){
			return;
		}
		var Height = TopBarHeight;
		foreach(var Vm in CandVms){
			Vm.Height = Height;
			Vm.ShowComment = Ui.IsCandidateCommentVisible;
		}
		OnPropertyChanged(nameof(TopBarHeight));
	}

	[Doc("解除事件訂閱。")]
	public void Dispose(){
		if(Input is not null){
			Input.AfterInput -= AfterInputHandler;
		}
		if(Ui is not null){
			Ui.PropertyChanged -= OnUiChanged;
		}
		GC.SuppressFinalize(this);
	}
}
