namespace Ngan.Ime.UI.Views.VoiceInput;

using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Ngan.Ime.Core.Language;
using Ngan.Ime.UI.Icons;
using Ngan.Ime.UI.Infra;

[Doc($$"""
{{nameof(ViewVoiceInput)}} 的實現：三欄佈局 ＋ 全綁定的錄音視覺。
""")]
public partial class ViewVoiceInput{

	[Doc("草稿字級比例。")]
	const f64 DraftFontScale = 0.55;

	[Doc("語言按鈕字級比例。")]
	const f64 LangFontScale = 0.48;

	[Doc("狀態文字字級比例。")]
	const f64 StatusFontScale = 0.4;

	[Doc("錯誤文字字級比例。")]
	const f64 ErrorFontScale = 0.34;

	[Doc("提示文字字級比例。")]
	const f64 HintFontScale = 0.34;

	[Doc("錄音按鈕直徑比例。")]
	const f64 RecordDiameterScale = 2.05;

	[Doc("麥克風圖標尺寸比例。")]
	const f64 RecordIconScale = 0.8;

	[Doc("組出三欄佈局與覆蓋層。")]
	protected override partial object Build(VmVoiceInput vm){
		_LangSelector = new ViewVoiceInputLangSelector{
			IsVisible = false,
			IsHitTestVisible = false,
		};
		_LangSelector.OnSelected += OnLangSelected;
		_LangSelector.OnCloseRequested += CloseLangSelector;

		var Root = new Grid()
			.Cols("*,Auto,2*")
			.With(G => G.Background = Brushes.Black)
			.Children(
				MkDraftArea(vm)
				,new Border()
					.Width(1)
					.Background(UiCfg.Inst.GapLineBrush)
				,MkControlArea(vm)
				,_LangSelector
			);
		return Root;
	}

	[Doc("左欄：多行草稿，忙碌時只讀。")]
	Control MkDraftArea(VmVoiceInput vm){
		_DraftTextBox = new TextBox()
			.With(T => {
				T.AcceptsReturn = true;
				T.TextWrapping = TextWrapping.Wrap;
				T.Background = Brushes.Black;
				T.Foreground = Brushes.White;
				T.BorderBrush = UiCfg.Inst.GapLineBrush;
				T.BorderThickness = new Avalonia.Thickness(1);
				T.FontSize = UiCfg.Inst.BaseFontSize * DraftFontScale;
				T.Padding = new Avalonia.Thickness(8);
			})
			.Text(vm, X => X.DraftText, BindingMode.TwoWay)
			.IsReadOnly(vm, X => X.IsDraftReadOnly);
		return new Border()
			.Background(Brushes.Black)
			.BorderBrush(UiCfg.Inst.GapLineBrush)
			.BorderThickness(new Avalonia.Thickness(0))
			.Padding(new Avalonia.Thickness(8, 6))
			.Child(_DraftTextBox);
	}

	[Doc("右欄：語言列、狀態區、錄音區、能力提示、動作列。")]
	Control MkControlArea(VmVoiceInput vm){
		return new StackGrid(IsRow: true)
			.Rows("Auto,*,Auto,Auto,Auto")
			.Children(
				MkLangBar(vm)
				,MkStatusArea(vm)
				,MkRecordArea(vm)
				,new TextBlock()
					.Text(Todo.I18n("本宿主暫不支援上屏"))
					.TextWrapping(TextWrapping.Wrap)
					.Foreground(Brushes.Gray)
					.HorizontalAlignment(HAlign.Center)
					.FontSize(UiCfg.Inst.BaseFontSize * HintFontScale)
					.Margin(new Avalonia.Thickness(6, 1))
					.IsVisible(vm, X => X.ShowCommitUnsupported)
				,MkActionBar(vm)
			);
	}

	[Doc("語言入口：點擊打開覆蓋層，忙碌時禁用。")]
	Control MkLangBar(VmVoiceInput vm){
		_LangButton = new Button()
			.With(B => {
				B.Background = UiCfg.Inst.CandidateBgColor;
				B.Foreground = Brushes.White;
				B.BorderBrush = UiCfg.Inst.GapLineBrush;
				B.BorderThickness = new Avalonia.Thickness(1);
				B.CornerRadius = new Avalonia.CornerRadius(UiCfg.Inst.BaseFontSize * 0.35);
				B.Padding = new Avalonia.Thickness(8, 4);
				B.HorizontalAlignment = HAlign.Stretch;
				B.HorizontalContentAlignment = HAlign.Left;
				B.Click += (_, _) => OpenLangSelector();
			})
			.IsEnabled(vm, X => X.CanSelectLang)
			.Content(new TextBlock()
				.FontSize(UiCfg.Inst.BaseFontSize * LangFontScale)
				.Foreground(Brushes.White)
				.Text(vm, X => X.CurrentLangDisplay));
		return new Border()
			.Background(Brushes.Black)
			.BorderBrush(UiCfg.Inst.GapLineBrush)
			.BorderThickness(new Avalonia.Thickness(0, 0, 0, 0.5))
			.Padding(new Avalonia.Thickness(6, 4))
			.Child(_LangButton);
	}

