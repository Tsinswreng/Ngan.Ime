using Avalime.Core.Infra;
using Avalime.Core.Language;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;

namespace Avalime.UI.Views.VoiceInput;

/// 語音输入页的完整前端实现。
/// 录音 Provider 尚未接入，但布局、文本编辑、语言选择与宿主能力降级均按最终数据流工作。
public partial class ViewVoiceInput{
	/// 建立视图、订阅 VM 状态并组装控件树。
	public partial ViewVoiceInput(){
		Ctx = Di.DiOrMk<VmVoiceInput>();
		Render();
		_ctxPropertyChangedHandler = (_, E)=>{
			if(E.PropertyName is nameof(VmVoiceInput.Phase) or nameof(VmVoiceInput.IsRecording)){
				SyncRecordingVisual();
			}
		};
		if(Ctx is not null){
			Ctx.PropertyChanged += _ctxPropertyChangedHandler;
		}
		SyncRecordingVisual();
	}

	/// 使用两个等权重主体行保证文本区至少占一半高度，并在最上层放语言选择覆盖层。
	private partial void Render(){
		var ControlArea = new GridStack(IsRow: true);
		ControlArea.SetRowDefs([
			new(1, GUT.Auto),
			new(1, GUT.Star),
		]);
		ControlArea.A(MkLangBar())
		.A(MkRecordArea());

		this.SetContent(new Grid(), O=>{
			O.Background = Brushes.Black;
			O.A(Root.Grid, _=>{
				Root.SetRowDefs([
					new(1, GUT.Star),
					new(1, GUT.Auto),
					new(1, GUT.Star),
				]);
				Root.A(MkDraftArea())
				.A(MkDraftActionBar())
				.A(ControlArea.Grid);
			})
			.A(new ViewVoiceInputLangSelector(), V=>{
				LangSelector = V;
				V.IsVisible = false;
				V.IsHitTestVisible = false;
				V.OnSelected += OnLangSelected;
				V.OnCloseRequested += CloseLangSelector;
			});
		});
	}

	/// 建立多行草稿文本框，长内容在控件内部滚动。
	private partial Control MkDraftArea(){
		var R = new Border{
			Background = UiCfg.Inst.CandidateBgColor,
			BorderBrush = UiCfg.Inst.GapLineBrush,
			BorderThickness = new(0, 0, 0, 0.5),
			Padding = new(8),
		};
		R.SetChild(new TextBox(), T=>{
			DraftTextBox = T;
			T.AcceptsReturn = true;
			T.TextWrapping = TextWrapping.Wrap;
			T.Background = Brushes.Black;
			T.Foreground = Brushes.White;
			T.BorderBrush = UiCfg.Inst.GapLineBrush;
			T.FontSize = UiCfg.Inst.BaseFontSize * 0.55;
			T.Padding = new(10);
			Ctx.Bind(T, X=>X.Text, X=>X.DraftText, Mode: BindingMode.TwoWay);
			Ctx.Bind(T, X=>X.IsReadOnly, X=>X.CanEditDraft, Converter: new FnConvtr<bool, bool>(X=>!X));
		});
		return R;
	}

	/// 建立清空与上屏操作；Windows 上屏能力不足时显示说明并禁用按钮。
	private partial Control MkDraftActionBar(){
		var R = new GridStack(IsRow: false);
		R.SetColDefs([
			new(1, GUT.Auto),
			new(1, GUT.Star),
			new(1, GUT.Auto),
		]);
		R.A(new Button(), B=>{
			ClearTextButton = B;
			B.SetContent("清空");
			B.Click += (_, _)=>ClearText();
			Ctx.Bind(B, X=>X.IsEnabled, X=>X.CanClearText);
		})
		.A(new TextBlock(), T=>{
			CommitUnsupportedText = T;
			T.Text = "Windows 宿主尚未接入上屏";
			T.Foreground = Brushes.Gray;
			T.VerticalAlignment = VAlign.Center;
			T.HorizontalAlignment = HAlign.Center;
			T.FontSize = UiCfg.Inst.BaseFontSize * 0.38;
			Ctx.Bind(T, X=>X.IsVisible, X=>X.IsCommitSupported, Converter: new FnConvtr<bool, bool>(X=>!X));
		})
		.A(new Button(), B=>{
			CommitTextButton = B;
			B.Background = UiCfg.Inst.MainColor;
			B.Foreground = Brushes.Black;
			B.SetContent("上屏");
			B.Click += (_, _)=>CommitText();
			Ctx.Bind(B, X=>X.IsEnabled, X=>X.CanCommitText);
		});
		return R.Grid;
	}