	[Doc("狀態區：唯一的可伸縮列，狀態文字居中、錯誤文字在下。")]
	Control MkStatusArea(VmVoiceInput vm){
		_StatusText = new TextBlock()
			.HorizontalAlignment(HAlign.Center)
			.VerticalAlignment(VAlign.Bottom)
			.Foreground(Brushes.LightGray)
			.TextWrapping(TextWrapping.Wrap)
			.FontSize(UiCfg.Inst.BaseFontSize * StatusFontScale)
			.Text(vm, X => X.StatusText);
		_ErrorText = new TextBlock()
			.HorizontalAlignment(HAlign.Center)
			.VerticalAlignment(VAlign.Top)
			.Foreground(Brushes.OrangeRed)
			.TextWrapping(TextWrapping.Wrap)
			.FontSize(UiCfg.Inst.BaseFontSize * ErrorFontScale)
			.Text(vm, X => X.ErrorText)
			.IsVisible(vm, X => X.HasError);
		return new StackGrid(IsRow: true)
			.Rows("*,Auto,Auto,*")
			.Children(
				new Border()
				,_StatusText
				,_ErrorText
				,new Border()
			);
	}

	[Doc("錄音按鈕：圓形，顏色與可用性全部綁定階段。")]
	Control MkRecordArea(VmVoiceInput vm){
		var Diameter = UiCfg.Inst.BaseFontSize * RecordDiameterScale;
		var IconSize = UiCfg.Inst.BaseFontSize * RecordIconScale;
		_RecordIcon = Icons.Microphone()
			.With(I => {
				I.Width = IconSize;
				I.Height = IconSize;
				I.HorizontalAlignment = HAlign.Center;
				I.VerticalAlignment = VAlign.Center;
				// 麥克風底座與拾音弧線造成可見墨量偏左，按圖標尺寸右移少許。
				I.RenderTransform = new TranslateTransform(IconSize / 12, 0);
			})
			.Fill(vm, X => X.RecordIconBrush);
		_RecordButton = new Button()
			.With(B => {
				B.Width = Diameter;
				B.Height = Diameter;
				B.Padding = new Avalonia.Thickness(0);
				B.BorderThickness = new Avalonia.Thickness(1);
				B.CornerRadius = new Avalonia.CornerRadius(Diameter / 2);
				B.HorizontalAlignment = HAlign.Center;
				B.VerticalAlignment = VAlign.Center;
				B.HorizontalContentAlignment = HAlign.Center;
				B.VerticalContentAlignment = VAlign.Center;
				B.Click += (_, _) => vm.ToggleRecordingSafely();
			})
			.Background(vm, X => X.RecordButtonBackground)
			.BorderBrush(vm, X => X.RecordButtonBorderBrush)
			.IsEnabled(vm, X => X.RecordButtonEnabled)
			.Content(new Grid()
				.Width(IconSize)
				.Height(IconSize)
				.Children(_RecordIcon));
		return new Border()
			.Background(Brushes.Black)
			.Padding(new Avalonia.Thickness(4, 2))
			.Child(_RecordButton);
	}

	[Doc("底部動作列：清空與上屏等寬。")]
	Control MkActionBar(VmVoiceInput vm){
		_ClearButton = new Button()
			.With(B => {
				B.CornerRadius = new Avalonia.CornerRadius(0);
				B.HorizontalAlignment = HAlign.Stretch;
				B.HorizontalContentAlignment = HAlign.Center;
				B.Click += (_, _) => vm.ClearText();
			})
			.Content(Todo.I18n("清空"))
			.IsEnabled(vm, X => X.CanClearText);
		_CommitButton = new Button()
			.With(B => {
				B.Background = UiCfg.Inst.MainColor;
				B.Foreground = Brushes.Black;
				B.CornerRadius = new Avalonia.CornerRadius(0);
				B.HorizontalAlignment = HAlign.Stretch;
				B.HorizontalContentAlignment = HAlign.Center;
				B.Click += (_, _) => vm.CommitText();
			})
			.Content(Todo.I18n("上屏"))
			.IsEnabled(vm, X => X.CanCommitText);
		return new Grid()
			.Cols("*,*")
			.Children(_ClearButton, _CommitButton);
	}

	[Doc("打開語言覆蓋層前再檢查可用性，避免延遲點擊繞過按鈕禁用。")]
	void OpenLangSelector(){
		if(vm.CanSelectLang != true || _LangSelector is null){
			return;
		}
		_LangSelector.IsVisible = true;
		_LangSelector.IsHitTestVisible = true;
	}

	[Doc("套用使用者選的語言並收起覆蓋層。")]
	void OnLangSelected(INormLang Lang){
		vm.ApplyLang(Lang);
		CloseLangSelector();
	}

	[Doc("收起覆蓋層。")]
	void CloseLangSelector(){
		if(_LangSelector is null){
			return;
		}
		_LangSelector.IsVisible = false;
		_LangSelector.IsHitTestVisible = false;
	}

	[Doc("解除覆蓋層事件並釋放子視圖與 ViewModel。")]
	public partial void Dispose(){
		if(_LangSelector is not null){
			_LangSelector.OnSelected -= OnLangSelected;
			_LangSelector.OnCloseRequested -= CloseLangSelector;
			_LangSelector.Dispose();
		}
		vm?.Dispose();
		GC.SuppressFinalize(this);
	}
}