	/// 建立当前语言入口，识别忙碌期间通过绑定禁用。
	private partial Control MkLangBar(){
		var R = new Border{
			Background = Brushes.Black,
			BorderBrush = UiCfg.Inst.GapLineBrush,
			BorderThickness = new(0, 0, 0, 0.5),
			Padding = new(8, 5),
		};
		R.SetChild(new Button(), B=>{
			LangButton = B;
			B.Background = UiCfg.Inst.CandidateBgColor;
			B.Foreground = Brushes.White;
			B.BorderBrush = UiCfg.Inst.GapLineBrush;
			B.CornerRadius = new(UiCfg.Inst.BaseFontSize);
			B.Padding = new(16, 5);
			B.HorizontalAlignment = HAlign.Center;
			B.Click += (_, _)=>OpenLangSelector();
			Ctx.Bind(B, X=>X.IsEnabled, X=>X.CanSelectLang);
			B.SetContent(new TextBlock(), T=>{
				LangText = T;
				T.FontSize = UiCfg.Inst.BaseFontSize * 0.48;
				T.Foreground = Brushes.White;
				Ctx.Bind(T, X=>X.Text, X=>X.CurrentLangDisplay);
			});
		});
		return R;
	}

	/// 建立状态、错误及圆形录音按钮，保持下半区紧凑可用。
	private partial Control MkRecordArea(){
		var R = new GridStack(IsRow: true);
		R.SetRowDefs([
			new(1, GUT.Star),
			new(1, GUT.Auto),
			new(1, GUT.Auto),
			new(1, GUT.Auto),
			new(1, GUT.Star),
		]);
		R.A(new Border())
		.A(new TextBlock(), T=>{
			StatusText = T;
			T.HorizontalAlignment = HAlign.Center;
			T.Foreground = Brushes.LightGray;
			T.FontSize = UiCfg.Inst.BaseFontSize * 0.45;
			Ctx.Bind(T, X=>X.Text, X=>X.StatusText);
		})
		.A(new TextBlock(), T=>{
			ErrorText = T;
			T.HorizontalAlignment = HAlign.Center;
			T.Foreground = Brushes.OrangeRed;
			T.FontSize = UiCfg.Inst.BaseFontSize * 0.38;
			Ctx.Bind(T, X=>X.Text, X=>X.ErrorText);
			Ctx.Bind(T, X=>X.IsVisible, X=>X.HasError);
		})
		.A(new Button(), B=>{
			RecordButton = B;
			var Diameter = UiCfg.Inst.BaseFontSize * 2.7;
			B.Width = Diameter;
			B.Height = Diameter;
			B.BorderThickness = new(0);
			B.CornerRadius = new(Diameter / 2);
			B.HorizontalAlignment = HAlign.Center;
			B.VerticalAlignment = VAlign.Center;
			B.Click += async(_, _)=>await ToggleRecording(default);
			B.SetContent(Avalime.UI.Icons.Icons.Microphone(), I=>{
				RecordIcon = I;
				I.Width = UiCfg.Inst.BaseFontSize;
				I.Height = UiCfg.Inst.BaseFontSize;
			});
		})
		.A(new Border());
		return R.Grid;
	}

	/// 打开覆盖层前再次检查状态，防止延迟点击绕过按钮禁用。
	private partial void OpenLangSelector(){
		if(Ctx?.CanSelectLang != true || LangSelector is null){
			return;
		}
		LangSelector.IsVisible = true;
		LangSelector.IsHitTestVisible = true;
	}

	/// 应用选择并关闭覆盖层。
	private partial void OnLangSelected(NormLangDetail Lang){
		Ctx?.ApplyLang(Lang);
		CloseLangSelector();
	}

	/// 统一关闭语言覆盖层，供选择与取消复用。
	private partial void CloseLangSelector(){
		if(LangSelector is null){
			return;
		}
		LangSelector.IsVisible = false;
		LangSelector.IsHitTestVisible = false;
	}

	/// 按当前录音状态切换背景和图标对比度。
	private partial void SyncRecordingVisual(){
		if(RecordButton is null || RecordIcon is not Avalonia.Controls.Shapes.Shape Icon){
			return;
		}
		var Recording = Ctx?.IsRecording == true;
		RecordButton.Background = Recording ? UiCfg.Inst.MainColor : Brushes.White;
		Icon.Fill = Brushes.Black;
	}

	/// 把录音操作交给 VM，View 不直接接触 Provider。
	private async partial Task<nil> ToggleRecording(CT Ct){
		if(Ctx is not null){
			await Ctx.ToggleRecording(Ct);
		}
		return NIL;
	}

	/// 清空操作只调用 VM。
	private partial void ClearText(){
		Ctx?.ClearText();
	}

	/// 上屏操作只调用 VM。
	private partial void CommitText(){
		Ctx?.CommitText();
	}

	/// 解除订阅与覆盖层事件，并异步取消仍在进行的识别会话。
	public partial void Dispose(){
		if(_ctxPropertyChangedHandler is not null && Ctx is not null){
			Ctx.PropertyChanged -= _ctxPropertyChangedHandler;
		}
		if(LangSelector is not null){
			LangSelector.OnSelected -= OnLangSelected;
			LangSelector.OnCloseRequested -= CloseLangSelector;
			LangSelector.Dispose();
		}
		if(Ctx is not null){
			_ = Ctx.CancelRecognition(default);
		}
	}
}
